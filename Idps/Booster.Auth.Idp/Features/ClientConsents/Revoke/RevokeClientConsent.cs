using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Idp.Features.ClientConsents.Revoke;

public class RevokeClientConsentEndpoint : Endpoint<RevokeClientConsentCommand, RevokeClientConsentResponse>
{
    public override void Configure()
    {
        Post("clients/consents/revoke");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, IdpPolicies.ClientTarget, AuthConstants.ManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Kullanıcının uygulamaya verdiği onayı iptal eder";
            s.Description = "Kullanıcı bir sonraki girişte onay ekranını yeniden görür.";
        });
    }

    public override async Task HandleAsync(RevokeClientConsentCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record RevokeClientConsentCommand(Guid ClientId, Guid ConsentId) : ICommand<RevokeClientConsentResponse>;

public readonly record struct RevokeClientConsentResponse(Guid Id);

public class RevokeClientConsentCommandHandler(IAsyncRepository<Consent, IdpDbContext> consentRepository)
    : ICommandHandler<RevokeClientConsentCommand, RevokeClientConsentResponse>
{
    public async Task<RevokeClientConsentResponse> ExecuteAsync(RevokeClientConsentCommand command, CancellationToken ct)
    {
        var consent = await consentRepository.GetAsync(
            c => c.Id == command.ConsentId && c.ClientId == command.ClientId && c.DeletedTime == null,
            withoutGlobalFilter: true,
            cancellationToken: ct)
            ?? throw new BusinessException(IdpMessageKeys.ConsentNotFound);

        consent.RevokedAt ??= DateTimeOffset.UtcNow;
        await consentRepository.SaveChangesAsync(ct);

        return new RevokeClientConsentResponse(consent.Id);
    }
}

public class RevokeClientConsentValidator : Validator<RevokeClientConsentCommand>
{
    public RevokeClientConsentValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ConsentId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
