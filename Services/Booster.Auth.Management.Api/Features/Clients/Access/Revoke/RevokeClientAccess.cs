using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.Authorization.Fga.Attributes;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Clients.Access.Revoke;

public class RevokeClientAccessEndpoint : EndpointWithoutRequest<AccessChangeResponse>
{
    public override void Configure()
    {
        Delete("access");
        Group<ClientsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, ManagementPolicies.ClientTarget, AuthConstants.GrantManagerRelation)));

        Summary(s => s.Summary = "Uygulama üzerindeki yetkiyi geri alır");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var command = new RevokeClientAccessCommand(
            Query<Guid>("ClientId"),
            Query<Guid>("PrincipalId", isRequired: false),
            Query<string>("Relation", isRequired: false) ?? string.Empty);

        await Send.OkAsync(await command.ExecuteAsync(ct), ct);
    }
}

public record RevokeClientAccessCommand(Guid ClientId, Guid PrincipalId, string Relation)
    : ICommand<AccessChangeResponse>;

public class RevokeClientAccessCommandHandler(
    IAccessManager accessManager,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<RevokeClientAccessCommand, AccessChangeResponse>
{
    public async Task<AccessChangeResponse> ExecuteAsync(RevokeClientAccessCommand command, CancellationToken ct)
    {
        RevokeParams.Ensure(command.PrincipalId, command.Relation);

        // Owner yetkisini yalnızca owner geri alabilir.
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

        await accessManager.RevokeAsync(
            AuthConstants.ClientType, command.ClientId, command.PrincipalId, command.Relation, ct);

        return new AccessChangeResponse(command.PrincipalId, command.Relation);
    }
}

public class RevokeClientAccessValidator : Validator<RevokeClientAccessCommand>
{
    public RevokeClientAccessValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.PrincipalId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Relation).AssignableRelation();
    }
}
