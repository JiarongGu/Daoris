using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>A driver error a person can act on. Exit code 2 territory: tool error, not policy.</summary>
public sealed class DriverException(string message) : Exception(message);

/// <param name="QuestId">The quest the session exists to serve.</param>
/// <param name="Title">One line: what is wanted.</param>
/// <param name="Body">Why, and the evidence — the asker's words, verbatim.</param>
/// <param name="Asker">Who asked.</param>
/// <param name="Repository">Whose agent the session is.</param>
/// <param name="Root">The working tree it runs in.</param>
/// <param name="ServiceUrl">Where the service is, so the session can claim and close its own quest.</param>
public sealed record SessionTarget(
    string QuestId,
    string Title,
    string Body,
    string Asker,
    string Repository,
    string Root,
    string ServiceUrl)
{
    /// <summary>Addresses the quest carries (D65 §2), handed over as the asker gave them.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Files the quest carries, with where this machine keeps each — or null where it does not.</summary>
    public IReadOnlyList<QuestFileView> Attachments { get; init; } = [];

    /// <summary>What the service publishes when this closes done (D65 §4) — told, so the session knows.</summary>
    public IReadOnlyList<QuestStepView> Then { get; init; } = [];

    /// <summary>The quest this one follows, when it is a step of a chain.</summary>
    public string? Parent { get; init; }

    /// <summary>
    /// The ask this session answers — an INTAKE (D65 §1b), which serves no quest: its spawn carries the
    /// ask and its own session instead, and no quest variable at all. Null for a quest's session.
    /// </summary>
    public string? Ask { get; init; }

    /// <summary>The session's own record — handed to an intake, whose connector names it when it publishes.</summary>
    public string? Session { get; init; }

    /// <summary>
    /// The instruction, when it is not a quest's — an intake's job (<see cref="IntakePrompt"/>). Null
    /// composes the claiming instruction, as every target always has.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// The directory the session is handed as <c>DAORIS_QUEST_ATTACHMENTS</c> — the one the service
    /// keeps this quest's files in — or null when none of them is on this machine.
    /// </summary>
    public string? AttachmentsDirectory => Attachments
        .Select(file => file.Path)
        .OfType<string>()
        .Select(System.IO.Path.GetDirectoryName)
        .FirstOrDefault(directory => !string.IsNullOrEmpty(directory));
}

/// <summary>
/// The claiming instruction (D46 §3), composed once and delivered per harness. Project-agnostic on
/// purpose: it travels to repositories that know nothing of this one's decision numbering, so it
/// speaks in the canon's names, never in D-numbers.
/// </summary>
public static class TargetPrompt
{
    /// <summary>
    /// The instruction a session is handed — an intake's own where the target carries one, so every
    /// door that delivers a target (the pipe's argument, the protocol's prompt, <c>DAORIS_TARGET</c>)
    /// delivers the same words without knowing which kind of session it is.
    /// </summary>
    public static string Compose(SessionTarget target) => target.Prompt ?? Claiming(target);

    private static string Claiming(SessionTarget target) =>
        $"""
        You are the agent for `{target.Repository}`, working inside its own repository and nowhere else.

        Your target is quest `#{target.QuestId}`, asked by `{target.Asker}`:

        # {target.Title}

        {target.Body}
        {Carried(target)}
        First take the quest (respond to `#{target.QuestId}` with `take`), then do the work inside this
        repository under its own doctrine and gates, then close it: `done` when it has landed, or
        `decline` with the reason — the reason is the part the asker can act on. If the quest is already
        taken or closed, stand down and finish without changing anything.

        Never write outside this repository. Work another repository needs is a quest published to it,
        never an edit — that is the rule the whole arrangement rests on. Anything that cannot be taken
        back or that leaves the repository — a push, a publish, a release — is not yours to do; surface
        it and finish.
        """;

    /// <summary>
    /// What the asker gave beside their words, said plainly — each link, each file where it lies, and a
    /// file this machine does not hold said to be elsewhere rather than listed as if a path would open
    /// it. Empty when the quest carries nothing, so an ordinary target reads exactly as it always has.
    /// </summary>
    private static string Carried(SessionTarget target)
    {
        if (target.Links.Count == 0 && target.Attachments.Count == 0 && target.Parent is null && target.Then.Count == 0)
        {
            return "";
        }

        var text = new StringBuilder();

        // Where it sits in a chain the asker composed: what it follows, and what its close publishes —
        // so a verifying session knows whose work it checks, and a developing one that a check comes.
        if (target.Parent is { } parent)
        {
            text.AppendLine().AppendLine(
                $"It follows quest `#{parent}`, which is done — read that quest for the work this one builds on.");
        }

        if (target.Then.Count > 0)
        {
            var next = target.Then[0];
            text.AppendLine().AppendLine(
                $"When you close it `done`, the asker's next step is published to `{next.To}`: \"{next.Title}\""
                + (target.Then.Count > 1 ? $", with {target.Then.Count - 1} more after it." : ".")
                + " Close it `done` only when that step can start from what you landed.");
        }

        if (target.Links.Count > 0)
        {
            text.AppendLine().AppendLine("Links the asker gave with it — read them; they are part of the ask:");
            foreach (var link in target.Links) text.AppendLine($"- {link}");
        }

        if (target.Attachments.Count > 0)
        {
            text.AppendLine().AppendLine(target.AttachmentsDirectory is { } directory
                ? $"Files the asker attached, kept for you in `{directory}` (also `DAORIS_QUEST_ATTACHMENTS`) — read them, never edit them:"
                : "Files the asker attached:");
            foreach (var file in target.Attachments)
            {
                text.AppendLine(file.Path is { } path
                    ? $"- `{file.Name}` — {path}"
                    : $"- `{file.Name}` — not on this machine: it stayed where the quest was published. Ask for "
                      + "what it shows if the work needs it.");
            }
        }

        return text.ToString();
    }
}

/// <param name="Repository">Whose agent the conversation is.</param>
/// <param name="Root">The working tree it runs in.</param>
/// <param name="ServiceUrl">Where the service is, so the session can take or publish quests itself.</param>
/// <remarks>
/// A chat carries no quest (D49 §3) — that is the whole point: it is for work not yet shaped as an
/// ask. It may take one mid-conversation through its own connector, exactly as a driven session does.
/// </remarks>
public sealed record ChatTarget(string Repository, string Root, string ServiceUrl);

/// <summary>How the driver talks to the spawned process once it is running (D53).</summary>
public enum SessionWire
{
    /// <summary>
    /// The harness takes its whole target at once and prints text; the driver reads the text, keeps
    /// it as the transcript, and observes the exit. The original door, and still the default.
    /// </summary>
    Pipe,

    /// <summary>
    /// The harness speaks the **Agent Client Protocol** on stdio: JSON-RPC frames, a session created
    /// on the working tree, the target delivered as a prompt, and tool boundaries, turn boundaries
    /// and thoughts arriving as structured updates.
    /// </summary>
    /// <remarks>
    /// The gain over the pipe is a timeline that needs nothing parsed out of another program's
    /// stdout — the coupling D23/D24 exist to prevent, and which D52 rejected by name. What does NOT
    /// change is where a session record comes from: the wire's own stop reason flattens an aborted,
    /// blocked or errored turn to `end_turn`, so the record still moves on the exit code and the
    /// quest's state (D46 §4) and the wire only enriches the console and the transcript.
    /// </remarks>
    Acp,
}

/// <summary>
/// One harness adapter: how a session is spawned, and how the target reaches it. An adapter names a
/// harness, never a model (D24) — which model answers is that harness's own configuration in that
/// repository.
/// </summary>
public interface ISessionAdapter
{
    string Name { get; }

    /// <summary>
    /// Which door the driver holds this harness's session over (D53). Default <see cref="SessionWire.Pipe"/>,
    /// so an adapter that says nothing behaves exactly as every adapter did before the door existed —
    /// the same silence-preserves rule the toolchain and the session trees follow.
    /// </summary>
    SessionWire Wire => SessionWire.Pipe;

    /// <summary>
    /// The permission posture D37 sanctions, in <b>this harness's own vocabulary</b>, for adapters
    /// whose wire carries one (ACP3/D53).
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>The posture lives in three different places across three harnesses</b>, which is
    /// why this is a property of the adapter and not a constant in the session. Claude Code names it
    /// <c>acceptEdits</c> and Codex names it <c>agent</c> — both ACP modes, both observed rather than
    /// guessed (`docs/2026-09-22-acp3-probe-evidence.md`). dsh's <c>session/new</c> carries no modes
    /// at all, so its posture is an environment variable set at spawn instead.</para>
    ///
    /// <para><b>Null means this wire carries no posture</b>, and the session then asks for none —
    /// leaving the agent at its own default. That is the safe direction: every default observed is
    /// equal to or stricter than the one Daoris would set, so a forgotten posture stalls a session
    /// rather than widening it. It is never a licence to guess a neighbouring mode.</para>
    /// </remarks>
    string? AcpPosture => null;

    /// <summary>
    /// The process that would be the session: spawned in the root, target delivered, output
    /// redirected so the driver can keep the transcript. Preparation only — the driver owns the
    /// process lifetime, because observing it is the driver's half of the contract (D46 §5).
    /// </summary>
    ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command);

    /// <summary>
    /// Whether this harness can be wired for turn-taking (D49 §3) — stdin carrying the person's
    /// messages, stdout streaming the harness's.
    /// </summary>
    /// <remarks>
    /// Declared honestly and opted into, never assumed: an adapter that has not been wired for a
    /// conversation says so, and asking for one errors naming what the harness is — the same rule
    /// that governs an unknown adapter name (D23). Default false, so a new adapter is non-interactive
    /// until someone has actually done the work.
    /// </remarks>
    bool Interactive => false;

    /// <summary>
    /// Hand a pipe-door session the MCP servers Daoris offers it (D65 §1f), written to a file under
    /// Daoris's home — the harness's own way of taking servers at spawn, where it has one. The
    /// protocol door carries them on the wire instead (ACP4) and never comes here.
    /// </summary>
    /// <remarks>
    /// Default: nothing — a harness with no such flag is handed nothing, and says nothing, which
    /// is the silence-preserves rule every adapter default follows. An adapter opts in when the
    /// flag is real and verified against the binary, like every other claim about somebody else's
    /// tool.
    /// </remarks>
    void HandServers(ProcessStartInfo info, string configFile) { }

    /// <summary>The process that would be a CHAT: the same spawn, with stdin open.</summary>
    ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
        throw new DriverException(
            $"the `{Name}` adapter cannot hold a conversation — it spawns an agent that takes its "
            + "target once and runs to completion. Chat with an adapter that declares `interactive`.");

    /// <summary>
    /// This harness AS A TOOL (D49 §4): where its binary is, how it reports its version, which
    /// environment variable names its configuration home, and how to run its own install, update and
    /// login flows.
    /// </summary>
    /// <remarks>
    /// Null — the default — is an adapter Daoris manages nothing about. It spawns exactly as it did
    /// before the toolchain existed, which is what lets a new adapter arrive without first answering
    /// questions about an installer it may not have. An adapter opts in when the answers are real:
    /// every field here is a claim about somebody else's tool, and a guessed one is worse than none.
    /// </remarks>
    HarnessToolchain? Toolchain => null;
}

/// <summary>What every adapter shares: the process shell, and the target riding in the environment.</summary>
internal static class Spawning
{
    /// <summary>
    /// A redirected process in the repository root. The environment, not arguments, carries the
    /// target's pieces: any script shape can read it without parsing, and nothing quest-sized ever
    /// hits a shell's quoting rules.
    /// </summary>
    /// <param name="redirectInput">
    /// Open stdin. A pipe-door session is given its whole target at once and has nobody to take turns
    /// with, so it gets none; a protocol-door session needs one, because the driver writes frames
    /// into it (D53).
    /// </param>
    public static ProcessStartInfo InRoot(
        SessionTarget target, string fileName, IEnumerable<string> arguments, bool redirectInput = false)
    {
        var info = Shell(target.Root, fileName, arguments, target.Repository, target.ServiceUrl);
        if (redirectInput) info.RedirectStandardInput = true;

        // An intake (D65 §1b) serves an ask, not a quest: the ask and its own session, and the quest
        // variables absent rather than blank — the chat rule, for the same reason.
        if (target.Ask is { } ask)
        {
            info.Environment[IntakeRoom.AskVariable] = ask;
            if (target.Session is { } session) info.Environment[IntakeRoom.SessionVariable] = session;
            info.Environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return info;
        }

        info.Environment["DAORIS_QUEST_ID"] = target.QuestId;
        info.Environment["DAORIS_QUEST_TITLE"] = target.Title;
        info.Environment["DAORIS_QUEST_BODY"] = target.Body;
        info.Environment["DAORIS_QUEST_ASKER"] = target.Asker;
        info.Environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);

        // The quest's files (D65 §2), when this machine holds any — absent rather than empty when it
        // does not, because a blank directory would read to a session as one that was emptied.
        if (target.AttachmentsDirectory is { } attachments)
        {
            info.Environment["DAORIS_QUEST_ATTACHMENTS"] = attachments;
        }

        return info;
    }

    /// <summary>
    /// The same shell for a CHAT (D49 §3), with stdin open so the person's messages reach the harness.
    /// </summary>
    /// <remarks>
    /// The quest variables are absent rather than empty: a conversation serves no quest, and a blank
    /// `DAORIS_QUEST_ID` would read to a session as an id it failed to parse. What it gets is what is
    /// true — which repository it is the agent for, and where to reach the service if the conversation
    /// turns into a quest worth taking or publishing.
    /// </remarks>
    public static ProcessStartInfo ChatInRoot(
        ChatTarget target, string fileName, IEnumerable<string> arguments)
    {
        var info = Shell(target.Root, fileName, arguments, target.Repository, target.ServiceUrl);
        info.RedirectStandardInput = true;
        return info;
    }

    private static ProcessStartInfo Shell(
        string root, string fileName, IEnumerable<string> arguments, string repository, string serviceUrl)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,

            // 🔴 The shell is a window, not a console. A console child of a GUI process is given a
            // console of its own unless this says otherwise — and nothing said otherwise, so every
            // git, every probe and every session flashed a terminal onto the desktop (owner,
            // 2026-09-23: *"a console window keep popup up"*). Held by a source scan over every
            // spawn the desktop makes, because the next spawn site will forget it too.
            CreateNoWindow = true,

            // 🔴 A transcript is read as UTF-8 or it is not the transcript. .NET defaults a
            // redirected stream to the CONSOLE's codepage; on this machine that is CP936, and the
            // first real deployment recorded an em-dash (`e2 80 94`) as `e9 88 a5 3f` — decoded as
            // GBK and re-encoded. The result is still valid UTF-8, so nothing downstream can tell it
            // was ever wrong, and a platform that speaks 简体中文 loses every Chinese character in a
            // session record. Set here, once, because every adapter goes through this shell and the
            // protocol door parses JSON-RPC off the same stream.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        info.Environment["DAORIS_REPOSITORY"] = repository;
        info.Environment["DAORIS_SERVICE_URL"] = serviceUrl;

        return info;
    }
}

/// <summary>
/// The stub: runs whatever command the configuration names, in the repository root, with the target
/// in the environment. A test double with real mechanics — real spawn, real cwd, real delivery, real
/// exit — which is what lets the family rehearsal gate the whole loop with no model in it (D46 §8).
/// </summary>
public sealed class StubAdapter : ISessionAdapter
{
    public string Name => "stub";

    /// <summary>
    /// Interactive, so the whole conversation loop can be gated with no model in it (D46 §8's argument,
    /// one layer on): a scripted exchange is a real spawn, a real stdin, a real stream and a real exit.
    /// </summary>
    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
        Spawning.InRoot(target, Command(command)[0], Command(command).Skip(1));

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
        Spawning.ChatInRoot(target, Command(command)[0], Command(command).Skip(1));

    /// <summary>
    /// A toolchain with real mechanics and no tool behind it — the same trick as the adapter itself
    /// (D46 §8). There is nothing to install or update (the "binary" is whatever the config names),
    /// but the <b>version, the environment seam and the login question are genuine</b>, which is what
    /// lets the family rehearsal gate profile selection and the logged-out refusal with no account,
    /// no credential and no model anywhere in it.
    /// </summary>
    public HarnessToolchain? Toolchain => new(
        Binary: [],
        VersionArguments: ["--version"],
        ProfileVariable: "DAORIS_STUB_CONFIG_DIR",
        // A login flow too, so signing in to another account (D66 §3) can be gated with no account:
        // whatever the configured command does with `--login` is the stub's sign-in.
        LoginArguments: ["--login"],
        LoginCheck: new LoginQuestion(
            ["--login-state"], LoggedIn: @"logged-in", LoggedOut: @"logged-out",
            Account: @"logged-in as (\S+)"),
        // Claude Code's own words for a refused credential (AGT3b), mirrored so the rehearsal can
        // gate a refused account with no account behind it.
        Refused: "API Error: 401");

    private static IReadOnlyList<string> Command(IReadOnlyList<string>? command) =>
        command is { Count: > 0 }
            ? command
            : throw new DriverException(
                "the stub adapter needs a command — name one in driver.json: "
                + """{ "commands": { "stub": ["node", "path/to/agent.mjs"] } }""");
}

/// <summary>
/// The stub's protocol twin (D53/ACP1): the same fake-binary trick, one door over.
/// </summary>
/// <remarks>
/// <para>It exists for the reason <see cref="StubAdapter"/> exists — so the whole door can be gated
/// with no model, no account and no credential anywhere in it (D46 §8). The command it runs is
/// whatever the configuration names, and the rehearsal names a small ACP agent that speaks the wire
/// and nothing else.</para>
///
/// <para><b>Stdin is open, and that is the difference that matters.</b> A pipe-door session is handed
/// its target once; a protocol-door session is written to, frame by frame, for as long as it lives.</para>
/// </remarks>
public sealed class AcpStubAdapter : ISessionAdapter
{
    public string Name => "acp-stub";

    public SessionWire Wire => SessionWire.Acp;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = command is { Count: > 0 }
            ? command
            : throw new DriverException(
                "the acp-stub adapter needs a command — name one in driver.json: "
                + """{ "commands": { "acp-stub": ["node", "path/to/acp-agent.mjs"] } }""");

        return Spawning.InRoot(target, resolved[0], resolved.Skip(1), redirectInput: true);
    }
}

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
    /// D37 in Claude Code's vocabulary — the same posture the pipe door passes as
    /// <c>--permission-mode acceptEdits</c>, observed on this wire in the evaluation's §1a. Never
    /// <c>bypassPermissions</c>, however available the wire makes it.
    /// </summary>
    public string? AcpPosture => "acceptEdits";

    /// <summary>
    /// The adapter takes turns on its own wire, which is all this seam asks of an interactive
    /// harness — a conversation over ACP is the same session entity by a different door (D49 §3).
    /// </summary>
    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.InRoot(target, resolved[0], resolved.Skip(1), redirectInput: true);
    }

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
    {
        // A chat already redirects stdin — it is the person's channel over the pipe door, and the
        // driver's frames over this one.
        var resolved = Resolve(command);
        return Spawning.ChatInRoot(target, resolved[0], resolved.Skip(1));
    }

    /// <summary>
    /// The adapter's own mechanisms. Pinned EXACT by default (D53's note on a harness that moved
    /// 0.79 in the week it was evaluated): a protocol door whose adapter changes under a running
    /// loop is the moving target D49 §4 already refuses for harnesses.
    /// </summary>
    public HarnessToolchain? Toolchain => new(
        Product: "Claude Code",
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
        TrustFile: ClaudeTrust.FileName);

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : ["claude-agent-acp"];
}

/// <summary>
/// <b>dsh over the protocol door</b> (ACP3/D53): a configuration of the door, not a second seam.
/// </summary>
/// <remarks>
/// <para><b>A profile IS a home here.</b> <c>dsh --profile &lt;name&gt;</c> boots a directory under
/// <c>$DSH_HOME/profiles</c>, so the one variable isolates credentials, settings and sessions
/// together — which is why <c>DSH_HOME</c> is the account seam and why what Daoris writes for this
/// harness goes inside a directory it created rather than beside one it did not.</para>
///
/// <para>🔴 <b>Its wire carries no posture.</b> Observed at 0.1.6-alpha.2: <c>session/new</c> answers
/// with <c>sessionId</c> and <c>configOptions</c> and no <c>modes</c> key — and the one config option
/// is the model catalogue, which D24 forbids Daoris to touch. So this door offers exactly one knob
/// and it is the one that must not be turned. The posture is set in the environment instead, below.</para>
///
/// <para><b>No login question</b> (SES3): dsh has no account to be logged out of, and only a definite
/// *out* refuses — so `unknown` is permissive and a session starts.</para>
///
/// <para><b>No model is named</b> (D24): which model answers is the profile's own `settings.yaml`.</para>
/// </remarks>
public sealed class DshAdapter : ISessionAdapter
{
    public string Name => "dsh";

    public SessionWire Wire => SessionWire.Acp;

    /// <summary>
    /// The profile dsh boots for the automation surface. Its ACP server is <b>automation-only by
    /// their own decision</b> — they removed it as an editor UI — which is exactly the half Daoris
    /// wants and none of the product half D53 declined.
    /// </summary>
    public const string Profile = "acp";

    /// <summary>
    /// dsh's permission posture, as an environment variable read at boot.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>`workspace-write` is D37 in dsh's vocabulary</b>, read from its own shipped bundle: it
    /// feeds both the sandbox policy's mode and the approval policy, which derives to <c>ask</c> for
    /// every value except <c>danger-full-access</c>, where it becomes <c>never</c>. Writes inside the
    /// workspace are sandbox-legal and proceed without asking; anything escalating past the sandbox
    /// asks, which over ACP arrives as <c>session/request_permission</c> and is refused by
    /// construction (D52). Failing closed on escalation is the behaviour, not a limitation.
    ///
    /// <para>It is stated even though it is <b>also dsh's default</b>. A posture that happens to match
    /// somebody else's default is not one Daoris has set, and the default is theirs to change.</para>
    /// </remarks>
    public const string PermissionVariable = "DSH_PERMISSION_MODE";

    /// <summary>The value that expresses D37 here. Never <c>danger-full-access</c>, which is approvals off.</summary>
    public const string Posture = "workspace-write";

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        var info = Spawning.InRoot(
            target, resolved[0], resolved.Skip(1).Concat(["--profile", Profile]), redirectInput: true);

        // Set HERE rather than in the driver's one line, because this posture is genuinely this
        // harness's own mechanism — the other two protocol adapters express the same boundary as a
        // mode on the wire, and a driver that applied one mechanism to all three would set nothing
        // for two of them and report success.
        info.Environment[PermissionVariable] = Posture;

        return info;
    }

    /// <summary>
    /// Pinned exact, and <b>vendored nowhere</b>: 561 MB per machine, and a harness's own packaging is
    /// its own problem — but the version the toolchain installs is asserted, not assumed (D53).
    /// </summary>
    public HarnessToolchain? Toolchain => new(
        Product: "dsh",
        Maker: "DeepSeek",
        Binary: ["dsh"],
        // `-V, --version` — verified against the installed CLI, which printed its exact version.
        VersionArguments: ["--version"],
        ProfileVariable: "DSH_HOME",
        Install: ["npm", "install", "-g", "@deepseek-ai/dsh"],
        // No login flow and no login question: there is no account here to be out of.
        Package: "@deepseek-ai/dsh");

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : ["dsh"];
}

/// <summary>
/// <b>Codex over the protocol door</b> (ACP3/D53, closing HARNESS2): the ACP project's Codex adapter,
/// which drives the machine's `codex` exactly as the Claude one drives `claude`.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The posture is `agent`, and it was established rather than guessed</b> — the one thing
/// HARNESS2 said the ACP route does not settle for free. Read from codex-acp@1.12.0's own bundle, the
/// wire offers three modes: <c>read-only</c> (asks for everything, so a driven session stalls on its
/// first edit), <c>agent</c> (workspace-write sandbox, approvals on request) and
/// <c>agent-full-access</c> (<c>dangerFullAccess</c> with approvals <c>never</c>, which is precisely
/// what D37 forbids). There is no third reading.</para>
///
/// <para>🔴 <b>It is stricter than `acceptEdits`, not equivalent.</b> `agent` runs with
/// <c>networkAccess: false</c>, which Claude Code's posture does not — and a session that cannot
/// reach the network fails in ways that look like something else entirely. Recorded here because the
/// surprise belongs next to the constant.</para>
///
/// <para>🔴 <b>`CODEX_HOME` must already exist</b>, where the Claude adapter creates its own config
/// directory. Pointed at a path that is not there, this adapter exits 1 before <c>initialize</c>
/// completes, naming the directory — which is why the toolchain carries <c>ProfileMustExist</c>.</para>
///
/// <para><b>No model is named</b> (D24), and the account belongs to `codex`: this adapter runs the
/// harness and reads the home the harness logged into, so it has no login flow of its own.</para>
/// </remarks>
public sealed class CodexAcpAdapter : ISessionAdapter
{
    public string Name => "codex-acp";

    public SessionWire Wire => SessionWire.Acp;

    public bool Interactive => true;

    /// <summary>D37 in Codex's vocabulary. Never <c>agent-full-access</c>, which is approvals off.</summary>
    public string? AcpPosture => "agent";

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.InRoot(target, resolved[0], resolved.Skip(1), redirectInput: true);
    }

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.ChatInRoot(target, resolved[0], resolved.Skip(1));
    }

    public HarnessToolchain? Toolchain => new(
        Product: "Codex",
        Maker: "OpenAI",
        // 🔴 The BINARY is `codex-acp` — the adapter's own bin, not this adapter's Daoris name and
        // not `codex`. Verified against the installed package's `bin` map, the same check that
        // caught the `claude-agent-acp` guess.
        Binary: ["codex-acp"],
        VersionArguments: ["--version"],
        ProfileVariable: "CODEX_HOME",
        Install: ["npm", "install", "-g", "@agentclientprotocol/codex-acp"],
        Package: "@agentclientprotocol/codex-acp",
        // No login of its own: it runs `codex` and reads the home `codex` logged into.
        AccountOf: "codex",
        ProfileMustExist: true);

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : ["codex-acp"];
}

/// <summary>The seam that points the Agent SDK at a <c>claude</c> Daoris chose (ACP2, §1a).</summary>
public static class ClaudeAcp
{
    /// <summary>The environment variable the ACP adapter's SDK reads to find its CLI.</summary>
    public const string ExecutableVariable = "CLAUDE_CODE_EXECUTABLE";

    /// <summary>
    /// Point this spawn at a managed <c>claude</c>.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Null leaves the variable unset</b>, so the SDK finds its CLI the way it always did.
    /// Setting it to an empty string — or to a path that is not there — would break a machine that
    /// works today, which is the additive rule every part of the toolchain holds (D48 §2a).
    /// </remarks>
    public static void PointAtClaude(ProcessStartInfo info, string? managedClaude)
    {
        if (managedClaude is not { Length: > 0 }) return;

        info.Environment[ExecutableVariable] = managedClaude;
        // 🔴 The SDK runs that `claude` with this process's environment, so the pin's own switch
        // travels here (AGT2) — the same one the pipe door's spawn of the same binary carries.
        foreach (var (name, value) in ClaudeCodeAdapter.StayPinned) info.Environment[name] = value;
    }
}

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

    /// <summary>
    /// The supported harness holds a conversation (D49 §3): run without a one-shot prompt it takes
    /// turns on its own streams, which is all this seam asks of it.
    /// </summary>
    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        var arguments = resolved.Skip(1)
            .Concat(["-p", TargetPrompt.Compose(target), "--permission-mode", "acceptEdits"]);

        return Spawning.InRoot(target, resolved[0], arguments);
    }

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
    /// </remarks>
    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.ChatInRoot(
            target, resolved[0], resolved.Skip(1).Concat(["--permission-mode", "acceptEdits"]));
    }

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
        Maker: "Anthropic",
        Binary: ["claude"],
        VersionArguments: ["--version"],
        ProfileVariable: "CLAUDE_CONFIG_DIR",
        Install: ["npm", "install", "-g", "@anthropic-ai/claude-code"],
        UpdateArguments: ["update"],
        LoginArguments: ["auth", "login"],
        LoginCheck: new LoginQuestion(
            ["auth", "status"],
            LoggedIn: @"""loggedIn""\s*:\s*true",
            LoggedOut: @"""loggedIn""\s*:\s*false",
            Account: @"""email""\s*:\s*""([^""]+)"""),
        // A pin comes from the release bucket, against its SIGNED manifest (AGT2b) — the npm package
        // installs the same native binary, with nothing but npm's own integrity check behind it.
        Channel: ClaudeReleases.Channel,
        // 🔴 Where it records the workspaces a person has accepted (DEPLOY1). Read before every
        // driven spawn, because an untrusted tree makes the repository's own allow-list inert and
        // the session cannot then take or close its quest — nine minutes and a real login, three
        // times over, before this was measured rather than assumed.
        TrustFile: ClaudeTrust.FileName,
        PinnedEnvironment: StayPinned,
        // An account that is an API key (AGT3). Measured on 2.1.280 with an invalid key: `auth
        // status` reads it (api_key, no email) and a `-p` run takes it with no prompt.
        KeyVariable: "ANTHROPIC_API_KEY",
        // What it prints when its provider refuses the credential (AGT3b) — measured on 2.1.280: a
        // `-p` run with an invalid key was silent for 189 s of retries, then printed "Failed to
        // authenticate. API Error: 401 API key is invalid." and exited 1.
        Refused: "API Error: 401");

    /// <summary>
    /// What a pinned <c>claude</c> runs with so it stays the version pinned (AGT2). 🔴 Measured on a
    /// pinned 2.1.270 with no login: its own <c>claude doctor</c> read <i>Auto-updates: enabled</i> and
    /// called a copy in Daoris's folder an npm-global install; with <c>DISABLE_UPDATES=1</c> it read
    /// disabled, refused <c>claude update</c>, and stayed 2.1.270. <c>DISABLE_AUTOUPDATER</c> stops
    /// only the background check, and a pin is every path.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> StayPinned =
        new Dictionary<string, string> { ["DISABLE_UPDATES"] = "1" };

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : ["claude"];
}

/// <summary>
/// The adapters that exist, by name. D23 one layer up: `claude-code` first and supported, `codex`
/// second and explicit — and an unknown name is an error naming what exists, never a silent fallback,
/// because a driver that quietly spawned a different harness than the person configured is the same
/// failure as a repository that asked for one layout and received another.
/// </summary>
public sealed class AdapterSet(IReadOnlyDictionary<string, ISessionAdapter> adapters)
{
    /// <summary>Every adapter this build has, in a stable order — what a roster enumerates.</summary>
    public IReadOnlyList<string> Names => [.. adapters.Keys.OrderBy(k => k, StringComparer.Ordinal)];

    /// <summary>The plugin a harness came from (D64), or null for one this build carries.</summary>
    public string? DeclaredBy(string name) =>
        adapters.TryGetValue(name, out var adapter) && adapter is DeclaredAcpAdapter declared
            ? declared.Plugin
            : null;

    /// <summary>
    /// This set plus every harness the catalogue's contributing plugins declare (D64 §3). The
    /// catalogue has already refused any name this set carries, so nothing here can be replaced —
    /// a plugin adds, and the built-in set is exactly what it was.
    /// </summary>
    public AdapterSet WithPlugins(PluginCatalog catalog)
    {
        var joined = new Dictionary<string, ISessionAdapter>(adapters, StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in catalog.Contributing)
        {
            foreach (var harness in plugin.Manifest.Harnesses)
            {
                joined.TryAdd(harness.Name, new DeclaredAcpAdapter(harness, plugin.Manifest.Id));
            }
        }

        return new AdapterSet(joined);
    }

    public ISessionAdapter Resolve(string name)
    {
        if (adapters.TryGetValue(name, out var adapter)) return adapter;

        throw new DriverException(
            $"unknown adapter '{name}' — one of: {string.Join(", ", adapters.Keys.OrderBy(k => k, StringComparer.Ordinal))}. "
            + "An adapter is added deliberately, never guessed.");
    }

    public static AdapterSet Built() => new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        // The stub gate-verifies the mechanism with no model in it (D46 §8); `claude-code` is the
        // supported harness on top of that proven loop. `codex` arrives explicitly, later — and until
        // it does, asking for it errors naming these three, which is the honest answer.
        ["stub"] = new StubAdapter(),
        // The same trick one door over (D53/ACP1): it proves the PROTOCOL with no model in it, and a
        // real harness rides that proven door in ACP2 rather than being the thing that proves it.
        ["acp-stub"] = new AcpStubAdapter(),
        ["claude-code"] = new ClaudeCodeAdapter(),
        // The supported harness over the protocol door (ACP2/D53). It arrives BESIDE the pipe door
        // rather than replacing it: D23's "on proof" means a real driven run over ACP, and until
        // that has happened `claude-code` remains what a machine drives with unless it says otherwise.
        ["claude-code-acp"] = new ClaudeAcpAdapter(),
        // Two more CONFIGURATIONS of the same door (ACP3/D53) — not two more seams, which is the
        // whole argument for adopting a protocol rather than a product. Each states its own posture
        // in its own vocabulary, and each arrives beside what was already here.
        ["dsh"] = new DshAdapter(),
        ["codex-acp"] = new CodexAcpAdapter(),
    });
}
