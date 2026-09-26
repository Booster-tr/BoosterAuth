using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Providers.Abstractions.Stores.Models;
using Booster.Caching.Abstraction;
using Booster.Hashing.OneWays.Simple;
using System.Security.Cryptography;

namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;

public abstract class PasswordlessProviderBase(
    ILocalUserStore principalStore,
    ISecureCacheStore<PasswordlessCode> codeStore,
    ISimpleHasher codeHasher) : IPasswordlessProvider
{
    protected virtual TimeSpan CodeLifetime => TimeSpan.FromMinutes(5);


    protected virtual int MaxAttempts => 5;

    public abstract string ProviderType { get; }

    public abstract IdentifierType IdentifierType { get; }

    public abstract LoginProviderType LoginProvider { get; }

    public ProviderKind Kind => ProviderKind.Passwordless;

    protected abstract string Normalize(string identifier, ProviderConfig config);

    protected abstract string? ResolveTarget(LocalUser principal);

    public async Task<PasswordlessStartResult> StartAsync(
        PasswordlessStartRequest request,
        ProviderConfig config,
        CancellationToken ct)
    {
        if (!config.IsActive)
            return PasswordlessStartResult.Fail(PasswordlessFailures.ProviderInactive);

        var identifier = Normalize(request.Identifier, config);
        if (string.IsNullOrEmpty(identifier))
            return PasswordlessStartResult.Fail(PasswordlessFailures.InvalidIdentifier);

        var principal = await principalStore.FindAsync(identifier, [IdentifierType], ct);

        if (principal is null || !principal.IsActive)
            return PasswordlessStartResult.Fail(PasswordlessFailures.PrincipalNotFound);

        var target = ResolveTarget(principal);
        if (string.IsNullOrWhiteSpace(target))
            return PasswordlessStartResult.Fail(PasswordlessFailures.NoDeliveryTarget);

        var pendingKey = PendingKey(config.IsolationId, principal.Id);
        var pending = await codeStore.GetAsync(pendingKey, ct);

        if (pending is not null && pending.ExpiresAt > DateTimeOffset.UtcNow)
            return PasswordlessStartResult.AlreadySent(principal.Id, target, pending.ExpiresAt);

        var code = GenerateCode();
        var expiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);

        var record = new PasswordlessCode
        {
            PrincipalId = principal.Id,
            Target = target,
            CodeHash = codeHasher.Hash(code),
            ExpiresAt = expiresAt
        };

        await codeStore.StoreAsync(CacheKey(request.ChallengeKey), record, CodeLifetime, ct);

        await codeStore.StoreAsync(pendingKey, record, CodeLifetime, ct);

        return PasswordlessStartResult.Success(principal.Id, target, code, expiresAt);
    }

    public async Task<IdentityResult> VerifyAsync(
        PasswordlessVerifyRequest request,
        ProviderConfig config,
        CancellationToken ct)
    {
        var key = CacheKey(request.ChallengeKey);

        var record = await codeStore.GetAsync(key, ct);
        if (record is null)
            return IdentityResult.Fail(PasswordlessFailures.CodeInvalid);


        if (record.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await ConsumeAsync(key, config.IsolationId, record.PrincipalId, ct);
            return IdentityResult.Fail(PasswordlessFailures.CodeExpired);
        }

        if (!codeHasher.Verify(request.Code.Trim(), record.CodeHash))
        {
            await RegisterFailedAttemptAsync(key, config.IsolationId, record, ct);
            return IdentityResult.Fail(PasswordlessFailures.CodeInvalid);
        }

        await ConsumeAsync(key, config.IsolationId, record.PrincipalId, ct);

        var principal = await principalStore.FindAsync(record.Target, [IdentifierType], ct);

        if (principal is null || !principal.IsActive || principal.Id != record.PrincipalId)
            return IdentityResult.Fail(PasswordlessFailures.PrincipalNotFound);

        await principalStore.MarkIdentifierVerifiedAsync(principal.Id, IdentifierType, ct);

        return IdentityResult.Success(
            principalId: record.PrincipalId.ToString(),
            providerKey: config.ProviderKey,
            claims: principal.Claims);
    }

    private async Task RegisterFailedAttemptAsync(
        string key, Guid isolationId, PasswordlessCode record, CancellationToken ct)
    {
        if (record.AttemptCount + 1 >= MaxAttempts)
        {
            await ConsumeAsync(key, isolationId, record.PrincipalId, ct);
            return;
        }

        await codeStore.StoreAsync(
            key,
            record with { AttemptCount = record.AttemptCount + 1 },
            CodeLifetime,
            ct);
    }

    private string CacheKey(string challengeKey) => $"{ProviderType}:{challengeKey}";


    private string PendingKey(Guid isolationId, Guid principalId)
        => $"{ProviderType}:pending:{isolationId}:{principalId}";

    private async Task ConsumeAsync(string key, Guid isolationId, Guid principalId, CancellationToken ct)
    {
        await codeStore.RemoveAsync(key, ct);
        await codeStore.RemoveAsync(PendingKey(isolationId, principalId), ct);
    }

    private static string GenerateCode()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}

public static class PasswordlessFailures
{
    public const string ProviderInactive = "provider_inactive";
    public const string InvalidIdentifier = "invalid_identifier";
    public const string PrincipalNotFound = "principal_not_found";
    public const string NoDeliveryTarget = "no_delivery_target";
    public const string CodeInvalid = "code_invalid";
    public const string CodeExpired = "code_expired";
}
