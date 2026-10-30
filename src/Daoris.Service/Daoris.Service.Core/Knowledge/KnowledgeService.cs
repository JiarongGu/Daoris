namespace Daoris.Knowledge;

/// <summary>
/// Which point in a repository's history its fed knowledge came from (D48 §6).
/// </summary>
/// <remarks>
/// <para>Two checkouts of one repository are two points in its history, and the remote must decide
/// which one speaks. This is what lets it: the driver stamps it from git, and the deployment keeps it
/// beside the entries so the next feed can be compared rather than simply believed.</para>
///
/// <para>It is also `claims-need-checks` applied to the index itself. The remote's copy of a
/// repository's knowledge is a <i>claim about a commit</i>; served provenance names the commit, so
/// staleness is something a person can see instead of something they must assume.</para>
/// </remarks>
/// <param name="Commit">The full SHA — short forms collide, and this is an identity.</param>
/// <param name="CommittedAt">Commit time, not feed time: the ordering must be the history's.</param>
/// <param name="Branch">Which line it came from — judged against the repository's declared default.</param>
/// <param name="Origin">Which machine fed it, from the key that carried it. Null where nobody said.</param>
public sealed record FeedProvenance(
    string Commit, DateTimeOffset CommittedAt, string Branch, string? Origin = null)
{
    /// <summary>The form a person reads, in a sentence or a table cell.</summary>
    public string ShortCommit => Clipped.ShortCommit(Commit, 8);

    /// <summary>
    /// The commit this deployment held when the feeding machine asked git whether <see cref="Commit"/>
    /// descends from it (SYNC5a). Null where the machine could not order the two: nothing was held, the
    /// histories diverged, or the client predates the rule — and then commit time decides.
    /// </summary>
    public string? Base { get; init; }

    /// <summary>
    /// What the fed content says, hashed by the deployment (<see cref="FeedDigest"/>) — how the same
    /// commit fed twice is told from the same commit read two ways. Null on a row from before SYNC5a.
    /// </summary>
    public string? Digest { get; init; }
}

/// <summary>
/// How much one repository contributes to the index, and what it was fed from. <paramref name="IndexedAt"/> and
/// <paramref name="IndexedCommit"/> say when this process read the repository's checkout and the commit it was at
/// (KNOWCURRENT1); null where it read none, as a fed repository's or one not read yet.
/// </summary>
public sealed record RepositorySummary(
    string Repository, int Total, int Local, int Canonical, string Workspace = Workspaces.Default,
    FeedProvenance? Fed = null, DateTimeOffset? IndexedAt = null, string? IndexedCommit = null);

/// <summary>Why a knowledge feed was not taken — or <see cref="None"/> when it was.</summary>
public enum FeedRefusal
{
    None,

    /// <summary>The repository has not declared `remote.join`, so nothing of it may leave its machine.</summary>
    NotJoined,

    /// <summary>Joined, but knowledge is a second declaration — its records travel and its lessons stay home.</summary>
    NotSharing,

    /// <summary>The feed named no commit, so nothing could be compared to what is already held.</summary>
    NoProvenance,

    /// <summary>
    /// Fed from a line that is not the repository's canonical one. A feature-branch checkout is work
    /// in flight: unmerged lessons are not yet the family's (D48 §6).
    /// </summary>
    NotDefaultBranch,

    /// <summary>
    /// Fed from a commit older than the one the deployment already holds. Not a problem — information:
    /// the index keeps the newer view, and the machine that is behind is simply behind.
    /// </summary>
    Stale,

    /// <summary>
    /// The feeding machine checked its ancestry against a commit another machine has since replaced
    /// (SYNC5a). Information: the next pass asks git again, against what is held then.
    /// </summary>
    Moved,

    /// <summary>
    /// The commit held, read differently (SYNC0c). The first reading stands, as information: two
    /// readings of one commit are two tools disagreeing, and flapping between them helps nobody.
    /// </summary>
    ContentDiffers,

    /// <summary>What was fed breaks the rules of its own shape — a code map judged whole at the door.</summary>
    Malformed,

    /// <summary>
    /// A declaration naming no commit, where one naming a commit is held (SYNC5b). Nothing orders the
    /// two, so the named one stands. Information: the repository is registered either way, and its
    /// records and quests travel.
    /// </summary>
    Unordered,
}

/// <param name="Refusal"><see cref="FeedRefusal.None"/> when the feed was taken.</param>
/// <param name="Message">The full answer, phrased once here so no two doors can drift on it.</param>
/// <param name="Entries">How many entries now stand for that repository, when accepted.</param>
public sealed record FeedOutcome(FeedRefusal Refusal, string Message, int Entries)
{
    /// <summary>Whether the feed was taken. Derived, never stored twice: two fields that can disagree
    /// about the same fact eventually do.</summary>
    public bool Accepted => Refusal == FeedRefusal.None;

    /// <summary>
    /// Whether this refusal is something to REPORT rather than to fix (D48 §6).
    /// </summary>
    /// <remarks>
    /// A stale or branch feed is the system working: two machines hold two points in one history, and
    /// the deployment kept the canonical newer one. A sync that logged those as failures would teach
    /// the person to ignore its failures, which is the one thing a report must never do.
    /// </remarks>
    public bool Information =>
        Refusal is FeedRefusal.Stale or FeedRefusal.NotDefaultBranch or FeedRefusal.Moved or FeedRefusal.ContentDiffers
            or FeedRefusal.Unordered;
}

/// <summary>
/// The service, as a client sees it: search, read, list, refresh.
/// </summary>
/// <remarks>
/// Every surface — MCP today, a web API and a desktop shell later — talks to this rather than to the
/// store and the search directly. One place to add caching, tracing or a permission check, instead of
/// three places that have to agree.
///
/// It refreshes on first use if the index is empty, because an empty index that requires a separate
/// setup call is a first run that looks broken.
/// </remarks>
public sealed class KnowledgeService(
    IKnowledgeStore store,
    IKnowledgeSearch search,
    IKnowledgeSource source,
    IDisclosurePolicy? disclosure = null,
    Lyntai.Inference.IVectorProvider? embedder = null,
    Lyntai.Memory.IVectorStore? vectors = null,
    // Last and optional: a service composed without one still searches and still finds convergence —
    // it simply cannot say who is out there, and reports an empty family rather than refusing to start.
    Registry? registry = null,
    // Where pushed registrations persist. Optional for the same reason as the registry; without it a
    // registration lives only as long as the process, which is fine for a test and wrong for a service.
    RegistrationStore? registrations = null,
    // Whether the source reads the registered roots and nothing else fills the index — a local host,
    // fed by nobody (D47 §4). Then the registry decides what is a ghost, even when a refresh can read
    // nothing (POLISH5). A fed host's empty source says nothing about what it holds.
    bool readsRegisteredRoots = false,
    // The embedder's window (SEM3, D123): the most characters one embedded text carries. The deployment
    // states it, because only the deployment knows which model answers; a longer entry becomes pieces.
    int embedWindow = EntryPieces.DefaultWindow,
    // How old a reading may be before an answer re-reads the source (ORIENT1c): set by a deployment over
    // one checkout, which is merged into while the index persists. Null reads once, at first use, and
    // only into an empty index, as before; set, the first use re-reads whatever the store already held.
    TimeSpan? rereadAfter = null,
    // The clock the window is measured on; the system's unless a test moves one by hand.
    TimeProvider? time = null,
    // The watch of that checkout (FABRIC-EVENT6): while it is in service, a reading is read again when it has been marked
    // stale or has reached the watch's deadline, and the minute above is the fallback of a checkout nothing watches.
    CheckoutFreshness? freshness = null,
    // How a registered root is judged cheaply (KNOWCURRENT1): a host over the registered roots, which names no one checkout,
    // asks this of each root at use, at most once a minute, and reads again only a root whose answer moved. Null judges
    // nothing and reads once, as a fed host does.
    Func<string, RootSignature>? rootSignature = null,
    // Whether a root's record folders differ from its commit, asked once as the root is read (KNOWFRESH1a-ii) and never per
    // hit; null says nothing of it, as a host that cannot ask git does.
    Func<string, bool?>? uncommitted = null,
    // The finished trees' records (KEEPSIZE1c): only a local host is given them, since a record holds a person's words and this machine's names.
    IWorkRecordSearch? records = null,
    // Whether one record file of a root differs from its commit (KNOWCENTRAL2a): a citation's recheck asks it of the cited file
    // alone. Null leaves every recheck of a read reading inconclusive, as a host that cannot ask git.
    Func<string, string, bool?>? fileUncommitted = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>How many rechecks in a row find a cited id gone before its owner's next session is asked to cite it again.</summary>
    public const int GoneTries = 3;

    /// <summary>How often a host over the registered roots judges them (KNOWCURRENT1): the minute a one-checkout server reads again by.</summary>
    public static readonly TimeSpan RootsJudgedEvery = TimeSpan.FromMinutes(1);

    /// <summary>What one registered root was when this process read it: by whom it is named, when, from which commit, by which signature.</summary>
    private sealed record RootReading(string Repository, DateTimeOffset ReadAt, string? Commit, RootSignature? Signature);

    /// <summary>How long a search waits, in all, for the uncommitted readings of the repositories its hits come from before it says "not yet read".</summary>
    public static readonly TimeSpan StandingWait = TimeSpan.FromSeconds(3);

    private readonly Dictionary<string, RootReading> _readings = new(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Each root's uncommitted status, asked once for the commit and signature it was taken at and shared: a background pass walks
    /// the ones a refresh started, and a hit whose stored reading is another's starts its own (KNOWCENTRAL2a). Under <see cref="_readings"/>'s lock.
    /// </summary>
    private readonly Dictionary<string, (string? Commit, RootSignature? Signature, Lazy<Task<bool?>> Dirty)> _probes =
        new(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _judgedAt;

    /// <summary>When the last refresh finished, on <see cref="_time"/>; what the reread window is measured from.</summary>
    private DateTimeOffset _refreshedAt;

    private readonly KnowledgeIndex _index = new(store, disclosure, time);

    /// <summary>
    /// ONE detector, not one per call.
    /// </summary>
    /// <remarks>
    /// It was constructed per request, which threw away the vectors it had just computed — so every
    /// look cost a full re-embed of the corpus, measured at 31 seconds over 449 entries. The interesting
    /// use is a person moving a threshold and looking again, which made that the common path rather than
    /// the rare one.
    /// </remarks>
    private readonly ConvergenceDetector _convergence = new(store, embedder, vectors, embedWindow);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>One feed judged and written at a time (SYNC5a): the check and the write are one step.</summary>
    private readonly SemaphoreSlim _feedGate = new(1, 1);
    private bool _everRefreshed;

    /// <summary>Whether semantic recall is available, which depends on an embedder being configured.</summary>
    public bool SemanticEnabled => embedder is not null && vectors is not null;

    /// <summary>
    /// How many index entries a search that names no kinds answers with (ORIENT2e; the orientation design §3.1),
    /// so a question about why something was decided is not crowded out by rows of identifiers.
    /// </summary>
    public const int IndexEntriesUnasked = 2;

    /// <summary>Every kind but the index: what a search naming no kinds fills its places with once two index entries hold theirs.</summary>
    private static readonly IReadOnlySet<EntryKind> AllButIndex =
        Enum.GetValues<EntryKind>().Where(kind => kind != EntryKind.Index).ToHashSet();

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default) =>
        (await AnswerAsync(query, ct).ConfigureAwait(false)).Hits;

    /// <summary>
    /// Search, and say which tier ANSWERED (TIER1, D24): a door reports this, never
    /// <see cref="SemanticEnabled"/>, which is only what was configured.
    /// </summary>
    /// <remarks>
    /// <para>A composition that is not the hybrid is the lexical search alone — the factory builds nothing
    /// else — so it answers by words, and a failure there propagates as it always has.</para>
    ///
    /// <para>A search that names no kinds answers with the best <see cref="IndexEntriesUnasked"/> index entries, at
    /// the places they rank, and fills the rest from every other kind in the order the same search ranks them
    /// (ORIENT2e): here, in Core, so every door answers one question alike. The answer says when more matched.</para>
    /// </remarks>
    public async Task<SearchAnswer> AnswerAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        var answer = await AnswerEntriesAsync(query, ct).ConfigureAwait(false);
        if (records is null || query.Provenance == Provenance.Canonical) return answer;
        if (query.Kinds is not null && !query.Kinds.Contains(EntryKind.Record)) return answer;

        IReadOnlyList<KnowledgeHit> found;
        try
        {
            found = await records.SearchRecordsAsync(query, ct).ConfigureAwait(false);
        }
        catch (NewerWorkRecordsException)
        {
            return answer;
        }

        if (query.Kinds is null) found = [.. found.Take(RecordsUnasked)];
        if (found.Count == 0) return answer;

        // Reciprocal rank: the two tables' bm25 scores are never compared, only each hit's place in its own list.
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        var hits = new Dictionary<string, KnowledgeHit>(StringComparer.Ordinal);
        foreach (var list in new[] { answer.Hits, found })
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                var id = list[rank].Entry.Id;
                scores[id] = scores.GetValueOrDefault(id) + 1.0 / (RankConstant + rank + 1);
                hits.TryAdd(id, list[rank]);
            }
        }

        var merged = scores.OrderByDescending(pair => pair.Value).Take(query.Limit).Select(pair => hits[pair.Key] with { Score = pair.Value }).ToList();
        return answer with { Hits = merged };
    }

    /// <summary>How many records a search that names no kinds answers with, as <see cref="IndexEntriesUnasked"/> caps the index's entries.</summary>
    public const int RecordsUnasked = 3;

    private const int RankConstant = 60;

    private async Task<SearchAnswer> AnswerEntriesAsync(KnowledgeQuery query, CancellationToken ct)
    {
        RefuseNewerIndex();
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        var answer = await AnswerAnyAsync(query, ct).ConfigureAwait(false);
        if (query.Kinds is not null) return answer;

        var kept = new List<KnowledgeHit>();
        var index = 0;
        foreach (var hit in answer.Hits)
        {
            if (hit.Entry.Kind == EntryKind.Index && ++index > IndexEntriesUnasked) continue;
            kept.Add(hit);
        }
        if (index <= IndexEntriesUnasked) return answer;

        // What ranks below the window: every other kind, in the order the same search gives it. An entry the
        // window already holds keeps its place; the rest come after, as they would have ranked.
        var rest = await AnswerAnyAsync(query with { Kinds = AllButIndex }, ct).ConfigureAwait(false);
        var held = kept.Select(hit => hit.Entry.Id).ToHashSet(StringComparer.Ordinal);
        kept.AddRange(rest.Hits.Where(hit => held.Add(hit.Entry.Id)).Take(Math.Max(0, query.Limit - kept.Count)));

        var failures = new[] { answer.Failure, rest.Failure }.OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        return new SearchAnswer(
            kept, answer.Lexical && rest.Lexical, answer.Semantic && rest.Semantic,
            failures.Count == 0 ? null : string.Join("; ", failures), MoreIndex: true);
    }

    /// <summary>
    /// The index's refusal, before an operation on it does anything else (KSCHEMA1): a search's tiers would each fold
    /// it into a failure and answer nothing, and a feed or a retire would write its other half first.
    /// </summary>
    private void RefuseNewerIndex()
    {
        if (store.Refusal is { } refused) throw refused.Again();
    }

    private async Task<SearchAnswer> AnswerAnyAsync(KnowledgeQuery query, CancellationToken ct) =>
        search is IAnsweringSearch answering
            ? await answering.AnswerAsync(query, ct).ConfigureAwait(false)
            : new SearchAnswer(await search.SearchAsync(query, ct).ConfigureAwait(false), Lexical: true, Semantic: false);

    public async Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default)
    {
        if (id.StartsWith("record:", StringComparison.Ordinal))
        {
            if (records is null) return null;
            try
            {
                return await records.FindRecordAsync(id, ct).ConfigureAwait(false);
            }
            catch (NewerWorkRecordsException)
            {
                return null;
            }
        }

        RefuseNewerIndex();
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return await store.FindAsync(id, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// What each entry's reading and, for a decision, its own record say of its standing, by entry id (KNOWFRESH1a-ii).
    /// </summary>
    /// <remarks>
    /// <para>🔴 The entry, its file's entries and its repository's reading are read together, from the index every connector
    /// writes (KNOWCENTRAL2a): a reading this process kept beside marks another connector wrote said one commit over another's
    /// record. An entry another write replaced since it was found, or one whose reading names no commit, says <c>unknown</c>.
    /// The commit is the stored reading's, read or fed, and costs no git call; the uncommitted status is asked once for that
    /// reading's commit and signature. A decision's marks are derived from its file's note entries
    /// (<see cref="DecisionNotes.Marks(IEnumerable{KnowledgeEntry})"/>), so a fed host derives what a local one does.</para>
    ///
    /// <para>A decision whose file has no entry of its own before its notes (a section of one decisions log) has none to
    /// place its notes by, and says its reading alone.</para>
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, EntryStanding>> StandingsAsync(
        IEnumerable<KnowledgeEntry> entries, CancellationToken ct = default)
    {
        RefuseNewerIndex();
        var asked = entries.ToList();
        var indexed = asked.Where(entry => entry.Kind != EntryKind.Record).Select(entry => entry.Id).ToList();
        var held = indexed.Count == 0
            ? null
            : await store.HeldAsync(indexed, entry => entry.Kind == EntryKind.Decision, ct).ConfigureAwait(false);
        var deadline = Task.Delay(StandingWait, ct);
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

            var decision = held.Files.TryGetValue((now.Repository, now.RelativePath), out var file) ? DecisionStandingOf(now, file) : null;
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
        if (RootOf(reading.Repository) is not { } root || ProbeOf(root, reading.Commit, reading.Signature) is not { } probe) return default;
        var task = probe.Value;
        if (!task.IsCompleted) await Task.WhenAny(task, deadline).ConfigureAwait(false);
        if (!task.IsCompleted) return (false, true, false);
        return task.Result is { } dirty ? (dirty, false, false) : (false, false, true);
    }

    /// <summary>The root's status for this commit and signature: the one already asked, or one started now; null without a seam or a commit.</summary>
    private Lazy<Task<bool?>>? ProbeOf(string root, string? commit, RootSignature? signature)
    {
        if (commit is null || uncommitted is null) return null;
        lock (_readings)
        {
            if (_probes.TryGetValue(root, out var held) && held.Commit == commit && held.Signature == signature) return held.Dirty;
            var probe = Probe(root);
            _probes[root] = (commit, signature, probe);
            return probe;
        }
    }

    /// <summary>One status of a root, started when first asked for; a git that throws answers nothing.</summary>
    private Lazy<Task<bool?>> Probe(string root) => new(() => Task.Run<bool?>(() =>
    {
        try
        {
            return uncommitted!(root);
        }
        catch (Exception)
        {
            return null;
        }
    }));

    /// <summary>The root a registered repository's checkout is at on this machine, or null where none is read here.</summary>
    private string? RootOf(string repository) =>
        readsRegisteredRoots
            ? LocalRoots().Where(r => string.Equals(r.Repository, repository, StringComparison.Ordinal)).Select(r => r.Root).FirstOrDefault()
            : null;

    /// <summary>A decision entry's notes after it and the marks that target it, from the entries of its file; null where they cannot be placed.</summary>
    private static DecisionStanding? DecisionStandingOf(KnowledgeEntry entry, IReadOnlyList<KnowledgeEntry> file) =>
        MarksOn(entry, file) is not { } placed
            ? null
            : new DecisionStanding(
                placed.Later,
                [.. placed.Targeted.Select(mark => $"{mark.Kind} by {(mark.By is { } by ? $"D{by}" : "a later note here")}")]);

    /// <summary>How many notes of its file follow a decision entry, and the marks that target it in line order; null where they cannot be placed.</summary>
    private static (int Later, IReadOnlyList<NoteMark> Targeted)? MarksOn(KnowledgeEntry entry, IReadOnlyList<KnowledgeEntry> file)
    {
        var decision = file.Where(held => held.Kind == EntryKind.Decision).ToList();
        if (decision.All(held => held.Anchor is not null)) return null;

        var notes = decision.Where(held => held.Anchor is not null && held.Lines is not null).ToList();
        var marks = DecisionNotes.Marks(notes);
        IEnumerable<NoteMark> targeted;
        int later;
        if (entry.Anchor is null)
        {
            targeted = marks.Where(mark => mark.Target is null && mark.By is not null);
            later = notes.Count;
        }
        else if (entry.Lines is { } own)
        {
            targeted = marks.Where(mark => mark.Target == own.First);
            later = notes.Count(note => note.Lines!.Value.First > own.First);
        }
        else
        {
            return null;
        }

        return (later, [.. targeted.OrderBy(mark => mark.Line)]);
    }

    /// <summary>
    /// Cite an entry at the reading the index stores for it (KNOWCENTRAL2a; the memory coordinator design's §5): its commit, read
    /// or fed, and its body's digest, the root read again first where it moved. An entry its own record already supersedes is
    /// refused, naming what superseded it.
    /// </summary>
    /// <param name="lines">The span a hit named, which must be lines of the entry; null cites it whole.</param>
    public async Task<CitationAnswer> CiteAsync(string id, LineSpan? lines = null, CancellationToken ct = default)
    {
        RefuseNewerIndex();
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        await ReadAgainWhereMovedAsync(RepositoryOfId(id), ct).ConfigureAwait(false);
        var held = await store.HeldAsync([id], entry => entry.Kind == EntryKind.Decision, ct).ConfigureAwait(false);
        if (!held.Entries.TryGetValue(id, out var entry)) return new(null, $"No entry with id `{id}` is indexed. Ids come from `knowledge_search`.");
        if (lines is { } range && entry.Cut(range) is null) return new(null, $"Lines {range} are not lines of `{id}`, as a hit names them.");
        if (!held.Readings.TryGetValue(entry.Repository, out var reading) || reading.Commit is null)
        {
            return new(null, $"No reading of `{entry.Repository}` names a commit, so nothing of it can be cited yet.");
        }

        if (Marked(entry, held, "superseded") is { } superseded)
        {
            var successor = superseded.By is { } by ? $"D{by}" : null;
            return new(null, $"`{id}` is already superseded by {successor ?? "a later note of its own record"}: cite that instead.", successor);
        }

        return new(new Citation(id, lines, reading.Commit, reading.Fed, FeedDigest.Of(entry.Body)));
    }

    /// <summary>
    /// Whether a citation still holds (KNOWCENTRAL2a; the memory coordinator design's §5). The root's signature is compared with
    /// the stored one and the root read again where it moved; the entry, its file's marks and the reading are read in one
    /// transaction; then the cited file's own state is asked against that commit, and the signature again. No in-process gate is
    /// taken but a moved root's read.
    /// </summary>
    /// <param name="attempt">How many rechecks in a row this one is: a cited id found gone asks to be cited again at <see cref="GoneTries"/>.</param>
    public async Task<CitationCheck> RecheckAsync(Citation citation, int attempt = 1, CancellationToken ct = default)
    {
        RefuseNewerIndex();
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        var root = await ReadAgainWhereMovedAsync(RepositoryOfId(citation.Id), ct).ConfigureAwait(false);
        var held = await store.HeldAsync([citation.Id], entry => entry.Kind == EntryKind.Decision, ct).ConfigureAwait(false);
        if (!held.Entries.TryGetValue(citation.Id, out var entry))
        {
            var again = attempt >= GoneTries;
            return Inconclusive($"`{citation.Id}` is no longer indexed{(again ? "; cite it again" : "")}", null, again);
        }

        if (!held.Readings.TryGetValue(entry.Repository, out var reading) || reading.Commit is null)
        {
            return Inconclusive($"no reading of `{entry.Repository}` names a commit", null);
        }

        if (FeedDigest.Of(entry.Body) != citation.Digest) return Inconclusive("its body changed since it was cited; cite it again", reading, citeAgain: true);

        // A fed reading answers for its fed commit; a read one, for its file as that commit has it, here.
        if (!reading.Fed)
        {
            if (root is null || fileUncommitted is null) return Inconclusive("its file's state cannot be asked here", reading);
            bool? modified;
            try
            {
                modified = fileUncommitted(root, entry.RelativePath);
            }
            catch (Exception)
            {
                modified = null;
            }

            if (modified is null) return Inconclusive("its file's state is unknown", reading);
            if (modified == true) return Inconclusive("its file has changes not committed", reading);
            var moved = rootSignature is null ? RootSignature.Head(root) != reading.Commit : rootSignature(root) != reading.Signature;
            if (moved) return Inconclusive("its root moved during the check", reading);
        }

        if (Marked(entry, held, "superseded") is { } superseded)
        {
            var by = superseded.By is { } failed ? $"D{failed}" : null;
            return new(CitationStanding.Failed, $"superseded by {by ?? "a later note of its own record"}", by, reading.Commit, reading.Fed);
        }

        if (Marked(entry, held, "amended") is { } amended)
        {
            var by = amended.By is { } later ? $"D{later}" : null;
            return new(CitationStanding.Amended, $"cited record amended by {by ?? "a later note of its own record"}", by, reading.Commit, reading.Fed);
        }

        return new(CitationStanding.Holds, $"holds, {(reading.Fed ? "fed" : "read")} at {Short(reading.Commit)}", null, reading.Commit, reading.Fed);

        static CitationCheck Inconclusive(string why, IndexReading? reading, bool citeAgain = false) =>
            new(CitationStanding.Inconclusive, $"inconclusive: {why}", null, reading?.Commit, reading?.Fed ?? false, citeAgain);
    }

    /// <summary>The first mark of this kind its own record puts on a decision entry, or null.</summary>
    private static NoteMark? Marked(KnowledgeEntry entry, HeldTogether held, string kind) =>
        held.Files.TryGetValue((entry.Repository, entry.RelativePath), out var file) && MarksOn(entry, file) is { } placed
            ? placed.Targeted.FirstOrDefault(mark => mark.Kind == kind)
            : null;

    /// <summary>The repository an entry id names: what stands before its first colon.</summary>
    private static string RepositoryOfId(string id) => id.IndexOf(':') is var colon and > 0 ? id[..colon] : id;

    /// <summary>
    /// The root a repository's checkout is at here, read again first where its signature moved from the stored reading's
    /// (KNOWCENTRAL2a); null where no checkout of it is read here.
    /// </summary>
    private async Task<string?> ReadAgainWhereMovedAsync(string repository, CancellationToken ct)
    {
        await ReloadRegistryAsync(ct).ConfigureAwait(false);
        if (RootOf(repository) is not { } root) return null;
        if (rootSignature is null) return root;

        if ((await store.ReadingAsync(repository, ct).ConfigureAwait(false))?.Signature != rootSignature(root))
        {
            await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await RefreshReadingAsync(ct, [root]).ConfigureAwait(false);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        return root;
    }

    /// <param name="workspace">The circle to summarize (D48). Null is every one this machine holds.</param>
    /// <remarks>
    /// Carries each repository's fed provenance where there is one (D48 §6) — served, never implied.
    /// A deployment that scans its own checkouts has none, and says so by absence: what it shows is
    /// the machine's own state, which is the one thing it cannot be stale about.
    /// </remarks>
    public async Task<IReadOnlyList<RepositorySummary>> SummarizeAsync(
        string? workspace = null, CancellationToken ct = default)
    {
        RefuseNewerIndex();
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        var all = await store.AllAsync(ct).ConfigureAwait(false);
        // One read for the whole table rather than one per repository: it is a small table, and the
        // alternative is a query inside a projection.
        var fed = registrations is null
            ? new Dictionary<string, FeedProvenance>(StringComparer.OrdinalIgnoreCase)
            : await registrations.AllProvenanceAsync(ct).ConfigureAwait(false);

        var read = Readings().ToDictionary(r => r.Repository, StringComparer.Ordinal);
        return all
            .Where(e => workspace is null || Workspaces.Same(workspace, e.Workspace))
            .GroupBy(e => e.Repository, StringComparer.Ordinal)
            .Select(g => new RepositorySummary(
                g.Key,
                g.Count(),
                g.Count(e => e.Provenance == Provenance.Local),
                g.Count(e => e.Provenance == Provenance.Canonical),
                g.Select(e => e.Workspace).First(),
                fed.TryGetValue(g.Key, out var provenance) ? provenance : null,
                read.TryGetValue(g.Key, out var reading) ? reading.ReadAt : null,
                reading?.Commit))
            .OrderByDescending(r => r.Total)
            .ToList();
    }

    /// <summary>
    /// Where different repositories learned the same lesson independently.
    /// </summary>
    /// <remarks>
    /// Works with or without an embedder. Without one it still finds identical copies and
    /// restatements; with one it also finds convergence. A feature that returned nothing without an
    /// optional dependency would have made that dependency mandatory in all but name.
    /// </remarks>
    public async Task<IReadOnlyList<ConvergenceCandidate>> FindConvergenceAsync(
        ConvergenceOptions? options = null, CancellationToken ct = default)
    {
        RefuseNewerIndex();
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return await _convergence.FindAsync(options, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Who is out there, what each owns, and what is worth asking of them.
    /// </summary>
    /// <remarks>
    /// Read from each repository's own manifest rather than configured here, so the declaration sits
    /// next to the thing it describes and is reviewed by the people it describes. It is also the gate
    /// on addressing a quest: a repository with no manifest has no client to see one, and a quest
    /// nobody can read looks exactly like a quest that was read and ignored.
    /// </remarks>
    /// <param name="workspace">
    /// The circle to answer for (D48). Null is every workspace this machine holds — right for the
    /// judgement that needs both sides (<see cref="QuestExchange"/>), and something a door showing a
    /// person must narrow or say it did not.
    /// </param>
    public async Task<IReadOnlyList<Registration>> RegistryAsync(
        string? workspace = null, CancellationToken ct = default)
    {
        await ReloadRegistryAsync(ct).ConfigureAwait(false);
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        // A quest is addressed by the registry, so it answers over an index a newer Daoris wrote, counting nothing in
        // it (KSCHEMA1): each row's entries read none.
        var counts = store.Refusal is null
            ? await store.CountByRepositoryAsync(ct).ConfigureAwait(false)
            : new Dictionary<string, int>();

        var all = registry?.Read(counts) ?? [];
        return workspace is null
            ? all
            : all.Where(r => Workspaces.Same(r.InWorkspace, workspace)).ToList();
    }

    /// <summary>
    /// Take a repository off the map (D48 §3).
    /// </summary>
    /// <remarks>
    /// <b>Nothing on disk is touched.</b> This ends a registration: the repository stops being
    /// addressable and stops being indexed here, and its entries leave the index now. Its files, its
    /// history and its doctrine are its own — the one thing a person must be able
    /// to trust about a remove is what it does not do.
    /// </remarks>
    /// <returns>Whether there was a registration to retire; false is an answer, not a failure.</returns>
    public async Task<bool> RetireAsync(string repository, CancellationToken ct = default)
    {
        // Refused whole over an index a newer Daoris wrote (KSCHEMA1): the registration alone would go, and the index
        // would keep serving what was taken off the map.
        RefuseNewerIndex();
        var stored = registrations is not null
            && await registrations.DeleteAsync(repository, ct).ConfigureAwait(false);
        var known = registry?.Retire(repository) ?? false;

        // 🔴 Its entries go with it (REV3). "They leave on the next refresh" held only for a host that
        // refreshes — a fed deployment never does, so it served a retired repository's knowledge to
        // every keyed caller for as long as it ran. It reads no source, so it always writes, moving the generation a
        // refresh begun before it read (KNOWCENTRAL2a).
        if (stored || known)
        {
            await store.ReplaceRepositoryAsync(repository, [], ReadingWrite.Taken(_time.GetUtcNow()), ct: ct).ConfigureAwait(false);
        }

        return stored || known;
    }

    /// <summary>
    /// Register everything a folder's subdirectories propose (D48 §3) — the bootstrap, run deliberately.
    /// </summary>
    /// <remarks>
    /// The scan proposes no workspace, so re-importing a folder never re-points a repository someone
    /// wired: unstated is preserved by the upsert. Existing rows are updated from their manifests,
    /// which is what makes `import` the right answer to "I edited several declarations at once".
    /// </remarks>
    /// <param name="workspace">
    /// The person's word for which circle every row lands in (D77) — "set this folder up as a
    /// workspace" as one statement. A statement re-points, as `connect --workspace` does; null is
    /// silence, and silence moves nobody.
    /// </param>
    /// <returns>The registrations as they now stand, in name order.</returns>
    public async Task<IReadOnlyList<Registration>> ImportAsync(
        string folder, DateTimeOffset now, string? workspace = null, CancellationToken ct = default)
    {
        var stated = string.IsNullOrWhiteSpace(workspace) ? null : Workspaces.Normalize(workspace);
        var imported = new List<Registration>();
        foreach (var proposal in RegistryImport.Propose(folder))
        {
            imported.Add(await RegisterAsync(
                stated is null ? proposal : proposal with { Workspace = stated }, now, ct).ConfigureAwait(false));
        }

        return imported;
    }

    /// <summary>
    /// The workspace a repository is wired to on this machine, or the default when nobody has said.
    /// </summary>
    /// <remarks>
    /// The ambient scope every door resolves before it asks anything: a session asking "has anyone
    /// solved this" means its own circle (design §4), and its own circle is a registry row — not
    /// anything in its tree.
    /// </remarks>
    public async Task<string> WorkspaceOfAsync(string repository, CancellationToken ct = default) =>
        (await RegistryAsync(ct: ct).ConfigureAwait(false))
            .Named(repository)
            ?.InWorkspace
        ?? Workspaces.Default;

    /// <summary>
    /// Record what a repository said about itself, when it told us rather than we found it.
    /// </summary>
    /// <remarks>
    /// <para>Persisted before it is served: a pushed registration is the only registration a remote
    /// service has, and one that evaporates on restart looks exactly like a repository that never
    /// connected.</para>
    ///
    /// <para>What is served is what the store DECIDED, not what arrived — because the workspace is
    /// preserved rather than overwritten when a registration says nothing about it (D48 §2). Serving
    /// the incoming record instead would re-point every repository to `default` in memory the moment
    /// an ordinary sync tick re-registered it, and the store on disk would keep saying otherwise.</para>
    /// </remarks>
    /// <returns>The registration as it now stands, workspace resolved.</returns>
    public async Task<Registration> RegisterAsync(
        Registration registration, DateTimeOffset now, CancellationToken ct = default)
    {
        // Where it stood before, so a move is known as one (OVERVIEWNOISE1d).
        var before = registrations is not null
            ? (await registrations.AllAsync(ct).ConfigureAwait(false)).Named(registration.Repository)
            : (registry?.Read() ?? []).Named(registration.Repository);

        var effective = registrations is not null
            ? await registrations.UpsertAsync(registration, now, ct).ConfigureAwait(false)
            : registration with { Workspace = registration.InWorkspace };

        registry?.Register(effective);
        if (before is not null && !Workspaces.Same(before.InWorkspace, effective.InWorkspace))
        {
            await RestampAsync(effective.Repository, effective.InWorkspace, ct).ConfigureAwait(false);
        }

        return effective;
    }

    /// <summary>
    /// A moved repository's entries carried into its new workspace (OVERVIEWNOISE1d).
    /// </summary>
    /// <remarks>
    /// An entry is stamped with the workspace it was indexed under, and a local host reads its checkouts again only
    /// into an empty store or on request: a repository moved between workspaces kept its old stamp, and the
    /// workspace it moved to counted none of its knowledge. Re-stamped in one write of the store's, which leaves the bodies
    /// as they are and moves the generation, so a reading that took the old wiring is refused after it, whichever
    /// connector took it (KNOWCENTRAL2a); over an index a newer Daoris wrote, nothing is rewritten (KSCHEMA1).
    /// </remarks>
    private async Task RestampAsync(string repository, string workspace, CancellationToken ct)
    {
        if (store.Refusal is not null) return;
        await store.RestampAsync(repository, workspace, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A declaration arriving at a shared deployment from a checkout (SYNC5b): the manifest at a commit,
    /// ordered as a feed is, so two checkouts of one repository stop overwriting each other.
    /// </summary>
    /// <remarks>
    /// Three rules are the registration's own. The first is taken from any line and without a commit,
    /// because nothing else of a repository can travel until it is registered. After that, a
    /// declaration is taken only from the line it calls canonical, because one on a feature branch is
    /// not yet the family's. And one naming no commit cannot be ordered against one that does, so it
    /// does not replace it.
    /// </remarks>
    /// <param name="provenance">The commit the manifest was read at, with the held one git said it
    /// descends from; null when the checkout could not name one.</param>
    /// <returns>The judgement, and the registration as it now stands when it was taken or already held.</returns>
    public async Task<(FeedOutcome Outcome, Registration? Registered)> RegisterFedAsync(
        Registration registration, FeedProvenance? provenance, DateTimeOffset now, CancellationToken ct = default)
    {
        var held = registrations
            ?? throw new InvalidOperationException("a fed registration is held in the registration store, and this service was composed without one");
        var existing = (await RegistryAsync(ct: ct).ConfigureAwait(false))
            .Named(registration.Repository);

        await _feedGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var standing = await held.RegistrationProvenanceAsync(registration.Repository, ct).ConfigureAwait(false);

            if (provenance is null)
            {
                if (standing is not null)
                {
                    return (new(
                        FeedRefusal.Unordered,
                        $"`{registration.Repository}`'s registration is held at `{standing.ShortCommit}`"
                        + $"{(standing.Origin is null ? "" : $", from {standing.Origin}")}, and this one names no commit, "
                        + "so nothing orders the two and the held one stands. It is registered either way: its "
                        + "records and quests still travel.",
                        Entries: 0), null);
                }

                return (new(FeedRefusal.None, $"Registered `{registration.Repository}`.", 0),
                    await RegisterAsync(registration, now, ct).ConfigureAwait(false));
            }

            // The line the declaration calls canonical, so a repository that renamed its default branch
            // is not held to the name it used before; the stored one where this declaration names none.
            var canonical = string.IsNullOrWhiteSpace(registration.DefaultBranch)
                ? existing?.DefaultBranch
                : registration.DefaultBranch.Trim();
            if (standing is not null
                && canonical is { Length: > 0 }
                && !string.Equals(provenance.Branch, canonical, StringComparison.Ordinal))
            {
                return (new(
                    FeedRefusal.NotDefaultBranch,
                    $"`{registration.Repository}`'s registration came from `{provenance.Branch}`, and its canonical "
                    + $"line is `{canonical}` — a declaration on another line is not yet the family's, so the one "
                    + $"held at `{standing.ShortCommit}` stands. Its records and quests still travel from this checkout.",
                    Entries: 0), null);
            }

            var arriving = provenance with { Digest = FeedDigest.Of(registration) };
            var verdict = FeedOrder.Judge(standing, arriving);
            if (verdict == FeedVerdict.AlreadyHeld)
            {
                return (new(
                    FeedRefusal.None,
                    $"`{registration.Repository}`'s registration is already held at `{arriving.ShortCommit}` as declared.",
                    Entries: 0), existing);
            }

            if (verdict != FeedVerdict.Take)
            {
                return (NotTaken(verdict, registration.Repository, "registration", standing!, arriving), null);
            }

            // Over an index a newer Daoris wrote (KSCHEMA1), a declaration that stops sharing what it shared is refused
            // whole, since what it fed could not be taken back; one that never shared has nothing there to take.
            var withdraws = !(registration.Joined && registration.SharesKnowledge);
            if (withdraws && existing is { Joined: true, SharesKnowledge: true }) RefuseNewerIndex();

            var registered = await RegisterAsync(registration, now, ct).ConfigureAwait(false);

            // 🔴 A declaration that no longer joins-and-shares takes back what it fed while it did (REV3):
            // from here its knowledge stays home, and a deployment that kept serving it would be the
            // disclosure the declaration was written to prevent. Its held commit goes too, or sharing
            // again from that commit would be "already held" into an index that holds nothing.
            if (!(registered.Joined && registered.SharesKnowledge) && store.Refusal is null)
            {
                await store.ReplaceRepositoryAsync(
                    registered.Repository, [], ReadingWrite.Taken(_time.GetUtcNow()),
                    inside => held.ForgetKnowledgeAsync(registered.Repository, inside), ct).ConfigureAwait(false);
            }

            // After the row: a store that failed between the two must never claim a declaration it does not hold.
            await held.RecordRegistrationProvenanceAsync(registration.Repository, arriving, ct).ConfigureAwait(false);
            return (new(
                FeedRefusal.None,
                $"Registered `{registration.Repository}` as declared at `{arriving.ShortCommit}`.",
                Entries: 0), registered);
        }
        finally
        {
            _feedGate.Release();
        }
    }

    /// <summary>
    /// The repositories this machine's checkouts took out of a circle that has not been told yet (SYNC5b)
    /// — what the sync carries to that circle's deployment.
    /// </summary>
    public async Task<IReadOnlyList<string>> RetiredAsync(string workspace, CancellationToken ct = default) =>
        registrations is null ? [] : await registrations.RetiredAsync(workspace, ct).ConfigureAwait(false);

    /// <summary>The circle has been told, or no longer needs to be.</summary>
    public async Task<bool> ClearRetiredAsync(string repository, string workspace, CancellationToken ct = default) =>
        registrations is not null
        && await registrations.ClearRetiredAsync(repository, workspace, ct).ConfigureAwait(false);

    /// <summary>
    /// One repository's own knowledge, whole — what a sync loop feeds from (D47 §4). Local provenance
    /// only: canonical content is identical everywhere by construction and never feeds.
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeEntry>> LocalEntriesAsync(
        string repository, CancellationToken ct = default)
    {
        RefuseNewerIndex();
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return (await store.AllAsync(ct).ConfigureAwait(false))
            .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
                && entry.Provenance == Provenance.Local)
            .ToList();
    }

    /// <summary>
    /// Accept one repository's knowledge content from a feed — the only ingest a remote deployment has,
    /// since it never scans a filesystem (D47 §4). The disclosure judgement runs HERE, at the door,
    /// over what the repository's own reviewed manifest declared: join admits records, knowledge is a
    /// second declaration, and silence refused both. Accepted entries are normalized — the repository
    /// name from the registration, provenance forced Local, because canonical doctrine is distributed
    /// by `sync`, never by the feed — and replace that repository's entries wholesale, so the feed is
    /// idempotent.
    /// </summary>
    public async Task<FeedOutcome> FeedAsync(
        string repository, IReadOnlyList<KnowledgeEntry> entries, FeedProvenance? provenance = null,
        CancellationToken ct = default)
    {
        RefuseNewerIndex();
        var (registration, refused) = await AdmitAsync(repository, provenance, "knowledge", ct).ConfigureAwait(false);
        if (refused is not null) return refused;

        var normalized = entries
            .Select(entry => entry with
            {
                Repository = registration!.Repository,
                Provenance = Provenance.Local,
                // The RECEIVING deployment's wiring decides the circle, never the feed's claim about it
                // (D48): a feed that could name its own workspace could write itself into someone
                // else's, which is the scoping bug that becomes a disclosure.
                Workspace = registration.InWorkspace,
            })
            .ToList();
        var arriving = provenance! with { Digest = FeedDigest.Of(normalized) };

        // The judgement and the write are one step, so two feeds arriving together cannot both find
        // the same commit held and both take: that is what makes the base a compare-and-swap.
        await _feedGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Read before the held commit is judged, so another connector's write between the judgement and this one refuses it.
            var generations = await store.GenerationsAsync(ct).ConfigureAwait(false);
            var held = registrations is null
                ? null
                : await registrations.ProvenanceAsync(registration!.Repository, ct).ConfigureAwait(false);

            var verdict = FeedOrder.Judge(held, arriving);
            if (verdict == FeedVerdict.AlreadyHeld)
            {
                return new(
                    FeedRefusal.None,
                    $"`{registration!.Repository}` is already held at `{arriving.ShortCommit}` with this content.",
                    normalized.Count);
            }

            if (verdict != FeedVerdict.Take) return NotTaken(verdict, registration!.Repository, "knowledge", held!, arriving);

            // 🔴 The provenance is recorded in the entries' own write (KNOWCENTRAL2a): a fault between the two left new bodies
            // under the old commit, and a store that failed mid-replace claiming a commit it does not hold would refuse the
            // next feed from that commit as a duplicate of work that never happened.
            var named = registration!.Repository;
            var wrote = await store.ReplaceRepositoryAsync(
                named, normalized,
                new ReadingWrite(generations.TryGetValue(named, out var generation) ? generation : 0, arriving.Commit, null, Fed: true, _time.GetUtcNow()),
                registrations is null ? null : inside => registrations.RecordProvenanceAsync(named, arriving, inside),
                ct).ConfigureAwait(false);
            if (!wrote)
            {
                return new(
                    FeedRefusal.Moved,
                    $"`{named}` was written here by another connector while this feed was judged, so nothing of it was "
                    + "taken. The next pass judges it again.",
                    Entries: 0);
            }
        }
        finally
        {
            _feedGate.Release();
        }

        return new(
            FeedRefusal.None,
            $"Indexed {normalized.Count} entries from `{registration.Repository}` at `{arriving.ShortCommit}`.",
            normalized.Count);
    }

    /// <summary>
    /// Accept one repository's code map from a feed (MAP3b) — the map a deployment with no checkout
    /// answers with. The same gates and the same ordering as knowledge, each held at its own commit.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Judged whole again here</b>, by the reader that judges the file on disk: the feeding
    /// machine's reading is a claim, and a map this deployment stores is one it has checked. What is
    /// kept is the map rewritten in its canonical form, so its digest is over what it says.</para>
    ///
    /// <para>A null map is the repository saying it keeps none at that commit. The held map goes and
    /// the commit stays, so a checkout from before the deletion cannot bring the map back.</para>
    /// </remarks>
    /// <param name="file">Which candidate the map was read from; ignored when there is no map.</param>
    /// <param name="map">The file's text, or null when the checkout keeps no map.</param>
    public async Task<FeedOutcome> FeedCodeMapAsync(
        string repository, string? file, string? map, FeedProvenance? provenance, CancellationToken ct = default)
    {
        var (registration, refused) = await AdmitAsync(repository, provenance, "a code map", ct).ConfigureAwait(false);
        if (refused is not null) return refused;

        // A composition without the store has nowhere to hold what was fed. Every real one has it
        // (ServiceFactory), so this is a wiring mistake, said as one.
        var held = registrations
            ?? throw new InvalidOperationException("a fed code map is held in the registration store, and this service was composed without one");

        string? body = null;
        string? name = null;
        var modules = 0;
        if (map is not null)
        {
            if (file is null || !CodeMapReader.Candidates.Contains(file, StringComparer.Ordinal))
            {
                return new(
                    FeedRefusal.Malformed,
                    $"`{registration!.Repository}` fed a code map from `{file}`, and a code map is read from "
                    + $"{string.Join(" or ", CodeMapReader.Candidates.Select(c => $"`{c}`"))} — nowhere else.",
                    Entries: 0);
            }

            if (System.Text.Encoding.UTF8.GetByteCount(map) > CodeMapReader.MaxBytes)
            {
                return new(
                    FeedRefusal.Malformed,
                    $"`{file}` is over {CodeMapReader.MaxBytes / 1024 / 1024} MB — a code map is a small file.",
                    Entries: 0);
            }

            var (parsed, problem) = CodeMapReader.Parse(map, file);
            if (problem is not null) return new(FeedRefusal.Malformed, problem, Entries: 0);

            body = CodeMapReader.Write(parsed!);
            name = file;
            modules = parsed!.Modules.Count;
        }

        var arriving = provenance! with { Digest = FeedDigest.Of($"{name}\n{body}") };

        await _feedGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var standing = await held.CodeMapProvenanceAsync(registration!.Repository, ct).ConfigureAwait(false);
            var verdict = FeedOrder.Judge(standing, arriving);
            if (verdict == FeedVerdict.AlreadyHeld)
            {
                return new(
                    FeedRefusal.None,
                    $"`{registration.Repository}`'s code map is already held at `{arriving.ShortCommit}` as it reads.",
                    modules);
            }

            if (verdict != FeedVerdict.Take) return NotTaken(verdict, registration.Repository, "code map", standing!, arriving);

            await held.RecordCodeMapAsync(registration.Repository, name, body, arriving, ct).ConfigureAwait(false);
        }
        finally
        {
            _feedGate.Release();
        }

        return new(
            FeedRefusal.None,
            body is null
                ? $"`{registration.Repository}` keeps no code map at `{arriving.ShortCommit}`; none is held here now."
                : $"Held `{registration.Repository}`'s code map ({modules} modules) at `{arriving.ShortCommit}`.",
            modules);
    }

    /// <summary>
    /// Which commit this deployment holds a repository's knowledge, code map and declaration at — what a
    /// feeding machine asks git about before it feeds (SYNC5a, SYNC5b).
    /// </summary>
    public async Task<FeedHeld> HeldAsync(string repository, CancellationToken ct = default) =>
        registrations is null
            ? new FeedHeld(null, null)
            : new FeedHeld(
                (await registrations.ProvenanceAsync(repository, ct).ConfigureAwait(false))?.Commit,
                (await registrations.CodeMapProvenanceAsync(repository, ct).ConfigureAwait(false))?.Commit,
                (await registrations.RegistrationProvenanceAsync(repository, ct).ConfigureAwait(false))?.Commit);

    /// <summary>
    /// The door every feed from a checkout passes: joined, sharing, naming a commit, on the canonical
    /// line. Phrased once here so the knowledge feed and the code map feed cannot drift on it.
    /// </summary>
    /// <param name="what">What was fed, as the refusal says it: "knowledge", "a code map".</param>
    private async Task<(Registration? Registration, FeedOutcome? Refused)> AdmitAsync(
        string repository, FeedProvenance? provenance, string what, CancellationToken ct)
    {
        var registration = (await RegistryAsync(ct: ct).ConfigureAwait(false))
            .Named(repository);

        if (registration is null || !registration.Joined)
        {
            return (null, new(
                FeedRefusal.NotJoined,
                $"`{repository}` has not joined this deployment — the manifest's `remote.join` is the "
                + "declaration that admits it, and silence means local.",
                Entries: 0));
        }

        if (!registration.SharesKnowledge)
        {
            return (null, new(
                FeedRefusal.NotSharing,
                $"`{registration.Repository}` joined without sharing knowledge — its records travel, its "
                + "knowledge and its code map stay home. `remote.knowledge` is the declaration that changes that.",
                Entries: 0));
        }

        // ——— Which point in the history is speaking (D48 §6). Wholesale replacement is the right
        // shape — the index is derived data, and merging two machines' derivations invents a second
        // truth beside git — but it is only safe once the deployment can tell a newer view from an
        // older one. Unguarded, two machines feeding one repository are a flapping generator.
        if (provenance is null)
        {
            return (null, new(
                FeedRefusal.NoProvenance,
                $"`{registration.Repository}` fed {what} without naming a commit. This deployment "
                + "replaces what a repository fed wholesale, so it takes a feed only from a point in "
                + "the history it can compare with what it already holds.",
                Entries: 0));
        }

        if (registration.DefaultBranch is { Length: > 0 } canonical
            && !string.Equals(provenance.Branch, canonical, StringComparison.Ordinal))
        {
            // The branch, the PR and the review already display work in flight better than an index
            // would. Records and quests still travel from any checkout: they are records of activity,
            // not claims of truth.
            return (null, new(
                FeedRefusal.NotDefaultBranch,
                $"`{registration.Repository}` fed {what} from `{provenance.Branch}`, and its canonical "
                + $"line is `{canonical}` — what is unmerged is not yet the family's. Its session records "
                + "and quests still travel from this checkout.",
                Entries: 0));
        }

        return (registration, null);
    }

    /// <summary>A feed the ordering did not take, said as the information it is.</summary>
    private static FeedOutcome NotTaken(
        FeedVerdict verdict, string repository, string what, FeedProvenance held, FeedProvenance arriving)
    {
        var from = held.Origin is null ? "" : $", from {held.Origin}";
        return verdict switch
        {
            FeedVerdict.ContentDiffers => new(
                FeedRefusal.ContentDiffers,
                $"`{repository}`'s {what} is held at `{held.ShortCommit}` as {held.Origin ?? "it was first"} read it, "
                + "and this machine read the same commit differently. The first reading stands: two readings of "
                + "one commit are two tools disagreeing, not two histories.",
                Entries: 0),
            FeedVerdict.Moved => new(
                FeedRefusal.Moved,
                $"`{repository}`'s {what} moved here since this machine checked: it asked git about "
                + $"`{Short(arriving.Base!)}`, and this deployment now holds `{held.ShortCommit}`{from}. "
                + "Nothing is lost — the next pass asks git again.",
                Entries: 0),
            _ => new(
                FeedRefusal.Stale,
                $"`{repository}`'s {what} is already fed from a newer commit "
                // `.UtcDateTime`, because the sentence writes `Z`. A commit time carries the
                // committer's own offset, so formatting it directly printed a local wall clock and
                // called it UTC — an explanation that contradicted the correct ordering underneath it.
                + $"(`{held.ShortCommit}`, {held.CommittedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z{from}) — this feed is from "
                + $"`{arriving.ShortCommit}`, {arriving.CommittedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z, so this "
                + "deployment keeps what it has. Nothing is wrong: this checkout is simply behind.",
                Entries: 0),
        };
    }

    private static string Short(string commit) => Clipped.ShortCommit(commit, 8);

    /// <summary>Re-read every repository and rebuild the index.</summary>
    public async Task<IndexReport> RefreshAsync(CancellationToken ct = default)
    {
        RefuseNewerIndex();
        return (await RefreshUnlessFreshAsync(freshFor: null, ct).ConfigureAwait(false))!;
    }

    /// <summary>
    /// A refresh, or nothing when <paramref name="freshFor"/> names a window the last reading is still inside:
    /// judged under the refresh's own lock, so two answers that find the index stale at once read it once.
    /// </summary>
    private async Task<IndexReport?> RefreshUnlessFreshAsync(TimeSpan? freshFor, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (freshFor is { } window && _everRefreshed)
            {
                var age = _time.GetUtcNow() - _refreshedAt;
                // A watch in service marks what moved and the deadline reconciles what it missed; a checkout nobody
                // watches is read again by the window it was handed.
                var fresh = freshness is { Watching: true }
                    ? !freshness.IsStale && age < CheckoutFreshness.Deadline
                    : age < window;
                if (fresh) return null;
            }

            // Taken as the read begins, so what moves during it marks the next one; given back if the read fails.
            var marked = freshness?.Take() ?? false;
            try
            {
                return await RefreshReadingAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                if (marked) freshness!.MarkStale();
                throw;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>One read of every repository into the index, under <see cref="_refreshLock"/>.</summary>
    /// <param name="onlyRoots">Some roots to read alone, or null for all (KNOWCURRENT1).</param>
    private async Task<IndexReport> RefreshReadingAsync(CancellationToken ct, IReadOnlyList<string>? onlyRoots = null)
    {
        // (The body keeps the block it had inside the lock, so the change to the lock's shape is the whole diff.)
        {
            // 🔴 Each repository's generation, read before the registry and the source (KNOWCENTRAL2a): a write any connector
            // makes after this, a retirement included, refuses this refresh's write of that repository.
            var generations = await store.GenerationsAsync(ct).ConfigureAwait(false);
            await ReloadRegistryAsync(ct).ConfigureAwait(false);

            // A root named before the registry was read again is read only while it is still registered.
            if (onlyRoots is not null && readsRegisteredRoots)
            {
                var registered = LocalRoots().Select(r => r.Root).ToHashSet(_readings.Comparer);
                onlyRoots = [.. onlyRoots.Where(registered.Contains)];
            }

            // What each root is, taken BEFORE it is read, so a write during the read is judged moved at the next use.
            var taken = new List<(string Repository, string Root, RootSignature? Signature, string? Commit, Lazy<Task<bool?>>? Dirty)>();
            if (readsRegisteredRoots)
            {
                foreach (var (repository, root) in LocalRoots())
                {
                    if (onlyRoots is not null && !onlyRoots.Contains(root, _readings.Comparer)) continue;
                    var signature = rootSignature?.Invoke(root);
                    var commit = signature is null ? RootSignature.Head(root) : signature.Commit;
                    // One status a root a reading (KNOWFRESH1a-ii), only for a root that has a commit to differ from. It is not
                    // waited for here: a process start can take seconds a repository, and no answer waits on a repository's status.
                    var dirty = commit is not null && uncommitted is not null ? Probe(root) : null;
                    taken.Add((repository, root, signature, commit, dirty));
                }
            }

            // The wiring, read once per refresh rather than once per entry: it is a small table and the
            // corpus is not. Read BEFORE the scan so every entry of one repository is stamped alike.
            var wiring = (registry?.Read() ?? [])
                .ToDictionary(r => r.Repository, r => r.InWorkspace, StringComparer.OrdinalIgnoreCase);

            var readAt = taken
                .GroupBy(t => t.Repository, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => (g.First().Commit, g.First().Signature), StringComparer.Ordinal);
            var (report, refused) = await _index.RefreshReadingAsync(
                source,
                repository => wiring.TryGetValue(repository, out var workspace)
                    ? workspace
                    : Workspaces.Default,
                readsRegisteredRoots && registry is not null ? wiring.ContainsKey : null,
                onlyRoots, generations,
                repository => readAt.TryGetValue(repository, out var read) ? read : (null, null),
                ct).ConfigureAwait(false);

            // A root whose write was refused keeps no reading here, so its next use reads it again.
            taken.RemoveAll(t => refused.Contains(t.Repository));

            report = report with { Absent = AbsentCheckouts() };

            // Embedding happens here rather than inside the index, because it is the expensive,
            // optional half: the store is usable the moment the refresh returns, and semantic recall
            // arrives when it arrives.
            string? semanticError = null;
            EmbeddingReport? embedded = null;
            if (embedder is not null && vectors is not null)
            {
                try
                {
                    var entries = await store.AllAsync(ct).ConfigureAwait(false);
                    embedded = await SemanticKnowledgeSearch.IndexAsync(entries, embedder, vectors, embedWindow, ct: ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw; // the caller's, not a failure to absorb
                }
                catch (Exception error)
                {
                    // The lexical index is complete and usable. Failing the whole refresh because an
                    // embedding endpoint is unreachable, misconfigured or slow would trade the half
                    // that works for the half that does not — and it did, on the first real run,
                    // against a local server started without embeddings enabled.
                    semanticError = error.Message;
                }
            }

            _everRefreshed = true;
            _refreshedAt = _time.GetUtcNow();
            _judgedAt = _refreshedAt;
            lock (_readings)
            {
                if (onlyRoots is null) _readings.Clear();
                foreach (var (repository, root, signature, commit, dirty) in taken)
                {
                    _readings[root] = new RootReading(repository, _refreshedAt, commit, signature);
                    if (dirty is not null) _probes[root] = (commit, signature, dirty);
                }
            }

            // The statuses are read one at a time behind the answer, so a hit finds its repository's done or asks for it.
            var pending = taken.Select(t => t.Dirty).OfType<Lazy<Task<bool?>>>().ToList();
            if (pending.Count > 0)
            {
                _ = Task.Run(async () =>
                {
                    foreach (var status in pending) await status.Value.ConfigureAwait(false);
                }, CancellationToken.None);
            }

            return report with { SemanticError = semanticError, Embedded = embedded, Read = Readings() };
        }
    }

    /// <summary>
    /// A registered repository's code map (MAP3a), read from its checkout on each ask — the person's
    /// machine showing the person's state, as the local index does (WSP4). Null for a repository
    /// nobody registered. One with no checkout here answers with what was fed (MAP3b), judged again on
    /// the way out, or with no file when nothing was.
    /// </summary>
    public async Task<CodeMapRead?> CodeMapAsync(string repository, CancellationToken ct = default)
    {
        await ReloadRegistryAsync(ct).ConfigureAwait(false);
        var registration = (registry?.Read() ?? []).Named(repository);
        if (registration is null) return null;

        if (!string.IsNullOrWhiteSpace(registration.Root) && Directory.Exists(registration.Root))
        {
            return CodeMapReader.Read(registration.Root);
        }

        // No checkout here: what was fed — at a shared deployment by the machine with the checkout
        // (MAP3b), on a machine by its sync from the circle's remote (MAP3e) — said with where it came
        // from, including a commit that keeps none.
        if (registrations is null) return new CodeMapRead(null, null, null);
        var fed = await registrations.CodeMapProvenanceAsync(registration.Repository, ct).ConfigureAwait(false);
        if (await registrations.FedCodeMapAsync(registration.Repository, ct).ConfigureAwait(false)
            is not { File: { } file, Body: { } body })
        {
            return new CodeMapRead(null, null, null, fed);
        }

        var (map, problem) = CodeMapReader.Parse(body, file);
        return new CodeMapRead(map, file, problem, fed);
    }

    /// <summary>The commit a teammate's code map is held at on this machine, or null where none is (MAP3e).</summary>
    public async Task<string?> FedCodeMapCommitAsync(string repository, CancellationToken ct = default) =>
        registrations is null
            ? null
            : (await registrations.CodeMapProvenanceAsync(repository, ct).ConfigureAwait(false))?.Commit;

    /// <summary>
    /// Hold a teammate's code map as the circle's remote holds it (MAP3e) — already judged whole by
    /// <see cref="CodeMapWire.Read"/>, and digested here over what is kept, as a fed map is.
    /// </summary>
    public async Task HoldFedCodeMapAsync(string repository, FedCodeMap map, CancellationToken ct = default)
    {
        var held = registrations
            ?? throw new InvalidOperationException("a fed code map is held in the registration store, and this service was composed without one");
        await held.RecordCodeMapAsync(
            repository, map.File, map.Body, map.Fed with { Digest = FeedDigest.Of($"{map.File}\n{map.Body}") }, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Stop holding a teammate's code map the remote no longer holds (MAP3e).</summary>
    /// <returns>Whether there was one to forget.</returns>
    public async Task<bool> ForgetFedCodeMapAsync(string repository, CancellationToken ct = default) =>
        registrations is not null && await registrations.ForgetCodeMapAsync(repository, ct).ConfigureAwait(false);

    /// <summary>
    /// The registry as the store holds it NOW, re-read before every answer that reads it (REV3).
    /// </summary>
    /// <remarks>
    /// Every host on a machine opens one store: the desktop's HTTP host, and a connector for each
    /// session. A copy loaded once at start made a retire in one invisible to the others for as long
    /// as they ran, and a session's refresh then swept a repository added after it started. The table
    /// is small, and the corpus the same answers read is not.
    /// </remarks>
    private async Task ReloadRegistryAsync(CancellationToken ct)
    {
        if (registry is null || registrations is null) return;
        registry.Replace(await registrations.AllAsync(ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Registered repositories whose checkout is not where the registry says it is (D48 §3).
    /// </summary>
    /// <remarks>
    /// A row with no path is not an absence — a teammate's mirrored registration has no checkout here
    /// by construction (D47 §9), and reporting it as missing would turn a normal remote family into a
    /// screen of false alarms.
    /// </remarks>
    private IReadOnlyList<string> AbsentCheckouts() =>
        (registry?.Read() ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.Root) && !Directory.Exists(r.Root))
            .Select(r => r.Repository)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Index on first use when there is nothing to search. A persisted index survives restarts, so
    /// this normally costs one cheap count and does nothing.
    /// </summary>
    private async Task EnsureIndexedAsync(CancellationToken ct)
    {
        // An index a newer Daoris wrote is never read into here (KSCHEMA1): no refresh is tried, so none is retried, and
        // the host said why once, at its start.
        if (store.Refusal is not null) return;

        // A source that moves under the store is re-read at first use and once its reading is older than
        // the window (ORIENT1c): what the store held from an earlier process is that process's reading.
        if (rereadAfter is { } window)
        {
            await RefreshUnlessFreshAsync(window, ct).ConfigureAwait(false);
            return;
        }

        // A host over the registered roots judges each by its signature, so a record merged into one is found without
        // anyone telling the index (KNOWCURRENT1): what the store held from an earlier process is read again at first use.
        if (rootSignature is not null && readsRegisteredRoots && registry is not null)
        {
            await KeepRootsCurrentAsync(ct).ConfigureAwait(false);
            return;
        }

        if (_everRefreshed) return;

        var existing = await store.CountByRepositoryAsync(ct).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            await EmbedWhatPersistedAsync(ct).ConfigureAwait(false);
            return;
        }

        await RefreshAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the registered roots again as they move (KNOWCURRENT1). The first use reads them all, since what the store held is an
    /// earlier process's reading. After that each root is judged by its <see cref="RootSignature"/> at most once a
    /// <see cref="RootsJudgedEvery"/>, and only a root whose signature moved is read again; a root that is no longer registered
    /// takes a whole read, which retires what it held.
    /// </summary>
    private async Task KeepRootsCurrentAsync(CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = _time.GetUtcNow();
            if (!_everRefreshed)
            {
                await RefreshReadingAsync(ct).ConfigureAwait(false);
                return;
            }

            if (_judgedAt is { } judged && now - judged < RootsJudgedEvery) return;
            _judgedAt = now;

            await ReloadRegistryAsync(ct).ConfigureAwait(false);
            var registered = LocalRoots();
            var present = registered.Select(r => r.Root).ToHashSet(_readings.Comparer);
            if (_readings.Keys.Any(root => !present.Contains(root)))
            {
                await RefreshReadingAsync(ct).ConfigureAwait(false);
                return;
            }

            var moved = registered
                .Where(r => !_readings.TryGetValue(r.Root, out var held) || held.Signature != rootSignature!(r.Root))
                .Select(r => r.Root)
                .ToList();
            if (moved.Count > 0) await RefreshReadingAsync(ct, moved).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>When and from which commit each registered repository was read, by repository name.</summary>
    private List<RepositoryReading> Readings()
    {
        lock (_readings)
        {
            return _readings.Values
                .Select(r => new RepositoryReading(r.Repository, r.ReadAt, r.Commit))
                .OrderBy(r => r.Repository, StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>The registered roots this machine holds a checkout of, with the repository each is registered as.</summary>
    private List<(string Repository, string Root)> LocalRoots() =>
        (registry?.Read() ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.Root) && Directory.Exists(r.Root))
            .Select(r => (r.Repository, Root: r.Root!))
            .ToList();

    /// <summary>
    /// 🔴 SEM1: the rows persist and the vectors do not. A start over an index already on disk skipped
    /// the refresh, so the semantic half answered nothing until somebody rebuilt the index — while
    /// every door said the tier was semantic. What is on disk is embedded once, on first use, under
    /// the refresh's own lock; the disk is not re-read.
    /// </summary>
    /// <remarks>
    /// Paid once per process, and the MCP host is one per session, so each session's first search
    /// embeds the corpus. A persistent vector store is what removes that cost; this is what makes the
    /// tier true in the meantime. A failure here leaves the lexical half whole, as a refresh's does,
    /// and the next refresh reports the reason.
    /// </remarks>
    private async Task EmbedWhatPersistedAsync(CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_everRefreshed) return;

            if (embedder is not null && vectors is not null)
            {
                try
                {
                    var entries = await store.AllAsync(ct).ConfigureAwait(false);
                    await SemanticKnowledgeSearch.IndexAsync(entries, embedder, vectors, embedWindow, ct: ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // The lexical half is whole; the embedder's reason is a refresh's to report.
                }
            }

            _everRefreshed = true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
