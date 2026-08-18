using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Options;
using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class DiagnosticAgentServiceTests
{
    private sealed class FakeCapabilityDiscoveryService : ICapabilityDiscoveryService
    {
        public Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SystemCapabilities { IsAdministrator = false });
        public Task WarmUpAsync() => Task.CompletedTask;
        public void Invalidate() { }
    }

    private sealed class FakeOllama : IOllamaService
    {
        private readonly Queue<List<ChatStreamChunk>> _turns;
        public FakeOllama(params List<ChatStreamChunk>[] turns) => _turns = new Queue<List<ChatStreamChunk>>(turns);

        public Task<OllamaStatus> GetStatusAsync(CancellationToken ct) =>
            Task.FromResult(new OllamaStatus { Connected = true });

        public Task<OllamaModelsResponse> GetModelsAsync(CancellationToken ct) =>
            Task.FromResult(new OllamaModelsResponse());

        public async IAsyncEnumerable<ChatStreamChunk> StreamRawAsync(
            string model, IReadOnlyList<object> messages, IReadOnlyList<object>? tools,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var turn = _turns.Count > 0
                ? _turns.Dequeue()
                : new List<ChatStreamChunk> { new() { Type = "done" } };
            foreach (var chunk in turn)
            {
                await Task.Yield();
                yield return chunk;
            }
        }
    }

    private sealed class FakeExecutor : IDiagnosticActionExecutor
    {
        private readonly Func<string, object, CancellationToken, Task<ActionExecutionResult>> _handler;
        public FakeExecutor(Func<string, object, CancellationToken, Task<ActionExecutionResult>> handler) => _handler = handler;
        public Task<ActionExecutionResult> ExecuteAsync(string actionId, object parameters, CancellationToken ct) =>
            _handler(actionId, parameters, ct);
    }

    private static DiagnosticAgentService CreateAgent(IOllamaService ollama, IDiagnosticActionExecutor executor) =>
        new(ollama, new DiagnosticActionCatalog(Microsoft.Extensions.Options.Options.Create(new EventOptions())), executor,
            new FakeCapabilityDiscoveryService(),
            NullLogger<DiagnosticAgentService>.Instance);

    private static ChatStreamChunk Delta(string text) => new() { Type = "delta", Content = text };
    private static ChatStreamChunk Done() => new() { Type = "done" };

    private static ChatStreamChunk ToolCall(string name, string argsJson)
    {
        using var doc = JsonDocument.Parse(argsJson);
        return new ChatStreamChunk
        {
            Type = "toolcalls",
            ToolCalls = new List<ToolCallRaw> { new() { Id = "call-1", Name = name, Arguments = doc.RootElement.Clone() } }
        };
    }

    private static FakeExecutor SuccessExecutor(EventsQueryActionResult data) =>
        new((_, _, _) => Task.FromResult(new ActionExecutionResult
        {
            ActionId = "events.query",
            Success = true,
            StartedAt = DateTimeOffset.Now,
            CompletedAt = DateTimeOffset.Now,
            Data = data
        }));

    private static async Task<List<AgentEvent>> Collect(DiagnosticAgentService agent, OllamaChatRequest request, CancellationToken ct = default)
    {
        var events = new List<AgentEvent>();
        await foreach (var evt in agent.RunAsync(request, ct))
        {
            events.Add(evt);
        }
        return events;
    }

    private static OllamaChatRequest UserSays(string text) => new()
    {
        Model = "test-model",
        Messages = new List<OllamaChatMessage> { new() { Role = "user", Content = text } }
    };

    /// <summary>Baut eine Modellrunde, deren Antworttext auf mehrere Chunks verteilt ist.</summary>
    private static List<ChatStreamChunk> TextTurn(params string[] parts)
    {
        var chunks = parts.Select(Delta).ToList();
        chunks.Add(Done());
        return chunks;
    }

    private sealed class CountingExecutor : IDiagnosticActionExecutor
    {
        public int Calls { get; private set; }

        public Task<ActionExecutionResult> ExecuteAsync(string actionId, object parameters, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new ActionExecutionResult
            {
                ActionId = actionId,
                Success = true,
                StartedAt = DateTimeOffset.Now,
                CompletedAt = DateTimeOffset.Now,
                Data = new EventsQueryActionResult
                {
                    Query = new EventsQueryParameters(),
                    Events = new List<EventsQueryResultEvent>
                    {
                        new()
                        {
                            EventId = 1000,
                            Provider = "Application Error",
                            Level = "High",
                            Timestamp = DateTimeOffset.Now,
                            Message = "winget.exe abgestürzt"
                        }
                    },
                    Summary = new EventsQuerySummary { Total = 1 }
                }
            });
        }
    }

    private static string AssembledText(IEnumerable<AgentEvent> events) =>
        string.Concat(events.Where(e => e.Type == "assistant.delta").Select(e => e.Content));

    [Fact]
    public async Task PlainAnswer_ModelTextCannotCreateOrCompleteNodes()
    {
        var ollama = new FakeOllama(new List<ChatStreamChunk> { Delta("Ich kann das lokal noch nicht prüfen."), Done() });
        var agent = CreateAgent(ollama, SuccessExecutor(new EventsQueryActionResult { Query = new EventsQueryParameters() }));

        var events = await Collect(agent, UserSays("Kannst du auf dem Rechner nachschauen?"));

        Assert.Contains(events, e => e.Type == "assistant.delta");
        Assert.Contains(events, e => e.Type == "assistant.completed");
        Assert.DoesNotContain(events, e => e.Type == "graph.nodeAdded");
        Assert.DoesNotContain(events, e => e.Type == "action.completed");
        Assert.DoesNotContain(events, e => e.Type == "evidence.added");
    }

    [Fact]
    public async Task PlainYes_DoesNotTriggerAnyAction()
    {
        var ollama = new FakeOllama(new List<ChatStreamChunk> { Delta("Verstanden."), Done() });
        var agent = CreateAgent(ollama, SuccessExecutor(new EventsQueryActionResult { Query = new EventsQueryParameters() }));

        var events = await Collect(agent, UserSays("ja"));

        Assert.DoesNotContain(events, e => e.Type == "action.proposed");
        Assert.DoesNotContain(events, e => e.Type == "action.started");
    }

    [Fact]
    public async Task ValidToolCall_ExecutesAndProducesEvidenceOnlyFromRealResult()
    {
        var data = new EventsQueryActionResult
        {
            Query = new EventsQueryParameters(),
            Events = new List<EventsQueryResultEvent>
            {
                new() { EventId = 129, Provider = "stornvme", Level = "Warning", Timestamp = DateTimeOffset.Now, Message = "Reset" }
            },
            Summary = new EventsQuerySummary { Total = 1 }
        };

        var ollama = new FakeOllama(
            new List<ChatStreamChunk> { ToolCall("events.query", """{ "levels": ["Warning"], "sinceHours": 24 }""") },
            new List<ChatStreamChunk> { Delta("Ich habe ein Ereignis 129 gefunden."), Done() });
        var agent = CreateAgent(ollama, SuccessExecutor(data));

        var events = await Collect(agent, UserSays("Ich kann den Rechner nicht updaten."));

        // Reihenfolge: proposed -> nodeAdded(ready) -> nodeUpdated(running) -> started -> completed -> nodeUpdated(completed) -> evidence
        Assert.Contains(events, e => e.Type == "action.proposed" && e.ActionId == "events.query");
        Assert.Contains(events, e => e.Type == "graph.nodeAdded" && e.Node!.State == "ready");
        Assert.Contains(events, e => e.Type == "graph.nodeUpdated" && e.NodePatch!.State == "running");
        Assert.Contains(events, e => e.Type == "action.started");
        Assert.Contains(events, e => e.Type == "action.completed");
        Assert.Contains(events, e => e.Type == "graph.nodeUpdated" && e.NodePatch!.State == "completed");
        Assert.Contains(events, e => e.Type == "evidence.added" && e.Evidence!.EventId == 129);
    }

    [Fact]
    public async Task UnknownToolCall_IsRejected_NoCompletedNode()
    {
        var ollama = new FakeOllama(
            new List<ChatStreamChunk> { ToolCall("system.reboot", "{}") },
            new List<ChatStreamChunk> { Delta("Das kann ich nicht."), Done() });
        var agent = CreateAgent(ollama, SuccessExecutor(new EventsQueryActionResult { Query = new EventsQueryParameters() }));

        var events = await Collect(agent, UserSays("Starte den Rechner neu."));

        Assert.Contains(events, e => e.Type == "error" && e.Code == "invalid_tool_call");
        Assert.DoesNotContain(events, e => e.Type == "graph.nodeUpdated" && e.NodePatch!.State == "completed");
        Assert.DoesNotContain(events, e => e.Type == "evidence.added");
    }

    [Fact]
    public async Task ExecutorTimeout_SetsNodeFailed()
    {
        var throwing = new FakeExecutor((_, _, _) => throw new OperationCanceledException());
        var ollama = new FakeOllama(
            new List<ChatStreamChunk> { ToolCall("events.query", "{}") },
            new List<ChatStreamChunk> { Delta("Abgebrochen."), Done() });
        var agent = CreateAgent(ollama, throwing);

        var events = await Collect(agent, UserSays("Prüfe die Ereignisse."), CancellationToken.None);

        Assert.Contains(events, e => e.Type == "graph.nodeUpdated" && e.NodePatch!.State == "failed");
        Assert.Contains(events, e => e.Type == "action.completed" &&
            e.Result is ActionExecutionResult r && !r.Success);
    }

    [Fact]
    public async Task UserCancellation_SetsNodeCancelled()
    {
        var throwing = new FakeExecutor((_, _, _) => throw new OperationCanceledException());
        var ollama = new FakeOllama(new List<ChatStreamChunk> { ToolCall("events.query", "{}") });
        var agent = CreateAgent(ollama, throwing);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var events = await Collect(agent, UserSays("Prüfe die Ereignisse."), cts.Token);

        Assert.Contains(events, e => e.Type == "graph.nodeUpdated" && e.NodePatch!.State == "cancelled");
    }

    [Theory]
    [InlineData("""{"name":"events.query","arguments":{"levels":["Error"],"maximumResults":10}}""")]
    [InlineData("```json\n{\"name\":\"events.query\",\"arguments\":{\"sinceHours\":24}}\n```")]
    [InlineData("""<tool_call>{"name":"events.query","arguments":{}}</tool_call>""")]
    public async Task TextToolCall_IsValidatedAndExecuted(string content)
    {
        var executor = new CountingExecutor();
        var ollama = new FakeOllama(
            TextTurn(content),
            TextTurn("Ich habe ein Ereignis gefunden."));
        var agent = CreateAgent(ollama, executor);

        var events = await Collect(agent, UserSays("mein Winget funktioniert nicht"));

        Assert.Equal(1, executor.Calls);
        Assert.Contains(events, e => e.Type == "action.proposed" && e.ActionId == "events.query");
        Assert.Contains(events, e => e.Type == "graph.nodeAdded" && e.Node!.State == "ready");
        Assert.Contains(events, e => e.Type == "graph.nodeUpdated" && e.NodePatch!.State == "running");
        Assert.Contains(events, e => e.Type == "graph.nodeUpdated" && e.NodePatch!.State == "completed");
        Assert.Contains(events, e => e.Type == "evidence.added" && e.Evidence!.EventId == 1000);
    }

    [Fact]
    public async Task TextToolCall_SplitAcrossChunks_IsExecuted()
    {
        var executor = new CountingExecutor();
        var ollama = new FakeOllama(
            TextTurn("{\"name\":", "\"events.query\",", "\"arguments\":{", "\"maximumResults\":10}", "}"),
            TextTurn("Auswertung folgt."));
        var agent = CreateAgent(ollama, executor);

        var events = await Collect(agent, UserSays("mein Winget funktioniert nicht"));

        Assert.Equal(1, executor.Calls);
        Assert.Contains(events, e => e.Type == "action.completed" && e.ActionId == "events.query");
    }

    [Fact]
    public async Task TextToolCall_RawJsonIsNeverShownInChat()
    {
        var ollama = new FakeOllama(
            TextTurn("""{"name":"events.query","arguments":{"levels":["Error"]}}"""),
            TextTurn("Es wurde ein Absturz von winget gefunden."));
        var agent = CreateAgent(ollama, new CountingExecutor());

        var events = await Collect(agent, UserSays("mein Winget funktioniert nicht"));

        var text = AssembledText(events);
        Assert.DoesNotContain("events.query", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"arguments\"", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("winget", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NormalAnswer_IsStreamedCompletely()
    {
        var executor = new CountingExecutor();
        var ollama = new FakeOllama(TextTurn("Seit wann ", "tritt das Problem ", "auf?"));
        var agent = CreateAgent(ollama, executor);

        var events = await Collect(agent, UserSays("mein Winget funktioniert nicht"));

        Assert.Equal(0, executor.Calls);
        Assert.Equal("Seit wann tritt das Problem auf?", AssembledText(events));
    }

    [Fact]
    public async Task TextToolCall_UnknownActionName_IsRejectedWithoutExecution()
    {
        var executor = new CountingExecutor();
        var ollama = new FakeOllama(
            TextTurn("""{"name":"system.reboot","arguments":{}}"""),
            TextTurn("Das darf ich nicht."));
        var agent = CreateAgent(ollama, executor);

        var events = await Collect(agent, UserSays("Starte den Rechner neu."));

        Assert.Equal(0, executor.Calls);
        Assert.Contains(events, e => e.Type == "error" && e.Code == "invalid_tool_call");
        Assert.DoesNotContain(events, e => e.Type == "action.started");
        Assert.DoesNotContain(events, e => e.Type == "evidence.added");
    }

    [Fact]
    public async Task TextToolCall_InvalidParameters_IsRejectedWithoutExecution()
    {
        var executor = new CountingExecutor();
        var ollama = new FakeOllama(
            TextTurn("""{"name":"events.query","arguments":{"sinceHours":99999}}"""),
            TextTurn("Der Zeitraum war ungültig."));
        var agent = CreateAgent(ollama, executor);

        var events = await Collect(agent, UserSays("Prüfe die Ereignisse."));

        Assert.Equal(0, executor.Calls);
        Assert.Contains(events, e => e.Type == "error" && e.Code == "invalid_tool_call");
        Assert.DoesNotContain(events, e => e.Type == "action.started");
    }

    [Fact]
    public async Task TextToolCall_MalformedJson_IsRejectedWithoutExecution()
    {
        var executor = new CountingExecutor();
        var ollama = new FakeOllama(
            TextTurn("<tool_call>{\"name\":\"events.query\",</tool_call>"),
            TextTurn("Der Aufruf war fehlerhaft."));
        var agent = CreateAgent(ollama, executor);

        var events = await Collect(agent, UserSays("Prüfe die Ereignisse."));

        Assert.Equal(0, executor.Calls);
        Assert.Contains(events, e => e.Type == "error" && e.Code == "invalid_tool_call");
    }

    [Fact]
    public async Task NativeToolCall_TakesPrecedence_TextJsonIsNotExecutedTwice()
    {
        var executor = new CountingExecutor();
        // Das Modell liefert denselben Aufruf zusätzlich als JSON-Text.
        var firstTurn = new List<ChatStreamChunk>
        {
            Delta("""{"name":"events.query","arguments":{}}"""),
            ToolCall("events.query", "{}")
        };
        var ollama = new FakeOllama(firstTurn, TextTurn("Auswertung folgt."));
        var agent = CreateAgent(ollama, executor);

        var events = await Collect(agent, UserSays("mein Winget funktioniert nicht"));

        Assert.Equal(1, executor.Calls);
        Assert.Single(events, e => e.Type == "action.started");
        Assert.Single(events, e => e.Type == "graph.nodeAdded");
        Assert.DoesNotContain("events.query", AssembledText(events), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TextToolCall_InsideThinkingBlock_IsNotExecuted()
    {
        var executor = new CountingExecutor();
        var ollama = new FakeOllama(TextTurn(
            "<think>Vielleicht {\"name\":\"events.query\",\"arguments\":{}} </think>Keine Prüfung war erforderlich."));
        var agent = CreateAgent(ollama, executor);

        var events = await Collect(agent, UserSays("Bitte bewerte das Problem."));

        Assert.Equal(0, executor.Calls);
        Assert.DoesNotContain(events, e => e.Type == "action.started");
        Assert.DoesNotContain("events.query", AssembledText(events), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Keine Prüfung", AssembledText(events));
    }
}
