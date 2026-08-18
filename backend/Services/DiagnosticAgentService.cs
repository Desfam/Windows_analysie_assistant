using System.Runtime.CompilerServices;
using System.Text;
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

        yield return Status("understanding", "Problem wird analysiert", "Die Anfrage wird für die Diagnose eingeordnet.");

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
            var content = new AssistantContentBuffer();

            yield return Status("planning", "Diagnoseschritt wird geplant", "Der nächste sichere Diagnoseschritt wird bestimmt.");

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
                    content.Append(raw.Content);
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

            var hasNativeToolCalls = toolCalls is { Count: > 0 };
            if (hasNativeToolCalls)
            {
                content.Discard();
            }
            else
            {
                var rawText = content.TakePending();
                var sanitized = ModelOutputSanitizer.Sanitize(rawText);
                if (sanitized.HadThinkingContent)
                {
                    yield return Status("evaluating", "Ergebnisse werden ausgewertet", "Die gefundenen Systeminformationen werden für den nächsten Diagnoseschritt eingeordnet.");
                }

                var pending = sanitized.Text;
                if (!string.IsNullOrWhiteSpace(pending))
                {
                    var parsed = TextToolCallParser.Parse(pending);
                    switch (parsed.Outcome)
                    {
                        case TextToolCallOutcome.Parsed:
                            toolCalls = new List<ToolCallRaw> { parsed.Call! };
                            break;

                        case TextToolCallOutcome.Invalid:
                            yield return Error("invalid_tool_call", parsed.Error!);
                            break;

                        default:
                            yield return new AgentEvent { Type = "assistant.delta", Content = pending };
                            break;
                    }
                }
                else if (sanitized.HasIncompleteThinkingBlock || content.IsEmpty())
                {
                    yield return Error("model_no_result", "Das Modell hat keine finale Antwort geliefert.");
                    yield break;
                }
            }

            if (toolCalls is null || toolCalls.Count == 0)
            {
                yield return Status("completed", "Abschließende Antwort wird erstellt", "Die finale Antwort wird vorbereitet.");
                yield return new AgentEvent { Type = "assistant.completed", MessageId = messageId };
                yield break;
            }

            messages.Add(BuildAssistantToolCallMessage(toolCalls));

            var userCancelled = false;
            foreach (var call in toolCalls)
            {
                ActionExecutionResult? actionResult = null;
                await foreach (var evt in HandleToolCallAsync(call, messages, cancellationToken))
                {
                    if (evt.Type == "action.completed" && evt.Result is ActionExecutionResult result)
                    {
                        actionResult = result;
                    }
                    yield return evt;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    userCancelled = true;
                    break;
                }

                // Ein erfolgreicher Winget-Status beantwortet weder die Quellenfrage noch beweist er
                // die Erreichbarkeit. Der zweite, parameterlose R0-Schritt ist daher serverseitig fest.
                if (call.Name == "winget.status" && actionResult?.Success == true)
                {
                    var sourcesCall = new ToolCallRaw { Name = "winget.sources.list", Arguments = EmptyObjectArguments() };
                    messages.Add(BuildAssistantToolCallMessage(new List<ToolCallRaw> { sourcesCall }));
                    await foreach (var evt in HandleToolCallAsync(sourcesCall, messages, cancellationToken))
                    {
                        yield return evt;
                    }
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
            messages.Add(BuildToolResultMessage(call.Name, JsonSerializer.Serialize(new { error }, JsonOptions)));
            yield break;
        }

        var definition = validation.Definition;
        var nodeId = Guid.NewGuid().ToString("n");
        var executionId = Guid.NewGuid().ToString("n");

        yield return new AgentEvent
        {
            Type = "action.proposed",
            ActionId = definition.ActionId,
            ExecutionId = executionId,
            NodeId = nodeId,
            Parameters = validation.Parameters,
            Reason = null
        };

        yield return Status("executing", definition.Title, "Die validierte Diagnoseaktion wird ausgeführt.");

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
            messages.Add(BuildToolResultMessage(call.Name,
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
                ActionState = state,
                Result = failedResult
            };

            _logger.LogInformation("Diagnostic execution audit: ExecutionId={ExecutionId} ActionId={ActionId} State={State} Success=false", executionId, definition.ActionId, state);

            messages.Add(BuildToolResultMessage(call.Name,
                JsonSerializer.Serialize(failedResult, JsonOptions)));
            yield break;
        }

        var result = execResult!;
        yield return new AgentEvent
        {
            Type = "action.completed",
            ActionId = definition.ActionId,
            ExecutionId = executionId,
            ActionState = result.Success ? "completed" : "failed",
            Result = result
        };

        _logger.LogInformation("Diagnostic execution audit: ExecutionId={ExecutionId} ActionId={ActionId} State={State} Success={Success} ExitCode={ExitCode} TimedOut={TimedOut}",
            executionId, definition.ActionId, result.Success ? "completed" : "failed", result.Success, result.Execution?.ExitCode, result.Execution?.TimedOut);

        if (result.Success)
        {
            yield return new AgentEvent
            {
                Type = "graph.nodeUpdated",
                NodePatch = new AgentGraphNodePatch { Id = nodeId, State = "completed", Result = SummarizeResult(result) }
            };

            foreach (var evidence in ExtractEvidence(definition.ActionId, result))
            {
                yield return new AgentEvent { Type = "evidence.added", Evidence = evidence };
            }

            messages.Add(BuildToolResultMessage(call.Name, JsonSerializer.Serialize(result, JsonOptions)));
        }
        else
        {
            yield return new AgentEvent
            {
                Type = "graph.nodeUpdated",
                NodePatch = new AgentGraphNodePatch { Id = nodeId, State = "failed", Error = result.Error, Result = SummarizeResult(result) }
            };
            messages.Add(BuildToolResultMessage(call.Name, JsonSerializer.Serialize(result, JsonOptions)));
        }
    }

    private static string SummarizeResult(ActionExecutionResult result)
    {
        if (!result.Success)
        {
            return result.Error ?? "Die Diagnoseaktion ist fehlgeschlagen.";
        }

        return result.Data switch
        {
            WingetStatusActionResult winget => $"Version {winget.Version ?? "unbekannt"} · Exitcode {result.Execution?.ExitCode ?? -1}",
            WingetSourcesActionResult sources => sources.Parsed
                ? $"{sources.Sources.Count} Quellen gelesen · Exitcode {result.Execution?.ExitCode ?? -1}"
                : $"Quellenprozess erfolgreich, Ausgabe nicht strukturiert auswertbar · Exitcode {result.Execution?.ExitCode ?? -1}",
            EventsQueryActionResult events => $"{events.Summary.Total} Ereignisse ausgewertet",
            DiagnosticStatusActionResult status => status.Summary,
            _ => "Echtes Diagnoseergebnis liegt vor."
        };
    }

    private static IEnumerable<AgentEvidence> ExtractEvidence(string actionId, ActionExecutionResult result)
    {
        if (actionId == "winget.status" && result.Data is WingetStatusActionResult winget)
        {
            yield return new AgentEvidence
            {
                Id = $"winget-status-{result.CompletedAt.ToUnixTimeMilliseconds()}",
                Provider = "winget",
                Summary = winget.Callable
                    ? $"Winget lokal ausgeführt: {winget.Version ?? "Version nicht lesbar"}."
                    : "Winget wurde gefunden, war aber nicht lokal aufrufbar.",
                Timestamp = result.CompletedAt
            };
            yield break;
        }

        if (actionId == "winget.sources.list" && result.Data is WingetSourcesActionResult sources)
        {
            if (sources.Parsed)
            {
                foreach (var source in sources.Sources)
                {
                    yield return new AgentEvidence
                    {
                        Id = $"winget-source-{source.Name}-{result.CompletedAt.ToUnixTimeMilliseconds()}",
                        Provider = "winget source",
                        Summary = $"Quelle {source.Name}: {source.Argument ?? "Adresse nicht lesbar"}.",
                        Timestamp = result.CompletedAt
                    };
                }
            }
            else if (sources.ProcessSucceeded)
            {
                yield return new AgentEvidence
                {
                    Id = $"winget-sources-raw-{result.CompletedAt.ToUnixTimeMilliseconds()}",
                    Provider = "winget source",
                    Summary = "Winget-Quellen wurden lokal gelesen, die Ausgabe war jedoch nicht strukturiert auswertbar.",
                    Timestamp = result.CompletedAt
                };
            }
            yield break;
        }

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

    private static JsonElement EmptyObjectArguments()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static object BuildToolResultMessage(string toolName, string jsonContent) => new
    {
        role = "tool",
        tool_name = toolName,
        content = jsonContent
    };

    private static AgentEvent Error(string code, string message) =>
        new() { Type = "error", Code = code, Message = message };

    private static AgentEvent Status(string phase, string title, string description) => new()
    {
        Type = "agent.status",
        Phase = phase,
        Title = title,
        Description = description
    };

    /// <summary>
    /// Puffer für den bisherigen Antworttext. Beim Streamen werden Chunks gesammelt und erst
    /// am Ende ausgewertet; dadurch werden Toolcalls aus mehreren NDJSON-Fragmenten zuverlässig
    /// erkannt, ohne JSON-Rohtext im Chat anzuzeigen.
    /// </summary>
    private sealed class AssistantContentBuffer
    {
        private readonly StringBuilder _buffer = new();

        public void Append(string delta) => _buffer.Append(delta);

        public string? TakePending()
        {
            if (_buffer.Length == 0)
            {
                return null;
            }

            var text = _buffer.ToString();
            _buffer.Clear();
            return text;
        }

        public bool IsEmpty() => _buffer.Length == 0;

        public void Discard() => _buffer.Clear();
    }
}
