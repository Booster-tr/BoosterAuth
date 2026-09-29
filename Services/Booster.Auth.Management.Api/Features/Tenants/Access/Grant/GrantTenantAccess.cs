using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Auth.Management.Api.Managers.PrincipalLookupManagers;
using Booster.Authorization.Fga.Attributes;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Tenants.Access.Grant;

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
            s.Description = "Kullanıcı e-posta, kullanıcı adı ya da telefon numarasıyla bulunur. " +
                            "Aramalar kişi başına sınırlıdır. Aynı yetki tekrar verilirse sonuç değişmez.";
        });
    }

    public override async Task HandleAsync(GrantTenantAccessCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record GrantTenantAccessCommand(Guid TenantId, string Identifier, string Relation)
    : ICommand<TenantAccessChangeResponse>;

public readonly record struct TenantAccessChangeResponse(Guid PrincipalId, string Relation);

public class GrantTenantAccessCommandHandler(
    IPrincipalLookupManager principalLookupManager,
    IAccessManager accessManager)
    : ICommandHandler<GrantTenantAccessCommand, TenantAccessChangeResponse>
{
    public async Task<TenantAccessChangeResponse> ExecuteAsync(
        GrantTenantAccessCommand command, CancellationToken ct)
    {
        var principalId = await principalLookupManager.FindAsync(command.Identifier, ct);

        await accessManager.GrantAsync(
            AuthConstants.TenantType, command.TenantId, principalId, command.Relation, ct);

        return new TenantAccessChangeResponse(principalId, command.Relation);
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

        RuleFor(x => x.Relation).TenantRelation();
    }
}
