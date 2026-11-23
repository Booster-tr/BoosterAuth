using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.ExceptionManager.Abstraction.Translations;
using Booster.Logging.Abstraction;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.ExceptionManager.Abstraction.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly string ServiceName =
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";

    public async ValueTask<bool> TryHandleAsync(
       HttpContext httpContext,
       Exception exception,
       CancellationToken cancellationToken)
    {
        var scope = httpContext.RequestServices;
        var translationService = scope.GetRequiredService<ITranslationManager>();

        var logWriter = scope.GetService<ILogWriter>();
        // CorrelationMiddleware id'yi bu başlığa yazar; ondan önce patlayan istekte başlık yoktur.
        var correlationId = httpContext.Request.Headers[HeaderConstants.CorrelationIdHeaderName].ToString();
        if (string.IsNullOrEmpty(correlationId))
            correlationId = "unknown";
        
        var langHeader = httpContext.Request.Headers.AcceptLanguage.FirstOrDefault() ?? "tr-tr";
        langHeader = langHeader.Length > 5 ? langHeader[..5].ToLowerInvariant() : langHeader.ToLowerInvariant();

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

        var statusCode = StatusCodes.Status500InternalServerError;
        var errorMessage = string.Empty;
        var validationErrors = new List<object>();

        if (exception is ValidationException vEx)
        {
            statusCode = StatusCodes.Status400BadRequest;

            var results = await Task.WhenAll(
                          vEx.Errors.Select(async error =>
                          {
                              var finalMsg = await translationService.GetAsync(
                                  error.ErrorMessage,
                                  langHeader);

                              if (error.FormattedMessagePlaceholderValues != null)
                              {
                                  foreach (var placeholder in error.FormattedMessagePlaceholderValues)
                                  {
                                      finalMsg = finalMsg.Replace($"{{{placeholder.Key}}}", placeholder.Value?.ToString());
                                  }
                              }

                              return new
                              {
                                  property = error.PropertyName,
                                  message = finalMsg
                              };
                          })
                      );

            validationErrors.AddRange(results);
            errorMessage = await translationService.GetAsync(ValidationErrors.ValidationError, langHeader, cancellationToken);
        }

        else if (exception is ForbiddenException fEx)
        {
            statusCode = StatusCodes.Status403Forbidden;
            errorMessage = await translationService.GetAsync(fEx.Message, langHeader, cancellationToken);
        }
        else if (exception is TooManyRequestsException tEx)
        {
            statusCode = StatusCodes.Status429TooManyRequests;
            errorMessage = await translationService.GetAsync(tEx.Message, langHeader, cancellationToken);
        }
        else if (exception is BusinessException bEx)
        {
            statusCode = StatusCodes.Status400BadRequest;
            errorMessage = await translationService.GetAsync(bEx.Message, langHeader, cancellationToken);
        }
        else
        {
            errorMessage = await translationService.GetAsync(ValidationErrors.InternalServerError, langHeader, cancellationToken);
        }

        
        if (logWriter is not null)
        {
            var logParameters = new Dictionary<string, string?>
            {
                ["CorrelationId"] = correlationId,
                ["IpAddress"] = ipAddress,
                ["ServiceName"] = ServiceName,
                ["Path"] = httpContext.Request.Path,
                ["StatusCode"] = statusCode.ToString()
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
                await logWriter.LogErrorAsync(errorMessage, exception, logParameters, cancellationToken);
            else
                await logWriter.LogWarningAsync(errorMessage, logParameters, cancellationToken);
        }


        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = await translationService.GetAsync(ValidationErrors.ProblemTitle, langHeader),
            Detail = errorMessage,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;

        // Is kurali hatalarinda mesaj anahtari makine okunur kod olarak da doner (or. LICENSE_RESTRICTED);
        // istemci metne degil koda gore davranir (panel: lisans ekranina yonlendirir).
        if (exception is BusinessException codeEx)
            problemDetails.Extensions["code"] = codeEx.Message;

        if (validationErrors.Any())
        {
            problemDetails.Extensions["errors"] = validationErrors;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);


        return true;
    }
}
