using Lyntai;
using Lyntai.Inference;
using Lyntai.Providers.Http;
using Lyntai.Providers.Ollama;

namespace Daoris.Knowledge.Hosting;

/// <summary>
/// The composition both hosts share, as a source file linked into each (`Compile Include` in the two
/// csproj files). It cannot live in Core — it constructs providers, and Core holds
/// <see cref="IVectorProvider"/> and nothing that implements one (D22, D24) — and it must not be
/// written twice: these ~50 lines were copy-pasted between the hosts, sitting just outside the
/// factory that exists to stop exactly that drift.
/// </summary>
internal static class HostComposition
{
    /// <summary>
    /// The deployment's embedder, when it named a model — null leaves the service lexical-only rather
    /// than refusing to start (D24). An Ollama ROOT speaks Ollama's own wire — the same judgement the
    /// sibling's DI door applies (its D160), replicated because this composition builds by hand;
    /// `Produces` must say Vector, or the default is a chat backend posting /chat/completions (D130).
    /// </summary>
    public static IVectorProvider? BuildEmbedder(ServiceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EmbedModel)) return null;

        var embedUrl = options.EmbedUrl ?? "http://localhost:11434";
        return IsOllamaRoot(embedUrl)
            ? new OllamaProvider(
                "daoris-embed",
                new OllamaOptions { BaseUrl = embedUrl, Model = options.EmbedModel, Produces = ProviderKinds.Vector },
                () => new HttpClient(),
                new LyntaiOptions())
            : new HttpModelProvider(
                "daoris-embed",
                new HttpModelOptions { BaseUrl = embedUrl, Model = options.EmbedModel, Produces = ProviderKinds.Vector },
                () => new HttpClient(),
                new LyntaiOptions());
    }

    /// <summary>Where the index lives by default — one file under the profile, shared by every session.</summary>
    public static string DefaultDatabasePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris", "knowledge.db");

    /// <summary>
    /// The folder above the workspace holding this binary, when there is one — the shared walk-up from
    /// the BINARY, never the working directory: hosts are launched from wherever their client happens
    /// to be. Null when no manifest is found above the binary (a published install); what to do THEN
    /// stays each host's own, because the honest fallback differs by transport — stderr is the MCP
    /// host's only channel, and the HTTP host keeps the parent-of-CWD heuristic as a last resort.
    /// </summary>
    public static string? AboveWorkspace()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json")))
        {
            directory = directory.Parent;
        }

        return directory?.Parent?.FullName;
    }

    // The sibling's own root test (internal there): Ollama's well-known port with no /v1 suffix — a
    // /v1 base targets its OpenAI-shaped surface, where the native wire would 404 on every call.
    private static bool IsOllamaRoot(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
        && uri.Port == 11434
        && !uri.AbsolutePath.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase);
}
