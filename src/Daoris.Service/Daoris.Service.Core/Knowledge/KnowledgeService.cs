namespace Daoris.Knowledge;

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
///
/// The service owns the index's reading state and the registry's doors; the search (<see cref="KnowledgeAnswers"/>), the standings
/// (<see cref="KnowledgeStandings"/>), the citations (<see cref="KnowledgeCitations"/>), the feeds (<see cref="KnowledgeFeeds"/>) and
/// the code maps (<see cref="KnowledgeCodeMaps"/>) are types of their own, and their doors here are one line each.
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
    // fed by nobody. Then the registry decides what is a ghost, even when a refresh can read
    // nothing. A fed host's empty source says nothing about what it holds.
    bool readsRegisteredRoots = false,
    // The embedder's window: the most characters one embedded text carries. The deployment
    // states it, because only the deployment knows which model answers; a longer entry becomes pieces.
    int embedWindow = EntryPieces.DefaultWindow,
    // How old a reading may be before an answer re-reads the source: set by a deployment over
    // one checkout, which is merged into while the index persists. Null reads once, at first use, and
    // only into an empty index, as before; set, the first use re-reads whatever the store already held.
    TimeSpan? rereadAfter = null,
    // The clock the window is measured on; the system's unless a test moves one by hand.
    TimeProvider? time = null,
    // The watch of that checkout: while it is in service, a reading is read again when it has been marked
    // stale or has reached the watch's deadline, and the minute above is the fallback of a checkout nothing watches.
    CheckoutFreshness? freshness = null,
    // How a registered root is judged cheaply: a host over the registered roots, which names no one checkout,
    // asks this of each root at use, at most once a minute, and reads again only a root whose answer moved. Null judges
    // nothing and reads once, as a fed host does.
    Func<string, RootSignature>? rootSignature = null,
    // Whether a root's record folders differ from its commit, asked once as the root is read and never per
    // hit; null says nothing of it, as a host that cannot ask git does.
    Func<string, bool?>? uncommitted = null,
    // The finished trees' records: only a local host is given them, since a record holds a person's words and this machine's names.
    IWorkRecordSearch? records = null,
    // Whether one record file of a root differs from its commit: a citation's recheck asks it of the cited file
    // alone. Null leaves every recheck of a read reading inconclusive, as a host that cannot ask git.
    Func<string, string, bool?>? fileUncommitted = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>How many rechecks in a row find a cited id gone before its owner's next session is asked to cite it again.</summary>
    public const int GoneTries = 3;

    /// <summary>How often a host over the registered roots judges them: the minute a one-checkout server reads again by.</summary>
    public static readonly TimeSpan RootsJudgedEvery = TimeSpan.FromMinutes(1);

    /// <summary>What one registered root stood at when this process read it: by whom it is named, when, from which commit, by which signature.</summary>
    private sealed record RootReading(string Repository, DateTimeOffset ReadAt, string? Commit, RootSignature? Signature);

    /// <summary>How long a search waits, in all, for the uncommitted readings of the repositories its hits come from before it says "not yet read".</summary>
    public static readonly TimeSpan StandingWait = TimeSpan.FromSeconds(3);

    private readonly Dictionary<string, RootReading> _readings = new(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

    private readonly RootProbes _probes = new(uncommitted);
    private DateTimeOffset? _judgedAt;

    /// <summary>When the last refresh finished, on <see cref="_time"/>; what the reread window is measured from.</summary>
    private DateTimeOffset _refreshedAt;

    private readonly KnowledgeIndex _index = new(store, disclosure, time);

    /// <summary>
    /// ONE detector, not one per call.
    /// </summary>
    /// <remarks>
    /// One built per request would throw away the vectors it had just computed — so every
    /// look would cost a full re-embed of the corpus, measured at 31 seconds over 449 entries. The interesting
    /// use is a person moving a threshold and looking again, which makes that the common path rather than
    /// the rare one.
    /// </remarks>
    private readonly ConvergenceDetector _convergence = new(store, embedder, vectors, embedWindow);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _feedGate = new(1, 1);
    private bool _everRefreshed;

    private KnowledgeAnswers? _answers;
    private KnowledgeStandings? _standings;
    private KnowledgeCitations? _citations;
    private KnowledgeFeeds? _feeds;
    private KnowledgeCodeMaps? _codeMaps;
    private KnowledgeRegistrar? _registrar;

    private KnowledgeRegistrar Registrar => LazyInitializer.EnsureInitialized(
        ref _registrar, () => new(store, registry, registrations, _time, RefuseNewerIndex, _feedGate))!;

    private KnowledgeAnswers Answers => LazyInitializer.EnsureInitialized(ref _answers, () => new(search, records, RefuseNewerIndex, EnsureIndexedAsync))!;
    private KnowledgeStandings Standings => LazyInitializer.EnsureInitialized(ref _standings, () => new(store, _probes, RootOf, RefuseNewerIndex))!;
    private KnowledgeCitations Citations => LazyInitializer.EnsureInitialized(
        ref _citations, () => new(store, fileUncommitted, rootSignature, RefuseNewerIndex, EnsureIndexedAsync, ReadAgainWhereMovedAsync))!;
    private KnowledgeFeeds Feeds => LazyInitializer.EnsureInitialized(
        ref _feeds, () => new(store, registrations, ct => RegistryAsync(ct: ct), RegisterAsync, RefuseNewerIndex, _time, _feedGate))!;
    private KnowledgeCodeMaps CodeMaps => LazyInitializer.EnsureInitialized(ref _codeMaps, () => new(registry, registrations, ReloadRegistryAsync))!;
    private ReadingBehind? _behind;
    private ReadingBehind Behind => LazyInitializer.EnsureInitialized(
        ref _behind, () => new(async ct => store.Refusal is not null || (await store.CountByRepositoryAsync(ct).ConfigureAwait(false)).Count > 0, EnsureIndexedAsync))!;

    /// <summary>Whether semantic recall is available, which depends on an embedder being configured.</summary>
    public bool SemanticEnabled => embedder is not null && vectors is not null;

    /// <summary>
    /// How many index entries a search that names no kinds answers with,
    /// so a question about why something is decided is not crowded out by rows of identifiers.
    /// </summary>
    public const int IndexEntriesUnasked = 2;

    /// <summary>How many records a search that names no kinds answers with, as <see cref="IndexEntriesUnasked"/> caps the index's entries.</summary>
    public const int RecordsUnasked = 3;

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default) =>
        (await AnswerAsync(query, ct).ConfigureAwait(false)).Hits;

    /// <summary>Search, and say which tier ANSWERED.</summary>
    public Task<SearchAnswer> AnswerAsync(KnowledgeQuery query, CancellationToken ct = default) => Answers.AnswerAsync(query, ct);

    /// <summary>
    /// The index's refusal, before an operation on it does anything else: a search's tiers would each fold
    /// it into a failure and answer nothing, and a feed or a retire would write its other half first.
    /// </summary>
    private void RefuseNewerIndex()
    {
        if (store.Refusal is { } refused) throw refused.Again();
    }

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

    /// <summary>What each entry's reading and, for a decision, its own record say of its standing, by entry id.</summary>
    public Task<IReadOnlyDictionary<string, EntryStanding>> StandingsAsync(
        IEnumerable<KnowledgeEntry> entries, CancellationToken ct = default) => Standings.OfAsync(entries, ct);

    /// <summary>The root a registered repository's checkout is at on this machine, or null where none is read here.</summary>
    private string? RootOf(string repository) =>
        readsRegisteredRoots
            ? LocalRoots().Where(r => string.Equals(r.Repository, repository, StringComparison.Ordinal)).Select(r => r.Root).FirstOrDefault()
            : null;

    /// <summary>
    /// Cite an entry at the reading the index stores for it. An entry its
    /// own record already supersedes is refused, naming what superseded it.
    /// </summary>
    /// <param name="lines">The span a hit named, which must be lines of the entry; null cites it whole.</param>
    public Task<CitationAnswer> CiteAsync(string id, LineSpan? lines = null, CancellationToken ct = default) =>
        Citations.CiteAsync(id, lines, ct);

    /// <summary>Whether a citation still holds.</summary>
    /// <param name="attempt">How many rechecks in a row this one is: a cited id found gone asks to be cited again at <see cref="GoneTries"/>.</param>
    public Task<CitationCheck> RecheckAsync(Citation citation, int attempt = 1, CancellationToken ct = default) =>
        Citations.RecheckAsync(citation, attempt, ct);

    /// <summary>
    /// The root a repository's checkout is at here, read again first where its signature moved from the stored reading's
    /// null where no checkout of it is read here.
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

    /// <param name="workspace">The circle to summarize. Null is every one this machine holds.</param>
    /// <remarks>
    /// Carries each repository's fed provenance where there is one — served, never implied.
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
    public async Task<ConvergenceFound> FindConvergenceAsync(
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
    /// nobody can read looks exactly like a quest that is read and ignored.
    /// </remarks>
    /// <param name="workspace">
    /// The circle to answer for. Null is every workspace this machine holds — right for the
    /// judgement that needs both sides (<see cref="QuestExchange"/>), and something a door showing a
    /// person must narrow or say it did not.
    /// </param>
    public async Task<IReadOnlyList<Registration>> RegistryAsync(
        string? workspace = null, CancellationToken ct = default)
    {
        await ReloadRegistryAsync(ct).ConfigureAwait(false);
        await Behind.ReadyAsync(ct).ConfigureAwait(false);
        // A quest is addressed by the registry, so it answers over an index a newer Daoris wrote, counting nothing in
        // it: each row's entries read none.
        var counts = store.Refusal is null
            ? await store.CountByRepositoryAsync(ct).ConfigureAwait(false)
            : new Dictionary<string, int>();

        var all = registry?.Read(counts) ?? [];
        return workspace is null
            ? all
            : all.Where(r => Workspaces.Same(r.InWorkspace, workspace)).ToList();
    }

    /// <summary>Take a repository off the map; nothing on disk is touched.</summary>
    /// <returns>Whether there is a registration to retire; false is an answer, not a failure.</returns>
    public Task<bool> RetireAsync(string repository, CancellationToken ct = default) => Registrar.RetireAsync(repository, ct);

    /// <summary>
    /// Register everything a folder's subdirectories propose — the bootstrap, run deliberately.
    /// </summary>
    /// <remarks>
    /// The scan proposes no workspace, so re-importing a folder never re-points a repository someone
    /// wired: unstated is preserved by the upsert. Existing rows are updated from their manifests,
    /// which is what makes `import` the right answer to "I edited several declarations at once".
    /// </remarks>
    /// <param name="workspace">
    /// The person's word for which circle every row lands in — "set this folder up as a
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

    /// <summary>Record what a repository said about itself, when it told us rather than we found it.</summary>
    /// <returns>The registration as it now stands, workspace resolved.</returns>
    public Task<Registration> RegisterAsync(Registration registration, DateTimeOffset now, CancellationToken ct = default) =>
        Registrar.RegisterAsync(registration, now, ct);

    /// <summary>A declaration arriving at a shared deployment from a checkout, ordered as a feed is.</summary>
    /// <param name="provenance">The commit the manifest is read at, with the held one git says it
    /// descends from; null when the checkout could not name one.</param>
    /// <returns>The judgement, and the registration as it now stands when it is taken or already held.</returns>
    public Task<(FeedOutcome Outcome, Registration? Registered)> RegisterFedAsync(
        Registration registration, FeedProvenance? provenance, DateTimeOffset now, CancellationToken ct = default) =>
        Feeds.RegisterFedAsync(registration, provenance, now, ct);

    /// <summary>
    /// The repositories this machine's checkouts took out of a circle that has not been told yet
    /// — what the sync carries to that circle's deployment.
    /// </summary>
    public async Task<IReadOnlyList<string>> RetiredAsync(string workspace, CancellationToken ct = default) =>
        registrations is null ? [] : await registrations.RetiredAsync(workspace, ct).ConfigureAwait(false);

    /// <summary>The circle has been told, or no longer needs to be.</summary>
    public async Task<bool> ClearRetiredAsync(string repository, string workspace, CancellationToken ct = default) =>
        registrations is not null
        && await registrations.ClearRetiredAsync(repository, workspace, ct).ConfigureAwait(false);

    /// <summary>
    /// One repository's own knowledge, whole — what a sync loop feeds from. Local provenance
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
    /// since it never scans a filesystem; the disclosure judgement runs at this door.
    /// </summary>
    public Task<FeedOutcome> FeedAsync(
        string repository, IReadOnlyList<KnowledgeEntry> entries, FeedProvenance? provenance = null,
        CancellationToken ct = default) => Feeds.FeedAsync(repository, entries, provenance, ct);

    /// <summary>
    /// Accept one repository's code map from a feed — the map a deployment with no checkout
    /// answers with. A null map is the repository saying it keeps none at that commit.
    /// </summary>
    /// <param name="file">Which candidate the map is read from; ignored when there is no map.</param>
    /// <param name="map">The file's text, or null when the checkout keeps no map.</param>
    public Task<FeedOutcome> FeedCodeMapAsync(
        string repository, string? file, string? map, FeedProvenance? provenance, CancellationToken ct = default) =>
        Feeds.FeedCodeMapAsync(repository, file, map, provenance, ct);

    /// <summary>
    /// Which commit this deployment holds a repository's knowledge, code map and declaration at — what a
    /// feeding machine asks git about before it feeds.
    /// </summary>
    public async Task<FeedHeld> HeldAsync(string repository, CancellationToken ct = default) =>
        registrations is null
            ? new FeedHeld(null, null)
            : new FeedHeld(
                (await registrations.ProvenanceAsync(repository, ct).ConfigureAwait(false))?.Commit,
                (await registrations.CodeMapProvenanceAsync(repository, ct).ConfigureAwait(false))?.Commit,
                (await registrations.RegistrationProvenanceAsync(repository, ct).ConfigureAwait(false))?.Commit);

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
                // watches is read again by the window it is handed.
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
    /// <param name="onlyRoots">Some roots to read alone, or null for all.</param>
    private async Task<IndexReport> RefreshReadingAsync(CancellationToken ct, IReadOnlyList<string>? onlyRoots = null)
    {
        // 🔴 Each repository's generation, read before the registry and the source: a write any connector
        // makes after this, a retirement included, refuses this refresh's write of that repository.
        var generations = await store.GenerationsAsync(ct).ConfigureAwait(false);
        await ReloadRegistryAsync(ct).ConfigureAwait(false);

        // A root named before the registry is read again is read only while it is still registered.
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
                // One status a root a reading, only for a root that has a commit to differ from. It is not
                // waited for here: a process start can take seconds a repository, and no answer waits on a repository's status.
                var dirty = commit is not null && _probes.Asks ? _probes.Probe(root) : null;
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
            onlyRoots is null ? null : readAt.Keys,
            ct).ConfigureAwait(false);

        // A root whose write is refused keeps no reading here, so its next use reads it again.
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
                if (dirty is not null) _probes.Hold(root, commit, signature, dirty);
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

    /// <summary>
    /// A registered repository's code map, read from its checkout on each ask; one with no checkout here
    /// answers with what is fed. Null for a repository nobody registered.
    /// </summary>
    public Task<CodeMapRead?> CodeMapAsync(string repository, CancellationToken ct = default) => CodeMaps.ReadAsync(repository, ct);

    /// <summary>The commit a teammate's code map is held at on this machine, or null where none is.</summary>
    public Task<string?> FedCodeMapCommitAsync(string repository, CancellationToken ct = default) => CodeMaps.FedCommitAsync(repository, ct);

    /// <summary>Hold a teammate's code map as the circle's remote holds it.</summary>
    public Task HoldFedCodeMapAsync(string repository, FedCodeMap map, CancellationToken ct = default) => CodeMaps.HoldFedAsync(repository, map, ct);

    /// <summary>Stop holding a teammate's code map the remote no longer holds.</summary>
    /// <returns>Whether there is one to forget.</returns>
    public Task<bool> ForgetFedCodeMapAsync(string repository, CancellationToken ct = default) => CodeMaps.ForgetFedAsync(repository, ct);

    /// <summary>
    /// The registry as the store holds it NOW, re-read before every answer that reads it.
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
    /// Registered repositories whose checkout is not where the registry says it is.
    /// </summary>
    /// <remarks>
    /// A row with no path is not an absence — a teammate's mirrored registration has no checkout here
    /// by construction, and reporting it as missing would turn a normal remote family into a
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
        // An index a newer Daoris wrote is never read into here: no refresh is tried, so none is retried, and
        // the host said why once, at its start.
        if (store.Refusal is not null) return;

        // A source that moves under the store is re-read at first use and once its reading is older than
        // the window: what the store held from an earlier process is that process's reading.
        if (rereadAfter is { } window)
        {
            await RefreshUnlessFreshAsync(window, ct).ConfigureAwait(false);
            return;
        }

        // A host over the registered roots judges each by its signature, so a record merged into one is found without
        // anyone telling the index: what the store held from an earlier process is read again at first use.
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
    /// Reads the registered roots again as they move. The first use reads them all, since what the store held is an
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

    /// <summary>When and from which commit each registered repository is read, by repository name.</summary>
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
    /// 🔴 The rows persist and the vectors do not. A start over an index already on disk would skip
    /// the refresh, so the semantic half would answer nothing until somebody rebuilt the index — while
    /// every door said the tier is semantic. What is on disk is embedded once, on first use, under
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
