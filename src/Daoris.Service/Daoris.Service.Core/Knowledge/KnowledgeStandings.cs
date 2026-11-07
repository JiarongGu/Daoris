namespace Daoris.Knowledge;

/// <summary>
/// What each entry's reading and, for a decision, its own record say of its standing, by entry id.
/// </summary>
/// <remarks>
/// <para>🔴 The entry, its file's entries and its repository's reading are read together, from the index every connector
/// writes: a reading this process keeps beside marks another connector writes would say one commit over another's
/// record. An entry another write replaces after it is found, or one whose reading names no commit, says <c>unknown</c>.
/// The commit is the stored reading's, read or fed, and costs no git call; the uncommitted status is asked once for that
/// reading's commit and signature. A decision's marks are derived from its file's note entries
/// (<see cref="DecisionNotes.Marks(IEnumerable{KnowledgeEntry})"/>), so a fed host derives what a local one does.</para>
///
/// <para>A decision whose file has no entry of its own before its notes (a section of one decisions log) has none to
/// place its notes by, and says its reading alone.</para>
/// </remarks>
internal sealed class KnowledgeStandings(
    IKnowledgeStore store, RootProbes probes, Func<string, string?> rootOf, Action refuseNewerIndex)
{
    public async Task<IReadOnlyDictionary<string, EntryStanding>> OfAsync(
        IEnumerable<KnowledgeEntry> entries, CancellationToken ct)
    {
        refuseNewerIndex();
        var asked = entries.ToList();
        var indexed = asked.Where(entry => entry.Kind != EntryKind.Record).Select(entry => entry.Id).ToList();
        var held = indexed.Count == 0
            ? null
            : await store.HeldAsync(indexed, entry => entry.Kind == EntryKind.Decision, ct).ConfigureAwait(false);
        var deadline = Task.Delay(KnowledgeService.StandingWait, ct);
        var statuses = new Dictionary<string, (bool Uncommitted, bool Pending, bool Unknown)>(StringComparer.Ordinal);
        var standings = new Dictionary<string, EntryStanding>(StringComparer.Ordinal);
        foreach (var entry in asked)
        {
            if (standings.ContainsKey(entry.Id)) continue;

            // A record is no file read at a commit; its own tip says what it stands on.
            if (held is null || entry.Kind == EntryKind.Record
                || !held.Entries.TryGetValue(entry.Id, out var now) || !SameText(now, entry)
                || !held.Readings.TryGetValue(entry.Repository, out var reading) || reading.Commit is null)
            {
                standings[entry.Id] = new EntryStanding(null, false, false);
                continue;
            }

            if (!statuses.TryGetValue(entry.Repository, out var status))
            {
                status = reading.Fed ? default : await StatusOfAsync(reading, deadline).ConfigureAwait(false);
                statuses[entry.Repository] = status;
            }

            var decision = held.Files.TryGetValue((now.Repository, now.RelativePath), out var file) ? DecisionMarks.StandingOf(now, file) : null;
            standings[entry.Id] = new EntryStanding(reading.Commit, reading.Fed, status.Uncommitted, decision, status.Pending, status.Unknown);
        }

        return standings;
    }

    /// <summary>Whether two readings of an entry say the same: its text and its place, whatever workspace stamps it.</summary>
    private static bool SameText(KnowledgeEntry held, KnowledgeEntry found) =>
        held.Kind == found.Kind && held.Title == found.Title && held.Body == found.Body && held.RelativePath == found.RelativePath
        && held.Anchor == found.Anchor && held.Lines == found.Lines;

    /// <summary>
    /// A read reading's uncommitted status, waited for until <paramref name="deadline"/>: pending where it has not landed, unknown
    /// where git did not answer, nothing where no root here or no seam can ask it.
    /// </summary>
    private async Task<(bool Uncommitted, bool Pending, bool Unknown)> StatusOfAsync(IndexReading reading, Task deadline)
    {
        if (rootOf(reading.Repository) is not { } root || probes.ProbeOf(root, reading.Commit, reading.Signature) is not { } probe) return default;
        var task = probe.Value;
        if (!task.IsCompleted) await Task.WhenAny(task, deadline).ConfigureAwait(false);
        if (!task.IsCompleted) return (false, true, false);
        return task.Result is { } dirty ? (dirty, false, false) : (false, false, true);
    }
}
