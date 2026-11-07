using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's native door's spawn: <c>claude -p</c> in its structured output, the posture as its
/// permission mode, the prompt on stdin, and its carriers as its own flags. No model is ever named: which model answers is its own
/// configuration, or the role's choice in its own words.
/// </summary>
/// <remarks>
/// <para><b>The permission mapping is the outward boundary, stated in the agent's own vocabulary.</b> Edits auto-accept, and every other tool
/// runs under the repository's own checked-in permission configuration, as an interactive session there would. Nothing at the outward
/// boundary is auto-approved by the driver: a session that cannot proceed ends.</para>
/// </remarks>
internal sealed class ClaudeCodeSpawn : ISpawn, IServerHanding
{
    private static readonly string[] Program = ["claude"];

    /// <summary>Claude Code's variables for the model and the effort; both its doors read them.</summary>
    public static SpawnChoiceVariables ChoiceVariablesOfClaude { get; } = new("ANTHROPIC_MODEL", "CLAUDE_CODE_EFFORT_LEVEL");

    public SpawnChoiceVariables ChoiceVariables => ChoiceVariablesOfClaude;

    /// <summary>Claude Code's seam, for every door that runs its program (<see cref="ISpawn.PointAtClaude"/>): the Agent SDK told which <c>claude</c> to run.</summary>
    internal static void PointAt(ProcessStartInfo info, string managedClaude) => ClaudeAcp.PointAtClaude(info, managedClaude);

    /// <summary>
    /// Its own structured output, checked against the binary:
    /// <c>stream-json</c> needs <c>--verbose</c> beside it, and partial messages let the words stream as they are written.
    /// </summary>
    private static readonly string[] StreamJsonOut = ["--output-format", "stream-json", "--verbose", "--include-partial-messages"];

    /// <summary>
    /// What a pinned <c>claude</c> runs with so it stays the version pinned. 🔴 Measured on a pinned 2.1.270 with no login: its own
    /// <c>claude doctor</c> read <i>Auto-updates: enabled</i>; with <c>DISABLE_UPDATES=1</c> it read disabled, refused <c>claude update</c>,
    /// and stayed 2.1.270. <c>DISABLE_AUTOUPDATER</c> stops only the background check, and a pin is every path.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> StayPinned = new Dictionary<string, string> { ["DISABLE_UPDATES"] = "1" };

    /// <summary>
    /// 🔴 Read in the managed binary (Claude Code 2.1.274): with <c>ENABLE_TOOL_SEARCH</c> unset, every MCP tool waits behind a
    /// <c>ToolSearch</c> step, a whole model round trip before the agent's first real move; a value its parser reads as false loads every
    /// tool with the first request instead. It loads every server's tools, not only the connector's.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> LoadToolsUpFront = new Dictionary<string, string> { ["ENABLE_TOOL_SEARCH"] = "false" };

    public ProcessRecipe Start(SpawnRequest request)
    {
        var resolved = Resolve(request.Command);
        return PromptOnInput(ProcessRecipe.Of(
            resolved, ["-p", "--permission-mode", Mode(request), .. request.Asked?.NativeArguments(resolved) ?? [], .. StreamJsonOut]));
    }

    /// <summary>
    /// <c>claude -p --resume &lt;id&gt;</c>, the id its <c>init</c> line named, with the posture and the structured
    /// output a start has. The conversation already holds the target; its next turn goes on the input, never in the arguments.
    /// </summary>
    public ProcessRecipe? Resume(SpawnRequest request)
    {
        if (request.Resume is not { } conversation) return null;
        var resolved = Resolve(request.Command);
        return PromptOnInput(ProcessRecipe.Of(
            resolved,
            ["-p", "--resume", conversation, "--permission-mode", Mode(request), .. request.Asked?.NativeArguments(resolved) ?? [], .. StreamJsonOut]));
    }

    /// <summary>
    /// A conversation on its structured wire: each message a <c>stream-json</c> line on stdin, one turn each. Its posture is a
    /// driven session's, for the same reason, or its mode that asks nothing where the person's own setting names it.
    /// One that goes on resumes its own conversation by the id kept for its record, never <c>--continue</c>.
    /// </summary>
    public ProcessRecipe Chat(SpawnRequest request)
    {
        var resolved = Resolve(request.Command);
        return ProcessRecipe.Of(
            resolved,
            [
                "-p", "--input-format", "stream-json", "--permission-mode", Mode(request),
                .. request.Resume is { Length: > 0 } conversation ? ["--resume", conversation] : Array.Empty<string>(),
                .. request.Asked?.NativeArguments(resolved) ?? [],
                .. StreamJsonOut,
            ]);
    }

    public IReadOnlyDictionary<string, string> ToolsUpFront => LoadToolsUpFront;

    /// <summary>
    /// <c>--mcp-config &lt;file&gt;</c>, verified on the binary: the file is Daoris's, under its home; the repository's own <c>.mcp.json</c>
    /// still governs its own servers, and what the session may call stays the repository's allow-list.
    /// </summary>
    public void HandServers(ProcessStartInfo info, string configFile)
    {
        info.ArgumentList.Add("--mcp-config");
        info.ArgumentList.Add(configFile);
    }

    /// <summary>The plan's servers as its own file (<see cref="ClaudeServers"/>), named on the command line; none where there are none.</summary>
    public string? Hand(ProcessStartInfo info, string home, ConnectorPlan plan)
    {
        var file = ClaudeServers.Write(home, plan);
        if (file is not null) HandServers(info, file);
        return file;
    }

    /// <summary>
    /// <c>--settings &lt;file&gt;</c> (2.1.280): the command-line tier, merged with the person's own settings and the
    /// repository's, <c>deny</c> beating <c>allow</c>. Rules are a scope, not a mode.
    /// </summary>
    public void HandSettings(ProcessStartInfo info, string settingsFile)
    {
        info.ArgumentList.Add("--settings");
        info.ArgumentList.Add(settingsFile);
    }

    /// <summary>
    /// <c>--plugin-dir &lt;path&gt;</c>, one per plugin a spawn composed, measured in an untrusted folder with a fresh
    /// configuration directory: each plugin's skills load, named <c>&lt;plugin&gt;:&lt;skill&gt;</c>.
    /// </summary>
    public void HandPlugins(ProcessStartInfo info, IReadOnlyList<string> plugins)
    {
        foreach (var plugin in plugins)
        {
            info.ArgumentList.Add("--plugin-dir");
            info.ArgumentList.Add(plugin);
        }
    }

    /// <summary>
    /// The prompt goes on stdin under a bare <c>-p</c> (<see cref="NativePrompt"/>), as BOM-less UTF-8: a process's
    /// arguments are readable machine-wide.
    /// </summary>
    private static ProcessRecipe PromptOnInput(ProcessRecipe recipe) => recipe with { Input = true, Utf8Input = true };

    /// <summary>
    /// The permission mode the start names: its repository's permission prompts', <c>default</c> asking or
    /// <c>bypassPermissions</c> unrestricted, or the person's own for a conversation; <c>acceptEdits</c> under auto and for every start
    /// the setup does not reach.
    /// </summary>
    private static string Mode(SpawnRequest request) => request.Mode ?? "acceptEdits";

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) => command is { Count: > 0 } ? command : Program;
}
