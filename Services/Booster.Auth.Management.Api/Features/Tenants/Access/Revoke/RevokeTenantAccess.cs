using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features.Tenants.Access.Grant;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Authorization.Fga.Attributes;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Tenants.Access.Revoke;

public class RevokeTenantAccessEndpoint : EndpointWithoutRequest<TenantAccessChangeResponse>
{
    public override void Configure()
    {
        Delete("access");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.TenantType, ManagementPolicies.TenantTarget, AuthConstants.OwnerRelation)));

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
    : ICommand<TenantAccessChangeResponse>;

public class RevokeTenantAccessCommandHandler(IAccessManager accessManager)
    : ICommandHandler<RevokeTenantAccessCommand, TenantAccessChangeResponse>
{
    public async Task<TenantAccessChangeResponse> ExecuteAsync(
        RevokeTenantAccessCommand command, CancellationToken ct)
    {
        await accessManager.RevokeAsync(
            AuthConstants.TenantType, command.TenantId, command.PrincipalId, command.Relation, ct);

        return new TenantAccessChangeResponse(command.PrincipalId, command.Relation);
    }
}

public class RevokeTenantAccessValidator : Validator<RevokeTenantAccessCommand>
{
    public RevokeTenantAccessValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.PrincipalId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Relation).TenantRelation();
    }
}
