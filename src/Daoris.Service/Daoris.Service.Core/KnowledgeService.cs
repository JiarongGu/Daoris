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
    public string ShortCommit => Commit.Length <= 8 ? Commit : Commit[..8];

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

/// <summary>How much one repository contributes to the index, and what it was fed from.</summary>
public sealed record RepositorySummary(
    string Repository, int Total, int Local, int Canonical, string Workspace = Workspaces.Default,
    FeedProvenance? Fed = null);

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
    TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>When the last refresh finished, on <see cref="_time"/>; what the reread window is measured from.</summary>
    private DateTimeOffset _refreshedAt;

    private readonly KnowledgeIndex _index = new(store, disclosure);

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

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return await search.SearchAsync(query, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Search, and say which tier ANSWERED (TIER1, D24): a door reports this, never
    /// <see cref="SemanticEnabled"/>, which is only what was configured.
    /// </summary>
    /// <remarks>
    /// A composition that is not the hybrid is the lexical search alone — the factory builds nothing
    /// else — so it answers by words, and a failure there propagates as it always has.
    /// </remarks>
    public async Task<SearchAnswer> AnswerAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return search is IAnsweringSearch answering
            ? await answering.AnswerAsync(query, ct).ConfigureAwait(false)
            : new SearchAnswer(await search.SearchAsync(query, ct).ConfigureAwait(false), Lexical: true, Semantic: false);
    }

    public async Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return await store.FindAsync(id, ct).ConfigureAwait(false);
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
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        var all = await store.AllAsync(ct).ConfigureAwait(false);
        // One read for the whole table rather than one per repository: it is a small table, and the
        // alternative is a query inside a projection.
        var fed = registrations is null
            ? new Dictionary<string, FeedProvenance>(StringComparer.OrdinalIgnoreCase)
            : await registrations.AllProvenanceAsync(ct).ConfigureAwait(false);

        return all
            .Where(e => workspace is null || Workspaces.Same(workspace, e.Workspace))
            .GroupBy(e => e.Repository, StringComparer.Ordinal)
            .Select(g => new RepositorySummary(
                g.Key,
                g.Count(),
                g.Count(e => e.Provenance == Provenance.Local),
                g.Count(e => e.Provenance == Provenance.Canonical),
                g.Select(e => e.Workspace).First(),
                fed.TryGetValue(g.Key, out var provenance) ? provenance : null))
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
        var counts = await store.CountByRepositoryAsync(ct).ConfigureAwait(false);

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
        var stored = registrations is not null
            && await registrations.DeleteAsync(repository, ct).ConfigureAwait(false);
        var known = registry?.Retire(repository) ?? false;

        // 🔴 Its entries go with it (REV3). "They leave on the next refresh" held only for a host that
        // refreshes — a fed deployment never does, so it served a retired repository's knowledge to
        // every keyed caller for as long as it ran.
        if (stored || known) await store.ReplaceRepositoryAsync(repository, [], ct).ConfigureAwait(false);
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
        var effective = registrations is not null
            ? await registrations.UpsertAsync(registration, now, ct).ConfigureAwait(false)
            : registration with { Workspace = registration.InWorkspace };

        registry?.Register(effective);
        return effective;
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

            var registered = await RegisterAsync(registration, now, ct).ConfigureAwait(false);

            // 🔴 A declaration that no longer joins-and-shares takes back what it fed while it did (REV3):
            // from here its knowledge stays home, and a deployment that kept serving it would be the
            // disclosure the declaration was written to prevent. Its held commit goes too, or sharing
            // again from that commit would be "already held" into an index that holds nothing.
            if (!(registered.Joined && registered.SharesKnowledge))
            {
                await store.ReplaceRepositoryAsync(registered.Repository, [], ct).ConfigureAwait(false);
                await held.ForgetKnowledgeAsync(registered.Repository, ct).ConfigureAwait(false);
            }

            // After the row, as the knowledge feed records its commit after its entries: a store that
            // failed between the two must never claim a declaration it does not hold.
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

            await store.ReplaceRepositoryAsync(registration!.Repository, normalized, ct).ConfigureAwait(false);

            // Recorded AFTER the entries land, so a store that fails mid-replace never claims a commit
            // it does not hold — the next feed from that commit would then be refused as a duplicate
            // of work that never happened.
            if (registrations is not null)
            {
                await registrations.RecordProvenanceAsync(registration.Repository, arriving, ct)
                    .ConfigureAwait(false);
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

    private static string Short(string commit) => commit.Length <= 8 ? commit : commit[..8];

    /// <summary>Re-read every repository and rebuild the index.</summary>
    public async Task<IndexReport> RefreshAsync(CancellationToken ct = default) =>
        (await RefreshUnlessFreshAsync(freshFor: null, ct).ConfigureAwait(false))!;

    /// <summary>
    /// A refresh, or nothing when <paramref name="freshFor"/> names a window the last reading is still inside:
    /// judged under the refresh's own lock, so two answers that find the index stale at once read it once.
    /// </summary>
    private async Task<IndexReport?> RefreshUnlessFreshAsync(TimeSpan? freshFor, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (freshFor is { } window && _everRefreshed && _time.GetUtcNow() - _refreshedAt < window) return null;

            await ReloadRegistryAsync(ct).ConfigureAwait(false);

            // The wiring, read once per refresh rather than once per entry: it is a small table and the
            // corpus is not. Read BEFORE the scan so every entry of one repository is stamped alike.
            var wiring = (registry?.Read() ?? [])
                .ToDictionary(r => r.Repository, r => r.InWorkspace, StringComparer.OrdinalIgnoreCase);

            var report = await _index.RefreshAsync(
                source,
                repository => wiring.TryGetValue(repository, out var workspace)
                    ? workspace
                    : Workspaces.Default,
                readsRegisteredRoots && registry is not null ? wiring.ContainsKey : null,
                ct).ConfigureAwait(false);

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
            return report with { SemanticError = semanticError, Embedded = embedded };
        }
        finally
        {
            _refreshLock.Release();
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
        // A source that moves under the store is re-read at first use and once its reading is older than
        // the window (ORIENT1c): what the store held from an earlier process is that process's reading.
        if (rereadAfter is { } window)
        {
            await RefreshUnlessFreshAsync(window, ct).ConfigureAwait(false);
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
