using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.ExceptionManager.Abstraction.Constants;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Tenants.Access;

public static class TenantAccessRules
{
    /// <summary>
    /// Firma üzerinde ATANABİLİR ilişkiler. <c>parent</c> gibi yapısal
    /// ilişkiler burada yoktur: onlar kayıtlar arasındaki bağı anlatır,
    /// bir kullanıcıya verilmez. Şemadan okumak yerine sabit liste
    /// tutuluyor çünkü bu üçü firma tipinin sözleşmesi; şema değişirse
    /// derleme değil, bu liste bilerek güncellenmeli.
    /// </summary>
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
