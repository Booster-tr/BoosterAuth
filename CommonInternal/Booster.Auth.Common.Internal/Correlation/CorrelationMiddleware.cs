using Microsoft.AspNetCore.Http;

namespace Booster.Auth.Common.Internal.Correlation;

/// <summary>Her istege bir correlation id verir (gelen gecerliyse o kullanilir) ve yanitta da dondurur.</summary>
public class CorrelationMiddleware(RequestDelegate next)
{
    // ExceptionManager'daki HeaderConstants.CorrelationIdHeaderName ile AYNI olmalı:
    // hata işleyici id'yi bu başlıktan okur. Farklı olsaydı hata loglarında id hep boş kalırdı.
    public const string CorrelationIdHeaderKey = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationHeader = context.Request.Headers[CorrelationIdHeaderKey];
        var correlationStr = (correlationHeader.Count > 0 && Guid.TryParse(correlationHeader[0], out var parsedId))
            ? parsedId.ToString()
            : Guid.NewGuid().ToString();

        // İstek başlığına yazılır: hata işleyici bu ara katmanın DIŞINDA çalışır, id'yi başlıktan okur.
        context.Request.Headers[CorrelationIdHeaderKey] = correlationStr;

        // İstemci (panel, mobil) bir hatayı bildirirken bu id'yi verebilsin.
        context.Response.Headers[CorrelationIdHeaderKey] = correlationStr;

        await next(context);
    }
}
