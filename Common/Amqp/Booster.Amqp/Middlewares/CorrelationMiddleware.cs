using Booster.Amqp.Contexts;
using Microsoft.AspNetCore.Http;

namespace Booster.Amqp.Middlewares;

public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    public CorrelationMiddleware(RequestDelegate next) => _next = next;

    public const string CorrelationIdHeaderKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        var correlationHeader = context.Request.Headers[CorrelationIdHeaderKey];
        var correlationStr = (correlationHeader.Count > 0 && Guid.TryParse(correlationHeader[0], out var parsedId))
            ? parsedId.ToString()
            : Guid.NewGuid().ToString();

        correlationContext.SetCorrelationId(correlationStr);
        context.Request.Headers[CorrelationIdHeaderKey] = correlationStr;

        await _next(context);
    }
}
