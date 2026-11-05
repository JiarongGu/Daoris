using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The stub: runs whatever command the configuration names, in the repository root, with the target
/// in the environment. A test double with real mechanics — real spawn, real cwd, real delivery, real
/// exit — which is what lets the family rehearsal gate the whole loop with no model in it (D46 §8).
/// </summary>
public sealed class StubAdapter : ISessionAdapter
{
    public string Name => "stub";

    /// <summary>The stub's configuration-home variable: its own, and its protocol twin's, which runs as its accounts (TOOL4j).</summary>
    public const string ConfigVariable = "DAORIS_STUB_CONFIG_DIR";

    /// <summary>
    /// Interactive, so the whole conversation loop can be gated with no model in it (D46 §8's argument,
    /// one layer on): a scripted exchange is a real spawn, a real stdin, a real stream and a real exit.
    /// </summary>
    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
        Shell.Start(ProcessRecipe.Of(Command(command)), SpawnRequests.Of(target, command));

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
        Shell.Chat(ProcessRecipe.Of(Command(command)), SpawnRequests.Of(target, command));

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
        ProfileVariable: ConfigVariable,
        // A login flow too, so signing in to another account (D66 §3) can be gated with no account:
        // whatever the configured command does with `--login` is the stub's sign-in.
        LoginArguments: ["--login"],
        LoginCheck: new LoginQuestion(
            ["--login-state"], LoggedIn: @"logged-in", LoggedOut: @"logged-out",
            Account: @"logged-in as (\S+)",
            // And its way to say an organisation and a plan (ACCOUNTID1), each on a line of its own after who, so one email
            // can be signed in to two organisations with no account behind either.
            Org: @"(?m)^org:[ \t]*(.+)$",
            Plan: @"(?m)^plan:[ \t]*(.+)$"),
        // Claude Code's own words for a refused credential (AGT3b), mirrored so the rehearsal can
        // gate a refused account with no account behind it. The stub's door carries only text, so they
        // are read from its transcript beside an exit that was not 0 (AGT3c).
        Refused: "API Error: 401",
        // And its words for an account's limit (TOOL4a, D125 §1.3 rule 4), mirrored for the same reason.
        Limits: ClaudeLimits.Words,
        // And its weekly reset fixed per account (TOOL6b), mirrored so a rehearsal can gate a week carried on.
        WeekFixed: true,
        // And its words for an account's windows (TOOL6c), mirrored so a frame replayed on its doors is read as Claude Code's.
        Windows: ClaudeWindows.Words,
        // And its words for a start refused for its sign-in (ROSTER1b), mirrored so a tick can gate one with no account.
        SignIn: ClaudeSignIn.Words,
        // And its provider's codes for an error's kind (AGENTERRORS1a), mirrored so a stand-in can gate a retry with no model: the kind
        // words and statuses alone, so a stand-in's failure that names no code (TURNFAIL1's) keeps today's end. No -32000 declaration:
        // the protocol stub answers every failed turn with it.
        ErrorWords: ClaudeErrors.Codes,
        // Its own home is nobody's, so a start that names no account still runs there (OWNSPACE1a): the rehearsal's no-account
        // starts keep running as they did.
        RunsInOwnHome: true);

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

    /// <summary>
    /// Interactive, so a conversation on this door can be gated with no model in it (CONV3b) — the
    /// argument that made <see cref="StubAdapter"/> interactive, one door over.
    /// </summary>
    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
        Shell.Start(ProcessRecipe.Of(Resolve(command)) with { Input = true }, SpawnRequests.Of(target, command));

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
        Shell.Chat(ProcessRecipe.Of(Resolve(command)), SpawnRequests.Of(target, command));

    /// <summary>
    /// A door onto the stub's accounts (TOOL4j, D125 §1.3 rule 4), as Claude Code's protocol door is onto Claude Code's
    /// (AGT7): the stub's directories through the stub's variable, the stub's default and order, and the stub's words for
    /// a limit, so a limit on this door cools the stub account it ran as and the carry-on rotates over the stub's order.
    /// </summary>
    /// <remarks>
    /// Present by a file look (<see cref="HarnessToolchain.ProbeByPresence"/>): the agent a rehearsal names here waits on
    /// its stdin for frames, so asking it a version would stall the probe. No binary of its own, as the stub has none, so
    /// a machine that names no command for it lists no such door. No sign-in, login question or limit table of its own:
    /// <c>AccountOf</c> says each is the stub's.
    /// </remarks>
    public HarnessToolchain? Toolchain => new(
        Binary: [],
        VersionArguments: [],
        ProfileVariable: StubAdapter.ConfigVariable,
        AccountOf: "stub",
        ProbeByPresence: true,
        RunsInOwnHome: true);

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 }
            ? command
            : throw new DriverException(
                "the acp-stub adapter needs a command — name one in driver.json: "
                + """{ "commands": { "acp-stub": ["node", "path/to/acp-agent.mjs"] } }""");
}
