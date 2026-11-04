namespace Daoris.Driver;

/// <summary>
/// A start that runs on another account of its scope's list than the one the scope begins at (TOOL4f, D125 §3.3): that
/// one was not ready, or kept for conversations, or the goal's walk ranked another ahead of it (TOOL6b, D130 §16.3).
/// Machine-local: it names accounts, so it goes into the conversation record and the machine log, never into the note,
/// which travels (§3.6).
/// </summary>
/// <param name="From">Where the scope begins: its default, else its list's first; never a pick and never the tool's own home.</param>
/// <param name="Cooling">Its cool-off, when that is why it was passed.</param>
/// <param name="Why">Why it was passed, as a clause a person reads on this machine; it names the account.</param>
public sealed record RotatedStart(string From, CoolingEntry? Cooling, string Why)
{
    /// <summary>The step that moved the start off it: the log's <c>why</c> (D130 §13 as §16 amends it).</summary>
    public WalkStep Step { get; init; } = WalkStep.Cooling;

    /// <summary>The workspace whose list it was, or null for this machine's: the log's <c>scope</c>.</summary>
    public string? Scope { get; init; }

    /// <summary>
    /// What the account it was moved off last said, as the log names it (TOOL6c): <c>refused</c>, <c>near</c>,
    /// <c>warned</c> or <c>clear</c>, or null where it said nothing (<see cref="AccountReadings.Standing"/>).
    /// </summary>
    public string? FromSaid { get; init; }

    /// <summary>What the account it ran on last said, as <see cref="FromSaid"/> names it.</summary>
    public string? ToSaid { get; init; }
}
