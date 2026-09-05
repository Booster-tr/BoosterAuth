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
    IdpUiOptions uiOptions,
    ITranslationManager translationManager) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var lang = ResolveLang(httpContext);

        switch (exception)
        {
            case AuthorizeRedirectException ex:
                {
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

    private async Task WriteProblemAsync(
        HttpContext ctx, int status, string detail, string lang, CancellationToken ct, object? errors = null)
    {
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

    private static string ResolveLang(HttpContext ctx)
    {
        var lang = ctx.Request.Cookies["accept-language"]?.ToString() ?? "tr-tr";
        return lang.Length > 5 ? lang[..5].ToLowerInvariant() : lang.ToLowerInvariant();
    }
}