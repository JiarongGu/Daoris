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

    /// <summary>
    /// Its agent refused the choice its actor asked at the open, before any prompt (D205 §3.5(c), MODELROLE1c2): it ended
    /// <c>choice-not-offered</c>, announced as any end, and the attention watch raises no <i>Ended</i> for it, since a fallback asks
    /// nothing of the person (§4.6). So too a pipe start whose door refused its model on a silent turn (§3.3's pipe exception,
    /// MODELROLE1c3), ended with the account's <c>account.refused-model</c> line.
    /// </summary>
    public bool NotOffered { get; init; }

    /// <summary>The note's <c>ended.*</c> code (FOUNDATION1 l-iii): the end's reason, where its writer composed the note as parts.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// The end's class (§4.2), judged by the one judge, <see cref="PartEnds.ClassOf"/>, with this look's own flags refining it: the
    /// journal's row cannot know whose decision the stop was, the look does (<see cref="ByPerson"/>, <see cref="Limit"/>, <see cref="NotOffered"/>).
    /// </summary>
    public string Class =>
        NotOffered ? "refused-choice"
        : Limit ? "limited"
        : ByPerson ? "stopped-person"
        : PartEnds.ClassOf(State, limited: false, Reason is null ? [] : [Reason]);

    /// <summary>The first <c>ended.*</c> code among a note's parts, or null.</summary>
    public static string? ReasonOf(IReadOnlyList<NotePart>? parts) =>
        parts?.Select(part => part.Code).FirstOrDefault(code => code is not null && code.StartsWith("ended.", StringComparison.Ordinal));
}
