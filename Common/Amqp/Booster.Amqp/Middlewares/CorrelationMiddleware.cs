using Booster.Amqp.Contexts;
using Microsoft.AspNetCore.Http;

namespace Booster.Amqp.Middlewares;

public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    public CorrelationMiddleware(RequestDelegate next) => _next = next;

    // ExceptionManager'daki HeaderConstants.CorrelationIdHeaderName ile AYNI olmalı:
    // hata işleyici id'yi bu başlıktan okur. Farklı olsaydı hata loglarında id hep boş kalırdı.
    public const string CorrelationIdHeaderKey = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        var correlationHeader = context.Request.Headers[CorrelationIdHeaderKey];
        var correlationStr = (correlationHeader.Count > 0 && Guid.TryParse(correlationHeader[0], out var parsedId))
            ? parsedId.ToString()
            : Guid.NewGuid().ToString();

        correlationContext.SetCorrelationId(correlationStr);

        // İstek başlığına da yazılır: hata işleyici bu ara katmanın DIŞINDA çalışır ve
        // AsyncLocal değeri oraya geri akmaz; id'yi başlıktan okur.
        context.Request.Headers[CorrelationIdHeaderKey] = correlationStr;

        // İstemci (panel, mobil) bir hatayı bildirirken bu id'yi verebilsin.
        context.Response.Headers[CorrelationIdHeaderKey] = correlationStr;

        await _next(context);
    }
}
