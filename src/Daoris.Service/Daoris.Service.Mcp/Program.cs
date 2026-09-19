using System.Text;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Lyntai;
using Lyntai.Inference;
using Lyntai.Providers.Http;
using Lyntai.Providers.Ollama;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// The knowledge index as an MCP server over stdio.
//
// Local-first, and local means local: it reads repositories on this machine, writes one SQLite file
// under the user's profile, and opens no socket. Nothing here needs a URL, a key or an account —
// that is the shared mode, and it is the HTTP host's job, not this one's.
//
// This process is spawned by its client and lives for the session; the DATABASE is what persists.
// Every session in every repository on this machine spawns over the same file, which is how a quest
// published in one repository's session is waiting when another repository's session starts.
//
//   DAORIS_KNOWLEDGE_ROOT  where the repositories are      (default: the parent of this workspace)
//   DAORIS_KNOWLEDGE_DB    where the index is kept         (default: ~/.daoris/knowledge.db)

// JSON-RPC over stdio is UTF-8, and on Windows the console defaults to the system ANSI codepage —
// so without this every em dash and every CJK character in the corpus arrives as mojibake. This
// repository's doctrine is full of both, so it is not a rare edge: it is most answers. BOM-less,
// because a byte-order mark at the head of the stream is not valid JSON-RPC.
try
{
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}
catch (IOException)
{
    // No console attached (the usual case when a client launches this) — the streams are already
    // byte pipes and need no re-encoding.
}

var builder = Host.CreateApplicationBuilder(args);

// stdio IS the protocol channel, so anything written to stdout corrupts it. Logs go to stderr —
// the single most common way to break a stdio MCP server, and silently, since the transport just
// stops parsing.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
// Quiet by default: a stdio server's stderr is the operator's only channel, and per-request info
// logs bury the one line that matters when something is actually wrong.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var serviceOptions = ServiceOptions.FromEnvironment(DefaultRepositoryRoot(), DefaultDatabasePath());

// The provider is built HERE, not in Core: the domain holds `IVectorProvider` and nothing that
// implements one, which is what keeps a model out of it (D22, D24). Everything downstream of that
// choice — which tier is active, what hybrid fuses, what gets reported — is ServiceFactory's, shared
// with the HTTP host so the two cannot disagree about whether semantic recall is on.
// An Ollama ROOT speaks Ollama's own wire — the same judgement the sibling's DI door applies (its
// D160), replicated because this composition root builds by hand; `Produces` must say Vector, or the
// default is a chat backend posting /chat/completions (its D130).
IVectorProvider? embedder = null;
if (!string.IsNullOrWhiteSpace(serviceOptions.EmbedModel))
{
    var embedUrl = serviceOptions.EmbedUrl ?? "http://localhost:11434";
    embedder = IsOllamaRoot(embedUrl)
        ? new OllamaProvider(
            "daoris-embed",
            new OllamaOptions { BaseUrl = embedUrl, Model = serviceOptions.EmbedModel, Produces = ProviderKinds.Vector },
            () => new HttpClient(),
            new LyntaiOptions())
        : new HttpModelProvider(
            "daoris-embed",
            new HttpModelOptions { BaseUrl = embedUrl, Model = serviceOptions.EmbedModel, Produces = ProviderKinds.Vector },
            () => new HttpClient(),
            new LyntaiOptions());
}

var composed = await ServiceFactory.CreateAsync(serviceOptions, embedder);
builder.Services.AddSingleton(composed.Service);
builder.Services.AddSingleton(composed.Quests);
builder.Services.AddSingleton(composed.Exchange);

builder.Services
    .AddMcpServer(options => options.ServerInfo = new() { Name = "daoris-knowledge", Version = "0.1.0" })
    .WithStdioServerTransport()
    .WithTools<KnowledgeTools>();

await builder.Build().RunAsync().ConfigureAwait(false);
await composed.DisposeAsync().ConfigureAwait(false);

return;

/// <summary>
/// The folder holding the repositories — by default the one containing this workspace, which is how
/// the family is actually laid out. Walks up to the workspace root rather than assuming a working
/// directory, because an MCP server is started by its client from wherever that client happens to be.
/// </summary>
static string DefaultRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json")))
    {
        directory = directory.Parent;
    }

    if (directory?.Parent?.FullName is { } aboveWorkspace) return aboveWorkspace;

    // A PUBLISHED binary has no workspace above it, and the working directory here is whatever
    // repository the client spawned this from — whose subdirectories are not repositories. Falling
    // back silently would index the wrong tree without a word (the ghost shape, again), so the
    // fallback says its name. stderr, because stdout is the protocol.
    var fallback = Directory.GetCurrentDirectory();
    // This default is computed eagerly even when the environment decides; only warn when it will be used.
    if (Environment.GetEnvironmentVariable(ServiceOptions.RootVariable) is not null) return fallback;
    Console.Error.WriteLine(
        $"daoris-knowledge: no workspace manifest above the binary and {ServiceOptions.RootVariable} is not set — "
        + $"falling back to '{fallback}', which is probably not the family. Set {ServiceOptions.RootVariable} "
        + "in the MCP server entry (the install script prints a ready snippet).");
    return fallback;
}

// The sibling's own root test (internal there): Ollama's well-known port with no /v1 suffix — a /v1
// base targets its OpenAI-shaped surface, where the native wire would 404 on every call.
static bool IsOllamaRoot(string baseUrl) =>
    Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
    && uri.Port == 11434
    && !uri.AbsolutePath.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase);

static string DefaultDatabasePath() => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris", "knowledge.db");
