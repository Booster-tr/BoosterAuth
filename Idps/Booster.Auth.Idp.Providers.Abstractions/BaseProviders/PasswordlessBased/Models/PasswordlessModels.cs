namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;

public sealed record PasswordlessStartRequest
{
    public required string Identifier { get; init; }

    public required string ChallengeKey { get; init; }
}

public sealed record PasswordlessStartResult
{
    public required bool Succeeded { get; init; }

    public string? FailureReason { get; init; }

    public Guid? PrincipalId { get; init; }

    public string? Target { get; init; }


    public string? Code { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public static PasswordlessStartResult Fail(string reason)
        => new() { Succeeded = false, FailureReason = reason };

    public static PasswordlessStartResult Success(
        Guid principalId, string target, string code, DateTimeOffset expiresAt)
        => new()
        {
            Succeeded = true,
            PrincipalId = principalId,
            Target = target,
            Code = code,
            ExpiresAt = expiresAt
        };

    public static PasswordlessStartResult AlreadySent(
        Guid principalId, string target, DateTimeOffset expiresAt)
        => new()
        {
            Succeeded = true,
            PrincipalId = principalId,
            Target = target,
            Code = null,
            ExpiresAt = expiresAt
        };
}

public sealed record PasswordlessVerifyRequest
{
    public required string ChallengeKey { get; init; }

    public required string Code { get; init; }
}

public sealed record PasswordlessCode
{
    public required Guid PrincipalId { get; init; }

    public required string Target { get; init; }

    public required string CodeHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }

    public int AttemptCount { get; init; }
}
