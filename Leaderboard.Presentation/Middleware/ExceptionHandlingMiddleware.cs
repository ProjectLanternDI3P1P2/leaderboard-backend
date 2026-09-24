using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ILogger = Serilog.ILogger;

namespace Leaderboard.Presentation.Middleware;

public sealed class ExceptionHandlingMiddleware(ILogger logger, IHostEnvironment environment) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (KeyNotFoundException exception)
        {
            logger.Warning(exception, "Resource not found");
            await HandleNotFoundExceptionAsync(context, exception);
        }
        catch (ValidationException exception)
        {
            logger.Warning(exception, "Validation error occurred");
            await HandleValidationExceptionAsync(context, exception);
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Unhandled exception occurred");
            await HandleExceptionAsync(context, exception);
        }
    }

    private static async Task HandleNotFoundExceptionAsync(HttpContext context, KeyNotFoundException exception)
    {
        var problemDetails = new ProblemDetails
        {
            Type = "https://httpstatuses.com/404",
            Title = "Resource not found",
            Detail = exception.Message,
            Status = StatusCodes.Status404NotFound,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails, context.RequestAborted);
    }

    private static async Task HandleValidationExceptionAsync(HttpContext context, ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => ToCamelCase(group.Key),
                group => group.Select(error => error.ErrorMessage).ToArray());

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Type = "https://httpstatuses.com/422",
            Title = "Validation error",
            Detail = "One or more validation errors occurred.",
            Status = StatusCodes.Status422UnprocessableEntity,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails, context.RequestAborted);
    }

    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0]))
        {
            return propertyName;
        }

        return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var problemDetails = new ProblemDetails
        {
            Type = "https://httpstatuses.com/500",
            Title = "Internal server error",
            Detail = environment.IsDevelopment() ? exception.Message : null,
            Status = StatusCodes.Status500InternalServerError,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails, context.RequestAborted);
    }
}
