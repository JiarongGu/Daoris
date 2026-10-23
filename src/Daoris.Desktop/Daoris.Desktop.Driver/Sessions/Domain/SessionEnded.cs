namespace Daoris.Driver;

/// <summary>
/// A session this driver brought to an end, and whose decision that was (SURF5b).
/// </summary>
/// <param name="ByPerson">
/// 🔴 True when the person asked for this ending — they pressed stop, or they closed the driver.
/// The one fact that separates an interruption worth making from a toast telling somebody what they
/// just pressed (design §4), and the driver is the only place that knows it.
/// </param>
/// <param name="Quest">The quest it served, for a listener that keys on the ask rather than the session (D64).</param>
/// <param name="Adapter">The harness it ran on, as the record names it.</param>
/// <param name="Account">The credential profile it ran as, or null for the harness's own home.</param>
public sealed record SessionEnded(
    string Session, string Repository, string State, bool ByPerson, string? Note = null,
    string? Quest = null, string? Adapter = null, string? Account = null)
{
    /// <summary>
    /// An account's limit made this end (LIMITRESUME1a, D206 point 1): the record's <c>limited</c> flag, read beside its state, so
    /// the attention watch raises no <i>Ended</i> for it, as the ledger counts no strike.
    /// </summary>
    public bool Limit { get; init; }
}
