using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Options;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.ExceptionManager.Abstraction.Translations;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Managers.ExceptionManagers;

public sealed class IdpExceptionHandler(
    IdpUiOptions uiOptions) : IExceptionHandler
{
    /// <summary>
    /// Tek bir hatanin islenmesi icin gereken her sey.
    /// <para>
    /// Isleyici SINGLETON'dir; bunlar alanda tutulsaydi ayni anda gelen iki hata
    /// birbirinin ceviri servisini, log yazicisini ve istisnasini ezerdi (yanlis log).
    /// </para>
    /// </summary>
    private readonly record struct ErrorContext(
        HttpContext Http,
        Exception Exception,
        ITranslationManager Translator,
        ILogger Logger,
        string Lang);

    /// <summary>Log kaydinda hatanin hangi servisten geldigi gorunsun diye.</summary>
    private static readonly string ServiceName =
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var lang = ResolveLang(httpContext);
        var scope = httpContext.RequestServices;
        var translationManager = scope.GetRequiredService<ITranslationManager>();

        // GetService, GetRequiredService DEGIL: AMQP logger kayitli degilse
        // hata isleyicinin kendisi patlamamali — o an zaten bir hata isleniyor.
        var error = new ErrorContext(httpContext, exception, translationManager, scope.GetRequiredService<ILoggerFactory>().CreateLogger("Booster.Errors"), lang);

        switch (exception)
        {
            case AuthorizeRedirectException ex:
                {
                    // Protokol hatası: istemciye yönlendirmeyle bildirilir, ama
                    // bir arıza olmadığı için uyarı seviyesinde kayda geçer.
                    await LogAsync(error, LogLevel.Warning, ex.Error, StatusCodes.Status302Found, ct);

                    var builder = new UriBuilder(ex.RedirectUri);
                    var query = $"error={Uri.EscapeDataString(ex.Error)}";
                    if (!string.IsNullOrEmpty(ex.Description))
                        query += $"&error_description={Uri.EscapeDataString(ex.Description)}";
                    if (!string.IsNullOrEmpty(ex.State))
                        query += $"&state={Uri.EscapeDataString(ex.State)}";
                    builder.Query = query;
                    httpContext.Response.Redirect(builder.ToString());
                    return true;
                }

            case AuthorizeException ex:
                {
                    var msg = string.IsNullOrEmpty(ex.Description)
                        ? await translationManager.GetAsync(ex.ErrorKey, lang, ct)
                        : ex.Description;
                    await LogAsync(error, LogLevel.Warning, ex.ErrorKey, StatusCodes.Status302Found, ct);
                    httpContext.Response.Redirect(uiOptions.GetErrorUrl(ex.ErrorKey, msg));
                    return true;
                }

            
            case ValidationException vEx:
                {
                    // /authorize tarayicida acilir: ham JSON yerine hata sayfasi. redirect_uri henuz dogrulanmadigi icin
                    // uygulamaya yonlendirilmez (acik yonlendirme olmasin).
                    if (httpContext.Request.Path.Equals("/authorize", StringComparison.OrdinalIgnoreCase))
                    {
                        var first = vEx.Errors.FirstOrDefault()?.ErrorMessage ?? ValidationErrors.ValidationError;
                        await LogAsync(error, LogLevel.Warning, first, StatusCodes.Status302Found, ct);
                        httpContext.Response.Redirect(uiOptions.GetErrorUrl("invalid_request", await translationManager.GetAsync(first, lang, ct)));
                        return true;
                    }

                    var errors = await Task.WhenAll(vEx.Errors.Select(async e => new
                    {
                        property = e.PropertyName,
                        message = await translationManager.GetAsync(e.ErrorMessage, lang, ct)
                    }));
                    await WriteProblemAsync(error, StatusCodes.Status400BadRequest,
                        await translationManager.GetAsync(ValidationErrors.ValidationError, lang, ct),
                        ct, errors);
                    return true;
                }

            case ForbiddenException fEx:
                {
                    await WriteProblemAsync(error, StatusCodes.Status403Forbidden,
                        await translationManager.GetAsync(fEx.Message, lang, ct), ct);
                    return true;
                }

            case BusinessException bEx:
                {
                    await WriteProblemAsync(error, StatusCodes.Status400BadRequest,
                        await translationManager.GetAsync(bEx.Message, lang, ct), ct);
                    return true;
                }

            default:
                {
                    await WriteProblemAsync(error, StatusCodes.Status500InternalServerError,
                        await translationManager.GetAsync(ValidationErrors.InternalServerError, lang, ct), ct);
                    return true;
                }
        }
    }

    /// <summary>
    /// Hatayı AMQP log hattına yazar.
    /// <para>
    /// 500 gerçek bir arıza, diğerleri çağıranın hatası. Aynı seviyeye
    /// yazılsalardı doğrulama hataları gerçek arızaları gürültüye boğardı.
    /// </para>
    /// </summary>
    private static Task LogAsync(
        ErrorContext e, LogLevel level, string detail, int status, CancellationToken ct)
    {
        var ctx = e.Http;
        // Standart ILogger: OpenTelemetry ile trace_id'ye bagli gider; 500 disindakiler cagiranin hatasidir (Warning).
        e.Logger.Log(level, level >= LogLevel.Error ? e.Exception : null,
            "{Detail} status={StatusCode} path={Path} correlation={CorrelationId} service={ServiceName} ip={IpAddress}",
            detail, status, ctx.Request.Path.Value,
            ctx.Request.Headers[HeaderConstants.CorrelationIdHeaderName].ToString(), ServiceName,
            ctx.Connection.RemoteIpAddress?.ToString());
        return Task.CompletedTask;
    }

    private static async Task WriteProblemAsync(
        ErrorContext e, int status, string detail, CancellationToken ct, object? errors = null)
    {
        var ctx = e.Http;

        await LogAsync(
            e,
            status == StatusCodes.Status500InternalServerError ? LogLevel.Error : LogLevel.Warning,
            detail,
            status,
            ct);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = await e.Translator.GetAsync(ValidationErrors.ProblemTitle, e.Lang, ct),
            Detail = detail,
            Instance = ctx.Request.Path
        };
        if (errors is not null)
            problem.Extensions["errors"] = errors;

        // Mesaj anahtari makine okunur kod olarak da doner (or. LICENSE_SERVICE_UNAVAILABLE).
        if (e.Exception is BusinessException codeEx)
            problem.Extensions["code"] = codeEx.Message;

        // Token ucu: standart OAuth 2.0 hata govdesi (RFC 6749 5.2). Hazir OIDC kutuphaneleri yalnizca "error" alanini
        // okur; ProblemDetails alanlari ek bilgi olarak kalir.
        var contentType = "application/problem+json";
        if (ctx.Request.Path.Equals("/token", StringComparison.OrdinalIgnoreCase))
        {
            var oauthError = status == StatusCodes.Status500InternalServerError ? "server_error" : OAuthErrors.ForToken(e.Exception);
            problem.Extensions["error"] = oauthError;
            problem.Extensions["error_description"] = detail;
            contentType = "application/json";
            if (oauthError == OAuthErrors.InvalidClient)
            {
                status = StatusCodes.Status401Unauthorized;
                problem.Status = status;
            }
        }

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = contentType;
        await ctx.Response.WriteAsJsonAsync(problem, ct);
    }

    /// <summary>
    /// Dil önce çerezden, yoksa Accept-Language başlığından okunur.
    /// <para>
    /// Çerez, kullanıcının dil seçicisiyle yaptığı AÇIK tercihtir ve tarayıcı
    /// varsayılanını ezmelidir; ekran bir dilde, hata mesajı başka dilde
    /// çıkmamalı. Başlık ise çerezi olmayan istemciler (mobil, servisler arası
    /// entegrasyon) için geçerli kalır.
    /// </para>
    /// </summary>
    private static string ResolveLang(HttpContext ctx)
    {
        var lang = ctx.Request.Cookies["accept-language"];

        if (string.IsNullOrWhiteSpace(lang))
            lang = ctx.Request.Headers.AcceptLanguage.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(lang))
            return "tr-tr";

        lang = lang.Trim().ToLowerInvariant();
        return lang.Length > 5 ? lang[..5] : lang;
    }
}