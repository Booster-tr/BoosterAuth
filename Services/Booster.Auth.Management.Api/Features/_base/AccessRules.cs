using Booster.Auth.Common.Internal.Constants;
using Booster.ExceptionManager.Abstraction.Constants;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features._base;

public static class AccessRules
{
    public static readonly string[] Assignable =
    [
        AuthConstants.OwnerRelation,
        AuthConstants.ManagerRelation,
        AuthConstants.ViewerRelation,
        AuthConstants.GrantManagerRelation
    ];

    public static IRuleBuilderOptions<T, string> AssignableRelation<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Must(relation => Assignable.Contains(relation, StringComparer.OrdinalIgnoreCase))
            .WithMessage(AuthNMessageKeys.InvalidRelation);
}

/// <summary>
/// Geri alma parametreleri sorgudan gelir; eksik ya da gecersiz deger 400 olmali (once bos Guid / bos iliski AuthZ'ye
/// gidip 500 donuyordu, or. parametreler govdeye yazildiginda).
/// </summary>
public static class RevokeParams
{
    public static void Ensure(Guid principalId, string relation)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        if (principalId == Guid.Empty)
            failures.Add(new("PrincipalId", ValidationErrors.NotEmptyError));
        if (string.IsNullOrWhiteSpace(relation))
            failures.Add(new("Relation", ValidationErrors.NotEmptyError));
        else if (!AccessRules.Assignable.Contains(relation, StringComparer.OrdinalIgnoreCase))
            failures.Add(new("Relation", AuthNMessageKeys.InvalidRelation));
        if (failures.Count > 0)
            throw new FluentValidation.ValidationException(failures);
    }
}

public readonly record struct AccessChangeResponse(Guid PrincipalId, string Relation);

public record AccessListResponse(IReadOnlyList<AccessItemResponse> Items, string? ContinuationToken);

public readonly record struct AccessItemResponse(Guid PrincipalId, string Relation);
