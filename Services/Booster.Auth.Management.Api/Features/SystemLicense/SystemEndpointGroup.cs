using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.SystemLicense;

public class SystemEndpointGroup : Group
{
    public SystemEndpointGroup()
        => Configure("system", ep => ep.Description(b => b.WithTags("System")));
}
