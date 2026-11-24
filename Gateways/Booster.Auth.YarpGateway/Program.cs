using Booster.Observability;
using Booster.Auth.YarpGateway;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

var clientIpOptions = builder.Configuration.GetSection("ClientIp").Get<ClientIpOptions>() ?? new ClientIpOptions();
var clientIp = new ClientIpResolver(clientIpOptions);

// Log + trace + metrik (OpenTelemetry). Trace burada baslar ve arka servislere traceparent ile gecer.
builder.Services.AddBoosterObservability(builder.Configuration, "Booster.Auth.YarpGateway");

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformContext =>
    {
        transformContext.AddRequestTransform(async vtc =>
        {
            //var headers = vtc.HttpContext.Request.Headers;

        });
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        // 1. App Key bazlı: Aynı uygulama anahtarından saniyede en fazla 50 istek
        //PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        //    RateLimitPartition.GetFixedWindowLimiter(
        //        ctx.Request.Headers["x-api-key"].FirstOrDefault() ?? "anonymous",
        //        _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromSeconds(1) })),

        // 2. IP bazlı: Aynı IP saniyede en fazla 25 istek
        PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 25, Window = TimeSpan.FromSeconds(1) })),

        // 3. Genel eşzamanlılık: Tüm gateway için en fazla 300 eşzamanlı istek
        PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetConcurrencyLimiter("global-concurrency",
                _ => new ConcurrencyLimiterOptions { PermitLimit = 300 }))
    );
});

var app = builder.Build();

// Once gercek istemci IP'si: hiz siniri ve arka servislere giden X-Forwarded-For bunu kullanir (bkz. ClientIpResolver).
app.Use((context, next) =>
{
    context.Connection.RemoteIpAddress = clientIp.Resolve(context);
    return next(context);
});

app.UseRateLimiter();

// Panelin "Servis Durumu" ekrani icin: diger servislerdeki GET /version ile ayni cevap
// (Common.Internal/VersionEndpointExtensions). Gizli bilgi icermez.
var startedAt = DateTimeOffset.UtcNow;
app.MapGet("/version", (IConfiguration configuration, IHostEnvironment environment) =>
    Results.Ok(new
    {
        service = environment.ApplicationName,
        version = configuration["Version"] ?? "unknown",
        build = configuration["BUILD_REVISION"],
        startedAt
    }));

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        if (context.Request.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });
});

app.Run();


