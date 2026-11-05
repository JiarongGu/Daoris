using System.Text.Json.Nodes;

namespace Daoris.Knowledge;

/// <summary>
/// Claude Code's call key: it names the call, <c>claudecode/toolUseId</c>, which its <c>PreToolUse</c> hook names beside the
/// <c>agent_id</c> that made it; the join is the driver's (<c>ChildLineage</c>). Measured on Claude Code 2.1.299.
/// </summary>
public sealed class ClaudeCallKeys : ICallKeys
{
    /// <summary>Claude Code, by the name the driver gives its harness.</summary>
    public const string Agent = "claude-code";

    /// <summary>Claude Code's id of the call, the hook's <c>tool_use_id</c>.</summary>
    public const string ToolUse = "claudecode/toolUseId";

    public string Harness => Agent;

    public string Key => ToolUse;

    public CallLineage? Read(JsonNode value) => CallLineage.Id(value) is { } toolUse
        ? new CallLineage(Agent) { ToolUse = toolUse }
        : new CallLineage(Agent) { Malformed = "toolUse" };
}
