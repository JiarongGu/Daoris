using Lyntai.Embeddings;
using Lyntai.Memory;

namespace Daoris.Knowledge;

/// <summary>Where the repositories are, where the index lives, and which model — if any — answers.</summary>
/// <param name="RepositoryRoot">The folder whose subdirectories are the repositories to index.</param>
/// <param name="DatabasePath">The SQLite index. Created if absent.</param>
/// <param name="EmbedModel">
/// Which model, when the deployment has one — read here only so every host reads it the same way. The
/// embedder itself is BUILT BY THE HOST and passed in; Core stays on <c>IEmbedder</c> (D22, D24).
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
/// <param name="SemanticEnabled">Whether the semantic tier answered. Report it; never imply it.</param>
/// <remarks>
/// Disposable, and it owns the store: the factory opened it, so the caller should not have to know that
/// a database handle came back inside something called a service.
/// </remarks>
public sealed record ComposedService(KnowledgeService Service, bool SemanticEnabled) : IAsyncDisposable
{
    internal SqliteKnowledgeStore? Store { get; init; }

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
/// package — it holds <c>IEmbedder</c> and nothing that implements one, which is what keeps a model out
/// of the domain. So the host reads the configuration, constructs the provider, and passes it here. What
/// must not diverge between hosts is the part that lives here: whether the semantic tier is on, what
/// hybrid fuses, and which tier gets reported.</para>
///
/// <para><b>The deployment picks the model, the feature never names one (D24).</b> Naming a model turns
/// the semantic tier on and hybrid search fuses both. Silence leaves the service lexical-only rather than
/// refusing to start — a knowledge index that will not run without an embedding endpoint is not
/// local-first, and every feature here still does its useful part with no model at all.</para>
/// </remarks>
public static class ServiceFactory
{
    public static async Task<ComposedService> CreateAsync(
        ServiceOptions options,
        IEmbedder? embedder = null,
        IVectorStore? vectors = null,
        IDisclosurePolicy? disclosure = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.DatabasePath)!);

        var store = await SqliteKnowledgeStore.OpenAsync(options.DatabasePath).ConfigureAwait(false);
        var source = FileSystemKnowledgeSource.UnderFolder(options.RepositoryRoot);

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
            store, search, source, disclosure ?? DisclosurePolicy.LocalOnly, embedder, vectors);

        return new ComposedService(service, service.SemanticEnabled) { Store = store };
    }
}
