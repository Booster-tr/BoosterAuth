using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Stores.ClientLogoStores;
using Booster.Authorization.Fga.Attributes;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Idp.Features.ClientLogos.Upload;

public class UploadClientLogoEndpoint(IClientLogoStore logoStore)
    : Endpoint<UploadClientLogoRequest, UploadClientLogoResponse>
{
    public override void Configure()
    {
        Put("clients/logo");
        AllowFileUploads();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, IdpPolicies.ClientTarget, AuthConstants.ManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Uygulama logosunu yükler";
            s.Description = "Varsa mevcut logo silinir ve yerine gönderilen dosya kaydedilir. " +
                            "PNG, JPEG, GIF ve WebP kabul edilir; en fazla 1 MB.";
        });
    }

    public override async Task HandleAsync(UploadClientLogoRequest req, CancellationToken ct)
    {
        var header = new byte[12];
        int read;
        await using (var probe = req.File.OpenReadStream())
            read = await probe.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);

        var format = ClientLogoFormat.Detect(header.AsSpan(0, read))
            ?? throw new BusinessException(IdpMessageKeys.ClientLogoInvalidFormat);

        await using var content = req.File.OpenReadStream();
        await logoStore.SaveAsync(req.ClientId, format, content, ct);

        await Send.OkAsync(new UploadClientLogoResponse(req.ClientId, logoStore.Find(req.ClientId)!.RelativeUrl), ct);
    }
}

public class UploadClientLogoRequest
{
    [QueryParam]
    public Guid ClientId { get; set; }

    public IFormFile File { get; set; } = default!;
}

public readonly record struct UploadClientLogoResponse(Guid Id, string LogoUrl);

public class UploadClientLogoValidator : Validator<UploadClientLogoRequest>
{
    public const long MaxBytes = 1024 * 1024;

    public UploadClientLogoValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(IdpMessageKeys.ClientLogoRequired)
            .Must(f => f.Length > 0).WithMessage(IdpMessageKeys.ClientLogoRequired)
            .Must(f => f.Length <= MaxBytes).WithMessage(IdpMessageKeys.ClientLogoTooLarge);
    }
}
