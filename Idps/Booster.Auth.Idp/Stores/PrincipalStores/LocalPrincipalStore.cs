using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Providers.Abstractions.Stores.Models;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Booster.Auth.Idp.Stores.PrincipalStores;

public sealed class LocalPrincipalStore(
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<LocalCredential, IdpDbContext> localCredentialRepository,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor) : ILocalUserStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public static string Tag(Guid principalId) => $"principal:{principalId}";

    private static string FindKey(
        Guid isolation, IReadOnlyList<IdentifierType> types, string identifier)
        => $"idp:local-user:{isolation}:{string.Join('-', types.Select(t => (int)t).Distinct().Order())}:{identifier}";

    private static string IdKey(Guid isolation, Guid id)
        => $"idp:local-user-by-id:{isolation}:{id}";

    public async Task<LocalUser?> FindAsync(
        string identifier,
        IReadOnlyList<IdentifierType> identifierTypes,
        CancellationToken ct,
        string? defaultCountryCode = null)
    {
        if (identifierTypes.Count == 0 || string.IsNullOrWhiteSpace(identifier))
            return null;

        var isolation = requestContextAccessor.Context().Isolation;

        return await cacheService.GetOrSetAsync(
            FindKey(isolation, identifierTypes, $"{identifier.Trim().ToLowerInvariant()}|{defaultCountryCode}"),
            async (context, token) =>
            {
                var principal = await principalRepository.GetAsync(
                    predicate: BuildPredicate(identifier, identifierTypes, defaultCountryCode),
                    include: q => q.Include(p => p.LocalCredential!),
                    enableTracking: false,
                    cancellationToken: token);

                if (principal is null)
                    return null;

                context.SetTags(Tag(principal.Id));

                return ToLocalUser(principal);
            },
            expiry: Ttl,
            ct: ct);
    }

    public async Task<LocalUser?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var isolation = requestContextAccessor.Context().Isolation;

        return await cacheService.GetOrSetAsync(
            IdKey(isolation, id),
            async (context, token) =>
            {
                var principal = await principalRepository.GetAsync(
                    p => p.Id == id, enableTracking: false, cancellationToken: token);

                if (principal is null)
                    return null;

                context.SetTags(Tag(id));

                return ToLocalUser(principal);
            },
            expiry: Ttl,
            ct: ct);
    }

    public async Task UpdatePasswordHashAsync(Guid principalId, string newHash, CancellationToken ct)
    {
        var credential = await localCredentialRepository.GetAsync(
            c => c.PrincipalId == principalId, cancellationToken: ct);

        if (credential is null)
            return;

        credential.PasswordHash = newHash;
        credential.PasswordChangedAt = DateTimeOffset.UtcNow;
        await localCredentialRepository.SaveChangesAsync(ct);

        await cacheService.RemoveByTagAsync(Tag(principalId), ct);
    }

    public async Task MarkIdentifierVerifiedAsync(
        Guid principalId, IdentifierType identifierType, CancellationToken ct)
    {
        if (identifierType is not (IdentifierType.Email or IdentifierType.Phone))
            return;

        var principal = await principalRepository.GetAsync(
            p => p.Id == principalId, cancellationToken: ct);

        if (principal is null)
            return;

        var alreadyVerified = identifierType == IdentifierType.Email
            ? principal.EmailVerified
            : principal.PhoneVerified;

        if (alreadyVerified)
            return;

        if (identifierType == IdentifierType.Email)
            principal.EmailVerified = true;
        else
            principal.PhoneVerified = true;

        principalRepository.Update(principal);
        await principalRepository.SaveChangesAsync(ct);

        await cacheService.RemoveByTagAsync(Tag(principalId), ct);
    }

    private static Expression<Func<Principal, bool>> BuildPredicate(
        string identifier, IReadOnlyList<IdentifierType> types, string? defaultCountryCode)
    {
        var userName = IdentifierNormalizer.UserName(identifier);
        var email = IdentifierNormalizer.Email(identifier);
        var phone = IdentifierNormalizer.Phone(identifier, defaultCountryCode) ?? identifier.Trim();

        return p =>
            (types.Contains(IdentifierType.Username) && p.UserName == userName) ||
            (types.Contains(IdentifierType.Email) && p.Email == email) ||
            (types.Contains(IdentifierType.Phone) && p.PhoneNumber == phone);
    }

    private static LocalUser ToLocalUser(Principal principal)
        => new()
        {
            Id = principal.Id,
            DisplayName = principal.DisplayName,
            Type = principal.Type,
            Status = principal.Status,
            UserName = principal.UserName,
            Email = principal.Email,
            EmailVerified = principal.EmailVerified,
            PhoneNumber = principal.PhoneNumber,
            PhoneVerified = principal.PhoneVerified,
            PasswordHash = principal.LocalCredential?.PasswordHash,
            FailedAttemptCount = principal.LocalCredential?.FailedAttemptCount ?? 0,
            LockedUntil = principal.LocalCredential?.LockedUntil,
            MustChangePassword = principal.LocalCredential?.MustChangePassword ?? false,
            Claims = BuildClaims(principal)
        };

    private static IReadOnlyDictionary<string, string> BuildClaims(Principal principal)
    {
        var claims = new Dictionary<string, string> { ["display_name"] = principal.DisplayName };

        if (!string.IsNullOrEmpty(principal.Email))
        {
            claims["email"] = principal.Email;
            claims["email_verified"] = principal.EmailVerified.ToString().ToLowerInvariant();
        }

        if (!string.IsNullOrEmpty(principal.UserName))
            claims["username"] = principal.UserName;

        if (!string.IsNullOrEmpty(principal.PhoneNumber))
        {
            claims["phone_number"] = principal.PhoneNumber;
            claims["phone_verified"] = principal.PhoneVerified.ToString().ToLowerInvariant();
        }

        return claims;
    }
}
