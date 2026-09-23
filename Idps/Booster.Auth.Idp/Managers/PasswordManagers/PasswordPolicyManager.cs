using Booster.Auth.Database.Models;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Database.Repositories;
using Booster.Hashing.OneWays.Argon;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Managers.PasswordManagers;

public interface IPasswordPolicyManager
{
    void Validate(PasswordPolicyData policy, string password, string propertyName);

    Task EnsureNotReusedAsync(PasswordPolicyData policy, Guid principalId, string newPassword, string propertyName, CancellationToken ct);

    Task RecordAsync(Guid principalId, string newPasswordHash, PasswordPolicyData policy, CancellationToken ct);

    bool IsExpired(PasswordPolicyData policy, DateTimeOffset? passwordChangedAt);
}

public sealed class PasswordPolicyManager(
    IAsyncRepository<PasswordHistory, IdpDbContext> historyRepository,
    IPasswordHasher passwordHasher) : IPasswordPolicyManager
{
    public void Validate(PasswordPolicyData policy, string password, string propertyName)
    {
        if (password.Length < policy.MinLength)
            throw Invalid(propertyName, IdpMessageKeys.PasswordTooShort);
        if (policy.RequireUppercase && !password.Any(char.IsUpper))
            throw Invalid(propertyName, IdpMessageKeys.PasswordRequiresUppercase);
        if (policy.RequireLowercase && !password.Any(char.IsLower))
            throw Invalid(propertyName, IdpMessageKeys.PasswordRequiresLowercase);
        if (policy.RequireDigit && !password.Any(char.IsDigit))
            throw Invalid(propertyName, IdpMessageKeys.PasswordRequiresDigit);
        if (policy.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
            throw Invalid(propertyName, IdpMessageKeys.PasswordRequiresNonAlphanumeric);
    }

    public async Task EnsureNotReusedAsync(
        PasswordPolicyData policy, Guid principalId, string newPassword, string propertyName, CancellationToken ct)
    {
        if (policy.HistoryCount <= 0)
            return;

        var recentHashes = await RecentHashesQuery(principalId, policy.HistoryCount).ToListAsync(ct);

        if (recentHashes.Any(hash => passwordHasher.Verify(newPassword, hash)))
            throw Invalid(propertyName, IdpMessageKeys.PasswordReused);
    }

    public async Task RecordAsync(
        Guid principalId, string newPasswordHash, PasswordPolicyData policy, CancellationToken ct)
    {
        if (policy.HistoryCount <= 0)
            return;

        await historyRepository.AddAsync(new PasswordHistory
        {
            Id = EntityId.New(),
            PrincipalId = principalId,
            PasswordHash = newPasswordHash,
            CreatedTime = DateTimeOffset.UtcNow
        }, ct);

        // sınırsız büyütürdü.
        var stale = await historyRepository.Query()
            .Where(x => x.PrincipalId == principalId)
            .OrderByDescending(x => x.CreatedTime)
            .Skip(policy.HistoryCount - 1)
            .ToListAsync(ct);

        foreach (var item in stale)
            historyRepository.Delete(item);
    }

    public bool IsExpired(PasswordPolicyData policy, DateTimeOffset? passwordChangedAt)
    {
        if (policy.ExpiryDays <= 0)
            return false;

        if (passwordChangedAt is null)
            return true;

        return passwordChangedAt.Value.AddDays(policy.ExpiryDays) <= DateTimeOffset.UtcNow;
    }

    private IQueryable<string> RecentHashesQuery(Guid principalId, int count)
        => historyRepository.Query()
            .Where(x => x.PrincipalId == principalId)
            .OrderByDescending(x => x.CreatedTime)
            .Take(count)
            .Select(x => x.PasswordHash);

    private static ValidationException Invalid(string property, string messageKey)
        => new([new ValidationFailure(property, messageKey)]);
}
