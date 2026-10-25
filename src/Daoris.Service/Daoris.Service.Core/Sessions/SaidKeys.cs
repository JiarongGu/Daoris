namespace Daoris.Knowledge;

/// <summary>A word that left a record's <c>said</c> under a producer's key (row p, the design §19.2): where it left and when.</summary>
/// <param name="Record">The record the word left.</param>
/// <param name="Word">The word's id on that record.</param>
/// <param name="Key">The producer's key it left under; <c>id:&lt;word id&gt;</c> for a word that had none.</param>
/// <param name="At">When it left, to the second.</param>
public sealed record SettledWord(string Record, string Word, string Key, DateTimeOffset At);

/// <summary>
/// What the keeping doors read of a producer's key before they refuse (FOUNDATION1 row p, D212, the foundation design §3.4 rule 5, §19.2): a
/// word keyed under it that left a record of the line, which the service's <c>said_settled</c> holds for seven days. The say door, the
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

    /// <summary>How many predecessors the walk back along <c>from</c> follows at most; a record already walked ends it.</summary>
    internal const int Walk = 256;

    /// <summary>
    /// The line's records from <paramref name="named"/>: the record itself and its predecessors back along <c>from</c>, one probe by id each,
    /// a record already walked ending the walk (a cycle planted by hand) and a predecessor that is gone ending it too.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> LineAsync(SessionStore sessions, Session named, CancellationToken ct)
    {
        List<string> records = [named.Id];
        var at = named;
        for (var hops = 0; hops < Walk && at.From is { } from && !records.Contains(from, StringComparer.Ordinal); hops++)
        {
            if (await sessions.FindAsync(from, ct).ConfigureAwait(false) is not { } before) break;
            records.Add(before.Id);
            at = before;
        }

        return records;
    }

    /// <summary>
    /// The key's settlement over <paramref name="records"/>, the line's, within the horizon of <paramref name="now"/>; null where none holds it
    /// or there is no key. Called inside the caller's exclusive step.
    /// </summary>
    public static Task<SettledWord?> Settled(SessionStore sessions, IReadOnlyList<string> records, string? key, DateTimeOffset now, CancellationToken ct = default) =>
        key is null
            ? Task.FromResult<SettledWord?>(null)
            : sessions.SettledAsync(records, key, (now - Horizon).ToUnixTimeSeconds(), ct);

    /// <summary>The key's settlement over the line the named record is on: its predecessors back along <c>from</c>. Null where none.</summary>
    internal static async Task<SettledWord?> SettledOnLineAsync(SessionStore sessions, Session named, string? key, DateTimeOffset now, CancellationToken ct)
    {
        if (key is null) return null;
        return await Settled(sessions, await LineAsync(sessions, named, ct).ConfigureAwait(false), key, now, ct).ConfigureAwait(false);
    }
}
