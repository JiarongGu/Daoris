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
    public bool Information => Refusal is FeedRefusal.Stale or FeedRefusal.NotDefaultBranch;
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
    RegistrationStore? registrations = null)
{
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
    private readonly ConvergenceDetector _convergence = new(store, embedder, vectors);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _everRefreshed;

    /// <summary>Whether semantic recall is available, which depends on an embedder being configured.</summary>
    public bool SemanticEnabled => embedder is not null && vectors is not null;

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return await search.SearchAsync(query, ct).ConfigureAwait(false);
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
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        var counts = (await store.AllAsync(ct).ConfigureAwait(false))
            .GroupBy(entry => entry.Repository, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

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
    /// addressable and stops being indexed here, and its entries leave on the next refresh by the ghost
    /// rule. Its files, its history and its doctrine are its own — the one thing a person must be able
    /// to trust about a remove is what it does not do.
    /// </remarks>
    /// <returns>Whether there was a registration to retire; false is an answer, not a failure.</returns>
    public async Task<bool> RetireAsync(string repository, CancellationToken ct = default)
    {
        var stored = registrations is not null
            && await registrations.DeleteAsync(repository, ct).ConfigureAwait(false);
        var known = registry?.Retire(repository) ?? false;
        return stored || known;
    }

    /// <summary>
    /// Register everything a folder's subdirectories propose (D48 §3) — the bootstrap, run deliberately.
    /// </summary>
    /// <remarks>
    /// An import proposes no workspace, so re-importing a folder never re-points a repository someone
    /// wired: unstated is preserved by the upsert. Existing rows are updated from their manifests,
    /// which is what makes `import` the right answer to "I edited several declarations at once".
    /// </remarks>
    /// <returns>The registrations as they now stand, in name order.</returns>
    public async Task<IReadOnlyList<Registration>> ImportAsync(
        string folder, DateTimeOffset now, CancellationToken ct = default)
    {
        var imported = new List<Registration>();
        foreach (var proposal in RegistryImport.Propose(folder))
        {
            imported.Add(await RegisterAsync(proposal, now, ct).ConfigureAwait(false));
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
            .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase))
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
        var registration = (await RegistryAsync(ct: ct).ConfigureAwait(false))
            .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));

        if (registration is null || !registration.Joined)
        {
            return new(
                FeedRefusal.NotJoined,
                $"`{repository}` has not joined this deployment — the manifest's `remote.join` is the "
                + "declaration that admits it, and silence means local.",
                Entries: 0);
        }

        if (!registration.SharesKnowledge)
        {
            return new(
                FeedRefusal.NotSharing,
                $"`{registration.Repository}` joined without sharing knowledge — its records travel, its "
                + "knowledge stays home. `remote.knowledge` is the declaration that changes that.",
                Entries: 0);
        }

        // ——— Which point in the history is speaking (D48 §6). Wholesale replacement is the right
        // shape — the index is derived data, and merging two machines' derivations invents a second
        // truth beside git — but it is only safe once the deployment can tell a newer view from an
        // older one. Unguarded, two machines feeding one repository are a flapping generator.
        if (provenance is null)
        {
            return new(
                FeedRefusal.NoProvenance,
                $"`{registration.Repository}` fed knowledge without naming a commit. This deployment "
                + "replaces a repository's knowledge wholesale, so it takes a feed only from a point in "
                + "the history it can compare with what it already holds.",
                Entries: 0);
        }

        if (registration.DefaultBranch is { Length: > 0 } canonical
            && !string.Equals(provenance.Branch, canonical, StringComparison.Ordinal))
        {
            // The branch, the PR and the review already display work in flight better than an index
            // would. Records and quests still travel from any checkout: they are records of activity,
            // not claims of truth.
            return new(
                FeedRefusal.NotDefaultBranch,
                $"`{registration.Repository}` fed knowledge from `{provenance.Branch}`, and its canonical "
                + $"line is `{canonical}` — unmerged lessons are not yet the family's. Its session records "
                + "and quests still travel from this checkout.",
                Entries: 0);
        }

        var held = registrations is null
            ? null
            : await registrations.ProvenanceAsync(registration.Repository, ct).ConfigureAwait(false);

        // Same commit re-feeds are idempotent, as they always were. Equal times with different commits
        // are unorderable, so the arriving one takes: a tie is not evidence of staleness.
        if (held is not null
            && !string.Equals(held.Commit, provenance.Commit, StringComparison.OrdinalIgnoreCase)
            && provenance.CommittedAt < held.CommittedAt)
        {
            return new(
                FeedRefusal.Stale,
                $"`{registration.Repository}` is already fed from a newer commit "
                + $"(`{held.ShortCommit}`, {held.CommittedAt:yyyy-MM-dd HH:mm}Z"
                + $"{(held.Origin is null ? "" : $", from {held.Origin}")}) — this feed is from "
                + $"`{provenance.ShortCommit}`, {provenance.CommittedAt:yyyy-MM-dd HH:mm}Z, so the index "
                + "keeps what it has. Nothing is wrong: this checkout is simply behind.",
                Entries: 0);
        }

        var normalized = entries
            .Select(entry => entry with
            {
                Repository = registration.Repository,
                Provenance = Provenance.Local,
                // The RECEIVING deployment's wiring decides the circle, never the feed's claim about it
                // (D48): a feed that could name its own workspace could write itself into someone
                // else's, which is the scoping bug that becomes a disclosure.
                Workspace = registration.InWorkspace,
            })
            .ToList();
        await store.ReplaceRepositoryAsync(registration.Repository, normalized, ct).ConfigureAwait(false);

        // Recorded AFTER the entries land, so a store that fails mid-replace never claims a commit it
        // does not hold — the next feed from that commit would then be refused as a duplicate of work
        // that never happened.
        if (registrations is not null)
        {
            await registrations.RecordProvenanceAsync(registration.Repository, provenance, ct)
                .ConfigureAwait(false);
        }

        return new(
            FeedRefusal.None,
            $"Indexed {normalized.Count} entries from `{registration.Repository}` at `{provenance.ShortCommit}`.",
            normalized.Count);
    }

    /// <summary>Re-read every repository and rebuild the index.</summary>
    public async Task<IndexReport> RefreshAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // The wiring, read once per refresh rather than once per entry: it is a small table and the
            // corpus is not. Read BEFORE the scan so every entry of one repository is stamped alike.
            var wiring = (registry?.Read(new Dictionary<string, int>()) ?? [])
                .ToDictionary(r => r.Repository, r => r.InWorkspace, StringComparer.OrdinalIgnoreCase);

            var report = await _index.RefreshAsync(
                source,
                repository => wiring.TryGetValue(repository, out var workspace)
                    ? workspace
                    : Workspaces.Default,
                ct).ConfigureAwait(false);

            report = report with { Absent = AbsentCheckouts() };

            // Embedding happens here rather than inside the index, because it is the expensive,
            // optional half: the store is usable the moment the refresh returns, and semantic recall
            // arrives when it arrives.
            string? semanticError = null;
            if (embedder is not null && vectors is not null)
            {
                try
                {
                    var entries = await store.AllAsync(ct).ConfigureAwait(false);
                    await SemanticKnowledgeSearch.IndexAsync(entries, embedder, vectors, ct: ct)
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
            return report with { SemanticError = semanticError };
        }
        finally
        {
            _refreshLock.Release();
        }
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
        (registry?.Read(new Dictionary<string, int>()) ?? [])
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
        if (_everRefreshed) return;

        var existing = await store.AllAsync(ct).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            _everRefreshed = true;
            return;
        }

        await RefreshAsync(ct).ConfigureAwait(false);
    }
}
