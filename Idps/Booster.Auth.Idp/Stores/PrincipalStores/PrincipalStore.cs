using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Stores;
using Booster.Auth.Idp.Providers.Local.Stores;
using Booster.Auth.Idp.Providers.Local.Stores.Models;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Booster.Auth.Idp.Stores.PrincipalStores;

public sealed class PrincipalStore(
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<LocalCredential, IdpDbContext> localCredentialRepository,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor) : ILocalUserStore, IProviderPrincipalStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private static string CacheKey(Guid isolation, string identifier)
        => $"idp:principal:{isolation}:{identifier}";

    public static string Tag(Guid id) => $"principal:{id}";

    public async Task<LocalUser?> FindAsync(
     string identifier,
     IReadOnlyList<IdentifierType> allowedIdentifiers,
     CancellationToken ct)
    {
        if (allowedIdentifiers.Count == 0)
            return null;

        var isolation = requestContextAccessor.Context().Isolation;
        var key = CacheKey(isolation, identifier);

        return await cacheService.GetOrSetAsync(
            key,
            async (context, token) =>
            {
                var principal = await principalRepository.GetAsync(
                    predicate: BuildPredicate(identifier, allowedIdentifiers),
                    include: q => q.Include(p => p.LocalCredential)!,
                    enableTracking: false,
                    cancellationToken: token);

                if (principal?.LocalCredential is null)
                    return null;

                context.SetTags(Tag(principal.Id));

                return ToLocalUser(principal);
            },
            expiry: Ttl,
            ct: ct);
    }

    /// <summary>
    /// Tek bir kimlik alanına göre arar; parola kaydı ŞART DEĞİLDİR.
    /// <para>
    /// <see cref="FindAsync"/> kullanılamaz: o arama LocalCredential'ı zorunlu
    /// tutar ve parolasız kullanıcıyı hiçbir zaman bulamaz.
    /// </para>
    /// </summary>
    public async Task<ProviderPrincipal?> FindByIdentifierAsync(
        string identifier, IdentifierType identifierType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        var isolation = requestContextAccessor.Context().Isolation;

        // Anahtar alan adını taşır: aynı değer farklı alanlarda başka
        // kullanıcılara ait olabilir, ön ek olmasaydı biri diğerinin
        // önbellek girdisini okurdu.
        var key = $"idp:principal-by-identifier:{isolation}:{identifierType}:{identifier}";

        return await cacheService.GetOrSetAsync(
            key,
            async (context, token) =>
            {
                var principal = await principalRepository.GetAsync(
                    predicate: BuildIdentifierPredicate(identifier, identifierType),
                    enableTracking: false,
                    cancellationToken: token);

                if (principal is null)
                    return null;

                context.SetTags(Tag(principal.Id));

                return ToProviderPrincipal(principal);
            },
            expiry: Ttl,
            ct: ct);
    }

    public async Task MarkIdentifierVerifiedAsync(
        Guid principalId, IdentifierType identifierType, CancellationToken ct)
    {
        // Yalnizca e-posta ve telefon dogrulanabilir; kullanici adi bir adres
        // degil, dogrulanacak bir seyi yok.
        if (identifierType is not (IdentifierType.Email or IdentifierType.Phone))
            return;

        var principal = await principalRepository.GetAsync(
            p => p.Id == principalId, cancellationToken: ct);

        if (principal is null)
            return;

        // Zaten dogrulanmissa CIKILIR: parolasiz giris her oturumda tekrarlanir,
        // her seferinde yazmak bos bir UPDATE ve gereksiz onbellek
        // gecersizlestirmesi demekti.
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

        // Onbellekteki kopya eski bayragi tasiyor; temizlenmezse token bir
        // sonraki okumaya kadar email_verified=false tasimaya devam ederdi.
        await cacheService.RemoveByTagAsync(Tag(principalId), ct);
    }

    public async Task<PrincipalInfo?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var isolation = requestContextAccessor.Context().Isolation;
        return await cacheService.GetOrSetAsync(
            $"idp:principal-by-id:{isolation}:{id}",
            async (context, innerCt) =>
            {
                var p = await principalRepository.GetAsync(
                    x => x.Id == id, enableTracking: false, cancellationToken: innerCt);
                if (p is null) return null;
                context.SetTags(Tag(id));
                return ToPrincipalInfo(p);
            },
            expiry: Ttl, ct: ct);
    }

    public async Task UpdatePasswordHashAsync(Guid principalId, string newHash, CancellationToken ct)
    {
        var credential = await localCredentialRepository.GetAsync(c => c.PrincipalId == principalId, cancellationToken: ct);

        if (credential is null)
            return;

        credential.PasswordHash = newHash;
        credential.PasswordChangedAt = DateTimeOffset.UtcNow;
        await localCredentialRepository.SaveChangesAsync(ct);

        await cacheService.RemoveByTagAsync(Tag(principalId), ct);
    }

    private static Expression<Func<Principal, bool>> BuildPredicate(
        string identifier, IReadOnlyList<IdentifierType> allowed)
    {
        return p =>
            (allowed.Contains(IdentifierType.Username) && p.UserName == identifier) ||
            (allowed.Contains(IdentifierType.Email) && p.Email == identifier) ||
            (allowed.Contains(IdentifierType.Phone) && p.PhoneNumber == identifier);
    }

    private static Expression<Func<Principal, bool>> BuildIdentifierPredicate(
        string identifier, IdentifierType identifierType)
        => identifierType switch
        {
            IdentifierType.Email => p => p.Email == identifier,
            IdentifierType.Phone => p => p.PhoneNumber == identifier,
            IdentifierType.Username => p => p.UserName == identifier,
            // Bilinmeyen alan için HİÇBİR kaydı döndürmeyen bir koşul: "hepsi"
            // dönmek, yanlış kullanıcıyla giriş yapılmasına yol açardı.
            _ => p => false
        };

    private static ProviderPrincipal ToProviderPrincipal(Principal principal)
        => new()
        {
            Id = principal.Id,
            IsActive = principal.Status == PrincipalStatus.Active,
            Email = principal.Email,
            PhoneNumber = principal.PhoneNumber,
            Claims = BuildClaims(principal)
        };

    private static LocalUser ToLocalUser(Principal principal)
        => new()
        {
            Id = principal.Id,
            PasswordHash = principal.LocalCredential!.PasswordHash,
            IsActive = principal.Status == PrincipalStatus.Active,
            Claims = BuildClaims(principal),
            FailedAttemptCount = principal.LocalCredential.FailedAttemptCount,
            LockedUntil = principal.LocalCredential.LockedUntil
        };
    private static PrincipalInfo ToPrincipalInfo(Principal principal)
        => new()
        {
            Id = principal.Id,
            DisplayName = principal.DisplayName,
            Email = principal.Email,
            EmailVerified = principal.EmailVerified,
            UserName = principal.UserName,
            PhoneNumber = principal.PhoneNumber,
            PhoneVerified = principal.PhoneVerified,
            Type = principal.Type,
            Status = principal.Status
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
