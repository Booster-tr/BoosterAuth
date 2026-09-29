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

namespace Booster.Auth.Management.Api.Features.UserPools.Access.Revoke;

public class RevokeUserPoolAccessEndpoint : EndpointWithoutRequest<AccessChangeResponse>
{
    public override void Configure()
    {
        Delete("access");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s => s.Summary = "Havuz üzerindeki yetkiyi geri alır");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var command = new RevokeUserPoolAccessCommand(
            Query<Guid>("UserPoolId"),
            Query<Guid>("PrincipalId", isRequired: false),
            Query<string>("Relation", isRequired: false) ?? string.Empty);

        await Send.OkAsync(await command.ExecuteAsync(ct), ct);
    }
}

public record RevokeUserPoolAccessCommand(Guid UserPoolId, Guid PrincipalId, string Relation)
    : ICommand<AccessChangeResponse>;

public class RevokeUserPoolAccessCommandHandler(
    IAccessManager accessManager,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<RevokeUserPoolAccessCommand, AccessChangeResponse>
{
    public async Task<AccessChangeResponse> ExecuteAsync(RevokeUserPoolAccessCommand command, CancellationToken ct)
    {
        // Owner yetkisini yalnızca owner geri alabilir.
        if (string.Equals(command.Relation, AuthConstants.OwnerRelation, StringComparison.OrdinalIgnoreCase))
        {
            var check = await authorizationClient.CheckAsync(
                new CheckRequest
                {
                    IsolationId = requestContextAccessor.Context().Isolation.ToString(),
                    User = AuthConstants.User(requestContextAccessor.Context().PrincipalId),
                    Relation = AuthConstants.OwnerRelation,
                    Object = AuthConstants.Pool(command.UserPoolId)
                },
                cancellationToken: ct);

            if (!check.Allowed)
                throw new ForbiddenException(AuthNMessageKeys.PoolOwnerRelationRequiresOwner);
        }

        await accessManager.RevokeAsync(
            AuthConstants.PoolType, command.UserPoolId, command.PrincipalId, command.Relation, ct);

        return new AccessChangeResponse(command.PrincipalId, command.Relation);
    }
}

public class RevokeUserPoolAccessValidator : Validator<RevokeUserPoolAccessCommand>
{
    public RevokeUserPoolAccessValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.PrincipalId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Relation).AssignableRelation();
    }
}
