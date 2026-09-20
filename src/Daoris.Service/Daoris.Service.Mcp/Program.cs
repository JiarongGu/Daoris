using System.Text;
using Daoris.Knowledge;
using Daoris.Knowledge.Hosting;
using Daoris.Knowledge.Mcp;
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

var serviceOptions = ServiceOptions.FromEnvironment(
    DefaultRepositoryRoot(), HostComposition.DefaultDatabasePath());

// The provider is built HOST-SIDE, not in Core: the domain holds `IVectorProvider` and nothing that
// implements one, which is what keeps a model out of it (D22, D24). Everything downstream of that
// choice — which tier is active, what hybrid fuses, what gets reported — is ServiceFactory's, shared
// with the HTTP host so the two cannot disagree about whether semantic recall is on.
var embedder = HostComposition.BuildEmbedder(serviceOptions);

// The write-through relay (D47 §5/§9): a verb on a remote-homed quest goes to the machine's remote,
// when one is configured — the same seam, the same client, the same exchange the HTTP host composes,
// so an agent's door and a browser's door cannot disagree about where a quest lives. The MCP host is
// always a LOCAL door; a shared deployment has no stdio.
var remoteQuests = RemoteConfig.Load() is { } remoteConfig ? new HttpRemoteQuests(remoteConfig) : null;

var composed = await ServiceFactory.CreateAsync(serviceOptions, embedder, remoteQuests: remoteQuests);
builder.Services.AddSingleton(composed.Service);
builder.Services.AddSingleton(composed.Quests);
builder.Services.AddSingleton(composed.Exchange);

// The ambient scope (D48 §4): this process is spawned BY a repository's session, so its working
// directory is that repository — which is the one thing that makes "my own circle" answerable without
// asking the agent to know wiring it has no business knowing. Captured at startup, because the
// directory a client launched this from is the fact; anything later is drift.
builder.Services.AddSingleton(AmbientWorkspace.Here());

builder.Services
    .AddMcpServer(options => options.ServerInfo = new() { Name = "daoris-knowledge", Version = "0.1.0" })
    .WithStdioServerTransport()
    .WithTools<KnowledgeTools>();

await builder.Build().RunAsync().ConfigureAwait(false);
await composed.DisposeAsync().ConfigureAwait(false);

return;

// The folder holding the repositories — the shared walk-up from the binary (HostComposition), with
// THIS host's fallback: a published binary has no workspace above it, and the working directory here
// is whatever repository the client spawned this from — whose subdirectories are not repositories.
// Falling back silently would index the wrong tree without a word (the ghost shape, again), so the
// fallback says its name. stderr, because stdout is the protocol.
static string DefaultRepositoryRoot()
{
    if (HostComposition.AboveWorkspace() is { } aboveWorkspace) return aboveWorkspace;

    var fallback = Directory.GetCurrentDirectory();
    // This default is computed eagerly even when the environment decides; only warn when it will be used.
    if (Environment.GetEnvironmentVariable(ServiceOptions.RootVariable) is not null) return fallback;
    Console.Error.WriteLine(
        $"daoris-knowledge: no workspace manifest above the binary and {ServiceOptions.RootVariable} is not set — "
        + $"falling back to '{fallback}', which is probably not the family. Set {ServiceOptions.RootVariable} "
        + "in the MCP server entry (the install script prints a ready snippet).");
    return fallback;
}
