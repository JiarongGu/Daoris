using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// What a layer run's turn is handed (the agent-lyntai design §4.1, §5.5): its door and the layer door it opens, the prompt the layer carries
/// on stdin, the transcript its lines are written to, D125's default cool-off for a limit not stated, and the turn's options the layer's
/// members carry: the working directory, the account, the model and the tool policy.
/// </summary>
/// <param name="Door">The Daoris door, which is the layer provider's id.</param>
/// <param name="Prompt">The turn's prompt.</param>
/// <param name="Transcript">The transcript file the run's lines are written to, a resume's appended.</param>
/// <param name="CoolOff">D125's default cool-off.</param>
internal sealed record LayerStart(string Door, string Prompt, string Transcript, TimeSpan CoolOff)
{
    /// <summary>The layer door it opens: a native door's turn is the layer's <c>cli</c>.</summary>
    public string Kind { get; init; } = AgentDoorKinds.Cli;

    /// <summary>The door's transport word a failure is read on (<c>native</c>, <c>protocol</c>).</summary>
    public string Transport { get; init; } = "native";

    public string? WorkingDirectory { get; init; }

    /// <summary>The account the walk picked, its key the layer's provider's (§4.2); null runs the door's own default home.</summary>
    public AgentAccount? Account { get; init; }

    public string? Model { get; init; }

    public AgentToolPolicy Policy { get; init; } = AgentToolPolicy.Write;

    /// <summary>The agent's own permission mode in place of the policy's (Lyntai's AG25, LQ4): today's driven mode.</summary>
    public string? PermissionMode { get; init; }

    /// <summary>The effort the start's choice asked (AG26, LQ5).</summary>
    public string? Effort { get; init; }

    /// <summary>The plugin folders the start composed (AG27, LQ6), each fully qualified.</summary>
    public IReadOnlyList<string> Plugins { get; init; } = [];

    /// <summary>The clock a failure is seen on.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;
}
