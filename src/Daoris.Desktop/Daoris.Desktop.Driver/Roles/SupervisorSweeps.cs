namespace Daoris.Driver;

/// <summary>
/// The sweeps over many decisions (§5, §3.4): a revoke's or a replacing appointment's, a cannot-go-on's, the comparison of the files
/// with memory, and the start's sweep of the files left by the last run. Each move is made under its session's lock in the
/// <see cref="SupervisorBook"/>; this type holds no state of its own.
/// </summary>
internal sealed class SupervisorSweeps(SupervisorBook book, MachineLog? log)
{
    /// <summary>
    /// A revoke's or a replacing appointment's sweep (§5): every held or released decision of the session goes stale, and the word of
    /// every stale one not yet taken back is taken back (SUPERVISORRACE1).
    /// </summary>
    public Task StaleAsync(string session, string why, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(session, async () =>
        {
            foreach (var decision in book.Live.Values.Where(each => each.Session == session).ToList())
            {
                if (decision.State is not (SupervisorState.Held or SupervisorState.Released) && !SupervisorBook.WordLeft(decision)) continue;
                await SupervisorBook.TakeBackWordAsync(session, decision, takeBack, ct).ConfigureAwait(false);
                book.Move(decision, SupervisorState.Stale, now, why);
            }

            return 0;
        }, ct);

    /// <summary>
    /// A revoke's or a replacing appointment's sweep (SUPERVISOR1a-ii; §5, §2.3), run only after the judge's mutation step has ended
    /// and its lock is let go: each session with a live decision kept under <paramref name="generation"/>, in turn and under that
    /// session's own decision lock, moves its held and released decisions of that generation to <c>stale</c> (<c>revoked</c>), a
    /// released one's word taken back. A release or an admission already holding the lock finishes first and is swept after; an
    /// admitted decision is a taken-in act, which a revoke does not recall. An admission that found the generation dead before the
    /// sweep came staled its released decision itself and took nothing back (SUPERVISORRACE1): the sweep takes back the word of every
    /// decision of that generation already stale and not yet taken back.
    /// </summary>
    /// <param name="repository">
    /// SUPERVISOR1e (§5): only the decisions of this repository, where a repository's own appointment replaced its workspace's for it
    /// alone, each moved with <c>appointment-changed</c>; null sweeps the generation whole, with <c>revoked</c>.
    /// </param>
    /// <returns>How many went stale.</returns>
    public async Task<int> RevokedAsync(
        string generation, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default, string? repository = null)
    {
        bool Swept(SupervisorLiveDecision each) => each.Generation == generation
                                 && (repository is null || string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase));
        var why = repository is null ? SupervisorWhy.Revoked : SupervisorWhy.RoleChanged;
        var swept = 0;
        var sessions = book.Live.Values.Where(Swept).Select(each => each.Session).Distinct(StringComparer.Ordinal).ToList();
        foreach (var session in sessions)
        {
            swept += await book.UnderAsync(session, async () =>
            {
                var moved = 0;
                foreach (var decision in book.Live.Values.Where(each => each.Session == session && Swept(each)).ToList())
                {
                    if (decision.State is not (SupervisorState.Held or SupervisorState.Released))
                    {
                        // Already stale: its state stands, its word is the sweep's where no one took it back.
                        if (SupervisorBook.WordLeft(decision)) await SupervisorBook.TakeBackWordAsync(session, decision, takeBack, ct).ConfigureAwait(false);
                        continue;
                    }

                    await SupervisorBook.TakeBackWordAsync(session, decision, takeBack, ct).ConfigureAwait(false);
                    if (book.Move(decision, SupervisorState.Stale, now, why) == SupervisorState.Stale) moved++;
                }

                return moved;
            }, ct).ConfigureAwait(false);
        }

        return swept;
    }

    /// <summary>
    /// Compare every live decision's files of <paramref name="session"/> with what memory wrote, each under the decision's lock, the
    /// whole JSON and every state line: a file that differs moves its decision to <c>stale</c> as tampered, its word taken back.
    /// </summary>
    /// <returns>How many went stale.</returns>
    public async Task<int> CompareAsync(string session, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default)
    {
        var tampered = 0;
        foreach (var id in book.LiveIds(session))
        {
            tampered += await book.UnderAsync(session, async () =>
            {
                if (!book.Live.TryGetValue(id, out var decision) || SupervisorStates.Final(decision.State)) return 0;
                if (SupervisorDecisionFiles.Read(book.Files.FileOf(session, id)) == decision.Written
                    && SupervisorDecisionFiles.Read(book.Files.StatesOf(session, id)) == string.Concat(decision.Lines)) return 0;
                if (decision.State == SupervisorState.Admitted) return 0;
                await SupervisorBook.TakeBackWordAsync(session, decision, takeBack, ct).ConfigureAwait(false);
                book.Move(decision, SupervisorState.Stale, now, SupervisorWhy.Tampered);
                return 1;
            }, ct).ConfigureAwait(false);
        }

        return tampered;
    }

    /// <summary>
    /// The start's sweep, before the first look's carry-ons: every decision file on disk read only to close it, <c>held</c> and
    /// <c>released</c> to <c>stale</c> (<see cref="SupervisorWhy.Restarted"/>), <c>admitted</c> to <c>interrupted</c>; each closed
    /// question remembered as the owner's. Memory is empty, so nothing is live and nothing is handable.
    /// </summary>
    /// <returns>The sessions with a decision the sweep closed, whose waiting supervisor's words the look takes back.</returns>
    public IReadOnlyList<string> SweepAtStart(DateTimeOffset now)
    {
        var swept = new List<string>();
        foreach (var folder in book.Files.Folders())
        {
            var session = SupervisorDecisionFiles.SessionOf(folder);
            if (!SessionEventText.IsLocalId(session)) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (book.Live.ContainsKey(id) || book.Files.Earlier(session, id) is not { } earlier) continue;
                var closed = earlier.State switch
                {
                    SupervisorState.Held or SupervisorState.Released => SupervisorState.Stale,
                    SupervisorState.Admitted => SupervisorState.Interrupted,
                    _ => (SupervisorState?)null,
                };
                if (closed is { } to)
                {
                    SupervisorDecisionFiles.TryAppend(
                        book.Files.StatesOf(session, id),
                        SupervisorDecisionFiles.StateLine(to, now, to == SupervisorState.Stale ? SupervisorWhy.Restarted : null, null));
                    log?.Info(to == SupervisorState.Stale ? "supervisor.stale" : "supervisor.interrupted", ("why", SupervisorWhy.Restarted));
                    if (!swept.Contains(session)) swept.Add(session);
                }

                var state = closed ?? earlier.State;
                // A question left to the owner (SUPERVISOR1b) stays decided across the restart, as one closed as theirs does.
                if (SupervisorStates.OwnersAgain(state) || state == SupervisorState.Left) book.Closed[(session, earlier.Question.Id)] = state;
            }
        }

        return swept;
    }
}
