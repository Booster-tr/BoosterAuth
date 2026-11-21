using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Booster.Auth.Common.Internal.Extensions;

/// <summary>
/// <c>GET /version</c>: servisin adi, surumu ve calisan build'i (kimlik dogrulamasiz).
/// <para>
/// Yonetim panelinin "Servis Durumu" ekrani tum servislere bu istegi atar: cevap
/// gelirse servis ayakta, gelmezse ulasilamiyor. Surum appsettings'teki "Version"
/// anahtarindan, build ise image'a CI'da yazilan BUILD_REVISION'dan (commit) gelir.
/// Gizli bilgi icermez.
/// </para>
/// </summary>
public static class VersionEndpointExtensions
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    public static IEndpointRouteBuilder MapBoosterVersion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/version", (IConfiguration configuration, IHostEnvironment environment) =>
                Results.Ok(new VersionInfo(
                    environment.ApplicationName,
                    configuration["Version"] ?? "unknown",
                    configuration["BUILD_REVISION"],
                    StartedAt)))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    public sealed record VersionInfo(string Service, string Version, string? Build, DateTimeOffset StartedAt);
}
