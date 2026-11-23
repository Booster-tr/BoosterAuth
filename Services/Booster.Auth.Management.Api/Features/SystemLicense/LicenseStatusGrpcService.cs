using Booster.Auth.Management.Api.Licensing;
using Booster.Auth.Management.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Booster.Auth.Management.Api.Features.SystemLicense;

/// <summary>IdP ve AuthZ icin lisans durumu. Servisler arasi gRPC yetki istemez (yalnizca cluster ici erisim).</summary>
[AllowAnonymous]
public sealed class LicenseStatusGrpcService(LicenseService license) : LicenseStatusService.LicenseStatusServiceBase
{
    public override async Task<LicenseStatusMessage> GetStatus(GetLicenseStatusRequest request, ServerCallContext context)
    {
        var s = await license.GetAsync(context.CancellationToken);
        return new LicenseStatusMessage
        {
            State = (int)s.State,
            IsTrial = s.IsTrial,
            PanelWritable = s.PanelWritable,
            EndUserFlowsAllowed = s.EndUserFlowsAllowed,
            LimitsKnown = s.Limits is not null,
            MaxUsers = s.Limits?.Users ?? 0,
            MaxPools = s.Limits?.Pools ?? 0,
            MaxClients = s.Limits?.Clients ?? 0,
        };
    }
}
