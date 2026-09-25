using Lyntai.Inference;
using Lyntai.Memory;

namespace Daoris.Knowledge;

/// <summary>Where the repositories are, where the index lives, and which model — if any — answers.</summary>
/// <param name="RepositoryRoot">The folder whose subdirectories are the repositories to index.</param>
/// <param name="DatabasePath">The SQLite index. Created if absent.</param>
/// <param name="EmbedModel">
/// Which model, when the deployment has one — read here only so every host reads it the same way. The
/// provider itself is BUILT BY THE HOST and passed in; Core stays on <c>IVectorProvider</c> (D22, D24).
/// </param>
/// <param name="EmbedUrl">The endpoint, for a deployment that has one.</param>
public sealed record ServiceOptions(
    string RepositoryRoot,
    string DatabasePath,
    string? EmbedModel = null,
    string? EmbedUrl = null)
{
    public const string RootVariable = "DAORIS_KNOWLEDGE_ROOT";
    public const string DatabaseVariable = "DAORIS_KNOWLEDGE_DB";
    public const string ModelVariable = "DAORIS_EMBED_MODEL";
    public const string UrlVariable = "DAORIS_EMBED_URL";

    /// <summary>Read from the environment, with the defaults every host shares.</summary>
    public static ServiceOptions FromEnvironment(string defaultRoot, string defaultDatabase) =>
        new(Environment.GetEnvironmentVariable(RootVariable) ?? defaultRoot,
            Environment.GetEnvironmentVariable(DatabaseVariable) ?? defaultDatabase,
            Environment.GetEnvironmentVariable(ModelVariable),
            Environment.GetEnvironmentVariable(UrlVariable) ?? "http://localhost:11434");
}

/// <param name="Service">The composed service. Convergence is reached through it, not beside it.</param>
/// <param name="Quests">Work one repository has asked of another — service state, not anyone's files.</param>
/// <param name="Exchange">The publish/respond judgement, shared so no two hosts can disagree on it.</param>
/// <param name="Sessions">Driver-started session records — the record is service state, the process never is (D46).</param>
/// <param name="Ledger">The open/advance judgement for sessions, shared for the same reason the exchange is.</param>
/// <param name="Keys">The shared deployment's machine credentials (D47 §7). Present in every mode —
/// minting is deployment administration — but only shared mode's gate consults them.</param>
/// <param name="SemanticEnabled">Whether the semantic tier answered. Report it; never imply it.</param>
/// <remarks>
/// Disposable, and it owns the store: the factory opened it, so the caller should not have to know that
/// a database handle came back inside something called a service.
/// </remarks>
public sealed record ComposedService(
    KnowledgeService Service, QuestStore Quests, QuestExchange Exchange,
    SessionStore Sessions, SessionLedger Ledger, ApiKeyStore Keys, bool SemanticEnabled) : IAsyncDisposable
{
    internal SqliteKnowledgeStore? Store { get; init; }

    /// <summary>
    /// Where this deployment keeps the bytes a quest carries (D65 §2) — null on a deployment that keeps
    /// none: a shared one, which holds names only, or a local one with no Daoris home (D63).
    /// </summary>
    public QuestFiles? Files { get; init; }

    /// <summary>Asks made at a workspace, and the judgement over them (D65 §1a).</summary>
    public AskDesk Asks { get; init; } = null!;

    /// <summary>This machine's remotes, by workspace — what a sync pass runs against (D69). Null on a remote.</summary>
    public IRemotes? Remotes { get; init; }

    public ValueTask DisposeAsync() => Store?.DisposeAsync() ?? ValueTask.CompletedTask;
}

/// <summary>
/// One composition, used by every host.
/// </summary>
/// <remarks>
/// <para>It exists because there are now two hosts — MCP over stdio for an agent, HTTP for the browser —
/// and the wiring was about to be written twice. Two copies of "which tier is active, and how is the
/// embedder built" would drift, and one of them would end up quietly lexical-only while reporting
/// otherwise. That is this project's own thesis; committing it inside the project would be worse than
/// finding it elsewhere.</para>
///
/// <para><b>No dependency-injection types here.</b> Core stays free of a container so a host can compose
/// however it likes; this hands back finished objects, and each host registers them in its own idiom.</para>
///
/// <para><b>The embedder arrives ready-made.</b> Core is deliberately not linked against any provider
/// package — it holds <c>IVectorProvider</c> and nothing that implements one, which is what keeps a model
/// out of the domain. So the host reads the configuration, constructs the provider, and passes it here. What
/// must not diverge between hosts is the part that lives here: whether the semantic tier is on, what
/// hybrid fuses, and which tier gets reported.</para>
///
/// <para><b>The deployment picks the model, the feature never names one (D24).</b> Naming a model turns
/// the semantic tier on and hybrid search fuses both. Silence leaves the service lexical-only rather than
/// refusing to start — a knowledge index that will not run without an embedding endpoint is not
/// local-first, and every feature here still does its useful part with no model at all.</para>
/// </remarks>
/// <summary>
/// The key store, opened alone — what `keys mint|list|revoke` needs and the whole of what it needs.
/// </summary>
/// <remarks>
/// It owns the connection, so a console verb disposes one thing and is done.
/// </remarks>
public sealed record KeyAdministration(ApiKeyStore Keys) : IAsyncDisposable
{
    internal SqliteKnowledgeStore? Store { get; init; }

    public ValueTask DisposeAsync() => Store?.DisposeAsync() ?? ValueTask.CompletedTask;
}

public static class ServiceFactory
{
    /// <summary>
    /// Open ONLY the credential store, for the deployment's key console (D47 §7).
    /// </summary>
    /// <remarks>
    /// <para>Key administration has nothing to do with knowledge, and composing the whole service to
    /// mint a key had a consequence nobody chose: the composition bootstraps a registry from the
    /// configured root the first time a store is used (D48 §3), so <c>keys mint</c> on a server
    /// imported whatever sat beside the binary — machine paths included — into the deployment that
    /// must be FED, NEVER SCANNED (D47 §4). Found by the family rehearsal's own assertion that every
    /// row at a workspace's deployment belongs to that workspace.</para>
    ///
    /// <para>The narrow door is also the honest one: an operator verb that quietly indexed a disk was
    /// doing something its name did not say.</para>
    /// </remarks>
    public static async Task<KeyAdministration> OpenKeysAsync(
        ServiceOptions options, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.DatabasePath)!);

        var store = await SqliteKnowledgeStore.OpenAsync(options.DatabasePath).ConfigureAwait(false);
        var keys = await ApiKeyStore.OpenAsync(store.Connection, ct).ConfigureAwait(false);

        return new KeyAdministration(keys) { Store = store };
    }

    public static async Task<ComposedService> CreateAsync(
        ServiceOptions options,
        IVectorProvider? embedder = null,
        IVectorStore? vectors = null,
        IDisclosurePolicy? disclosure = null,
        // This machine's remotes, by workspace (D48 §5) — what a take claims at (D69) and what a chain's
        // composition asks, because a chain is all shared or all local (D68). Passed in like the
        // embedder: the deployment decides, and both doors get the same exchange so neither can drift.
        // A shared host passes nothing — it is the remote.
        IRemotes? remotes = null,
        // What the index reads from, when the deployment is not the usual scan-this-folder one. A
        // shared host passes EmptyKnowledgeSource, because it is fed and never scans (D47 §4) — the
        // route refusal alone would leave the index-on-first-use path free to scan the server's disk.
        IKnowledgeSource? source = null,
        // Where a quest's files are kept, when this deployment keeps any (D65 §2). A local host passes
        // the home's keeper; a shared host passes nothing — it holds names, never bytes — and so does a
        // local host with no home, whose exchange then refuses files with the home's sentence (D63).
        QuestFiles? files = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.DatabasePath)!);

        var store = await SqliteKnowledgeStore.OpenAsync(options.DatabasePath).ConfigureAwait(false);
        var quests = await QuestStore.OpenAsync(store.Connection, ct).ConfigureAwait(false);
        var sessions = await SessionStore.OpenAsync(store.Connection, ct).ConfigureAwait(false);
        var registrations = await RegistrationStore.OpenAsync(store.Connection, ct).ConfigureAwait(false);
        var keys = await ApiKeyStore.OpenAsync(store.Connection, ct).ConfigureAwait(false);
        var asks = await AskStore.OpenAsync(store.Connection, ct).ConfigureAwait(false);

        // 🔴 A rebuilt index holds nothing, so nothing may claim it holds a commit (REV3). Otherwise the
        // re-feed that restores a shared deployment is judged already held, and it stays empty until
        // each repository commits again.
        if (store.Rebuilt) await registrations.ForgetAllKnowledgeProvenanceAsync(ct).ConfigureAwait(false);

        // The registry is the authority now (D48 §3): the list is what has been registered, not what a
        // folder happens to hold. Loaded before the first read so a restart is invisible to a client.
        var registry = new Registry();
        foreach (var registration in await registrations.AllAsync(ct).ConfigureAwait(false))
        {
            registry.Register(registration);
        }

        // Whether this deployment READS THE LOCAL DISK at all is one decision with two consequences,
        // and they must not drift apart: a fed deployment neither reads checkouts for knowledge
        // (D47 §4) nor bootstraps a registry from whatever sits beside its binary. A caller that
        // supplies its own source is saying "I am fed"; that is the same sentence, so it decides both.
        var readsLocalCheckouts = source is null;
        // The index reads the REGISTERED paths (D48 §3) — resolved per read, so a repository added or
        // retired a moment ago is in or out of the very next refresh without a restart.
        source ??= new FileSystemKnowledgeSource(() => RegisteredRoots(registry));

        IKnowledgeSearch search = new SqliteKnowledgeSearch(store);

        // Both or neither. A vector store with no embedder cannot answer, and an embedder with nowhere
        // to put its vectors is a slow no-op — either half alone would report a semantic tier that does
        // not work, which is the one outcome worse than having none.
        if (embedder is not null)
        {
            vectors ??= new InMemoryVectorStore();
            search = new HybridKnowledgeSearch(search, new SemanticKnowledgeSearch(store, embedder, vectors));
        }
        else
        {
            vectors = null;
        }

        var service = new KnowledgeService(
            store, search, source, disclosure ?? DisclosurePolicy.LocalOnly, embedder, vectors,
            registry, registrations, readsRegisteredRoots: readsLocalCheckouts);

        // The bootstrap (D48 §3): a store that has never been managed imports its configured root ONCE
        // and says so. Without it, a machine that has been running on DAORIS_KNOWLEDGE_ROOT would come
        // up to an empty family after the upgrade — and an empty family is indistinguishable from a
        // broken one. Once, because a second run would resurrect everything the person retired.
        if (readsLocalCheckouts && !await registrations.WasImportedAsync(ct).ConfigureAwait(false))
        {
            var imported = await service.ImportAsync(options.RepositoryRoot, DateTimeOffset.UtcNow, ct)
                .ConfigureAwait(false);
            await registrations.MarkImportedAsync(options.RepositoryRoot, ct).ConfigureAwait(false);
            if (imported.Count > 0)
            {
                // stderr, not stdout: the MCP host's stdout IS the protocol channel.
                Console.Error.WriteLine(
                    $"daoris: first run over this index — imported {imported.Count} repositories from "
                    + $"'{options.RepositoryRoot}' into the registry, which is the authority from now on. "
                    + "Add with `daoris connect`, remove with `daoris retire`, re-scan with `daoris import`.");
            }
        }

        var exchange = new QuestExchange(service, quests, remotes, files);
        return new ComposedService(
            service, quests, exchange,
            // The ledger reads the registry for the one thing a chat cannot inherit from a quest: which
            // repository it runs in, and therefore which circle its record belongs to (D49 §3) — and
            // the asks, for the one thing an intake cannot inherit from either (D65 §1b).
            sessions, new SessionLedger(quests, sessions, service, asks), keys, service.SemanticEnabled)
        {
            Store = store,
            Files = files,
            Remotes = remotes,
            // An ask's quests go through the same exchange every other door uses (D65 §1a).
            Asks = new AskDesk(service, asks, exchange, files),
        };
    }

    /// <summary>
    /// The checkouts the index reads — every registered repository that named one.
    /// </summary>
    /// <remarks>
    /// A row with no root is a FOREIGN registration: a teammate's repository, mirrored down from a
    /// remote, which this machine has no copy of (D47 §9). It belongs on the map and has nothing here
    /// to read, and the distinction is structural rather than a flag — a mirror has no field for a
    /// machine path, so it cannot carry one.
    /// </remarks>
    private static IReadOnlyList<string> RegisteredRoots(Registry registry) =>
        registry.Read(new Dictionary<string, int>())
            .Select(r => r.Root)
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => root!)
            .ToList();
}
