namespace Daoris.Driver;

// The parts of the provider contract (AGENTPROVIDER1 row a; the agent-provider design §2.3), each a plain record of declared data. A
// part whose port arrives in a later row is a `PortRef` stated absent: row a changes nothing that runs, so it states what today's
// toolchains declare and nothing it would have to invent.

/// <summary>
/// The name of a port a later row declares (the design §11), held in a part until then. A part stated absent reads as today's
/// behaviour for an agent that says nothing (D53).
/// </summary>
public sealed record PortRef(string Name);

/// <summary>Whether an agent may be asked to work in any repository, or only those opened to it (the design §2.3, OPENROUTER1's).</summary>
public enum Reach
{
    Everywhere,
    Opened,
}

/// <summary>
/// A provider's identity: its product, maker and mark as it declares them, whether it runs no model (<c>StandIn</c>), and the plugins
/// that declared it (a provider made from declarations). Never the commit attribution, which stays <c>CommitTrailers</c>' table keyed
/// by maker (O2-F9).
/// </summary>
public sealed record ProviderIdentity(string? Product, string? Maker, string? Mark, bool StandIn, IReadOnlyList<string> DeclaredBy);

/// <summary>
/// A door's identity: its plugin (set by the source, never a manifest) and its product and maker as its declaration says them, each
/// absent where it declares none and never read from the provider (C1-2.2, O1-F14). A built-in door states none: its provider's.
/// </summary>
public sealed record DoorIdentity(string? DeclaredBy, string? Product, string? Maker);

/// <summary>A program Daoris manages: its binary, how to ask its version, install and update it, and where its release comes from.</summary>
public sealed record ProgramFacts(
    IReadOnlyList<string> Binary,
    IReadOnlyList<string> VersionArguments,
    IReadOnlyList<string>? Install,
    IReadOnlyList<string>? UpdateArguments,
    string? Package,
    string? Channel,
    IReadOnlyDictionary<string, string>? PinnedEnvironment,
    bool ProbeByPresence)
{
    public static ProgramFacts Of(HarnessToolchain toolchain) => new(
        toolchain.Binary, toolchain.VersionArguments, toolchain.Install, toolchain.UpdateArguments, toolchain.Package,
        toolchain.Channel, toolchain.PinnedEnvironment, toolchain.ProbeByPresence);
}

/// <summary>
/// The facts of an account pool a tool declares: the variable an API key is handed in, the login question and the sign-in (its
/// arguments, its device code and its ports), and the settings file. Never the home variable a spawn sets, whether a start runs in
/// its own home, or the trust file, each a door's <see cref="DoorHome"/> (O2-F2).
/// </summary>
public sealed record PoolFacts(
    string? KeyVariable,
    LoginQuestion? Login,
    IReadOnlyList<string>? LoginArguments,
    IReadOnlyList<string>? DeviceLoginArguments,
    IReadOnlyList<int>? LoginPorts,
    string? SettingsFile,
    bool WeekFixed)
{
    public static PoolFacts None { get; } = new(null, null, null, null, null, null, false);

    public static PoolFacts Of(HarnessToolchain toolchain) => new(
        toolchain.KeyVariable, toolchain.LoginCheck, toolchain.LoginArguments, toolchain.DeviceLoginArguments, toolchain.LoginPorts,
        toolchain.SettingsFile, toolchain.WeekFixed);
}

/// <summary>
/// An agent's account pool: its <see cref="Key"/> (the string its account records hold), and its facts. For a provider made for a
/// plugin's <c>accountOf</c> naming a door, <see cref="References"/> names that door and the facts are its own declarations, one
/// level (C3-2.1, O3-F2).
/// </summary>
public sealed record AccountsPart(AccountKey Key, DoorId? References, PoolFacts Facts);

/// <summary>
/// The words an agent says things in, each standing on a recorded sentence: its limit, window, sign-in and refused words, and its
/// error words (kinds, statuses, transient sentences). A table not declared is null and reads as today's fallback (D210).
/// </summary>
public sealed record TablesPart(LimitWords? Limits, WindowWords? Windows, SignInWords? SignIn, string? Refused, ErrorWords? ErrorWords)
{
    public static TablesPart None { get; } = new(null, null, null, null, null);

    public static TablesPart Of(HarnessToolchain toolchain, bool withErrors) => new(
        toolchain.Limits, toolchain.Windows, toolchain.SignIn, toolchain.Refused, withErrors ? toolchain.ErrorWords : null);
}

/// <summary>How an account's windows are asked between turns (<see cref="UsageQuestion"/>); null asks nothing.</summary>
public sealed record UsagePart(IUsageQuestion? Question)
{
    public static UsagePart None { get; } = new((IUsageQuestion?)null);
}

/// <summary>
/// What a harness whose <c>accountOf</c> names this provider's or door's id reads of it, one level (O4-F1): its account facts, its
/// tables and its usage question. A provider lends all of what it declares but for <c>codex</c>, which lends its sign-in alone, no
/// words and no usage question, as today.
/// </summary>
public sealed record LentPart(PoolFacts Accounts, TablesPart Tables, UsagePart Usage)
{
    public static LentPart None { get; } = new(PoolFacts.None, TablesPart.None, UsagePart.None);
}

/// <summary>
/// What a door's spawn does about an account's home, each the door's own, never its provider's (O2-F2): the variable it hands the
/// home in, whether a start naming no account runs in the tool's own home, the trust file, and whether the home must already exist.
/// </summary>
public sealed record DoorHome(string? Variable, bool OwnHome, string? TrustFile, bool MustExist)
{
    /// <summary>A start that names no account runs in the tool's own home: a test double's, or a tool no account can be handed to.</summary>
    public bool StartsInOwnHome => OwnHome || Variable is not { Length: > 0 };

    public static DoorHome Of(HarnessToolchain? toolchain) => toolchain is null
        ? new DoorHome(null, false, null, false)
        : new DoorHome(toolchain.ProfileVariable, toolchain.RunsInOwnHome, toolchain.TrustFile, toolchain.ProfileMustExist);
}

/// <summary>The postures a door speaks in its agent's own words: ordinary, asking and full (D183, D184). Null maps none.</summary>
public sealed record PosturesPart(string? Ordinary, string? Asking, string? Full);

/// <summary>What a door declares it can do, a <see cref="Known{T}"/> each (the design §2.3).</summary>
public sealed record AbilitiesPart(Known<bool> Interactive, Known<bool> Resumes, Known<bool> TakesSettings);

/// <summary>A door's own words: its error words and its turn report's meaning (D179 point 8).</summary>
public sealed record DoorTablesPart(ErrorWords? ErrorWords, string? TurnReport);

/// <summary>
/// The variables, files and wire keys an agent names of its own, which the census reads (the design §9). Stated absent in row a:
/// the census's seed is its own fixture.
/// </summary>
public sealed record VocabularyPart(IReadOnlyList<string> Variables, IReadOnlyList<string> Files, IReadOnlyList<string> WireKeys);
