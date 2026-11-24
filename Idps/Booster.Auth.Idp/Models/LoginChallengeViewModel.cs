using Booster.Auth.Idp.Managers.LoginChallengeManagers;
using Booster.ExceptionManager.Abstraction.Constants;
using FluentValidation;

namespace Booster.Auth.Idp.Models;

/// <summary>Doğrulama ekranı (e-posta/telefon doğrulama, MFA) görüntü ve form modeli.</summary>
public sealed class LoginChallengeViewModel
{
    public string InteractionKey { get; set; } = default!;

    public string? Code { get; set; }

    // Aşağıdakiler yalnızca görüntü içindir; formdan BAĞLANMAZ, her istekte
    // sunucudaki yarım girişten yeniden okunur.
    public LoginStep Step { get; private set; }

    public ChallengeChannel Channel { get; private set; }

    public string MaskedTarget { get; private set; } = string.Empty;

    public IReadOnlyList<ChallengeChannel> MfaChannels { get; private set; } = [];

    /// <summary>Kod az önce (yeniden) gönderildi; ekran bunu duyurur.</summary>
    public bool Resent { get; private set; }

    /// <summary>Yeniden gonderim istendi ama bekleme suresi dolmadi; kod gonderilmedi.</summary>
    public bool ResendWait { get; private set; }

    /// <summary>Kaynak dosyadaki anahtar öneki: "Challenge.VerifyEmail", "Challenge.Mfa" ...</summary>
    public string TextKey => $"Challenge.{Step}";

    public static LoginChallengeViewModel From(string interactionKey, LoginChallengeView view, bool resent, bool resendWait = false) => new()
    {
        InteractionKey = interactionKey,
        Step = view.Step,
        Channel = view.Channel,
        MaskedTarget = view.MaskedTarget,
        MfaChannels = view.MfaChannels,
        Resent = resent || resendWait,
        ResendWait = resendWait
    };
}

public sealed class LoginChallengeValidator : AbstractValidator<LoginChallengeViewModel>
{
    public LoginChallengeValidator()
    {
        RuleFor(x => x.InteractionKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(@"^\s*[0-9]{6}\s*$").WithMessage(IdpMessageKeys.LoginCodeInvalid);
    }
}
