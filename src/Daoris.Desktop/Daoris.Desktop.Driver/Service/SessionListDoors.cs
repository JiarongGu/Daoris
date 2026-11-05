using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>
/// The reads of the service's session list (<c>GET /api/sessions</c>, the service's session routes): a class over the client, reached as its
/// <see cref="ServiceClient.SessionList"/> and by the client's own doors of the same names. <c>/api/sessions</c> alone means ACTIVE; a session
/// is reviewed, merged or discarded once it has ended, so a read of one asks the closed records too (GROUND1).
/// </summary>
public sealed class SessionListDoors
{
    private readonly ServiceClient _service;

    internal SessionListDoors(ServiceClient service) => _service = service;

    /// <summary>
    /// The session list as the service wrote it, the active records or with <paramref name="includeClosed"/> every one: the text the
    /// readers and the snapshot read from. A refused read throws with the service's own sentence.
    /// </summary>
    /// <exception cref="DriverException">A refused read.</exception>
    public async Task<string> JsonAsync(bool includeClosed, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new ListSessions(IncludeClosed: includeClosed ? true : null), ct).ConfigureAwait(false);
        sent.Answered();
        return sent.Reply.Payload;
    }

    /// <summary>
    /// One record as the list answers it, open or closed (<c>GET /api/sessions/{id}</c>), the text the readers read from; null where the
    /// service has no record of that id.
    /// </summary>
    /// <exception cref="DriverException">A refused read other than none.</exception>
    public async Task<string?> OneJsonAsync(string id, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new ReadSession(id), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        sent.Answered();
        return sent.Reply.Payload;
    }

    internal async Task<SessionResponse[]> ListedAsync(bool includeClosed, CancellationToken ct) =>
        (await _service.SendAsync(new ListSessions(IncludeClosed: includeClosed ? true : null), ct).ConfigureAwait(false)).Answered();

    /// <summary>How many sessions failed on each quest (DRV6), without the rest of a snapshot.</summary>
    public async Task<IReadOnlyDictionary<string, int>> StrikesAsync(CancellationToken ct = default) =>
        SessionReads.Strikes(await ListedAsync(true, ct).ConfigureAwait(false));

    /// <summary>
    /// Every session record this machine keeps, closed ones included, with the repository it ran in (WSSETUP6, D124 §4.2). A teammate's
    /// record says nothing of this machine's work, and is left out as the strikes leave it.
    /// </summary>
    public async Task<IReadOnlyList<SessionRun>> RunsAsync(CancellationToken ct = default) =>
    [
        .. (await ListedAsync(true, ct).ConfigureAwait(false))
            .Where(session => !SessionReads.IsTeams(session))
            .Select(session => new SessionRun(session.Id ?? "", session.Repository ?? ""))
            .Where(run => run.Session.Length > 0 && run.Repository.Length > 0),
    ];

    /// <summary>One session record as it stands, an ended one included (FOUNDATION1 m-ii); null where the service has none.</summary>
    public async Task<SessionView?> FindAsync(string id, CancellationToken ct = default) =>
        SessionReads.Views(await ListedAsync(true, ct).ConfigureAwait(false))
            .FirstOrDefault(session => string.Equals(session.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The asking session and every other session of its repository with a tree and a base here, chats included (LINESTOGETHER1c); an active
    /// session is taken as running. Null where the service has no such session or it holds no tree.
    /// </summary>
    public async Task<(RelatedCandidate Mine, IReadOnlyList<RelatedCandidate> Others)?> RelatedCandidatesAsync(string id, CancellationToken ct = default)
    {
        var all = SessionReads.Views(await ListedAsync(true, ct).ConfigureAwait(false));
        var running = SessionReads.Views(await ListedAsync(false, ct).ConfigureAwait(false)).Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var quests = (await _service.EveryQuestAsync(ct).ConfigureAwait(false))
            .ToDictionary(quest => quest.Id, quest => quest.Paths, StringComparer.OrdinalIgnoreCase);
        return RelatedCandidates.Pick(all, running, quest => quests.TryGetValue(quest, out var paths) ? paths : [], id, Directory.Exists);
    }

    /// <summary>This machine's active sessions — the one read an orphan sweep needs, without a whole snapshot.</summary>
    public async Task<IReadOnlyList<SessionView>> ActiveAsync(CancellationToken ct = default) =>
        SessionReads.Views(await ListedAsync(false, ct).ConfigureAwait(false));

    /// <summary>Whether this session took its own quest through its own connector (STANDDOWN2), read at its end.</summary>
    public async Task<bool> TookAsync(string id, CancellationToken ct = default) =>
        (await ListedAsync(true, ct).ConfigureAwait(false)).Any(session => session.Id == id && session.Took);

    /// <summary>
    /// Why the closure graph named for this session's take did not bind it (CLOSETAKE1, FABRIC-CLOSE1o-ii): <c>expired</c>, <c>replaced</c> or
    /// <c>remote</c>; null for a bound take, a take no graph was named for, and a host from before the field.
    /// </summary>
    public async Task<string?> TookBindAsync(string id, CancellationToken ct = default) =>
        (await ListedAsync(true, ct).ConfigureAwait(false)).FirstOrDefault(session => session.Id == id)?.TookBind is { Length: > 0 } why ? why : null;

    /// <summary>The quest a session serves, or null for a conversation's or an intake's (WSR1: what a landing names).</summary>
    public async Task<string?> QuestAsync(string id, CancellationToken ct = default) =>
        (await ListedAsync(true, ct).ConfigureAwait(false)).FirstOrDefault(session => session.Id == id)?.Quest is { Length: > 0 } quest ? quest : null;

    /// <summary>
    /// Where one session ran and what it started from — the two machine-local facts a review needs (SURF6). Both come back only over
    /// loopback: a caller on another machine reads nulls and has nothing to diff, which is the honest answer rather than a refusal.
    /// </summary>
    public async Task<(string? Tree, string? BaseCommit)> GroundAsync(string id, CancellationToken ct = default)
    {
        var session = (await ListedAsync(true, ct).ConfigureAwait(false)).FirstOrDefault(each => each.Id == id);
        return (session?.Tree, session?.BaseCommit);
    }

    /// <summary>The tree a session of this machine's held (D51), closed records included; null where the service holds no such record or says no tree.</summary>
    public async Task<string?> TreeAsync(string session, CancellationToken ct = default) =>
        (await ListedAsync(true, ct).ConfigureAwait(false))
            .Where(record => string.Equals(record.Id, session, StringComparison.OrdinalIgnoreCase))
            .Select(record => record.Tree)
            .FirstOrDefault(tree => tree is { Length: > 0 });

    /// <summary>The downloads' rule's read of this machine's active sessions (KEEP1b, D195 point 6): each id, when it was opened, and the versions its starts were handed.</summary>
    public async Task<IReadOnlyList<LiveSession>> LiveAsync(CancellationToken ct = default) =>
        SessionReads.Running(await ListedAsync(false, ct).ConfigureAwait(false));

    /// <summary>One record as a run that goes on reads it (MSG1c); null when no record of this machine has that id.</summary>
    public async Task<PriorSession?> RecordAsync(string id, CancellationToken ct = default) =>
        SessionReads.Record(await ListedAsync(true, ct).ConfigureAwait(false), id);

    /// <summary>The trees of this machine's records the person's words wait on (AUTOTIDY2d, MSG1b, D137 §2.2).</summary>
    public async Task<IReadOnlyList<string>> WaitingTreesAsync(CancellationToken ct = default) =>
        SessionReads.WaitingTrees(await ListedAsync(true, ct).ConfigureAwait(false));

    /// <summary>This machine's records, open and closed, by id: what the person's <i>Send to its session</i> finds a line's newest part among (COWORK1d).</summary>
    public async Task<IReadOnlyCollection<PriorSession>> RecordsAsync(CancellationToken ct = default) =>
        [.. SessionReads.Records(await ListedAsync(true, ct).ConfigureAwait(false)).Values];

    /// <summary>Every session record, closed ones included, as the service answers them (SESSUX1a, SESSUX1f).</summary>
    public async Task<IReadOnlyList<SessionRecord>> AllAsync(CancellationToken ct = default) =>
        SessionRecords.Parse(await JsonAsync(true, ct).ConfigureAwait(false));
}
