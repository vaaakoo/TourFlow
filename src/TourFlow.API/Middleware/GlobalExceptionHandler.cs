using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TourFlow.Application.Operations;
using TourFlow.Domain.Exceptions;

namespace TourFlow.API.Middleware;

/// <summary>Provides a safe, consistent error response for unhandled exceptions.</summary>
public sealed partial class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = StatusCodes.Status500InternalServerError;
        var title = "An unexpected error occurred.";
        ProblemDetails? validationProblem = null;
        if (exception is ValidationException validation)
        {
            status = StatusCodes.Status400BadRequest;
            var errors = validation.Errors.GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            validationProblem = new ValidationProblemDetails(errors) { Status = status };
        }
        else if (exception is OperationConflictException)
        {
            status = StatusCodes.Status409Conflict;
            title = exception.Message;
        }
        else if (exception is OperationForbiddenException)
        {
            status = StatusCodes.Status403Forbidden;
            title = exception.Message;
        }
        else if (exception is DomainException)
        {
            status = StatusCodes.Status400BadRequest;
            title = exception.Message;
        }
        else if (exception is BadHttpRequestException { StatusCode: >= 400 and < 500 } badRequest)
        {
            status = badRequest.StatusCode;
            title = "The request could not be read.";
        }
        else
        {
            LogUnhandledException(logger, exception, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = validationProblem ?? new ProblemDetails
            {
                Status = status,
                Title = title
            }
        });
    }

    [LoggerMessage(LogLevel.Error, "Unhandled exception for {RequestPath}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string requestPath);
}
