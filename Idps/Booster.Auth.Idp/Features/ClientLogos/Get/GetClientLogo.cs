using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Stores.ClientLogoStores;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;

namespace Booster.Auth.Idp.Features.ClientLogos.Get;


public class GetClientLogoEndpoint(IClientLogoStore logoStore) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("clients/logo");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, IdpPolicies.ClientTarget, AuthConstants.ViewerRelation)));

        Summary(s => s.Summary = "Uygulama logosunu getirir; logo yoksa 404 döner");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var logo = logoStore.Find(Query<Guid>("ClientId"));

        FileStream stream;
        try
        {
            stream = File.OpenRead(logo?.PhysicalPath ?? throw new FileNotFoundException());
        }
        catch (FileNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
        await Send.StreamAsync(stream, contentType: logo!.ContentType, cancellation: ct);
    }
}
