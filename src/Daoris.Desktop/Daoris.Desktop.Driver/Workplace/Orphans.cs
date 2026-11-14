namespace Daoris.Driver;

/// <summary>One record ended because nothing on this machine is running it.</summary>
public sealed record OrphanEnded(string Id, string Repository)
{
    /// <summary>The quest it served, or null: whether its done lands at the next look.</summary>
    public string? Quest { get; init; }

    /// <summary>The tree its record names, or null.</summary>
    public string? Tree { get; init; }
}

/// <summary>
/// Records that say a session runs when nothing on this machine runs it (2026-09-25): left by a crash,
/// a kill, a power cut — anything no shutdown order can reach. Found and ended, never guessed at.
/// </summary>
/// <remarks>
/// <para><b>An orphan is proven, not presumed.</b> This machine's record (never a teammate's, which
/// the service client already leaves out), in a state that claims a process, with no live process
/// for it here — not in this driver's registry and not marked by any other driver sharing the home
/// (<see cref="SessionProcesses.AliveOnThisMachine"/>).</para>
///
/// <para><b>The loop sweeps at every look, and takes <c>working</c> and a start whose opener went.</b> A record is <c>queued</c>,
/// then <c>starting</c>, between its open and its spawn, and a go-on or a resume is <c>working</c> before its spawn: each is marked
/// by the process that opened it (<see cref="SessionProcesses.MarkOpening(string)"/>), and is taken only once that process has gone; a
/// start no opener marked is left, and a parked one waits on the person by design. The person's own stop on one record takes it
/// whatever marks it: they asked about that one, and a start that never spawned would otherwise hold its repository, or Ask
/// Daoris's room, forever.</para>
///
/// <para><b>One home per service is the assumption</b>, and it is the deployed shape: a driver drives
/// its machine's local host (a shared host is reached through the sync, never driven directly),
/// and the markers are that home's. A second home driving the same host would see the first's
/// sessions as unmarked, which is why nothing but a driver's own loop and a person's stop ever asks.</para>
///
/// <para><b>Ended <c>stopped</c></b>, as the driven path records a session at the driver's close:
/// what ended it is not known, only that nothing runs it, and the note says exactly that. The sweep's stop
/// is <b>interrupted</b>, not the person's, so a take it ended is carried on; the person's stop on
/// one record is theirs, since they asked about that one, and never is.</para>
///
/// <para><b>A lost done's evidence is read as it ends</b>: where the record's quest closed done and waits on
/// its evidence, the tree's HEAD is read by the same code a session's end reads it with, posted as the sweep's, and kept on
/// the record it ends beneath the commits since its base. Any other record ends as before.</para>
/// </remarks>
public static class Orphans
{
    public const string Note =
        "nothing on this machine was running it any more — its process ended with the application that "
        + "started it, and the record had not been told.";

    /// <summary><see cref="Note"/> with its code.</summary>
    public static Noted Noted => Noted.Of(EndNoteCodes.StoppedOrphan, Note);

    /// <param name="only">One record the person asked to stop, or null for the sweep.</param>
    /// <param name="held">Whether a run of the sweeping loop holds a record, which that run concludes itself.</param>
    /// <returns>The records ended, in the order the service listed them.</returns>
    public static Task<IReadOnlyList<OrphanEnded>> EndAsync(
        ServiceClient service, SessionProcesses processes, string? only = null, CancellationToken ct = default, Func<string, bool>? held = null) =>
        EndAsync(service, processes, WorkingTree.ReadGitAsync, only, ct, held);

    /// <inheritdoc cref="EndAsync(ServiceClient, SessionProcesses, string?, CancellationToken, Func{string, bool}?)"/>
    /// <param name="git">How a lost done's tree is read: the review's seam, which a test stands git in at.</param>
    internal static async Task<IReadOnlyList<OrphanEnded>> EndAsync(
        ServiceClient service, SessionProcesses processes, WorkingTree.GitRead git, string? only = null, CancellationToken ct = default,
        Func<string, bool>? held = null)
    {
        var ended = new List<OrphanEnded>();
        var listed = await service.ActiveSessionsAsync(ct).ConfigureAwait(false);

        // The markers of processes that ended with no one removing theirs, once the service has listed every session still active.
        if (only is null) processes.SweepDeadMarkers(listed.Select(session => session.Id).ToHashSet(StringComparer.Ordinal));
        foreach (var session in listed)
        {
            if (only is not null && !string.Equals(session.Id, only, StringComparison.OrdinalIgnoreCase)) continue;
            // The person's stop takes a start that never spawned, `starting` or still `queued` (an Ask Daoris start left
            // `queued` by a driver that went held the room, and every later start is refused naming it). The sweep takes one only
            // where the process that opened it is proven gone: a live opener here is still starting it.
            var claimsProcess = (session.State == "working" && (only is not null || !processes.OpenerRuns(session.Id)))
                || (session.State is "starting" or "queued" && (only is not null || processes.OpenerWent(session.Id) || NeverMarked(session, processes)));
            if (!claimsProcess || processes.AliveOnThisMachine(session.Id) || (only is null && held?.Invoke(session.Id) == true)) continue;

            var evidence = await LostDoneAsync(service, session, git, ct).ConfigureAwait(false);
            try
            {
                await service.AdvanceAsync(session.Id, "stopped", Noted, evidence: evidence, ct: ct, interrupted: only is null).ConfigureAwait(false);
                ended.Add(new OrphanEnded(session.Id, session.Repository) { Quest = session.Quest, Tree = session.Tree });
            }
            catch (DriverException)
            {
                // Moved by someone else between the list and the write — its record says how.
            }
        }

        return ended;
    }

    internal static readonly TimeSpan UnmarkedGrace = TimeSpan.FromMinutes(2);

    /// <summary>A <c>queued</c> or <c>starting</c> record no process marks, untouched past <see cref="UnmarkedGrace"/>: its opener went first.</summary>
    private static bool NeverMarked(SessionView session, SessionProcesses processes) =>
        session.Updated is { } updated && DateTimeOffset.UtcNow - updated > UnmarkedGrace && !processes.HasMarker(session.Id);

    /// <summary>
    /// What the record of a lost session gains as it ends: where its quest closed done and waits on its evidence, the
    /// commits since its base and what is read at its tree's HEAD; null for every other, which ends as before. A quest the
    /// service does not answer is not read: the sweep's work is ending the record.
    /// </summary>
    private static async Task<string?> LostDoneAsync(ServiceClient service, SessionView session, WorkingTree.GitRead git, CancellationToken ct)
    {
        if (session is not { Quest: { Length: > 0 } questId, Tree: { Length: > 0 } tree }) return null;

        QuestView? quest;
        try
        {
            quest = await service.FindQuestAsync(questId, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                          // The client's own timeout: a host that took the question and stalled.
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }

        // A submission this session said, waiting on no evidence, has its commit posted alone as its own end would, and
        // the record it ends carries no evidence, as before.
        if (quest is not { AwaitsEvidence: true })
        {
            if (WorkplaceWorkOrders.Current.CommitWanted(quest, new EvidenceAt(tree, EvidenceCodes.Sweep) { Session = session.Id }) is not null)
            {
                await WorkplaceWorkOrders.Current.AtEndAsync(service, quest, new EvidenceAt(tree, EvidenceCodes.Sweep) { Session = session.Id }, null, git, ct)
                    .ConfigureAwait(false);
            }

            return null;
        }

        // The range only from a base the record names as a commit, since from none `git log` would list the whole history; and
        // only in a tree that is the top of its own repository, since git walks up (the read below says why it read nothing).
        var since = session.BaseCommit is { } baseCommit && WorkingTree.IsCommitId(baseCommit) ? baseCommit : null;
        var commits = since is not null && await WorkingTree.IsTopLevelAsync(tree, git, ct).ConfigureAwait(false)
            ? await WorkingTree.CommitsSinceAsync(tree, since, git, ct).ConfigureAwait(false)
            : null;
        return await WorkplaceWorkOrders.Current.AtEndAsync(
                service, quest, new EvidenceAt(tree, EvidenceCodes.Sweep) { Base = since, Session = session.Id }, commits, git, ct)
            .ConfigureAwait(false);
    }
}
