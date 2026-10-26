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
/// <param name="MachineDefault">
/// Where its starts begin: the order's first of the agent's set (POOL1a2, the account-pool design §2.2), where the machine's
/// default was; none where the agent has no account of Daoris's.
/// </param>
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
