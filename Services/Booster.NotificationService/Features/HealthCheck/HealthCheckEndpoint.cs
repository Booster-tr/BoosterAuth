namespace Booster.NotificationService.Features.HealthCheck;

using FastEndpoints;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class HealthCheckEndpoint(HealthCheckService healthService) : EndpointWithoutRequest<object>
{
    public override void Configure()
    {
        Get("health");
        AllowAnonymous();
        Description(x => x
            .WithTags("Infrastructure")
            .Produces(200)
            .Produces(503));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var report = await healthService.CheckHealthAsync(ct);

        if (report.Status == HealthStatus.Healthy)
        {
            await Send.ResponseAsync(new { Status = "Healthy", Machine = Environment.MachineName }, 200, ct);
        }
        else
        {
            await Send.ResponseAsync(new { Status = "Unhealthy", Details = report.Entries.Values.Select(x => x.Description) }, 503, ct);
        }
    }
}
