using System.Runtime.CompilerServices;
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
            Options = (prompt, carriers) => new Lyntai.Providers.ClaudeCli.ClaudeAgentOptions
            {
                Prompt = prompt, SettingsPath = carriers?.Settings, McpConfigPath = carriers?.Servers,
            },
        };

    /// <summary>
    /// What today's driven start hands Claude Code, as the layer's start carries it: the mode it runs under (today's word, or
    /// <c>acceptEdits</c> where none is asked), the model and effort its choice carries as today's native arguments carry them, and the
    /// plugin folders it composed.
    /// </summary>
    public static LayerStart Start(LayerStart start, SpawnRequest request, IReadOnlyList<string> plugins) => start with
    {
        PermissionMode = request.Mode ?? "acceptEdits",
        Model = request.Asked?.Model is { Source: "entry" or "override", Carried: true, Value: { } model } ? model : null,
        Effort = request.Asked?.Effort is { Source: "entry" or "override", Carried: true, Value: { } effort } ? effort : null,
        Plugins = plugins,
    };

    /// <summary>The binding of the driven kind alone, over <paramref name="command"/>.</summary>
    public static LayerBinding Binding(string command) =>
        new(Registration(command), AgentDoorKinds.Cli, new HashSet<StartKind> { StartKind.Driven });

    [ModuleInitializer]
    internal static void Install() => DrivenRuns.Bind(Door, Run);

    /// <summary>
    /// A driven start's run on the layer, held by its executor: the program the start resolved (its file and the arguments before
    /// <c>-p</c>) started in place of the door's command, under the environment its shell composed; the mode, model, effort and plugin
    /// folders it was handed carried by the layer's options, its rules and servers files by the carriers. A partner's piece stays on today's
    /// transport, its job one the launcher's seat does not name.
    /// </summary>
    internal static HeldRun? Run(ITransportHost host, TransportStart start, SpawnRequest request)
    {
        if (start.Piece is not null) return null;
        var args = start.Info.ArgumentList;
        var plugins = args.Select((arg, at) => (arg, at)).Where(each => each.arg == "--plugin-dir" && each.at + 1 < args.Count)
            .Select(each => args[each.at + 1]).ToList();
        return new LayerRun(
            Registration(Door),
            Start(new LayerStart(Door, Prompt: "", Transcript: "", AccountLimits.DefaultCoolOff), request, plugins) with
            {
                WorkingDirectory = start.Info.WorkingDirectory is { Length: > 0 } cwd ? cwd : null,
            },
            new LauncherSeat(ContainedKind.Agent, start.Whose, host.Home, host.Log)
            {
                Program = (start.Info.FileName, [.. args.TakeWhile(arg => arg != "-p")]),
            },
            exists: _ => true,
            new LayerHold(host, start));
    }
}
