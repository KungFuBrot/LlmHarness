using System.Text.Json;

namespace Harness.Core;

public enum Role
{
    User,
    Assistant,
}

/// <summary>Ein Baustein einer Nachricht. Provider übersetzen diese Teile in ihr eigenes Format.</summary>
public abstract record ContentPart;

public sealed record TextPart(string Text) : ContentPart;

/// <summary>Das Modell möchte ein Werkzeug aufrufen.</summary>
public sealed record ToolCallPart(string Id, string Name, JsonElement Input) : ContentPart;

/// <summary>Antwort der Harness auf einen Werkzeugaufruf.</summary>
public sealed record ToolResultPart(string CallId, string Content, bool IsError = false) : ContentPart;

public sealed record ChatMessage(Role Role, IReadOnlyList<ContentPart> Parts)
{
    public static ChatMessage User(string text) => new(Role.User, [new TextPart(text)]);

    public static ChatMessage Assistant(string text) => new(Role.Assistant, [new TextPart(text)]);

    public IEnumerable<ToolCallPart> ToolCalls => Parts.OfType<ToolCallPart>();

    public string Text => string.Concat(Parts.OfType<TextPart>().Select(p => p.Text));
}

public enum StopReason
{
    EndTurn,
    ToolUse,
    MaxTokens,
    Refusal,
}

public sealed record Usage(int InputTokens, int OutputTokens)
{
    public static Usage Zero { get; } = new(0, 0);

    public static Usage operator +(Usage a, Usage b) =>
        new(a.InputTokens + b.InputTokens, a.OutputTokens + b.OutputTokens);
}
