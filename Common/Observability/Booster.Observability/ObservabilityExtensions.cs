using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Booster.Observability;

/// <summary>
/// Tum servislerin ortak gozlem kurulumu: OpenTelemetry ile log + trace + metrik.
/// <list type="bullet">
/// <item>Loglar: isletim loglari <c>LogStream</c>'e, denetim kayitlari (<see cref="AuditCategory"/>) <c>AuditStream</c>'e
/// ayri ayri gider; ikisi de once <see cref="SensitiveDataRedactor"/>'dan gecer.</item>
/// <item>Trace: gelen HTTP / gRPC istekleri, giden HttpClient (gRPC dahil), Npgsql ve MassTransit (Kafka). Her log
/// satiri bulundugu istegin <c>trace_id</c>'sini tasir; log ile trace arasinda gecis yapilir.</item>
/// <item>Metrik: ASP.NET Core, HttpClient ve .NET calisma zamani.</item>
/// </list>
/// OTLP adresi verilmezse hicbir sey disariya gonderilmez; konsol logu (stdout) her durumda calisir.
/// </summary>
public static class ObservabilityExtensions
{
    public static IServiceCollection AddBoosterObservability(
        this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var options = configuration.GetSection("Observability").Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        var endpoint = (options.OtlpEndpoint ?? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])?.Trim().TrimEnd('/');
        var headers = options.OtlpHeaders ?? configuration["OTEL_EXPORTER_OTLP_HEADERS"];
        var enabled = !string.IsNullOrEmpty(endpoint);
        var version = configuration["Version"] ?? "unknown";

        void Resource(ResourceBuilder r) => r.AddService(serviceName, serviceVersion: version);

        services.AddLogging(logging =>
        {
            // Gurultulu ve kisisel veri tasiyabilen kutuphane loglari (or. FusionCache anahtarlari e-posta icerebilir,
            // EF Core SQL metni) Debug'ta kalir; isletim icin Warning yeter.
            logging.AddFilter("ZiggyCreatures", LogLevel.Warning);
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            logging.AddFilter("MassTransit", LogLevel.Warning);
            logging.AddFilter("Grpc", LogLevel.Warning);
            logging.AddFilter("System.Net.Http", LogLevel.Warning);

            if (!enabled)
                return;

            logging.AddOpenTelemetry(o =>
            {
                var resource = ResourceBuilder.CreateDefault();
                Resource(resource);
                o.SetResourceBuilder(resource);
                o.IncludeFormattedMessage = true;
                o.IncludeScopes = true;
                o.ParseStateValues = true;

                // Siralama onemli: once temizlik, sonra disa aktarma.
                o.AddProcessor(new SensitiveDataRedactor());
                o.AddProcessor(new FilteringLogProcessor(
                    new BatchLogRecordExportProcessor(new OtlpLogExporter(Exporter(endpoint!, "logs", headers, options.LogStream))),
                    r => r.CategoryName != AuditCategory.Name));
                o.AddProcessor(new FilteringLogProcessor(
                    new BatchLogRecordExportProcessor(new OtlpLogExporter(Exporter(endpoint!, "logs", headers, options.AuditStream))),
                    r => r.CategoryName == AuditCategory.Name));
            });
        });

        if (!enabled)
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(Resource)
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(a =>
                {
                    // Saglik / surum yoklamalari trace uretmesin (her 10 sn'de bir).
                    a.Filter = ctx => !IsProbe(ctx.Request.Path);
                })
                .AddHttpClientInstrumentation()
                .AddSource("MassTransit", "Npgsql")
                .AddOtlpExporter(e => Apply(e, Exporter(endpoint!, "traces", headers, null))))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(e => Apply(e, Exporter(endpoint!, "metrics", headers, null))));

        return services;
    }

    private static bool IsProbe(PathString path)
        => path.StartsWithSegments("/version") || path.StartsWithSegments("/health") || path.StartsWithSegments("/healthz");

    private static OtlpExporterOptions Exporter(string endpoint, string signal, string? headers, string? stream)
    {
        // "ad=deger" olmayan parcalar (or. elle doldurulmamis "CHANGE_ME") yok sayilir; gecersiz baslik servisi dusurmesin.
        var all = string.Join(',', (headers ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(h => h.IndexOf('=') > 0));
        // OpenObserve akisi "stream-name" basligiyla secer; diger OTLP alicilari bu basligi yok sayar.
        if (!string.IsNullOrEmpty(stream))
            all = string.IsNullOrEmpty(all) ? $"stream-name={stream}" : $"{all},stream-name={stream}";

        return new OtlpExporterOptions
        {
            Endpoint = new Uri($"{endpoint}/v1/{signal}"),
            Protocol = OtlpExportProtocol.HttpProtobuf,
            Headers = all
        };
    }

    private static void Apply(OtlpExporterOptions target, OtlpExporterOptions source)
    {
        target.Endpoint = source.Endpoint;
        target.Protocol = source.Protocol;
        target.Headers = source.Headers;
    }
}
