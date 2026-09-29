using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.ExceptionManager.Abstraction.Constants;
using FluentValidation;

namespace Booster.Auth.Z.Features._base;

public static class AuthZValidationRules
{
    public const string CorrelationIdPattern = @"^[\w\d-]{1,36}$";

    public const string ObjectPattern = "^[^:]+:[^:]+$";

    public static IRuleBuilderOptions<T, string> ObjectFormat<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(ObjectPattern).WithMessage(AuthZMessageKeys.InvalidObjectFormat);

    public static IRuleBuilderOptions<T, string> UserFormat<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(ObjectPattern).WithMessage(AuthZMessageKeys.InvalidUserFormat);

    public static IRuleBuilderOptions<T, string?> CorrelationIdFormat<T>(this IRuleBuilder<T, string?> rule)
        => rule.Matches(CorrelationIdPattern).WithMessage(AuthZMessageKeys.InvalidCorrelationId);
}
