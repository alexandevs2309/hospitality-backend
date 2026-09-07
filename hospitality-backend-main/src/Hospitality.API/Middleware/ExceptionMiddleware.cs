using System.Net;
using System.Text.Json;
using Hospitality.Domain.Exceptions;

namespace Hospitality.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no controlada: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, safeMessage, details) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message, (object?)null),
            NotFoundException => (HttpStatusCode.NotFound, exception.Message, (object?)null),
            ForbiddenAccessException => (HttpStatusCode.Forbidden, exception.Message, (object?)null),
            ConflictException => (HttpStatusCode.Conflict, exception.Message, (object?)null),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message, (object?)null),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "No autorizado.", (object?)null),
            InvalidOperationException when exception.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase)
                => (HttpStatusCode.Conflict, "Conflicto con el estado actual del recurso.", (object?)null),
            _ => (
                HttpStatusCode.InternalServerError,
                _env.IsDevelopment() ? exception.Message : "Ocurrió un error inesperado en el servidor.",
                (object?)null)
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            statusCode = statusCode,
            message = safeMessage,
            details = details,
            stackTrace = _env.IsDevelopment() ? exception.StackTrace : null
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(response, options);

        await context.Response.WriteAsync(json);
    }
}