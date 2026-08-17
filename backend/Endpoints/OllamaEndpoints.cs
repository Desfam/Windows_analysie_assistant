using System.Text;
using System.Text.Json;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Services;

namespace WindowsDiagnosticApp.Endpoints;

public static class OllamaEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapOllamaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ollama");

        group.MapGet("/status", async (IOllamaService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetStatusAsync(ct)));

        group.MapGet("/models", async (IOllamaService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetModelsAsync(ct)));

        group.MapGet("/config", (OllamaConfigStore config) =>
        {
            var validation = OllamaUrlValidator.Validate(config.BaseUrl, config.AllowPrivateNetwork);
            return Results.Ok(new OllamaConfigResponse
            {
                BaseUrl = config.BaseUrl,
                IsLocal = validation.IsLocal,
                AllowPrivateNetwork = config.AllowPrivateNetwork
            });
        });

        group.MapPut("/config", (OllamaConfigRequest request, OllamaConfigStore config) =>
        {
            var validation = OllamaUrlValidator.Validate(request.BaseUrl, config.AllowPrivateNetwork);
            if (!validation.IsValid || validation.NormalizedUrl is null)
            {
                return Results.BadRequest(new { error = validation.Error ?? "Ungültige Adresse." });
            }

            config.SetBaseUrl(validation.NormalizedUrl);
            return Results.Ok(new OllamaConfigResponse
            {
                BaseUrl = validation.NormalizedUrl,
                IsLocal = validation.IsLocal,
                AllowPrivateNetwork = config.AllowPrivateNetwork
            });
        });

        group.MapPost("/chat", StreamChatAsync);
    }

    private static async Task StreamChatAsync(
        OllamaChatRequest request,
        IDiagnosticAgentService agent,
        HttpContext context)
    {
        var ct = context.RequestAborted;
        context.Response.ContentType = "application/x-ndjson";
        context.Response.Headers.CacheControl = "no-cache";

        try
        {
            await foreach (var evt in agent.RunAsync(request, ct))
            {
                var line = JsonSerializer.Serialize(evt, JsonOptions) + "\n";
                await context.Response.WriteAsync(line, Encoding.UTF8, ct);
                await context.Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Vom Client abgebrochen – kein Fehler.
        }
    }
}
