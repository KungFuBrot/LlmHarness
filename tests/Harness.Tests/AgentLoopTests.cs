using System.Text.Json;
using Harness.Core;

namespace Harness.Tests;

/// <summary>Provider, der vorher festgelegte Antworten zurückgibt und die Anfragen mitschreibt.</summary>
internal sealed class ScriptedProvider(params LlmResponse[] script) : ILlmProvider
{
    private int _next;
    public List<LlmRequest> Requests { get; } = [];

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        Requests.Add(request with { Messages = request.Messages.ToList() });
        return Task.FromResult(script[Math.Min(_next++, script.Length - 1)]);
    }
}

internal sealed class EchoTool : ITool
{
    public ToolDefinition Definition { get; } = new(
        "echo", "Gibt den Text zurück.",
        JsonDocument.Parse("""{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}""").RootElement);

    public Task<ToolResult> InvokeAsync(JsonElement input, CancellationToken ct = default) =>
        Task.FromResult(ToolResult.Ok(input.GetProperty("text").GetString()!));
}

internal sealed class ThrowingTool : ITool
{
    public ToolDefinition Definition { get; } = new("boom", "Wirft.", JsonDocument.Parse("{}").RootElement);

    public Task<ToolResult> InvokeAsync(JsonElement input, CancellationToken ct = default) =>
        throw new InvalidOperationException("kaputt");
}

internal sealed class DenyAll : IPermissionPolicy
{
    public Task<PermissionDecision> CheckAsync(ToolCallPart call, CancellationToken ct = default) =>
        Task.FromResult(PermissionDecision.Deny);
}

public class AgentLoopTests
{
    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement;

    private static LlmResponse ToolUse(params ToolCallPart[] calls) =>
        new(new ChatMessage(Role.Assistant, calls), StopReason.ToolUse, new Usage(10, 5));

    private static LlmResponse Done(string text) =>
        new(ChatMessage.Assistant(text), StopReason.EndTurn, new Usage(10, 5));

    private static async Task<List<AgentEvent>> Run(AgentLoop loop, List<ChatMessage> conversation)
    {
        var events = new List<AgentEvent>();
        await foreach (var e in loop.RunAsync(conversation))
            events.Add(e);
        return events;
    }

    [Fact]
    public async Task Executes_tool_and_feeds_result_back()
    {
        var provider = new ScriptedProvider(
            ToolUse(new ToolCallPart("c1", "echo", Json("""{"text":"hallo"}"""))),
            Done("fertig"));
        var conversation = new List<ChatMessage> { ChatMessage.User("Sag hallo") };

        var events = await Run(new AgentLoop(provider, [new EchoTool()]), conversation);

        var finished = Assert.IsType<RunFinishedEvent>(events[^1]);
        Assert.Equal(2, finished.Turns);
        Assert.Equal(new Usage(20, 10), finished.TotalUsage);

        var result = Assert.Single(provider.Requests[1].Messages[^1].Parts.OfType<ToolResultPart>());
        Assert.Equal(("c1", "hallo", false), (result.CallId, result.Content, result.IsError));
        Assert.Equal(4, conversation.Count);
    }

    [Fact]
    public async Task Parallel_calls_are_answered_in_one_message()
    {
        var provider = new ScriptedProvider(
            ToolUse(
                new ToolCallPart("a", "echo", Json("""{"text":"1"}""")),
                new ToolCallPart("b", "echo", Json("""{"text":"2"}"""))),
            Done("ok"));
        var conversation = new List<ChatMessage> { ChatMessage.User("zwei") };

        await Run(new AgentLoop(provider, [new EchoTool()]), conversation);

        var results = conversation[2].Parts.OfType<ToolResultPart>().ToList();
        Assert.Equal(["a", "b"], results.Select(r => r.CallId));
    }

    [Fact]
    public async Task Unknown_tool_and_exceptions_become_error_results()
    {
        var provider = new ScriptedProvider(
            ToolUse(
                new ToolCallPart("a", "gibtsnicht", Json("{}")),
                new ToolCallPart("b", "boom", Json("{}"))),
            Done("ok"));
        var conversation = new List<ChatMessage> { ChatMessage.User("x") };

        await Run(new AgentLoop(provider, [new ThrowingTool()]), conversation);

        var results = conversation[2].Parts.OfType<ToolResultPart>().ToList();
        Assert.All(results, r => Assert.True(r.IsError));
        Assert.Contains("kaputt", results[1].Content);
    }

    [Fact]
    public async Task Denied_permission_does_not_run_the_tool()
    {
        var provider = new ScriptedProvider(
            ToolUse(new ToolCallPart("c1", "boom", Json("{}"))),
            Done("ok"));
        var conversation = new List<ChatMessage> { ChatMessage.User("x") };

        await Run(new AgentLoop(provider, [new ThrowingTool()], new AgentOptions { Permissions = new DenyAll() }), conversation);

        var result = Assert.Single(conversation[2].Parts.OfType<ToolResultPart>());
        Assert.True(result.IsError);
        Assert.DoesNotContain("kaputt", result.Content);
    }

    [Fact]
    public async Task Stops_at_max_turns()
    {
        var provider = new ScriptedProvider(ToolUse(new ToolCallPart("c", "echo", Json("""{"text":"x"}"""))));
        var conversation = new List<ChatMessage> { ChatMessage.User("endlos") };

        var events = await Run(new AgentLoop(provider, [new EchoTool()], new AgentOptions { MaxTurns = 3 }), conversation);

        Assert.IsType<RunAbortedEvent>(events[^1]);
        Assert.Equal(3, provider.Requests.Count);
    }
}
