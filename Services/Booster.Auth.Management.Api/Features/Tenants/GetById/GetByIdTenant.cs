using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Mapster;

namespace Booster.Auth.Management.Api.Features.Tenants.GetById;

public class GetByIdTenantEndpoint : EndpointWithoutRequest<GetByIdTenantResponse>
{
    public override void Configure()
    {
        // Kimlik rotada DEĞİL sorguda: yetki politikası hedefi sorgu
        // dizesinden okuyor ve tüm firma uçları aynı parametreyi kullanıyor.
        // Kimi rotadan kimi sorgudan alsaydı, her uç için ayrı seçici
        // yazmak gerekirdi.
        Get("detail");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.TenantType, ManagementPolicies.TenantTarget, AuthConstants.ViewerRelation)));

        Summary(s =>
        {
            s.Summary = "Tenant getirir";
            s.Description = "TenantId sorgu parametresiyle tek bir kiracının detayını döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetByIdTenantQuery(Query<Guid>("TenantId")).ExecuteAsync(ct), ct);
}

public record GetByIdTenantQuery(Guid TenantId) : ICommand<GetByIdTenantResponse>;

public readonly record struct GetByIdTenantResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedTime,
    DateTimeOffset? UpdatedTime,
    int UserPoolCount);

public class GetByIdTenantQueryHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository)
    : ICommandHandler<GetByIdTenantQuery, GetByIdTenantResponse>
{
    public async Task<GetByIdTenantResponse> ExecuteAsync(GetByIdTenantQuery command, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(
            t => t.Id == command.TenantId, enableTracking: false, cancellationToken: ct);
        tenant.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.TenantNotFound);

        // Havuz sayısı detay kartında gösteriliyor. Ekran zaten bu uca
        // gidiyor; ayrı bir sayım isteği atmak fazladan gidiş dönüş olurdu.
        var poolCount = await userPoolRepository.CountAsync(
            p => p.TenantId == command.TenantId, cancellationToken: ct);

        return tenant!.Adapt<GetByIdTenantResponse>() with { UserPoolCount = poolCount };
    }
}
