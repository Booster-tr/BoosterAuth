using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Auth.Management.Api.Managers.PrincipalLookupManagers;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.Authorization.Fga.Attributes;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Clients.Access.Grant;

public class GrantClientAccessEndpoint : Endpoint<GrantClientAccessCommand, AccessChangeResponse>
{
    public override void Configure()
    {
        Post("access");
        Group<ClientsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, ManagementPolicies.ClientTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Uygulama üzerinde yetki verir";
            s.Description = "Kullanıcı e-posta, kullanıcı adı ya da telefon numarasıyla bulunur. Aramalar kişi başına sınırlıdır.";
        });
    }

    public override async Task HandleAsync(GrantClientAccessCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record GrantClientAccessCommand(Guid ClientId, string Identifier, string Relation)
    : ICommand<AccessChangeResponse>;

public class GrantClientAccessCommandHandler(
    IPrincipalLookupManager principalLookupManager,
    IAccessManager accessManager,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<GrantClientAccessCommand, AccessChangeResponse>
{
    public async Task<AccessChangeResponse> ExecuteAsync(GrantClientAccessCommand command, CancellationToken ct)
    {
        // Owner yetkisini yalnızca owner verebilir; aksi hâlde yetki yöneticisi sahipliğe yükselebilirdi.
        if (string.Equals(command.Relation, AuthConstants.OwnerRelation, StringComparison.OrdinalIgnoreCase))
        {
            var check = await authorizationClient.CheckAsync(
                new CheckRequest
                {
                    IsolationId = requestContextAccessor.Context().Isolation.ToString(),
                    User = AuthConstants.User(requestContextAccessor.Context().PrincipalId),
                    Relation = AuthConstants.OwnerRelation,
                    Object = AuthConstants.Client(command.ClientId)
                },
                cancellationToken: ct);

            if (!check.Allowed)
                throw new ForbiddenException(AuthNMessageKeys.ClientOwnerRelationRequiresOwner);
        }

        var principalId = await principalLookupManager.FindAsync(command.Identifier, ct);

        await accessManager.GrantAsync(
            AuthConstants.ClientType, command.ClientId, principalId, command.Relation, ct);

        return new AccessChangeResponse(principalId, command.Relation);
    }
}

public class GrantClientAccessValidator : Validator<GrantClientAccessCommand>
{
    public GrantClientAccessValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(320).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.Relation).AssignableRelation();
    }
}
