namespace Daoris.Knowledge;

/// <summary>A word that left a record's <c>said</c> under a producer's key (row p, the design §19.2): where it left and when.</summary>
/// <param name="Record">The record the word left.</param>
/// <param name="Word">The word's id on that record.</param>
/// <param name="Key">The producer's key it left under; <c>id:&lt;word id&gt;</c> for a word that had none.</param>
/// <param name="At">When it left, to the second.</param>
public sealed record SettledWord(string Record, string Word, string Key, DateTimeOffset At);

/// <summary>
/// What the keeping doors read of a producer's key before they refuse (FOUNDATION1 row p, D212, the foundation design §3.4 rule 5, §19.2): a
/// word keyed under it that left a record of the line, which the service's <c>said_settled</c> holds for seven days, the line being the record
/// named, its predecessors back along <c>from</c> and, from row d-ii, the records the head walk visits forward to the head. The say door, the
/// helper desk and the partner desk each ask it inside their own exclusive step, before any refusal but the record's absence, so a producer's
/// retry after the take is answered as kept and no window lies between the check and a keep. The supervisor's door reads none: its retry
/// compares a decision, a question, a choice and a text, which a settlement cannot carry.
/// </summary>
public static class SaidKeys
{
    /// <summary>
    /// How long a settlement answers a retry after the word left (round 5, §19.2): seven days, trimmed by time alone and never by count.
    /// The attempt has no horizon: a word attempted is never sent again by itself, whatever its age.
    /// </summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromDays(7);

    /// <summary>
    /// The line's records about <paramref name="named"/> (rows p and d-ii, the design §3.4 rule 5, *C6-1.3*): the record itself, its
    /// predecessors back along <c>from</c>, one probe by id each, a record already walked (a cycle planted by hand) or a predecessor that is
    /// gone ending that walk, and the records <paramref name="forward"/> visited from it to the line's head. <c>Whole</c> is false where the
    /// walk back would pass <see cref="Lines.Bound"/> predecessors or the walk forward ended at the bound: a settlement read over a line cut
    /// short could miss the word a retry names and keep it again, so the caller refuses rather than read it.
    /// </summary>
    internal static async Task<(IReadOnlyList<string> Records, bool Whole)> LineAsync(
        SessionStore sessions, Session named, LineWalk forward, CancellationToken ct)
    {
        List<string> records = [named.Id];
        var at = named;
        for (var hops = 0; at.From is { } from && !records.Contains(from, StringComparer.Ordinal); hops++)
        {
            if (hops == Lines.Bound) return (records, false);
            if (await sessions.FindAsync(from, ct).ConfigureAwait(false) is not { } before) break;
            records.Add(before.Id);
            at = before;
        }

        foreach (var ahead in forward.Records)
        {
            if (!records.Contains(ahead.Id, StringComparer.Ordinal)) records.Add(ahead.Id);
        }

        return (records, forward.End != LineEnd.Bound);
    }

    /// <summary>
    /// The key's settlement over <paramref name="records"/>, the line's, within the horizon of <paramref name="now"/>; null where none holds it
    /// or there is no key. Called inside the caller's exclusive step.
    /// </summary>
    public static Task<SettledWord?> Settled(SessionStore sessions, IReadOnlyList<string> records, string? key, DateTimeOffset now, CancellationToken ct = default) =>
        key is null
            ? Task.FromResult<SettledWord?>(null)
            : sessions.SettledAsync(records, key, (now - Horizon).ToUnixTimeSeconds(), ct);

    /// <summary>
    /// The key's settlement over the line <paramref name="named"/> is on, back along <c>from</c> and forward over <paramref name="forward"/>'s
    /// records to the head (<see cref="LineAsync"/>): <c>Held</c> null where none holds it, and <c>Whole</c> false, with nothing read, where
    /// the line could not be read whole.
    /// </summary>
    internal static async Task<(SettledWord? Held, bool Whole)> SettledOnLineAsync(
        SessionStore sessions, Session named, LineWalk forward, string key, DateTimeOffset now, CancellationToken ct)
    {
        var (records, whole) = await LineAsync(sessions, named, forward, ct).ConfigureAwait(false);
        return whole ? (await Settled(sessions, records, key, now, ct).ConfigureAwait(false), true) : (null, false);
    }
}
