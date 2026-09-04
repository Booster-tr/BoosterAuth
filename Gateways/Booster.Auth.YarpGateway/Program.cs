using Booster.Auth.YarpGateway;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

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
                Ipv6Helper.Get(ctx),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 25, Window = TimeSpan.FromSeconds(1) })),

        // 3. Genel eşzamanlılık: Tüm gateway için en fazla 300 eşzamanlı istek
        PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetConcurrencyLimiter("global-concurrency",
                _ => new ConcurrencyLimiterOptions { PermitLimit = 300 }))
    );
});

var app = builder.Build();

app.UseRateLimiter();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        await next();

    });
});

app.Run();


