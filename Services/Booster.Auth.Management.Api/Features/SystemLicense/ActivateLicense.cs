using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Licensing;
using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Licensing;
using Booster.Auth.Manager.Options;
using Booster.Authorization.Fga.Attributes;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.SystemLicense;

/// <summary>
/// Lisans portalindan alinan token'i etkinlestirir. Servis yeniden baslamadan gecerli olur.
/// Gecersiz token reddedilir ve KAYDEDILMEZ. Lisans kisitliyken de calisir (yenileme icin tek yol budur).
/// </summary>
public class ActivateLicenseEndpoint(LicenseService license, ApiAuthOptions apiAuth, BootstrapOptions bootstrap)
    : Endpoint<ActivateLicenseRequest, GetLicenseResponse>
{
    public override void Configure()
    {
        Post("license/activate");
        Group<SystemEndpointGroup>();
        Options(x => x
            .RequireAuthorization(
                new RequireScopeAttribute(ManagementPolicies.PanelScope),
                new RequireRoleAttribute(AuthConstants.AdminRoleName))
            .WithMetadata(new AllowWhenLicenseRestricted()));
        Summary(s => s.Summary = "Lisans token'ini etkinlestirir");
    }

    public override async Task HandleAsync(ActivateLicenseRequest req, CancellationToken ct)
    {
        var by = User.FindFirst("email")?.Value ?? User.FindFirst("sub")?.Value ?? "admin";
        var e = await license.ActivateAsync(req.Token, by, ct);
        await Send.OkAsync(GetLicenseResponse.From(e, GetLicenseEndpoint.DomainTargets(apiAuth, bootstrap)), ct);
    }
}

public sealed record ActivateLicenseRequest(string Token);

public sealed class ActivateLicenseValidator : Validator<ActivateLicenseRequest>
{
    public ActivateLicenseValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(8192).WithMessage(ValidationErrors.NotEmptyError);
    }
}
