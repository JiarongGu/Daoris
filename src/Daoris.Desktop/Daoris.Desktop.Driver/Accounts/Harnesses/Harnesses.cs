using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>Whether a profile has been logged into — as the harness itself answers it.</summary>
public enum LoginState
{
    /// <summary>
    /// Nobody could say. The harness has no way to be asked, or answered in a shape this build does
    /// not read. <b>Permissive at spawn</b>, deliberately: refusing work because a tool reworded its
    /// own status line would be a worse failure than letting the harness refuse for itself.
    /// </summary>
    Unknown,

    /// <summary>The harness says this configuration home is logged in.</summary>
    In,

    /// <summary>The harness says it is not. This is the one state that refuses a spawn.</summary>
    Out,
}

/// <summary>
/// What one status answer says of a home (D66 §3, ACCOUNTID1): signed in or not, and on a yes who, with the organisation
/// and the plan the agent names beside them, each null where it says none. Read fresh and written nowhere.
/// </summary>
public readonly record struct LoginRead(LoginState Login, string? Account, string? Org = null, string? Plan = null);

/// <summary>
/// How a harness answers "is this configuration home logged in?" — the arguments to ask with, and the
/// two shapes its answer takes.
/// </summary>
/// <remarks>
/// <para><b>Patterns, not exit codes.</b> Both supported harnesses exit 0 whether or not they are
/// logged in, so the exit code says nothing and the OUTPUT is the answer. Verified against the real
/// binaries before this was written, because every clause of it is a claim about somebody else's
/// tool.</para>
///
/// <para><b>Neither pattern matching means <see cref="LoginState.Unknown"/></b>, never logged-out: an
/// answer this build cannot read is not evidence.</para>
/// </remarks>
/// <param name="Arguments">What to run, under the profile's configuration home.</param>
/// <param name="LoggedIn">A pattern whose presence means yes.</param>
/// <param name="LoggedOut">A pattern whose presence means no.</param>
/// <param name="Account">
/// A pattern whose first group is <b>who</b> is signed in there, read from the same answer (D66 §3):
/// an account is named by who signed in rather than by a name typed before anyone knew. Taken only on
/// a yes. Null asks nobody's name.
/// </param>
/// <param name="Org">
/// A pattern whose first group is the organisation the sign-in is in, read from the same answer and only on a yes
/// (ACCOUNTID1): one email may sign in to two organisations, and this is what tells the two accounts apart. Read fresh,
/// held in memory, written nowhere (D66's ACCOUNTID1 note). Null asks none.
/// </param>
/// <param name="Plan">The plan the sign-in is on, read as <paramref name="Org"/> is (ACCOUNTID1). Null asks none.</param>
public sealed record LoginQuestion(
    IReadOnlyList<string> Arguments, string LoggedIn, string LoggedOut, string? Account = null,
    string? Org = null, string? Plan = null)
{
    /// <summary>
    /// What one answer says (CODEXACCT1, read in one place so a table can hold it): yes where <see cref="LoggedIn"/> matches,
    /// with who where <see cref="Account"/> reads one; else no where <see cref="LoggedOut"/> matches; else unknown. Matched
    /// without case, the yes first, since it is the specific one.
    /// </summary>
    public LoginRead Read(string output) =>
        Matches(output, LoggedIn) ? new LoginRead(LoginState.In, Who(output, Account), Who(output, Org), Who(output, Plan))
        : new LoginRead(Matches(output, LoggedOut) ? LoginState.Out : LoginState.Unknown, null);

    /// <summary>The first group of one of the answer's patterns, trimmed — or null when there is none to read.</summary>
    private static string? Who(string output, string? pattern)
    {
        if (pattern is null) return null;
        try
        {
            var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            return match.Success && match.Groups.Count > 1 && match.Groups[1].Value.Trim() is { Length: > 0 } who
                ? who
                : null;
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private static bool Matches(string output, string pattern)
    {
        try
        {
            return Regex.IsMatch(output, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException)
        {
            // A pattern this build cannot run is no evidence, exactly like an answer it cannot read.
            return false;
        }
    }
}

/// <summary>
/// What one of a harness's own commands runs as (D57 rule 4; <see cref="HarnessProbe.CommandOf"/>): the command and its
/// arguments' head, the pin's executable where it came from a pin, and the version pinned.
/// </summary>
/// <param name="Harness">Whose command: the name its pin and its <c>driver.json</c> command are kept under.</param>
/// <param name="Run">The binary, then any arguments it is declared with; empty where <see cref="Missing"/>.</param>
/// <param name="Managed">The pinned executable, or null for a named command or <c>PATH</c>'s.</param>
/// <param name="Pinned">The version this machine pins, or null.</param>
public sealed record HarnessCommand(string Harness, IReadOnlyList<string> Run, string? Managed, string? Pinned)
{
    /// <summary>Pinned with nothing installed at the pin: nobody to run, and never a fall back to <c>PATH</c>.</summary>
    public bool Missing => Pinned is not null && Managed is null;
}

/// <summary>
/// What Daoris knows about a harness AS A TOOL (D49 §4) — where its binary is, how to ask its version,
/// which environment variable names its configuration home, and how to run its OWN install, update and
/// login flows.
/// </summary>
/// <remarks>
/// <para><b>Daoris manages directories and names, and a sign-in stays the tool's.</b> Nothing here
/// reads a credential; <see cref="LoginArguments"/> runs the harness's own flow INTO a profile
/// directory, so whatever it stores stays in its own store, under the user's OS account, exactly where
/// it lives without Daoris. The one secret Daoris keeps is an API key a person gives it
/// (<see cref="HarnessKeys"/>, D67 §1).</para>
///
/// <para><b>Install and update are the harness's own mechanism</b>, never a download Daoris invents:
/// a tool that installed its dependencies by a route their authors did not publish is a tool nobody
/// can support. They are person-actions and stay out of every gate.</para>
///
/// <para>A harness with no <see cref="ProfileVariable"/> supports no profiles, and asking for one on
/// it is refused rather than silently ignored — a spawn that quietly ran as the wrong account is the
/// failure this whole feature exists to prevent.</para>
/// </remarks>
/// <param name="Binary">The default command, when the driver config names none.</param>
/// <param name="VersionArguments">How to ask the version. Recorded at spawn, so "which tool produced this" is answerable.</param>
/// <param name="ProfileVariable">The environment seam: the variable naming this harness's configuration home.</param>
/// <param name="Install">
/// A WHOLE command, because the harness may not exist yet — installing one that is absent cannot be
/// arguments to a binary that is not there. It is usually that harness's package manager.
/// </param>
/// <param name="UpdateArguments">
/// Arguments TO the binary: only a harness that is present can update itself, and a present harness
/// updates through its own mechanism rather than through a second copy of its installer.
/// </param>
/// <param name="LoginArguments">
/// Arguments TO the binary, run with the profile's configuration home in the environment. It must be
/// the harness itself, because the credential it obtains belongs in the harness's own store.
/// </param>
/// <param name="LoginCheck">How to ask a profile's login state. Null leaves every profile <see cref="LoginState.Unknown"/>.</param>
/// <param name="AccountOf">
/// The harness whose ACCOUNT this one runs as, when it has none of its own (ACP2). 🔴 Declared, so
/// that a missing login flow is a fact rather than a gap — an unanswerable login question is
/// PERMISSIVE (SES3), so an omission would quietly widen what may spawn.
/// </param>
/// <param name="Package">
/// The package a managed install fetches, when Daoris owns the binary (TOOL2/D57). Declared rather
/// than parsed out of <paramref name="Install"/>, because the two are different questions: one is
/// "how does this tool put itself on a machine", the other is "what do I fetch into a directory I
/// own". A harness that declares none cannot be pinned, and says so.
/// </param>
/// <param name="TrustFile">
/// The file, beside this harness's configuration home, in which it records the workspaces a person
/// has accepted — for a harness that has such a notion (DEPLOY1). Null is "no notion of trust", and
/// that is most of them. It is READ and never written: the flag is the person's grant.
/// </param>
/// <param name="ProfileMustExist">
/// Whether this harness demands its profile directory already be there (ACP3). 🔴 Observed, not
/// assumed, and the two adapters disagree: <c>codex-acp</c> exits 1 before <c>initialize</c>
/// completes when <c>CODEX_HOME</c> names a path that is not there, while the Claude adapter
/// <b>creates</b> <c>CLAUDE_CONFIG_DIR</c> and populates it. Default false, so a harness that says
/// nothing behaves as every harness did before the field existed.
/// </param>
public sealed record HarnessToolchain(
    IReadOnlyList<string> Binary,
    IReadOnlyList<string> VersionArguments,
    string? ProfileVariable = null,
    IReadOnlyList<string>? Install = null,
    IReadOnlyList<string>? UpdateArguments = null,
    IReadOnlyList<string>? LoginArguments = null,
    LoginQuestion? LoginCheck = null,
    string? Package = null,
    string? AccountOf = null,
    bool ProfileMustExist = false,
    string? TrustFile = null,
    // A harness with no version question (a plugin-declared one, D64) is asked whether it is THERE
    // rather than run: an ACP agent started with no arguments waits on its stdin, and twenty seconds
    // of that per roster refresh is not a probe, it is a stall.
    bool ProbeByPresence = false,
    // What a PINNED binary runs with so it stays the version pinned (AGT2) — the tool's own switch,
    // measured rather than assumed, and applied to a managed binary only: a binary off PATH is the
    // machine's, and its updates are the machine's business (D48 §2a). Null declares none.
    IReadOnlyDictionary<string, string>? PinnedEnvironment = null,
    // What a person calls the tool this runs, and who makes it (AGT1): `dsh` meant nothing to the
    // owner until it said whose. A door onto a tool names that tool, not itself. Null for one that
    // declares neither — a plugin's, until it says.
    string? Product = null,
    string? Maker = null,
    // The tool's own variable for an API key (AGT3, D67 §1) — what an account that is a key is
    // handed at spawn. Declared only where measured; null means this agent takes no key from Daoris.
    string? KeyVariable = null,
    // The words this tool prints when its provider refuses the account's credential (AGT3b) — read
    // from what it said of a failed session's ending, never from its agent's words (AGT3c,
    // `Observation.Refused`), so the account is not spent again. Measured, never guessed.
    string? Refused = null,
    // The maker's OWN release channel a pin fetches from (AGT2b), verified end to end — never npm.
    // A toolchain declares this or `Package`, never both: which source a pin came from is not left
    // for anyone to work out. The CLI's `channel` is the twin.
    string? Channel = null,
    // The tool's own settings file in an account's configuration home, where the account's model and
    // effort live (AGT6, D98) — declared only where its keys were read from the tool itself. Null means
    // Daoris does not know this tool's settings and offers none. The CLI's `settingsFile` is the twin.
    string? SettingsFile = null,
    // The words this tool says an account's limit in (TOOL4a, D125 §1.3), read from the door's failure
    // by `AccountLimits.Read` and never from the transcript. Declared only by a tool seen hitting one,
    // each pattern standing on a recorded sentence. A door onto another agent reads its owner's (AGT7).
    // Null reads every failure as a failure. Not a twin: the CLI concludes no session.
    LimitWords? Limits = null,
    // Whether this tool's maker fixes an account's weekly reset at one time each week (TOOL6b, D130 §16.3 step 4): a weekly
    // reset a limit told is then carried a week at a time, where otherwise it is dropped at its reset. Declared only where
    // the maker's own page says so; not in `Limits`, whose entries grow only with a recorded sentence. A door onto another
    // agent reads its owner's (AGT7).
    bool WeekFixed = false,
    // How this tool says an account's windows (TOOL6c, D130 §5.2): its entry in the readings table, read by
    // `AccountReadings.Read` from the frame its door carries apart from its words. Declared only where a frame was recorded;
    // a door onto another agent reads its owner's (AGT7). Null says nothing, so near and pace stand aside (§5.3).
    WindowWords? Windows = null,
    // The words this tool refuses a start for its sign-in in (ROSTER1b, D150 §5.3), read from the door's failure by
    // `SignInRefusals.Read` and never from the transcript, as `Limits` is. Declared only by a tool seen refusing one, each
    // pattern standing on a recorded sentence. A door onto another agent reads its owner's (AGT7). Null reads every failure
    // as a failure. Not a twin: the CLI concludes no session.
    SignInWords? SignIn = null,
    // How this tool is asked an account's windows where its door carries none (CODEXUSE1): its own server under the account's
    // home, read into the same readings `Windows` fills and kept in `windows.json`. Declared only where an answer was recorded;
    // a door onto another agent reads its owner's (AGT7). Null asks nothing. Not a twin: the CLI asks no server, and reads
    // what this kept from `windows.json`.
    UsageQuestion? Usage = null,
    // The two-letter mark this tool wears on the page's strip (AGENTMARK1, D152): "CC" for Claude Code, "Cx" for Codex, so
    // two doors onto different tools never share one. Declared by the harness, never derived from its name; null declares
    // none and the page derives its own. Page-only: the CLI prints no strip, so there is no twin.
    string? Mark = null,
    // Whether a start that names no account may run in this tool's own home (OWNSPACE1a, D175's OWNSPACE1a note): true only
    // for a test double whose own home is nobody's, the stub, its protocol twin and a test's fake. Every real agent leaves it
    // false, since its own home is the person's and never Daoris's working space (D175 point 1). Not a twin: the CLI starts
    // no session.
    bool RunsInOwnHome = false,
    // What this door's turn counts mean (USAGECAP1a2, D179 point 2), the usage history's `meaning`: `per-turn` where a probe
    // measured each turn's own counts. A report's meaning is its wire's, so a door onto another agent declares its own and never
    // reads its owner's; null says it was never measured, which the history keeps as `unknown`. Not a twin: the CLI keeps no turn.
    string? TurnReport = null)
{
    /// <summary>The command this harness actually runs as: the machine's configured one, or the declared one.</summary>
    public IReadOnlyList<string> Command(IReadOnlyList<string>? configured) =>
        configured is { Count: > 0 } ? configured : Binary;

    /// <summary>
    /// Whether a start that names no account runs in the tool's own home (OWNSPACE1a, D175 point 1): a test double's, or a tool
    /// with no configuration-home variable, which no account of Daoris's can be handed to. Otherwise it takes a Daoris account.
    /// </summary>
    public bool StartsInOwnHome => RunsInOwnHome || ProfileVariable is not { Length: > 0 };

    /// <summary>
    /// Whose accounts this runs as (AGT7): <see cref="AccountOf"/> for a door onto another agent,
    /// else itself. Accounts, their defaults and their keys live under this name; a pin does not.
    /// </summary>
    public string Owner(string name) => AccountOf is { Length: > 0 } owner ? owner : name;
}

/// <summary>
/// One profile as it stands: its name, the directory Daoris owns the location of, and what the harness
/// says about logging in there. <b>Never anything from inside it.</b>
/// </summary>
/// <param name="Account">
/// Who the harness says is signed in there (D66 §3) — the name a person knows the account by, read
/// fresh on every probe and written nowhere. Null when signed out, or when the tool does not say.
/// </param>
/// <param name="Key">
/// For an account that is an API key (AGT3), the key's handle — its last four characters. Never
/// the key. Null for a sign-in.
/// </param>
/// <param name="Read">
/// When <paramref name="Login"/> was read (ROSTER1, D150 §5.3), or null where it never was: absent is never a reading. An
/// unknown that was read is a read that failed.
/// </param>
/// <param name="DisplayName">
/// The name the person gave the account (ACCT2, <see cref="AccountNames"/>), or null where they gave none and it reads as
/// its id, <paramref name="Name"/>, which names its folder, its readings and its place in every list.
/// </param>
/// <param name="Org">
/// The organisation the tool says the sign-in is in (ACCOUNTID1), read with <paramref name="Account"/> and kept as it is:
/// fresh, in memory, written nowhere. What tells two sign-ins of one email apart. Null where the tool does not say.
/// </param>
/// <param name="Plan">The plan the tool says the sign-in is on (ACCOUNTID1), as <paramref name="Org"/>.</param>
public sealed record ProfileReport(
    string Name, string Home, LoginState Login, string? Account = null, string? Key = null, DateTimeOffset? Read = null,
    string? DisplayName = null, string? Org = null, string? Plan = null);

/// <summary>
/// One harness as this machine has it (D49 §4): present or absent, its version, its profiles.
/// </summary>
/// <param name="Problem">Why it could not be probed — the sentence a person acts on, when absent.</param>
/// <param name="OwnLogin">
/// What the harness says about logging in to its OWN configuration home — the account a person
/// actually has before naming any profile, asked the same read-only way. Unknown when absent.
/// 🔴 The roster called a machine with no named profile "No accounts" while its owner was logged in.
/// </param>
/// <param name="OwnAccount">Who is signed in to the tool's own home, asked the way a profile is.</param>
/// <param name="OwnRead">When <paramref name="OwnLogin"/> was read (ROSTER1): only at a person's press (TOOL6g), so null before one.</param>
/// <param name="OwnOrg">The organisation of the tool's own sign-in (ACCOUNTID1), as a profile's <see cref="ProfileReport.Org"/>.</param>
/// <param name="OwnPlan">The plan of the tool's own sign-in (ACCOUNTID1), as a profile's <see cref="ProfileReport.Plan"/>.</param>
public sealed record HarnessReport(
    string Adapter,
    bool Present,
    string? Version,
    string? Problem,
    string? ProfileVariable,
    string? MachineDefault,
    IReadOnlyList<ProfileReport> Profiles,
    LoginState OwnLogin = LoginState.Unknown,
    string? OwnAccount = null,
    DateTimeOffset? OwnRead = null,
    string? OwnOrg = null,
    string? OwnPlan = null)
{
    /// <summary>
    /// The folders beside its accounts that are no account (HELPSTUCK1, <see cref="HarnessSettings.Strays"/>), each with whether
    /// it holds a sign-in, so one is said rather than lost. Carried for Ask Daoris's room; no wire answer carries it yet.
    /// </summary>
    public IReadOnlyList<StrayFolder> Strays { get; init; } = [];
}

/// <summary>
/// A folder beside an agent's accounts that is no account (HELPSTUCK1): a lock's or a hidden one, never listed, walked or
/// touched as one.
/// </summary>
/// <param name="Name">Its folder's name.</param>
/// <param name="SignedIn">
/// Whether it holds a sign-in by what Daoris kept of it while it was listed as an account: a reading that said signed in, a
/// sign-in's or a key's mark, or a key. Such a folder is a sign-in the person would lose by its removal, so it is said with the
/// way to keep it: renamed to an account's name while nothing runs on it.
/// </param>
public sealed record StrayFolder(string Name, bool SignedIn);
