using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.Helpers;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using Booster.Auth.Common.Internal.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.IdentityProviders.GetList;

public class GetListIdentityProviderEndpoint : EndpointWithoutRequest<ListModel<GetListIdentityProviderResponse>>
{
    public override void Configure()
    {
        Get("");
        Group<IdentityProvidersEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ViewerRelation)));
        Summary(s =>
        {
            s.Summary = "Dış kimlik sağlayıcılarını listeler";
            s.Description = "Havuz kapsamı için query string içine userPoolId filtresi verilir. " +
                            "İstemci sırrı DÖNMEZ ve dönmemesi gerekir: bir kez yazıldıktan sonra " +
                            "okunabilir olması, şifreli saklamanın anlamını ortadan kaldırırdı.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListIdentityProviderQuery(Query<Guid>("UserPoolId"))
        {
            DynamicQuery = nvc.ToDynamicFilter<GetListIdentityProviderResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListIdentityProviderQuery(Guid UserPoolId) : BaseDynamicRequest, ICommand<ListModel<GetListIdentityProviderResponse>>;

/// <summary>
/// <c>ClientSecretEncrypted</c> BU MODELDE YOK. Şifreli hâlini bile dışa
/// vermiyoruz: şifre metni, anahtara erişen biri için hâlâ çözülebilir bir
/// hedeftir ve yönetim arayüzünün ona ihtiyacı yok.
/// </summary>
public record GetListIdentityProviderResponse(
    Guid Id,
    Guid UserPoolId,
    string Key,
    string DisplayName,
    string ProviderType,
    EntityStatus Status,
    Dictionary<string, string> Settings,
    bool HasClientSecret);

public class GetListIdentityProviderQueryHandler(
    IAsyncRepository<IdentityProvider, AuthNDbContext> providerRepository)
    : ICommandHandler<GetListIdentityProviderQuery, ListModel<GetListIdentityProviderResponse>>
{
    public async Task<ListModel<GetListIdentityProviderResponse>> ExecuteAsync(
        GetListIdentityProviderQuery command, CancellationToken ct)
    {
        var data = await providerRepository.ListDynamicAsync(
            command.DynamicQuery,
            predicate: p => p.UserPoolId == command.UserPoolId,
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            cancellationToken: ct);

        return data.MapListModel<IdentityProvider, GetListIdentityProviderResponse>(command);
    }
}
