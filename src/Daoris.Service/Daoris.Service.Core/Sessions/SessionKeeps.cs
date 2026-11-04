using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The facts about a record's run that never travel and never move it (HELPMODE1a2, AUTONOMY1b2, D205, KEEP1b): the access mode,
/// the access setup, the actor's provenance and the downloaded versions a start was handed. Each is machine-local, kept without a state
/// move or a sync revision.
/// </summary>
internal sealed class SessionKeeps(SessionStore sessions)
{
    /// <summary>
    /// Record the access mode an Ask Daoris conversation of this machine's goes on under (HELPMODE1a2, D183 point 3): a conversation
    /// going on after the person's mode changed runs under the new one, and its record says so. The mode is a fact about its run,
    /// and it never travels.
    /// </summary>
    /// <param name="named">The mode, already judged by the ledger.</param>
    /// <returns>The record as it now reads; null where no Ask Daoris record of this machine's has that id.</returns>
    public async Task<Session?> KeepHelpModeAsync(string id, string named, CancellationToken ct = default) =>
        await sessions.KeepHelpModeAsync(id, named, ct).ConfigureAwait(false)
            ? await sessions.FindAsync(id, ct).ConfigureAwait(false)
            : null;

    /// <summary>
    /// Record the access setup a resumed driven session of this machine's now runs under (AUTONOMY1b2, D184 point 1), where the setup
    /// changed since it started: the setup is a fact about its run, and it never travels.
    /// </summary>
    /// <returns>The record as it now reads; null where no driven record of this machine's has that id.</returns>
    /// <exception cref="ArgumentException">A setup that is not a short map of words, or none.</exception>
    public async Task<Session?> KeepAccessAsync(string id, IReadOnlyDictionary<string, string> access, CancellationToken ct = default)
    {
        var setup = SessionAccess.Judged(access)
            ?? throw new ArgumentException("access is the setup the session now runs under, and names at least one pause.", nameof(access));
        return await sessions.KeepAccessAsync(id, setup, ct).ConfigureAwait(false)
            ? await sessions.FindAsync(id, ct).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// D205's later provenance door. Machine-local, without a state move or sync revision; omitted members keep their value.
    /// </summary>
    /// <returns>The record as it now reads; null where no record of this machine's has that id.</returns>
    /// <exception cref="ArgumentException">
    /// An unknown choice, a non-object value or provenance beyond its bounds.
    /// </exception>
    public async Task<Session?> KeepActorAsync(string id, string? choice, JsonElement? asked, JsonElement? changed, JsonElement? ran,
        JsonElement? chosen = null, CancellationToken ct = default)
    {
        if (choice is not null && choice is not ("1" or "2" or "3" or "4" or "before" or "rule-default" or "pick" or "handed"))
            throw new ArgumentException("unknown actor choice");
        foreach (var value in new[] { asked, changed, ran, chosen })
            if (value is { } element && (element.ValueKind != JsonValueKind.Object || element.GetRawText().Length > 16384))
                throw new ArgumentException("actor provenance must be a bounded object");
        if (ran is { } reported)
        {
            foreach (var name in new[] { "models", "entries" })
                if (reported.TryGetProperty(name, out var list) && (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > 8))
                    throw new ArgumentException($"ran.{name} keeps at most eight; overflow is counted separately");
            foreach (var name in new[] { "more", "entriesMore" })
                if (reported.TryGetProperty(name, out var count) && (count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out var n) || n < 0))
                    throw new ArgumentException($"ran.{name} must be a nonnegative count");
        }
        return await sessions.KeepActorAsync(id, choice, asked, changed, ran, chosen, ct).ConfigureAwait(false)
            ? await sessions.FindAsync(id, ct).ConfigureAwait(false) : null;
    }

    /// <summary>Record the downloaded versions a start was handed (KEEP1b, D195 point 6), added to its earlier starts without a move or revision.</summary>
    public async Task<Session?> KeepResolvedAsync(string id, IReadOnlyList<string> versions, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count is 0 or > SessionLedger.MostResolved)
        {
            throw new ArgumentException($"resolved names the versions a start was handed, one to {SessionLedger.MostResolved} of them.", nameof(versions));
        }

        if (versions.FirstOrDefault(version => !IsVersionKey(version)) is { } wrong)
        {
            throw new ArgumentException(
                $"`{OneLine(Clip(wrong, 80))}` is no downloaded version's key: each is agent|tool/<name>/<version>, and never a path.", nameof(versions));
        }

        var before = await sessions.FindAsync(id, ct).ConfigureAwait(false);
        if (before is null || before.Origin is not null) return null;
        if ((before.Resolved ?? []).Union(versions, StringComparer.Ordinal).Count() > SessionLedger.MostResolved)
        {
            throw new ArgumentException($"a record keeps at most {SessionLedger.MostResolved} versions its starts were handed.", nameof(versions));
        }

        return await sessions.KeepResolvedAsync(id, versions, ct).ConfigureAwait(false)
            ? await sessions.FindAsync(id, ct).ConfigureAwait(false)
            : null;
    }

    /// <summary>Whether a text is a downloaded version's key: <c>agent</c> or <c>tool</c>, then a name and a version, each a plain segment.</summary>
    private static bool IsVersionKey(string? key)
    {
        if (key is null || key.Length > 200) return false;
        var parts = key.Split('/');
        return parts.Length == 3 && parts[0] is "agent" or "tool"
            && parts.Skip(1).All(part => part.Length > 0 && part is not ("." or "..")
                && part.All(each => char.IsAsciiLetterOrDigit(each) || each is '.' or '-' or '_' or '+' or '@'));
    }
}
