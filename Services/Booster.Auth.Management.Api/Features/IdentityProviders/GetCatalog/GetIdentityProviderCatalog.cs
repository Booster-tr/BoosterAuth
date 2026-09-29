using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features.IdentityProviders._base;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.IdentityProviders.GetCatalog;

public class GetIdentityProviderCatalogEndpoint : EndpointWithoutRequest<IdentityProviderCatalogResponse>
{
    public override void Configure()
    {
        Get("catalog");
        Group<IdentityProvidersEndpointGroup>();

        Options(x => x.RequireAuthorization(new RequireScopeAttribute(ManagementPolicies.PanelScope)));

        Summary(s => s.Summary = "Sistemde desteklenen dış kimlik sağlayıcı türlerini ve alanlarını döner");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(
            new IdentityProviderCatalogResponse(
                FederationProviderCatalog.All
                    .Order(StringComparer.Ordinal)
                    .Select(type => new ProviderTypeResponse(type, FederationProviderCatalog.FieldsOf(type)))
                    .ToList()),
            ct);
}

public record IdentityProviderCatalogResponse(IReadOnlyList<ProviderTypeResponse> Items);

public record ProviderTypeResponse(string Type, IReadOnlyList<ProviderField> Fields);
