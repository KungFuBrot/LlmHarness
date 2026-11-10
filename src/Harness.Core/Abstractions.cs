using System.Text.Json;

namespace Harness.Core;

public sealed record ToolDefinition(string Name, string Description, JsonElement InputSchema);

public sealed record LlmRequest(
    string SystemPrompt,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<ToolDefinition> Tools);

public sealed record LlmResponse(ChatMessage Message, StopReason StopReason, Usage Usage);

/// <summary>
/// Adapter zu einem Modellanbieter. Die Harness kennt nur dieses Interface;
/// konkrete Provider (z. B. Anthropic über das offizielle C#-SDK) liegen in eigenen Projekten.
/// </summary>
public interface ILlmProvider
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);
}

public sealed record ToolResult(string Content, bool IsError = false)
{
    public static ToolResult Ok(string content) => new(content);

    public static ToolResult Error(string message) => new(message, IsError: true);
}

public interface ITool
{
    ToolDefinition Definition { get; }

    Task<ToolResult> InvokeAsync(JsonElement input, CancellationToken ct = default);
}

public enum PermissionDecision
{
    Allow,
    Deny,
}

/// <summary>Wird vor jedem Werkzeugaufruf gefragt, z. B. um einen Menschen freigeben zu lassen.</summary>
public interface IPermissionPolicy
{
    Task<PermissionDecision> CheckAsync(ToolCallPart call, CancellationToken ct = default);
}

public sealed class AllowAll : IPermissionPolicy
{
    public Task<PermissionDecision> CheckAsync(ToolCallPart call, CancellationToken ct = default) =>
        Task.FromResult(PermissionDecision.Allow);
}
