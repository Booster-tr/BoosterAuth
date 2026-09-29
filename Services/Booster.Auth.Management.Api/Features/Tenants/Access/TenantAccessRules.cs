using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.ExceptionManager.Abstraction.Constants;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Tenants.Access;

public static class TenantAccessRules
{
    public static readonly string[] Assignable =
    [
        AuthConstants.OwnerRelation,
        AuthConstants.ManagerRelation,
        AuthConstants.ViewerRelation
    ];

    public static IRuleBuilderOptions<T, string> TenantRelation<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Must(relation => Assignable.Contains(relation, StringComparer.OrdinalIgnoreCase))
            .WithMessage(AuthNMessageKeys.InvalidRelation);
}
