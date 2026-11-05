using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The supported harness (D46 §5): Claude Code in its non-interactive mode, in the repository root,
/// with the composed target as the prompt.
/// </summary>
/// <remarks>
/// <para><b>The permission mapping is the D37 boundary, stated in the harness's own vocabulary.</b>
/// Edits auto-accept — reversible, in-repository, the automated middle — and every other tool runs
/// under the repository's own checked-in permission configuration, exactly as an interactive session
/// there would. Nothing at the outward boundary is auto-approved by the driver, ever: a session that
/// cannot proceed ends, and the observation concludes it honestly.</para>
///
/// <para><b>The connector is the session's voice.</b> An adopted repository's own `.mcp.json` wires
/// the knowledge tools the prompt tells the session to claim and close its quest with — that wiring
/// is the connector's job at adoption, not something the driver may reach in and write (that would be
/// the very edit this whole system exists to prevent).</para>
///
/// <para><b>No model is ever named</b> (D24): which model answers is the harness's own configuration
/// in that repository. The default command is `claude` off the PATH; a machine whose shim needs a
/// path names it in `commands` like any other adapter command.</para>
/// </remarks>
public sealed class ClaudeCodeAdapter : ISessionAdapter
{
    public string Name => "claude-code";

    /// <summary>Claude Code loads the tree's <c>.claude/skills</c> and its <c>CLAUDE.md</c> whole itself (CONTEXTONE1c).</summary>
    public bool LoadsTreeSkills => true;

    /// <summary>
    /// The supported harness holds a conversation (D49 §3): run without a one-shot prompt it takes
    /// turns on its own streams, which is all this seam asks of it.
    /// </summary>
    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        var arguments = resolved.Skip(1)
            .Concat(["-p", "--permission-mode", DrivenMode(target)])
            .Concat(target.Asked?.NativeArguments(resolved) ?? [])
            .Concat(StreamJsonOut);

        return PromptOnInput(Spawning.InRoot(target, resolved[0], arguments, redirectInput: true));
    }

    /// <summary>
    /// The prompt goes on the harness's standard input under a bare <c>-p</c> (AGENTLYNTAI1s-i, <see cref="NativePrompt"/>): a process's
    /// arguments are readable machine-wide. The input is written as BOM-less UTF-8, because a byte order mark would be the prompt's
    /// first character, and the harness's own default for a pipe is not Daoris's to guess.
    /// </summary>
    private static ProcessStartInfo PromptOnInput(ProcessStartInfo info)
    {
        info.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        return info;
    }

    /// <summary>
    /// The permission mode a driven run names: its repository's permission prompts' (AUTONOMY1b, D184 point 3), <c>default</c> asking or
    /// <c>bypassPermissions</c> unrestricted, in place of <c>acceptEdits</c>; <c>acceptEdits</c> under auto and for every target the
    /// setup does not reach (D81).
    /// </summary>
    private static string DrivenMode(SessionTarget target) => target.Access?.PermissionMode ?? "acceptEdits";

    /// <summary>
    /// The harness's own structured output (D76, CONV3), checked against the binary before it was
    /// written (docs/2026-09-25-stream-json-evidence.md): `stream-json` needs `--verbose` beside it, and
    /// partial messages are what let the words stream as they are written.
    /// </summary>
    private static readonly string[] StreamJsonOut =
        ["--output-format", "stream-json", "--verbose", "--include-partial-messages"];

    public IStreamMapper StructuredOutput() => new ClaudeStreamJson();

    /// <summary>The harness's own resume (ANSWER1a, D131 §1): <c>--resume &lt;id&gt;</c>, the id its <c>init</c> line named.</summary>
    public bool Resumes => true;

    /// <summary>
    /// <c>claude -p --resume &lt;id&gt;</c>, with the posture and the structured output a start has. The answer is
    /// the conversation's next turn as it is: the conversation already holds the target, so it is not sent again. It goes on the
    /// harness's input, never in its arguments (AGENTLYNTAI1s-i): the driver writes it there as it starts the run
    /// (<see cref="NativePrompt"/>).
    /// 🔴 The flag is the maker's documented one, not yet run on this machine by a driven session (design §6).
    /// </summary>
    public ProcessStartInfo PrepareResume(SessionTarget target, IReadOnlyList<string>? command, string conversation)
    {
        var resolved = Resolve(command);
        var arguments = resolved.Skip(1)
            .Concat(["-p", "--resume", conversation, "--permission-mode", DrivenMode(target)])
            .Concat(target.Asked?.NativeArguments(resolved) ?? [])
            .Concat(StreamJsonOut);

        return PromptOnInput(Spawning.InRoot(target, resolved[0], arguments, redirectInput: true));
    }

    /// <summary>
    /// A person's message as one `stream-json` user line — the shape the binary took on stdin, one turn
    /// per line, in the probe the evidence records.
    /// </summary>
    public string FrameMessage(string text) => JsonSerializer.Serialize(new
    {
        type = "user",
        message = new { role = "user", content = new[] { new { type = "text", text } } },
    });

    /// <summary>
    /// The turn's stop as a <c>control_request</c> of subtype <c>interrupt</c> — answered by the binary in
    /// the probe with a <c>control_response</c>, the turn ending <c>aborted_streaming</c> or
    /// <c>aborted_tools</c>, and the session taking the next turn (stream-json evidence, § Stopping a turn).
    /// </summary>
    public string FrameInterrupt() => JsonSerializer.Serialize(new
    {
        type = "control_request",
        request_id = $"daoris-{Guid.NewGuid():N}",
        request = new { subtype = "interrupt" },
    });

    /// <summary>
    /// A conversation: no target prompt, because the person supplies the first message.
    /// </summary>
    /// <remarks>
    /// <para>The permission posture is the SAME as a driven session's and for the same reason — the
    /// repository's own checked-in configuration governs, and nothing at the outward boundary is ever
    /// auto-approved (D46 §5, obligation 3). A chat does not get more latitude because a person is
    /// watching; the person being present is why a dirty tree is allowed, not why a push would be.</para>
    ///
    /// <para>No model is named here either (D24): which model answers is the harness's own
    /// configuration in that repository.</para>
    ///
    /// <para>🔴 <b>Ask Daoris ran here in <c>acceptEdits</c> with its allow-list</b>, not the asking mode HELP1a meant, which reached
    /// the protocol door alone (HELPMODE1a1's finding). Over <c>-p</c> an unlisted tool is never allowed, so it read and proposed
    /// all the same. In full access it names <see cref="FullPosture"/> (<see cref="ChatTarget.PermissionMode"/>), in place of
    /// <c>acceptEdits</c>.</para>
    /// </remarks>
    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        // A conversation on the harness's structured wire (CONV3): each message a `stream-json` line on
        // stdin, one turn each, until input ends — the same two endings the chat door keeps. One that goes on
        // (MSG1c) resumes its own conversation by the id kept for its record, never `--continue` (D137 §4.2).
        return Spawning.ChatInRoot(
            target, resolved[0],
            resolved.Skip(1)
                .Concat(["-p", "--input-format", "stream-json", "--permission-mode", target.PermissionMode ?? "acceptEdits"])
                .Concat(target.Resume is { Length: > 0 } conversation ? ["--resume", conversation] : [])
                .Concat(target.Asked?.NativeArguments(resolved) ?? [])
                .Concat(StreamJsonOut));
    }

    /// <summary>
    /// <c>--permission-mode bypassPermissions</c>, Claude Code's mode that asks nothing, measured on 2.1.281 (the stream-json
    /// evidence, CONV4a's probe), and only by the person's own setting: Ask Daoris's full access (HELPMODE1a1, D183), a repository's
    /// unrestricted permission prompts (AUTONOMY1b, D184). That its deny rules and its hooks still hold under it is the maker's word,
    /// not yet measured here (D183's HELPMODE1a1 note, D184's AUTONOMY1b note).
    /// </summary>
    public string? FullPosture => "bypassPermissions";

    /// <summary>
    /// <c>--permission-mode default</c>, Claude Code's asking mode, for a repository's asking permission prompts (AUTONOMY1b, D184).
    /// Over <c>-p</c> no client answers an ask: the harness refuses a tool its rules do not allow itself (HELPMODE1a1's finding), and
    /// there is no wire to hold one on for the person (AUTONOMY1b3a).
    /// </summary>
    public string? AskingPosture => "default";

    /// <summary>
    /// `--mcp-config &lt;file&gt;` — verified on the binary (`claude --help`, 2026-09-23: *"Load MCP
    /// servers from JSON files"*). The file is Daoris's, under its home; the repository's own
    /// `.mcp.json` is untouched and still governs its own servers. What the session may CALL stays
    /// the repository's allow-list (D37, D46 §5) — a server offered is a tool available, not a tool
    /// approved.
    /// </summary>
    public void HandServers(ProcessStartInfo info, string configFile)
    {
        info.ArgumentList.Add("--mcp-config");
        info.ArgumentList.Add(configFile);
    }

    /// <summary>The rules Daoris composed (PERM1, D72), as this harness's settings flag.</summary>
    public bool TakesSettings => true;

    /// <summary>
    /// `--settings &lt;file&gt;` — *"load additional settings from"* (`claude --help`, 2.1.280, HELP3's
    /// evidence): the command-line tier, which the harness merges with the person's own settings and the
    /// repository's, `deny` beating `allow`. The posture stays `acceptEdits`: rules are a scope, not a mode.
    /// </summary>
    public void HandSettings(ProcessStartInfo info, string settingsFile)
    {
        info.ArgumentList.Add("--settings");
        info.ArgumentList.Add(settingsFile);
    }

    /// <summary>
    /// `--plugin-dir &lt;path&gt;` — *"Load a plugin from a directory or .zip for this session only"* (`claude --help`, 2.1.299):
    /// one per plugin a spawn composed (HELPPLUG1b). Measured in an untrusted folder with a fresh config directory (D170's
    /// note): each plugin's skills load, named <c>&lt;plugin&gt;:&lt;skill&gt;</c>, a dotted plugin name among them.
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
    /// The harness's own mechanisms (D49 §4), <b>verified against the real binary</b> before they were
    /// written down — every line of this is a claim about somebody else's tool, and a guessed one
    /// fails at the worst moment, in a person's terminal, saying something that is not true.
    /// </summary>
    /// <remarks>
    /// <para>Install is a whole command because a machine without the harness cannot run it; update
    /// and login are the harness's own subcommands, because a present harness updates and authenticates
    /// itself.</para>
    ///
    /// <para><b>The login question answers JSON with a boolean, and the binary exits 0 either way</b> —
    /// so the output is the answer and the exit code is deliberately not consulted. It also volunteers
    /// an email, an organisation and a subscription tier; Daoris reads the boolean and the email —
    /// who signed in, which is what a person names an account by (D66 §3) — and keeps neither the
    /// organisation nor the tier.</para>
    ///
    /// <para><c>CLAUDE_CONFIG_DIR</c> is the environment seam: a spawn under it is genuinely a separate
    /// account — a fresh directory reports logged out while the machine's own home reports logged in.</para>
    /// </remarks>
    public HarnessToolchain? Toolchain => new(
        Product: "Claude Code",
        Mark: "CC",
        Maker: "Anthropic",
        Binary: ["claude"],
        VersionArguments: ["--version"],
        ProfileVariable: "CLAUDE_CONFIG_DIR",
        Install: ["npm", "install", "-g", "@anthropic-ai/claude-code"],
        UpdateArguments: ["update"],
        LoginArguments: ["auth", "login"],
        LoginCheck: ClaudeCodeProvider.Login,
        // A pin comes from the release bucket, against its SIGNED manifest (AGT2b) — the npm package
        // installs the same native binary, with nothing but npm's own integrity check behind it.
        Channel: ClaudeReleases.Channel,
        // 🔴 Where it records the workspaces a person has accepted (DEPLOY1). Read before every
        // driven spawn, because an untrusted tree makes the repository's own allow-list inert and
        // the session cannot then take or close its quest — nine minutes and a real login, three
        // times over, before this was measured rather than assumed.
        TrustFile: ClaudeTrust.FileName,
        // Its user-tier settings, `model` and `effortLevel` among them (AGT6): read from its ACP adapter's
        // own settings reader and its SDK's settings schema, never guessed. The ACP door's are this one's.
        SettingsFile: AgentSettings.FileName,
        PinnedEnvironment: StayPinned,
        // An account that is an API key (AGT3). Measured on 2.1.280 with an invalid key: `auth
        // status` reads it (api_key, no email) and a `-p` run takes it with no prompt.
        KeyVariable: "ANTHROPIC_API_KEY",
        // What it prints when its provider refuses the credential (AGT3b) — measured on 2.1.280: a
        // `-p` run with an invalid key was silent for 189 s of retries, then printed "Failed to
        // authenticate. API Error: 401 API key is invalid." and exited 1. Measured in text mode; on
        // this door's `stream-json` they are read from a failed `result` and from what it writes
        // outside its frames and on stderr, never from its agent's words (AGT3c).
        Refused: "API Error: 401",
        // What it says when an account's limit refuses a turn (TOOL4a, D125 §0.2): five recorded
        // sentences and one the maker documents (TOOL4k), each naming its reset. Its ACP door reads
        // these as its owner's (AGT7).
        Limits: ClaudeLimits.Words,
        // Its maker fixes an account's weekly reset at one time each week (TOOL6b, D130 §0.3 C1: "The weekly limit resets
        // at a fixed time each week that is assigned to your account"), so a weekly reset a limit told is carried on.
        WeekFixed: true,
        // What it says about an account's windows (TOOL6c, limit-signals evidence §1): each window's use and reset on every
        // `rate_limit_event`, which its ACP door forwards as `usage_update._meta["_claude/rateLimit"]` and reads as its owner's.
        Windows: ClaudeWindows.Words,
        // What its ACP door says when it refuses a start for its sign-in (ROSTER1b): recorded on the install, 2026-10-04, on an
        // account nobody had signed in to. Its ACP door reads these as its owner's (AGT7).
        SignIn: ClaudeSignIn.Words,
        // Its own whole-account windows, asked of the native CLI with no prompt (USAGEREAD2a, D165 point 1): the protocol
        // door reads its owner's through `UsageOf` (AGT7).
        Usage: ClaudeUsage.Question,
        // Its `result.usage` is each turn's own total, summed over the turn's calls: measured on 2.1.282 (stream-json evidence,
        // CONV5; USAGECAP1a2).
        TurnReport: UsageHistory.PerTurn,
        // Its provider's error words (AGENTERRORS1a, the agent-errors design §4.4): the SDK's kind words, the statuses and the
        // recorded transient sentences, which its ACP door reads after its own entry.
        ErrorWords: ClaudeErrors.Words);

    /// <summary>
    /// What a pinned <c>claude</c> runs with so it stays the version pinned (AGT2). 🔴 Measured on a
    /// pinned 2.1.270 with no login: its own <c>claude doctor</c> read <i>Auto-updates: enabled</i> and
    /// called a copy in Daoris's folder an npm-global install; with <c>DISABLE_UPDATES=1</c> it read
    /// disabled, refused <c>claude update</c>, and stayed 2.1.270. <c>DISABLE_AUTOUPDATER</c> stops
    /// only the background check, and a pin is every path.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> StayPinned =
        new Dictionary<string, string> { ["DISABLE_UPDATES"] = "1" };

    public IReadOnlyDictionary<string, string> ToolsUpFront => LoadToolsUpFront;

    /// <summary>
    /// 🔴 Read in the managed binary (HELP5, Claude Code 2.1.274): with <c>ENABLE_TOOL_SEARCH</c> unset, every
    /// MCP tool waits behind a <c>ToolSearch</c> step, a whole model round trip before the agent's first real
    /// move; a value its parser reads as false loads every tool with the first request instead.
    /// </summary>
    /// <remarks>
    /// It loads every server's tools, not only the connector's (CONTEXTONE1d). Claude Code 2.1.314 also reads a server entry's
    /// <c>alwaysLoad</c> and a tool's <c>_meta</c> <c>anthropic/alwaysLoad</c>, but the protocol door's servers are composed by its
    /// adapter, not shown to carry the key, and a tool's mark does not hold the first request for a server still starting.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, string> LoadToolsUpFront =
        new Dictionary<string, string> { ["ENABLE_TOOL_SEARCH"] = "false" };

    private IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : Toolchain!.Binary;
}
