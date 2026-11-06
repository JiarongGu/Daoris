using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The supported harness over the <b>protocol door</b> (ACP2/D53): Claude Code through the ACP
/// project's adapter, which runs the Agent SDK.
/// </summary>
/// <remarks>
/// <para><b>Two seams, both established keylessly</b> in the dsh evaluation's §1a.
/// <c>CLAUDE_CODE_EXECUTABLE</c> points the SDK at a <c>claude</c> of Daoris's choosing — which is
/// what makes probe 3's "native binary not found" the useful part of that probe, since the adapter
/// need not carry a second copy of a tool the toolchain already manages. <c>CLAUDE_CONFIG_DIR</c> is
/// the same account seam the pipe door uses, applied by the same one line in the driver.</para>
///
/// <para><b>Nothing about the target or the posture is a command-line argument here.</b> The target
/// arrives as <c>session/prompt</c> and the posture is a <b>mode</b> on the wire — an adapter that
/// also passed <c>-p</c> would send the work twice, and one that passed <c>--permission-mode</c>
/// would be stating the posture in the other door's vocabulary.</para>
///
/// <para><b>It is a separate harness to the toolchain</b>, with its own package and its own pin. The
/// ACP adapter and <c>claude</c> are different programs at different versions, and one pin for both
/// would install the wrong thing under a name somebody trusted.</para>
///
/// <para><b>No model is named</b> (D24): which model answers is the harness's own configuration, and
/// the ACP wire's model catalogue is deliberately not read.</para>
/// </remarks>
public sealed class ClaudeAcpAdapter : ISessionAdapter
{
    public string Name => "claude-code-acp";

    public SessionWire Wire => SessionWire.Acp;

    /// <summary>
    /// <c>auto</c> where the adapter offers it — Claude Code's own mode, in which the harness judges
    /// each action — and <c>acceptEdits</c>, the posture observed in the evaluation's §1a, from one that
    /// does not (D81: a session does as much as the harness would on its own). Never <c>bypassPermissions</c>, which judges nothing, however available the wire
    /// makes it, but by the person's own setting (<see cref="FullPosture"/>: Ask Daoris's full access, D183; a repository's unrestricted
    /// permission prompts, D184). The pipe door still passes <c>--permission-mode acceptEdits</c>: <c>auto</c> is measured
    /// on this wire and not yet on that one.
    /// </summary>
    public string? AcpPosture => "auto|acceptEdits";

    /// <summary>Claude Code loads the tree's <c>.claude/skills</c> and its <c>CLAUDE.md</c> whole itself (CONTEXTONE1c).</summary>
    public bool LoadsTreeSkills => true;

    /// <summary>
    /// <c>bypassPermissions</c>, Claude Code's mode that asks nothing, offered on <c>session/new</c> (the evaluation's §1a), and
    /// only by the person's own setting: Ask Daoris's full access (HELPMODE1a1, D183), a repository's unrestricted permission prompts
    /// (AUTONOMY1b, D184). Where the adapter does not offer it, the start fails saying so.
    /// </summary>
    public string? FullPosture => "bypassPermissions";

    /// <summary>
    /// <c>default</c>, Claude Code's asking mode (Ask Daoris's restricted posture, <see cref="HelpRoom.Posture"/>), for a repository's
    /// asking permission prompts (AUTONOMY1b, D184).
    /// </summary>
    public string? AskingPosture => "default";

    /// <summary>
    /// The adapter takes turns on its own wire, which is all this seam asks of an interactive
    /// harness — a conversation over ACP is the same session entity by a different door (D49 §3).
    /// </summary>
    public bool Interactive => true;

    /// <summary>Its door's spawn (<see cref="ClaudeAcpSpawn"/>), which this class's spawn members are one-line doors to until row t.</summary>
    private static readonly ClaudeAcpSpawn Spawn = new();

    public ProcessStartInfo Prepare(SpawnRequest request) => DoorStarts.Started(Spawn, request);

    public ProcessStartInfo PrepareChat(SpawnRequest request) => DoorStarts.Chatted(Spawn, request);

    /// <summary>The rules Daoris composed (PERM1, D72), taken on the wire rather than as an argument.</summary>
    public bool TakesSettings => true;

    /// <summary>Its door's dialect (<see cref="ClaudeProtocolDialect"/>), which this class's <c>_meta</c> members are one-line doors to until row t.</summary>
    private static readonly ClaudeProtocolDialect Dialect = new();

    public object? AcpSessionMeta(string settingsFile) => Dialect.SessionMeta(settingsFile, []);

    public object? AcpSessionMeta(string settingsFile, IReadOnlyList<string> plugins) => Dialect.SessionMeta(settingsFile, plugins);

    /// <summary>
    /// The pipe door's switch (HELP5), on this door's spawn: the Agent SDK runs <c>claude</c> with this
    /// process's environment, which is how the pin's own switch reaches it too (AGT2).
    /// </summary>
    public IReadOnlyDictionary<string, string> ToolsUpFront => Spawn.ToolsUpFront;

    /// <summary>
    /// The adapter's own mechanisms. Pinned EXACT by default (D53's note on a harness that moved
    /// 0.79 in the week it was evaluated): a protocol door whose adapter changes under a running
    /// loop is the moving target D49 §4 already refuses for harnesses.
    /// </summary>
    public HarnessToolchain? Toolchain => new(
        Product: "Claude Code",
        Mark: "CC",
        Maker: "Anthropic",
        // 🔴 The BINARY is `claude-agent-acp`, not this adapter's Daoris name — verified against
        // the installed package, after a guess was caught by `harness list` reporting a pin that
        // was there as NOT INSTALLED. Every field here is a claim about somebody else's program.
        Binary: ["claude-agent-acp"],
        VersionArguments: ["--version"],
        // The same account seam the pipe door uses — one variable, applied by one line in the driver,
        // so neither door can run as an account the other would not have chosen.
        ProfileVariable: "CLAUDE_CONFIG_DIR",
        Install: ["npm", "install", "-g", "@agentclientprotocol/claude-agent-acp"],
        // No login flow and no login question of its own: the ACCOUNT belongs to `claude`, which the
        // profile directory carries. `daoris agent login claude-code` is still the verb, and this
        // adapter reads the home it produced. Unknown is permissive, by SES3's rule.
        Package: "@agentclientprotocol/claude-agent-acp",
        // No login of its own: it runs `claude` and reads the home `claude` logged into.
        AccountOf: "claude-code",
        // The same harness underneath, so the same trust record governs this door too.
        TrustFile: ClaudeTrust.FileName,
        // Its prompt response's `usage` is each turn's own, not a running sum: measured on claude-code-acp 0.79.0, two turns
        // (stream-json evidence, CONV5; USAGECAP1a2).
        TurnReport: UsageHistory.PerTurn,
        // Its own server's word that -32000 means authentication required (AGENTERRORS1a, the agent-errors design §4.4), read from
        // this entry alone; the provider's kind words and transient sentences are its owner's, read after it.
        ErrorWords: ClaudeAcpErrors.Words);
}
