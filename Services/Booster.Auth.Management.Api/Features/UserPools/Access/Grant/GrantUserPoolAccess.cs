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

namespace Booster.Auth.Management.Api.Features.UserPools.Access.Grant;

public class GrantUserPoolAccessEndpoint : Endpoint<GrantUserPoolAccessCommand, AccessChangeResponse>
{
    public override void Configure()
    {
        Post("access");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuz üzerinde yetki verir";
            s.Description = "Kullanıcı e-posta, kullanıcı adı ya da telefon numarasıyla bulunur. Aramalar kişi başına sınırlıdır.";
        });
    }

    public override async Task HandleAsync(GrantUserPoolAccessCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record GrantUserPoolAccessCommand(Guid UserPoolId, string Identifier, string Relation)
    : ICommand<AccessChangeResponse>;

public class GrantUserPoolAccessCommandHandler(
    IPrincipalLookupManager principalLookupManager,
    IAccessManager accessManager,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<GrantUserPoolAccessCommand, AccessChangeResponse>
{
    public async Task<AccessChangeResponse> ExecuteAsync(GrantUserPoolAccessCommand command, CancellationToken ct)
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
                    Object = AuthConstants.Pool(command.UserPoolId)
                },
                cancellationToken: ct);

            if (!check.Allowed)
                throw new ForbiddenException(AuthNMessageKeys.PoolOwnerRelationRequiresOwner);
        }

        var principalId = await principalLookupManager.FindAsync(command.Identifier, ct);

        await accessManager.GrantAsync(
            AuthConstants.PoolType, command.UserPoolId, principalId, command.Relation, ct);

        return new AccessChangeResponse(principalId, command.Relation);
    }
}

public class GrantUserPoolAccessValidator : Validator<GrantUserPoolAccessCommand>
{
    public GrantUserPoolAccessValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(320).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.Relation).AssignableRelation();
    }
}
