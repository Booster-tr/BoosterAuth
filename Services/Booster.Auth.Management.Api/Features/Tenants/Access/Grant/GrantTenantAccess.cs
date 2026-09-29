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

namespace Booster.Auth.Management.Api.Features.Tenants.Access.Grant;

public class GrantTenantAccessEndpoint : Endpoint<GrantTenantAccessCommand, AccessChangeResponse>
{
    public override void Configure()
    {
        Post("access");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.TenantType, ManagementPolicies.TenantTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Firma üzerinde yetki verir";
            s.Description = "Kullanıcı e-posta, kullanıcı adı ya da telefon numarasıyla bulunur. " +
                            "Aramalar kişi başına sınırlıdır. Aynı yetki tekrar verilirse sonuç değişmez.";
        });
    }

    public override async Task HandleAsync(GrantTenantAccessCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record GrantTenantAccessCommand(Guid TenantId, string Identifier, string Relation)
    : ICommand<AccessChangeResponse>;

public class GrantTenantAccessCommandHandler(
    IPrincipalLookupManager principalLookupManager,
    IAccessManager accessManager,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<GrantTenantAccessCommand, AccessChangeResponse>
{
    public async Task<AccessChangeResponse> ExecuteAsync(
        GrantTenantAccessCommand command, CancellationToken ct)
    {

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

        var principalId = await principalLookupManager.FindAsync(command.Identifier, ct);

        await accessManager.GrantAsync(
            AuthConstants.TenantType, command.TenantId, principalId, command.Relation, ct);

        return new AccessChangeResponse(principalId, command.Relation);
    }
}

public class GrantTenantAccessValidator : Validator<GrantTenantAccessCommand>
{
    public GrantTenantAccessValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(320).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.Relation).AssignableRelation();
    }
}
