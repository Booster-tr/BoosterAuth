using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Idp.Features.ClientSessions.Revoke;

public class RevokeClientSessionEndpoint : Endpoint<RevokeClientSessionCommand, RevokeClientSessionResponse>
{
    public override void Configure()
    {
        Post("clients/sessions/revoke");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, IdpPolicies.ClientTarget, AuthConstants.ManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Kullanıcının uygulamadaki oturumunu düşürür";
            s.Description = "Oturum yenilenemez; kullanıcı yeniden giriş yapmak zorunda kalır. " +
                            "Daha önce verilmiş erişim tokenları süreleri dolana kadar geçerlidir.";
        });
    }

    public override async Task HandleAsync(RevokeClientSessionCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record RevokeClientSessionCommand(Guid ClientId, Guid SessionId) : ICommand<RevokeClientSessionResponse>;

public readonly record struct RevokeClientSessionResponse(Guid Id);

public class RevokeClientSessionCommandHandler(
    IAsyncRepository<Session, IdpDbContext> sessionRepository,
    ISessionStore sessionStore,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<RevokeClientSessionCommand, RevokeClientSessionResponse>
{
    public async Task<RevokeClientSessionResponse> ExecuteAsync(RevokeClientSessionCommand command, CancellationToken ct)
    {
        var session = await sessionRepository.GetAsync(
            s => s.Id == command.SessionId && s.ClientId == command.ClientId && s.DeletedTime == null,
            withoutGlobalFilter: true,
            enableTracking: false,
            cancellationToken: ct)
            ?? throw new BusinessException(IdpMessageKeys.SessionNotFound);

        // Oturum deposu izolasyon filtresiyle çalışır; oturumun havuzuna geçilir.
        using var _ = requestContextAccessor.BeginSystemContext(session.IsolationId);

        await sessionStore.RevokeAsync(session.Id, ct);

        return new RevokeClientSessionResponse(session.Id);
    }
}

public class RevokeClientSessionValidator : Validator<RevokeClientSessionCommand>
{
    public RevokeClientSessionValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.SessionId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
