using System.Runtime.CompilerServices;

namespace Harness.Core;

/// <summary>Was im Verlauf eines Agentenlaufs passiert. Grundlage für CLI-Ausgabe, Logs und Tracing.</summary>
public abstract record AgentEvent;

public sealed record AssistantMessageEvent(ChatMessage Message, Usage Usage) : AgentEvent;

public sealed record ToolCalledEvent(ToolCallPart Call) : AgentEvent;

public sealed record ToolFinishedEvent(ToolCallPart Call, ToolResultPart Result) : AgentEvent;

public sealed record RunFinishedEvent(StopReason StopReason, int Turns, Usage TotalUsage) : AgentEvent;

public sealed record RunAbortedEvent(string Reason, int Turns, Usage TotalUsage) : AgentEvent;

public sealed class AgentOptions
{
    public string SystemPrompt { get; init; } = "";

    /// <summary>Obergrenze an Modellaufrufen pro Lauf, damit kein Agent endlos kreist.</summary>
    public int MaxTurns { get; init; } = 25;

    public IPermissionPolicy Permissions { get; init; } = new AllowAll();
}

/// <summary>
/// Die eigentliche Harness: Modell fragen, Werkzeugaufrufe ausführen, Ergebnisse zurückgeben,
/// bis das Modell fertig ist oder eine Grenze greift.
/// </summary>
public sealed class AgentLoop(ILlmProvider provider, IEnumerable<ITool> tools, AgentOptions? options = null)
{
    private readonly Dictionary<string, ITool> _tools = tools.ToDictionary(t => t.Definition.Name);
    private readonly AgentOptions _options = options ?? new AgentOptions();

    /// <summary>
    /// Führt den Lauf aus. <paramref name="conversation"/> wird fortgeschrieben (nur angehängt, nie umgeschrieben),
    /// damit der Verlauf nach dem Lauf vollständig vorliegt.
    /// </summary>
    public async IAsyncEnumerable<AgentEvent> RunAsync(
        List<ChatMessage> conversation,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var definitions = _tools.Values.Select(t => t.Definition).ToList();
        var total = Usage.Zero;

        for (var turn = 1; turn <= _options.MaxTurns; turn++)
        {
            var response = await provider.CompleteAsync(
                new LlmRequest(_options.SystemPrompt, conversation, definitions), ct);
            total += response.Usage;
            conversation.Add(response.Message);
            yield return new AssistantMessageEvent(response.Message, response.Usage);

            var calls = response.Message.ToolCalls.ToList();
            if (response.StopReason != StopReason.ToolUse || calls.Count == 0)
            {
                yield return new RunFinishedEvent(response.StopReason, turn, total);
                yield break;
            }

            foreach (var call in calls)
                yield return new ToolCalledEvent(call);

            // Alle Aufrufe eines Zuges parallel ausführen und gemeinsam in einer Nachricht zurückgeben.
            var results = await Task.WhenAll(calls.Select(c => ExecuteAsync(c, ct)));
            foreach (var (call, result) in calls.Zip(results))
                yield return new ToolFinishedEvent(call, result);

            conversation.Add(new ChatMessage(Role.User, results));
        }

        yield return new RunAbortedEvent($"MaxTurns ({_options.MaxTurns}) erreicht.", _options.MaxTurns, total);
    }

    private async Task<ToolResultPart> ExecuteAsync(ToolCallPart call, CancellationToken ct)
    {
        if (!_tools.TryGetValue(call.Name, out var tool))
            return new ToolResultPart(call.Id, $"Unbekanntes Werkzeug '{call.Name}'.", IsError: true);

        if (await _options.Permissions.CheckAsync(call, ct) == PermissionDecision.Deny)
            return new ToolResultPart(call.Id, "Der Aufruf wurde vom Nutzer nicht freigegeben.", IsError: true);

        try
        {
            var result = await tool.InvokeAsync(call.Input, ct);
            return new ToolResultPart(call.Id, result.Content, result.IsError);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Fehler gehen als Ergebnis an das Modell zurück, damit es reagieren kann.
            return new ToolResultPart(call.Id, $"{ex.GetType().Name}: {ex.Message}", IsError: true);
        }
    }
}
