using System.Text.Json;
using FleetRental.Application.Common;
using FleetRental.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Middleware;

/// <summary>
/// Translates domain and application exceptions into RFC 7807 problem responses.
/// </summary>
/// <remarks>
/// Centralised so controllers stay free of try/catch and every endpoint reports
/// the same failure the same way — the Angular error interceptor relies on that
/// consistency to decide what to show the user.
/// </remarks>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, title, detail, errors) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}.",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogInformation("{Status} on {Method} {Path}: {Detail}",
                status, context.Request.Method, context.Request.Path, detail);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static (int Status, string Title, string Detail, IReadOnlyDictionary<string, string[]>? Errors) Map(
        Exception exception) => exception switch
    {
        ValidationException ex =>
            (StatusCodes.Status400BadRequest, "Validation failed", ex.Message, ex.Errors),

        // Invariant violations are the caller's fault, not a server fault.
        DomainException ex =>
            (StatusCodes.Status400BadRequest, "Invalid request", ex.Message, null),

        AuthenticationFailedException ex =>
            (StatusCodes.Status401Unauthorized, "Authentication failed", ex.Message, null),

        ForbiddenException ex =>
            (StatusCodes.Status403Forbidden, "Forbidden", ex.Message, null),

        NotFoundException ex =>
            (StatusCodes.Status404NotFound, "Not found", ex.Message, null),

        // Double-booking collisions land here — the client should refresh and retry.
        ConflictException ex =>
            (StatusCodes.Status409Conflict, "Conflict", ex.Message, null),

        // Deliberately generic: never leak internals to the caller. The real
        // exception is in the logs above.
        _ => (StatusCodes.Status500InternalServerError, "Unexpected error",
            "Something went wrong. Please try again.", null),
    };
}
