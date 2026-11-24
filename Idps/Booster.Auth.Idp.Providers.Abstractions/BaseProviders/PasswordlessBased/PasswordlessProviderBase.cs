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

    /// <summary>Kullanici basina gonderim penceresi ve pencerede en fazla gonderim (bkz. StartAsync).</summary>
    protected virtual TimeSpan SendWindow => TimeSpan.FromMinutes(15);

    protected virtual int MaxSendsPerWindow => 5;

    /// <summary>Suresi dolan kod bu kadar daha tutulur; kullaniciya "suresi doldu" denebilsin.</summary>
    protected static readonly TimeSpan ExpiredCodeGrace = TimeSpan.FromMinutes(10);

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

        // Ayni etkilesimde kod zaten gitti ve gecerli: yeniden gondermek yerine ayni ekran (form iki kez gonderildi).
        var current = await codeStore.GetAsync(CacheKey(request.ChallengeKey), ct);
        if (current is not null && current.PrincipalId == principal.Id && current.ExpiresAt > DateTimeOffset.UtcNow)
            return PasswordlessStartResult.AlreadySent(principal.Id, target, current.ExpiresAt);

        // Kullanici basina gonderim siniri. Once kullanici basina TEK bekleyen kod vardi: ikinci sekme / cihaz kod
        // alamiyor, adresi bilen biri de kodu surekli tazeleyerek kurbanin girisini engelleyebiliyordu. Artik her
        // etkilesim kendi kodunu alir; yalnizca pencere icindeki gonderim sayisi sinirlidir (e-posta / SMS bombardimani).
        var pendingKey = PendingKey(config.IsolationId, principal.Id);
        var window = await codeStore.GetAsync(pendingKey, ct);
        var now = DateTimeOffset.UtcNow;
        if (window is null || window.ExpiresAt <= now)
            window = new PasswordlessCode { PrincipalId = principal.Id, Target = target, CodeHash = string.Empty, ExpiresAt = now.Add(SendWindow) };

        if (window.AttemptCount >= MaxSendsPerWindow)
            return PasswordlessStartResult.Fail(PasswordlessFailures.TooManyRequests);

        var code = GenerateCode();
        var expiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);

        var record = new PasswordlessCode
        {
            PrincipalId = principal.Id,
            Target = target,
            CodeHash = codeHasher.Hash(code),
            ExpiresAt = expiresAt
        };

        // Onbellek suresi kod suresinden UZUN: suresi dolan kod "hatali" degil "suresi doldu" desin.
        await codeStore.StoreAsync(CacheKey(request.ChallengeKey), record, CodeLifetime + ExpiredCodeGrace, ct);

        // Sayac: AttemptCount = penceredeki gonderim sayisi, ExpiresAt = pencerenin sonu.
        await codeStore.StoreAsync(pendingKey, window with { AttemptCount = window.AttemptCount + 1 }, window.ExpiresAt - now, ct);

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
            // Kod yakildiysa kullaniciya "tekrar deneyin" degil "yeni kod isteyin" denir.
            var burned = await RegisterFailedAttemptAsync(key, config.IsolationId, record, ct);
            return IdentityResult.Fail(burned ? PasswordlessFailures.AttemptsExceeded : PasswordlessFailures.CodeInvalid);
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

    private async Task<bool> RegisterFailedAttemptAsync(
        string key, Guid isolationId, PasswordlessCode record, CancellationToken ct)
    {
        if (record.AttemptCount + 1 >= MaxAttempts)
        {
            await ConsumeAsync(key, isolationId, record.PrincipalId, ct);
            return true;
        }

        await codeStore.StoreAsync(
            key,
            record with { AttemptCount = record.AttemptCount + 1 },
            record.ExpiresAt - DateTimeOffset.UtcNow + ExpiredCodeGrace,
            ct);
        return false;
    }

    private string CacheKey(string challengeKey) => $"{ProviderType}:{challengeKey}";


    private string PendingKey(Guid isolationId, Guid principalId)
        => $"{ProviderType}:pending:{isolationId}:{principalId}";

    private async Task ConsumeAsync(string key, Guid isolationId, Guid principalId, CancellationToken ct)
    {
        await codeStore.RemoveAsync(key, ct);
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
    public const string TooManyRequests = "too_many_requests";
    public const string AttemptsExceeded = "attempts_exceeded";
}
