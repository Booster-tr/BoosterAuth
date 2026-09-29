using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Features.Tenants.Access.Grant;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
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

public class RevokeTenantAccessCommandHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository,
    IAccessManager accessManager)
    : ICommandHandler<RevokeTenantAccessCommand, TenantAccessChangeResponse>
{
    public async Task<TenantAccessChangeResponse> ExecuteAsync(
        RevokeTenantAccessCommand command, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(
            t => t.Id == command.TenantId, enableTracking: false, cancellationToken: ct);
        tenant.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.TenantNotFound);

        // Firma sahibinin HİÇBİR yetkisi geri alınamaz. Alınabilseydi, başka
        // bir owner sahibi dışarıda bırakabilir ya da sahip yanlışlıkla
        // kendini firmadan düşürüp kurtarılamaz bir durum yaratabilirdi.
        if (tenant!.OwnerId == command.PrincipalId)
            throw new ForbiddenException(AuthNMessageKeys.TenantOwnerAccessCannotBeRevoked);

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
