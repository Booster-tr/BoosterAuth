using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Amqp.Contexts;
using Booster.Auth.Idp.Options;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.ExceptionManager.Abstraction.Translations;
using Booster.Logging.Abstraction;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

// Microsoft.Extensions.Logging.LogLevel ImplicitUsings ile geliyor; hangisi
// kastedildigi acik olsun diye takma ad veriliyor.
using LogLevel = Booster.Logging.Abstraction.LogLevel;

namespace Booster.Auth.Idp.Managers.ExceptionManagers;

public sealed class IdpExceptionHandler(
    IdpUiOptions uiOptions) : IExceptionHandler
{
    private ITranslationManager translationManager = null!;
    private ILogWriter? logWriter;
    private ICorrelationContext? correlationContext;

    /// <summary>WriteProblemAsync log yazarken istisnaya erisebilsin diye tutulur.</summary>
    private Exception? CurrentException;

    /// <summary>Log kaydinda hatanin hangi servisten geldigi gorunsun diye.</summary>
    private static readonly string ServiceName =
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var lang = ResolveLang(httpContext);
        var scope = httpContext.RequestServices;
        translationManager = scope.GetRequiredService<ITranslationManager>();

        // GetService, GetRequiredService DEGIL: AMQP logger kayitli degilse
        // hata isleyicinin kendisi patlamamali — o an zaten bir hata isleniyor.
        logWriter = scope.GetService<ILogWriter>();
        correlationContext = scope.GetService<ICorrelationContext>();
        CurrentException = exception;

        switch (exception)
        {
            case AuthorizeRedirectException ex:
                {
                    // Protokol hatası: istemciye yönlendirmeyle bildirilir, ama
                    // bir arıza olmadığı için uyarı seviyesinde kayda geçer.
                    await LogAsync(httpContext, exception, LogLevel.Warning, ex.Error, StatusCodes.Status302Found, ct);

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
                    await LogAsync(httpContext, exception, LogLevel.Warning, ex.ErrorKey, StatusCodes.Status302Found, ct);
                    httpContext.Response.Redirect(uiOptions.GetErrorUrl(ex.ErrorKey, msg));
                    return true;
                }

            
            case ValidationException vEx:
                {
                    var errors = await Task.WhenAll(vEx.Errors.Select(async e => new
                    {
                        property = e.PropertyName,
                        message = await translationManager.GetAsync(e.ErrorMessage, lang, ct)
                    }));
                    await WriteProblemAsync(httpContext, StatusCodes.Status400BadRequest,
                        await translationManager.GetAsync(ValidationErrors.ValidationError, lang, ct),
                        lang, ct, errors);
                    return true;
                }

            case ForbiddenException fEx:
                {
                    await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden,
                        await translationManager.GetAsync(fEx.Message, lang, ct), lang, ct);
                    return true;
                }

            case BusinessException bEx:
                {
                    await WriteProblemAsync(httpContext, StatusCodes.Status400BadRequest,
                        await translationManager.GetAsync(bEx.Message, lang, ct), lang, ct);
                    return true;
                }

            default:
                {
                    await WriteProblemAsync(httpContext, StatusCodes.Status500InternalServerError,
                        await translationManager.GetAsync(ValidationErrors.InternalServerError, lang, ct), lang, ct);
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
    private async Task LogAsync(
        HttpContext ctx, Exception exception, LogLevel level, string detail, int status, CancellationToken ct)
    {
        if (logWriter is null)
            return;

        var properties = new Dictionary<string, string?>
        {
            ["CorrelationId"] = correlationContext?.CorrelationId,
            ["IpAddress"] = ctx.Connection.RemoteIpAddress?.ToString(),
            ["ServiceName"] = ServiceName,
            ["Path"] = ctx.Request.Path,
            ["StatusCode"] = status.ToString()
        };

        await logWriter.LogAsync(level, detail, level >= LogLevel.Error ? exception : null, properties, ct);
    }

    private async Task WriteProblemAsync(
        HttpContext ctx, int status, string detail, string lang, CancellationToken ct, object? errors = null)
    {
        await LogAsync(
            ctx,
            CurrentException!,
            status == StatusCodes.Status500InternalServerError ? LogLevel.Error : LogLevel.Warning,
            detail,
            status,
            ct);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = await translationManager.GetAsync(ValidationErrors.ProblemTitle, lang, ct),
            Detail = detail,
            Instance = ctx.Request.Path
        };
        if (errors is not null)
            problem.Extensions["errors"] = errors;

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(problem, ct);
    }

    /// <summary>
    /// Dil önce Accept-Language başlığından, yoksa çerezden okunur.
    /// <para>
    /// Yalnızca çereze bakılsaydı başlık gönderen her istemci (panel, mobil,
    /// servisler arası entegrasyon) hata mesajını daima Türkçe alırdı — çerez
    /// yalnızca tarayıcıdaki dil tercihini taşır.
    /// </para>
    /// </summary>
    private static string ResolveLang(HttpContext ctx)
    {
        var lang = ctx.Request.Headers.AcceptLanguage.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(lang))
            lang = ctx.Request.Cookies["accept-language"];

        if (string.IsNullOrWhiteSpace(lang))
            return "tr-tr";

        lang = lang.Trim().ToLowerInvariant();
        return lang.Length > 5 ? lang[..5] : lang;
    }
}