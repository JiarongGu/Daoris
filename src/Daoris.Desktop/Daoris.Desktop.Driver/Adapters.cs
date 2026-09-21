using System.Diagnostics;

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
    string ServiceUrl);

/// <summary>
/// The claiming instruction (D46 §3), composed once and delivered per harness. Project-agnostic on
/// purpose: it travels to repositories that know nothing of this one's decision numbering, so it
/// speaks in the canon's names, never in D-numbers.
/// </summary>
public static class TargetPrompt
{
    public static string Compose(SessionTarget target) =>
        $"""
        You are the agent for `{target.Repository}`, working inside its own repository and nowhere else.

        Your target is quest `#{target.QuestId}`, asked by `{target.Asker}`:

        # {target.Title}

        {target.Body}

        First take the quest (respond to `#{target.QuestId}` with `take`), then do the work inside this
        repository under its own doctrine and gates, then close it: `done` when it has landed, or
        `decline` with the reason — the reason is the part the asker can act on. If the quest is already
        taken or closed, stand down and finish without changing anything.

        Never write outside this repository. Work another repository needs is a quest published to it,
        never an edit — that is the rule the whole arrangement rests on. Anything that cannot be taken
        back or that leaves the repository — a push, a publish, a release — is not yours to do; surface
        it and finish.
        """;
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

    /// <summary>The process that would be a CHAT: the same spawn, with stdin open.</summary>
    ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
        throw new DriverException(
            $"the `{Name}` adapter cannot hold a conversation — it spawns a harness that takes its "
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

        info.Environment["DAORIS_QUEST_ID"] = target.QuestId;
        info.Environment["DAORIS_QUEST_TITLE"] = target.Title;
        info.Environment["DAORIS_QUEST_BODY"] = target.Body;
        info.Environment["DAORIS_QUEST_ASKER"] = target.Asker;
        info.Environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);

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
        LoginCheck: new LoginQuestion(
            ["--login-state"], LoggedIn: @"logged-in", LoggedOut: @"logged-out"));

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
    /// an email, an organisation and a subscription tier; Daoris reads the boolean and keeps nothing
    /// else. That is `Daoris manages directories and names, never secrets` meeting a harness that
    /// offers more than it was asked for.</para>
    ///
    /// <para><c>CLAUDE_CONFIG_DIR</c> is the environment seam: a spawn under it is genuinely a separate
    /// account — a fresh directory reports logged out while the machine's own home reports logged in.</para>
    /// </remarks>
    public HarnessToolchain? Toolchain => new(
        Binary: ["claude"],
        VersionArguments: ["--version"],
        ProfileVariable: "CLAUDE_CONFIG_DIR",
        Install: ["npm", "install", "-g", "@anthropic-ai/claude-code"],
        UpdateArguments: ["update"],
        LoginArguments: ["auth", "login"],
        LoginCheck: new LoginQuestion(
            ["auth", "status"],
            LoggedIn: @"""loggedIn""\s*:\s*true",
            LoggedOut: @"""loggedIn""\s*:\s*false"),
        Package: "@anthropic-ai/claude-code");

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
    });
}
