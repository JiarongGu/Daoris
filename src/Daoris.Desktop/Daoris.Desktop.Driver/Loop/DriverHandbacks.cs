namespace Daoris.Driver;

/// <summary>
/// The hand-back read by Daoris, once per session that ended done: from its tree, never from its note. What it read is kept on the session's
/// record as a note, and a reached check with no verdict at this tip is asked of the broker on Daoris's own account, so it is run rather
/// than refused for; a red comes back to the session as a word (<see cref="CheckHanding"/>).
/// </summary>
internal sealed class DriverHandbacks(LoopContext context)
{
    /// <summary>Read each done session of this machine's not yet read, in its tree.</summary>
    internal async Task LookAsync(IEnumerable<PriorSession> records, CancellationToken ct)
    {
        var files = new CheckFiles(context.Home);
        foreach (var record in records.Where(HandbackReader.EndedDone))
        {
            if (File.Exists(files.PathOf(record.Session, "observed"))) continue;
            var folder = new LinePlaces(context.Home).Of(record.Tree).Folder;
            if (folder is null || !Directory.Exists(folder)) continue;
            await ObserveAsync(record, folder, files, ct).ConfigureAwait(false);
        }
    }

    private async Task ObserveAsync(PriorSession record, string folder, CheckFiles files, CancellationToken ct)
    {
        if (await HandbackReader.LineAsync(folder, ct).ConfigureAwait(false) is not { } line) return;
        var gates = HandbackReader.GatesOf(folder);
        var checks = files.Asks().Where(ask => ask.Session == record.Session).Select(ask => (ask, files.ReadState(ask.Id))).ToList();
        if (await HandbackReader.ReadAsync(folder, line, gates, checks, [], ct).ConfigureAwait(false) is not { } seen) return;

        foreach (var missing in seen.Missing)
        {
            files.WriteAsk(new CheckAsk(Guid.NewGuid().ToString("N")[..8], record.Session, missing.Gate, null, CheckAsk.ByDaoris, DateTimeOffset.UtcNow));
        }

        context.Events.Keep(record.Session, new SessionEvent { Kind = SessionEventKind.Note, Text = seen.Words() }, say: null);
        files.WriteObserved(record.Session, seen.Tip);
    }
}
