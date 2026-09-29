using System.Text.RegularExpressions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Grpc;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using StackExchange.Redis;

namespace Booster.Auth.Management.Api.Managers.PrincipalLookupManagers;


public sealed partial class PrincipalLookupManager(
    PrincipalLookupService.PrincipalLookupServiceClient lookupClient,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IRequestContextAccessor requestContextAccessor,
    IConnectionMultiplexer redis,
    ILogger<PrincipalLookupManager> logger) : IPrincipalLookupManager
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    public const int MaxAttempts = 20;
    public static readonly TimeSpan AttemptWindow = TimeSpan.FromHours(1);

    public async Task<Guid> FindAsync(string identifier, CancellationToken ct)
    {
        var context = requestContextAccessor.Context();
        var actor = context.PrincipalId;
        var isolation = context.Isolation;

        var db = redis.GetDatabase();
        var failureKey = $"mgmt:principal-lookup:fail:{actor}";
        var attemptKey = $"mgmt:principal-lookup:attempt:{actor}";

        var failures = (long?)await db.StringGetAsync(failureKey) ?? 0;
        if (failures >= MaxFailures)
            throw LimitExceeded(actor);

        var countryCode = await DefaultCountryCodeAsync(isolation, ct);

        var normalized = Normalize(identifier, countryCode)
                         ?? throw new BusinessException(AuthNMessageKeys.PrincipalIdentifierInvalid);

        if (await IncrementAsync(db, attemptKey, AttemptWindow) > MaxAttempts)
            throw LimitExceeded(actor);

        var response = await lookupClient.FindByIdentifierAsync(
            new FindByIdentifierRequest
            {
                IsolationId = isolation.ToString(),
                IdentifierType = (int)normalized.Type,
                Identifier = normalized.Value
            },
            cancellationToken: ct);

        if (response.Found && Guid.TryParse(response.PrincipalId, out var principalId))
            return principalId;

        var failed = await IncrementAsync(db, failureKey, FailureWindow);

        logger.LogWarning(
            "Kullanıcı araması sonuçsuz. Arayan: {Actor}, tür: {Type}, başarısız deneme: {Failures}/{Max}",
            actor, normalized.Type, failed, MaxFailures);

        throw new BusinessException(AuthNMessageKeys.PrincipalNotFound);
    }

    private TooManyRequestsException LimitExceeded(Guid actor)
    {
        logger.LogWarning("Kullanıcı arama sınırı aşıldı. Arayan: {Actor}", actor);

        return new TooManyRequestsException(AuthNMessageKeys.PrincipalLookupLimitExceeded);
    }

    private static async Task<long> IncrementAsync(IDatabase db, string key, TimeSpan window)
    {
        var count = await db.StringIncrementAsync(key);

        if (count == 1)
            await db.KeyExpireAsync(key, window);

        return count;
    }

    private async Task<string?> DefaultCountryCodeAsync(Guid isolation, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(
            p => p.Id == isolation, enableTracking: false, cancellationToken: ct);

        return pool?.Policy.Login.DefaultCountryCode;
    }

    internal static (IdentifierType Type, string Value)? Normalize(string? raw, string? countryCode)
    {
        var value = raw?.Trim();

        if (string.IsNullOrEmpty(value) || value.Length > 320)
            return null;

        if (value.Contains('@'))
            return EmailRegex().IsMatch(value) ? (IdentifierType.Email, value.ToLowerInvariant()) : null;

        if (PhoneRegex().IsMatch(value))
            return (IdentifierType.Phone, NormalizePhone(value, countryCode));

        return UserNameRegex().IsMatch(value) ? (IdentifierType.Username, value) : null;
    }

    private static string NormalizePhone(string value, string? countryCode)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        var cc = new string((countryCode ?? string.Empty).Where(char.IsDigit).ToArray());

        if (value.StartsWith('+'))
            return $"+{digits}";

        if (digits.StartsWith("00"))
            return $"+{digits[2..]}";

        if (cc.Length == 0)
            return $"+{digits}";

        if (digits.StartsWith('0'))
            return $"+{cc}{digits[1..]}";

        if (digits.StartsWith(cc) && digits.Length > 10)
            return $"+{digits}";

        return $"+{cc}{digits}";
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\+?[0-9\s\-().]{7,20}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^\S{1,100}$")]
    private static partial Regex UserNameRegex();
}
