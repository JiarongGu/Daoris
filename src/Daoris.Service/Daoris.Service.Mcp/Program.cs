using System.Text;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// The knowledge index as an MCP server over stdio.
//
// Local-first, and local means local: it reads repositories on this machine, writes one SQLite file
// under the user's profile, and opens no socket. Nothing here needs a URL, a key or an account —
// that is the shared mode, and it does not exist yet.
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

var repositoryRoot = Environment.GetEnvironmentVariable("DAORIS_KNOWLEDGE_ROOT")
                     ?? DefaultRepositoryRoot();
var databasePath = Environment.GetEnvironmentVariable("DAORIS_KNOWLEDGE_DB")
                   ?? DefaultDatabasePath();

Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

var store = await SqliteKnowledgeStore.OpenAsync(databasePath).ConfigureAwait(false);

builder.Services.AddSingleton<IKnowledgeStore>(store);
builder.Services.AddSingleton<IKnowledgeSearch>(new SqliteKnowledgeSearch(store));
builder.Services.AddSingleton<IKnowledgeSource>(FileSystemKnowledgeSource.UnderFolder(repositoryRoot));
// Nothing is leaving this machine, so nothing is withheld. Shared mode replaces exactly this line.
builder.Services.AddSingleton(DisclosurePolicy.LocalOnly);
builder.Services.AddSingleton<KnowledgeService>();

builder.Services
    .AddMcpServer(options => options.ServerInfo = new() { Name = "daoris-knowledge", Version = "0.1.0" })
    .WithStdioServerTransport()
    .WithTools<KnowledgeTools>();

await builder.Build().RunAsync().ConfigureAwait(false);
await store.DisposeAsync().ConfigureAwait(false);

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

    return directory?.Parent?.FullName ?? Directory.GetCurrentDirectory();
}

static string DefaultDatabasePath() => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris", "knowledge.db");
