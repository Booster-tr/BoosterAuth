using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Authorization.Fga.Attributes;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Tenants.Access.Grant;

// Yetki vermek owner işidir. Manager'a açsaydık, yönetici kendine
// owner yazıp sahibi devre dışı bırakabilirdi.
public class GrantTenantAccessEndpoint : Endpoint<GrantTenantAccessCommand, TenantAccessChangeResponse>
{
    public override void Configure()
    {
        Post("access");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.TenantType, ManagementPolicies.TenantTarget, AuthConstants.OwnerRelation)));

        Summary(s =>
        {
            s.Summary = "Firma üzerinde yetki verir";
            s.Description = "Aynı yetki tekrar verilirse sonuç değişmez; işlem idempotenttir.";
        });
    }

    public override async Task HandleAsync(GrantTenantAccessCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record GrantTenantAccessCommand(Guid TenantId, Guid PrincipalId, string Relation)
    : ICommand<TenantAccessChangeResponse>;

public readonly record struct TenantAccessChangeResponse(Guid PrincipalId, string Relation);

public class GrantTenantAccessCommandHandler(IAccessManager accessManager)
    : ICommandHandler<GrantTenantAccessCommand, TenantAccessChangeResponse>
{
    public async Task<TenantAccessChangeResponse> ExecuteAsync(
        GrantTenantAccessCommand command, CancellationToken ct)
    {
        await accessManager.GrantAsync(
            AuthConstants.TenantType, command.TenantId, command.PrincipalId, command.Relation, ct);

        return new TenantAccessChangeResponse(command.PrincipalId, command.Relation);
    }
}

public class GrantTenantAccessValidator : Validator<GrantTenantAccessCommand>
{
    public GrantTenantAccessValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.PrincipalId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Relation).TenantRelation();
    }
}
