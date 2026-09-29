using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Tenants.Access.Revoke;

public class RevokeTenantAccessEndpoint : EndpointWithoutRequest<AccessChangeResponse>
{
    public override void Configure()
    {
        Delete("access");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.TenantType, ManagementPolicies.TenantTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Firma üzerindeki yetkiyi geri alır";
            s.Description = "Parametreler SORGUDA taşınır: DELETE gövdesini yol üzerindeki ara " +
                            "katmanlar düşürebiliyor. Yalnızca YAZILMIŞ yetki geri alınabilir; " +
                            "türetilmiş yetki (owner'ın viewer'lığı) kaynağı kaldırılmadan kaybolmaz.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var command = new RevokeTenantAccessCommand(
            Query<Guid>("TenantId"),
            Query<Guid>("PrincipalId", isRequired: false),
            Query<string>("Relation", isRequired: false) ?? string.Empty);

        await Send.OkAsync(await command.ExecuteAsync(ct), ct);
    }
}

public record RevokeTenantAccessCommand(Guid TenantId, Guid PrincipalId, string Relation)
    : ICommand<AccessChangeResponse>;

public class RevokeTenantAccessCommandHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository,
    IAccessManager accessManager,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<RevokeTenantAccessCommand, AccessChangeResponse>
{
    public async Task<AccessChangeResponse> ExecuteAsync(
        RevokeTenantAccessCommand command, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(
            t => t.Id == command.TenantId, enableTracking: false, cancellationToken: ct);
        tenant.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.TenantNotFound);

        if (tenant!.OwnerId == command.PrincipalId)
            throw new ForbiddenException(AuthNMessageKeys.TenantOwnerAccessCannotBeRevoked);

        if (string.Equals(command.Relation, AuthConstants.OwnerRelation, StringComparison.OrdinalIgnoreCase))
        {
            var check = await authorizationClient.CheckAsync(
                new CheckRequest
                {
                    IsolationId = requestContextAccessor.Context().Isolation.ToString(),
                    User = AuthConstants.User(requestContextAccessor.Context().PrincipalId),
                    Relation = AuthConstants.OwnerRelation,
                    Object = AuthConstants.Tenant(command.TenantId)
                },
                cancellationToken: ct);

            if (!check.Allowed)
                throw new ForbiddenException(AuthNMessageKeys.TenantOwnerRelationRequiresOwner);
        }

        await accessManager.RevokeAsync(
            AuthConstants.TenantType, command.TenantId, command.PrincipalId, command.Relation, ct);

        return new AccessChangeResponse(command.PrincipalId, command.Relation);
    }
}

public class RevokeTenantAccessValidator : Validator<RevokeTenantAccessCommand>
{
    public RevokeTenantAccessValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.PrincipalId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Relation).AssignableRelation();
    }
}
