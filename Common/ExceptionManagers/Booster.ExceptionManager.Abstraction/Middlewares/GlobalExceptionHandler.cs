using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.ExceptionManager.Abstraction.Translations;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.ExceptionManager.Abstraction.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
       HttpContext httpContext,
       Exception exception,
       CancellationToken cancellationToken)
    {
        var scope = httpContext.RequestServices;
        var translationService = scope.GetRequiredService<ITranslationManager>();
        //var logWriter = scope.GetRequiredService<ILogWriter>();
        var correlationId = httpContext.Request.Headers[HeaderConstants.CorrelationIdHeaderName].ToString() ?? "unknown";
        
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
        // ForbiddenException BusinessException'dan türediği için ONDAN ÖNCE gelmeli:
        // yetki reddi hatalı istek değildir, istemci ikisini durum kodundan ayırt edebilmeli.
        else if (exception is ForbiddenException fEx)
        {
            statusCode = StatusCodes.Status403Forbidden;
            errorMessage = await translationService.GetAsync(fEx.Message, langHeader, cancellationToken);
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

        
        //if (logWriter != null)
        //{
        //    var logParameters = new Dictionary<string, string?>
        //    {
        //        { "CorrelationId", correlationId }
        //    };

        //    if (statusCode == StatusCodes.Status500InternalServerError)
        //    {

        //        await logWriter.LogErrorAsync(errorMessage, exception, logParameters , cancellationToken);
        //    }
        //    else
        //    {
        //        await logWriter.LogWarningAsync(errorMessage, logParameters, cancellationToken);
        //    }
        //}


        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = await translationService.GetAsync(ValidationErrors.ProblemTitle, langHeader),
            Detail = errorMessage,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;

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
