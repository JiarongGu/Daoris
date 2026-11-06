using Lyntai;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's driven kind bound to the layer (AGENTLYNTAI1d, the agent-lyntai design §4.1, §4.2): its CLI provider registered under
/// <c>claude-code</c> over the program a start resolved, opened as the layer's <c>cli</c> door for a driven start, its resume and its
/// go-ons; a conversation stays Daoris-run until 1f-i. A refused model is told by the door's two entries for it: the assistant's
/// <c>model_not_found</c> and a result's sentence no field read.
/// </summary>
internal static class ClaudeDrivenLayer
{
    public const string Door = "claude-code";

    /// <summary>The entries the layer's Claude door spells a refused model with (Lyntai's AG12).</summary>
    public static IReadOnlySet<string> ModelRefusals { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "claude.assistant.error=model_not_found", "claude.result:sentence:model-refused" };

    /// <summary>The registration a driven start builds its spawn's container from, <paramref name="command"/> the program it resolved.</summary>
    public static LayerRegistration Registration(string command) =>
        new(Door, builder => builder.AddClaudeCliAgentSession(command: command, id: Door, agent: Door))
        {
            ModelRefusals = ModelRefusals,
            Steps = call => ClaudeStreamJson.Step(call.Name, call.ArgumentsJson, call.CallId),
        };

    /// <summary>The binding of the driven kind alone, over <paramref name="command"/>.</summary>
    public static LayerBinding Binding(string command) =>
        new(Registration(command), AgentDoorKinds.Cli, new HashSet<StartKind> { StartKind.Driven });
}
