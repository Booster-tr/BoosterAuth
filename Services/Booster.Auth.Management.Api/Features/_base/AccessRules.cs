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

public readonly record struct AccessChangeResponse(Guid PrincipalId, string Relation);

public record AccessListResponse(IReadOnlyList<AccessItemResponse> Items, string? ContinuationToken);

public readonly record struct AccessItemResponse(Guid PrincipalId, string Relation);
