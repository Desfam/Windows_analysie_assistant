using System.Text.Json;

namespace WindowsDiagnosticApp.Infrastructure;

/// <summary>
/// Globale Fehlerbehandlung: protokolliert technische Details lokal und liefert
/// dem Frontend nur eine allgemeine Fehlermeldung ohne interne Details.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Vom Client abgebrochene Anfrage – kein Fehler.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unbehandelter Fehler bei {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var payload = JsonSerializer.Serialize(new
            {
                error = "Bei der Verarbeitung der Anfrage ist ein interner Fehler aufgetreten."
            });

            await context.Response.WriteAsync(payload);
        }
    }
}
