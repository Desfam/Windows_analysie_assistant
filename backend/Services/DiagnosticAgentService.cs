using System.Runtime.CompilerServices;
using System.Text.Json;
using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Serverseitige Orchestrierung: Benutzernachricht → Ollama-Planung → validierte Aktion →
/// echte Backend-Ausführung → strukturiertes Ergebnis → erneute Auswertung durch Ollama.
/// Das Modell erhält niemals die Möglichkeit, den Ausführungsstatus einer Aktion selbst zu setzen.
/// </summary>
public sealed class DiagnosticAgentService : IDiagnosticAgentService
{
    private const int MaxToolIterations = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOllamaService _ollama;
    private readonly DiagnosticActionCatalog _catalog;
    private readonly IDiagnosticActionExecutor _executor;
    private readonly ILogger<DiagnosticAgentService> _logger;

    public DiagnosticAgentService(
        IOllamaService ollama,
        DiagnosticActionCatalog catalog,
        IDiagnosticActionExecutor executor,
        ILogger<DiagnosticAgentService> logger)
    {
        _ollama = ollama;
        _catalog = catalog;
        _executor = executor;
        _logger = logger;
    }

    public async IAsyncEnumerable<AgentEvent> RunAsync(
        OllamaChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            yield return Error("no_model", "Es wurde kein Modell ausgewählt.");
            yield break;
        }

        var messages = BuildInitialMessages(request);
        var tools = _catalog.BuildToolsPayload();
        var messageId = Guid.NewGuid().ToString("n");
        var iteration = 0;

        while (true)
        {
            iteration++;
            if (iteration > MaxToolIterations)
            {
                yield return Error("tool_loop_limit", "Es wurden zu viele Werkzeugaufrufe angefordert.");
                yield break;
            }

            List<ToolCallRaw>? toolCalls = null;
            string? modelError = null;

            await foreach (var raw in _ollama.StreamRawAsync(request.Model!, messages, tools, cancellationToken))
            {
                if (raw.Type == "error")
                {
                    modelError = raw.Message ?? "Unbekannter Modellfehler.";
                    break;
                }

                if (raw.Type == "toolcalls")
                {
                    toolCalls = raw.ToolCalls;
                    break;
                }

                if (raw.Type == "delta" && !string.IsNullOrEmpty(raw.Content))
                {
                    yield return new AgentEvent { Type = "assistant.delta", Content = raw.Content };
                }

                if (raw.Type == "done")
                {
                    break;
                }
            }

            if (modelError is not null)
            {
                yield return Error("model_error", modelError);
                yield break;
            }

            if (toolCalls is null || toolCalls.Count == 0)
            {
                yield return new AgentEvent { Type = "assistant.completed", MessageId = messageId };
                yield break;
            }

            messages.Add(BuildAssistantToolCallMessage(toolCalls));

            var userCancelled = false;
            foreach (var call in toolCalls)
            {
                await foreach (var evt in HandleToolCallAsync(call, messages, cancellationToken))
                {
                    yield return evt;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    userCancelled = true;
                    break;
                }
            }

            if (userCancelled)
            {
                yield break;
            }
        }
    }

    private async IAsyncEnumerable<AgentEvent> HandleToolCallAsync(
        ToolCallRaw call, List<object> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var validation = _catalog.ValidateCall(call.Name, call.Arguments);
        if (!validation.IsValid || validation.Definition is null)
        {
            var error = validation.Error ?? "Die angeforderte Aktion ist nicht zulässig.";
            yield return Error("invalid_tool_call", error);
            messages.Add(BuildToolResultMessage(call.Id, JsonSerializer.Serialize(new { error }, JsonOptions)));
            yield break;
        }

        var definition = validation.Definition;
        var nodeId = Guid.NewGuid().ToString("n");
        var executionId = Guid.NewGuid().ToString("n");

        yield return new AgentEvent
        {
            Type = "action.proposed",
            ActionId = definition.ActionId,
            Parameters = validation.Parameters,
            Reason = null
        };

        yield return new AgentEvent
        {
            Type = "graph.nodeAdded",
            Node = new AgentGraphNode
            {
                Id = nodeId,
                Kind = "action",
                Title = definition.Title,
                Description = definition.Description,
                State = "ready",
                RiskLevel = definition.RiskLevel.ToString(),
                ChangesSystem = definition.ChangesSystem
            }
        };

        if (definition.RequiresConfirmation)
        {
            // Noch keine bestätigungspflichtige Aktion implementiert – sicherer Default: ablehnen.
            yield return new AgentEvent
            {
                Type = "graph.nodeUpdated",
                NodePatch = new AgentGraphNodePatch { Id = nodeId, State = "skipped" }
            };
            messages.Add(BuildToolResultMessage(call.Id,
                JsonSerializer.Serialize(new { error = "Bestätigung erforderlich, aber noch nicht unterstützt." }, JsonOptions)));
            yield break;
        }

        yield return new AgentEvent
        {
            Type = "graph.nodeUpdated",
            NodePatch = new AgentGraphNodePatch { Id = nodeId, State = "running" }
        };
        yield return new AgentEvent { Type = "action.started", ActionId = definition.ActionId, ExecutionId = executionId };

        ActionExecutionResult? execResult = null;
        string? failureReason = null;
        var cancelledByUser = false;

        using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(definition.TimeoutSeconds)))
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
        {
            try
            {
                execResult = await _executor.ExecuteAsync(definition.ActionId, validation.Parameters!, linked.Token);
            }
            catch (OperationCanceledException)
            {
                cancelledByUser = cancellationToken.IsCancellationRequested;
                failureReason = cancelledByUser ? "Vom Benutzer abgebrochen." : "Zeitüberschreitung bei der Ausführung.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Aktion {ActionId} ist unerwartet fehlgeschlagen.", definition.ActionId);
                failureReason = "Die Aktion ist unerwartet fehlgeschlagen.";
            }
        }

        if (failureReason is not null)
        {
            var state = cancelledByUser ? "cancelled" : "failed";
            yield return new AgentEvent
            {
                Type = "graph.nodeUpdated",
                NodePatch = new AgentGraphNodePatch { Id = nodeId, State = state, Error = failureReason }
            };

            var failedResult = new ActionExecutionResult
            {
                ActionId = definition.ActionId,
                Success = false,
                StartedAt = DateTimeOffset.Now,
                CompletedAt = DateTimeOffset.Now,
                Error = failureReason
            };
            yield return new AgentEvent
            {
                Type = "action.completed",
                ActionId = definition.ActionId,
                ExecutionId = executionId,
                Result = failedResult
            };

            messages.Add(BuildToolResultMessage(call.Id,
                JsonSerializer.Serialize(new { success = false, error = failureReason }, JsonOptions)));
            yield break;
        }

        var result = execResult!;
        yield return new AgentEvent
        {
            Type = "action.completed",
            ActionId = definition.ActionId,
            ExecutionId = executionId,
            Result = result
        };

        if (result.Success)
        {
            yield return new AgentEvent
            {
                Type = "graph.nodeUpdated",
                NodePatch = new AgentGraphNodePatch { Id = nodeId, State = "completed" }
            };

            foreach (var evidence in ExtractEvidence(definition.ActionId, result))
            {
                yield return new AgentEvent { Type = "evidence.added", Evidence = evidence };
            }

            messages.Add(BuildToolResultMessage(call.Id, JsonSerializer.Serialize(result.Data, JsonOptions)));
        }
        else
        {
            yield return new AgentEvent
            {
                Type = "graph.nodeUpdated",
                NodePatch = new AgentGraphNodePatch { Id = nodeId, State = "failed", Error = result.Error }
            };
            messages.Add(BuildToolResultMessage(call.Id,
                JsonSerializer.Serialize(new { success = false, error = result.Error }, JsonOptions)));
        }
    }

    private static IEnumerable<AgentEvidence> ExtractEvidence(string actionId, ActionExecutionResult result)
    {
        if (actionId != "events.query" || result.Data is not EventsQueryActionResult eventsResult)
        {
            yield break;
        }

        foreach (var evt in eventsResult.Events)
        {
            yield return new AgentEvidence
            {
                Id = $"evt-{evt.EventId}-{evt.Timestamp.ToUnixTimeSeconds()}",
                EventId = evt.EventId,
                Provider = evt.Provider,
                Summary = evt.Message,
                Timestamp = evt.Timestamp
            };
        }
    }

    private static List<object> BuildInitialMessages(OllamaChatRequest request)
    {
        var messages = new List<object> { new { role = "system", content = SystemPrompt.Text } };

        var context = OllamaService.BuildContextBlock(request.CaseContext);
        if (context is not null)
        {
            messages.Add(new { role = "system", content = context });
        }

        foreach (var message in request.Messages)
        {
            var role = message.Role is "user" or "assistant" or "system" ? message.Role : "user";
            messages.Add(new { role, content = message.Content });
        }

        return messages;
    }

    private static object BuildAssistantToolCallMessage(List<ToolCallRaw> toolCalls) => new
    {
        role = "assistant",
        content = "",
        tool_calls = toolCalls.Select(t => new
        {
            function = new { name = t.Name, arguments = t.Arguments }
        })
    };

    private static object BuildToolResultMessage(string? toolCallId, string jsonContent) => new
    {
        role = "tool",
        tool_call_id = toolCallId,
        content = jsonContent
    };

    private static AgentEvent Error(string code, string message) =>
        new() { Type = "error", Code = code, Message = message };
}
