using SettingsRegister.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace SettingsRegister.Api.ErrorHandling;

public sealed class ProblemDetailsExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ProblemDetailsExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await WriteProblemDetailsAsync(context, exception);
        }
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            throw exception;
        }

        (int statusCode, string title) = ResolveStatus(exception);

        ProblemDetails problemDetails = new()
        {
            Title = title,
            Detail = exception.Message,
            Status = statusCode,
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static (int StatusCode, string Title) ResolveStatus(Exception exception)
    {
        if (exception is EntityNotFoundException)
        {
            return (StatusCodes.Status404NotFound, "Resource Not Found");
        }

        if (exception is ConflictException)
        {
            return (StatusCodes.Status409Conflict, "Conflict");
        }

        if (exception is FeatureNotAvailableException)
        {
            return (StatusCodes.Status501NotImplemented, "Not Implemented");
        }

        if (exception is ValidationException)
        {
            return (StatusCodes.Status422UnprocessableEntity, "Validation Failed");
        }

        if (exception is ArgumentException || exception is ArgumentOutOfRangeException || exception is FormatException)
        {
            return (StatusCodes.Status400BadRequest, "Bad Request");
        }

        return (StatusCodes.Status500InternalServerError, "Internal Server Error");
    }
}

