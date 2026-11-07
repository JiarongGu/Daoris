namespace Daoris.Driver;

/// <summary>
/// The install's half of integration, a branch rule's <c>integrate</c>: each key as written, null where absent, so its reader
/// resolves each absent key (<see cref="Resolved"/>). <c>request</c> and <c>conflicts</c> are the unit's process, read under Current from here and under a
/// named workflow from its <c>integrate</c> step; the rest say how the line is integrated, and stay the rule's always.
/// </summary>
/// <param name="Request">Who requests: <see cref="Owner"/>, <see cref="Done"/> (through the rule's own <c>autoAccept</c>), or <see cref="Steward"/>.</param>
/// <param name="Commit">How each member lands: <see cref="Merge"/>, <see cref="Rebase"/> or <see cref="Squash"/>.</param>
/// <param name="Batch">Members per candidate, 1 to <see cref="MaxBatch"/>.</param>
/// <param name="Order"><see cref="Arrival"/>, or <see cref="Apart"/>.</param>
/// <param name="Gates"><see cref="Declared"/> (the accepted declaration's <c>integration.gates</c>), or <see cref="NoGates"/>.</param>
/// <param name="Unreached">What an unconfirmed failure no member reaches does: <see cref="Split"/>, <see cref="Hold"/> or <see cref="Land"/>.</param>
/// <param name="Reached">What an unconfirmed failure a member reaches does, the same three.</param>
/// <param name="Conflicts">Where a conflict of meaning goes: the member's <see cref="Session"/>, or held (<see cref="Hold"/>).</param>
/// <param name="Overlap">A start whose declared paths meet an open quest's: <see cref="Warn"/>, or <see cref="Wait"/>.</param>
/// <param name="Line">The line by name; null for the repository's canonical line.</param>
public sealed record LandingIntegrate(
    string? Request = null, string? Commit = null, int? Batch = null, string? Order = null, string? Gates = null,
    string? Unreached = null, string? Reached = null, string? Conflicts = null, string? Overlap = null, string? Line = null)
{
    public const string Owner = "owner";
    public const string Done = "done";
    public const string Steward = "steward";
    public const string Merge = "merge";
    public const string Rebase = "rebase";
    public const string Squash = "squash";
    public const string Arrival = "arrival";
    public const string Apart = "apart";
    public const string Declared = "declared";
    public const string NoGates = "none";
    public const string Split = "split";
    public const string Hold = "hold";
    public const string Land = "land";
    public const string Session = "session";
    public const string Warn = "warn";
    public const string Wait = "wait";

    /// <summary>The most members one candidate holds.</summary>
    public const int MaxBatch = 8;

    public static IReadOnlyList<string> Requests { get; } = [Owner, Done, Steward];
    public static IReadOnlyList<string> Commits { get; } = [Merge, Rebase, Squash];
    public static IReadOnlyList<string> Orders { get; } = [Arrival, Apart];
    public static IReadOnlyList<string> GateChoices { get; } = [Declared, NoGates];
    public static IReadOnlyList<string> Unconfirmeds { get; } = [Split, Hold, Land];
    public static IReadOnlyList<string> ConflictChoices { get; } = [Session, Hold];
    public static IReadOnlyList<string> Overlaps { get; } = [Warn, Wait];

    /// <summary>
    /// Every absent key read as its default: requested by the owner, each member merged, eight members where
    /// gates run and one where none, in arrival order, no gates, an unconfirmed failure split either way, a conflict to the member's
    /// session, an overlap warned of; the line stays null, the repository's canonical one.
    /// </summary>
    public LandingIntegrate Resolved()
    {
        var gates = Gates ?? NoGates;
        return new(Request ?? Owner, Commit ?? Merge, Batch ?? (gates == Declared ? MaxBatch : 1), Order ?? Arrival, gates,
            Unreached ?? Split, Reached ?? Split, Conflicts ?? Session, Overlap ?? Warn, Line);
    }

    /// <summary>
    /// What an unconfirmed failure does for a unit a member reaches or not: <see cref="Split"/> where absent, and
    /// where <see cref="Land"/> is set on a line no whole-set verification runs on, since <c>land</c> is only ever the owner's choice
    /// backed by one.
    /// </summary>
    public string Unconfirmed(bool reached, bool wholeSet)
    {
        var said = (reached ? Reached : Unreached) ?? Split;
        return said == Land && !wholeSet ? Split : said;
    }
}
