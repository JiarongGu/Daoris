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
// Local-first, and local means local: it reads repositories on this machine and writes one SQLite
// file under the user's profile. Nothing here needs a URL, a key or an account — that is the shared
// mode, and it is the HTTP host's job, not this one's. A quest shared with a team is committed here
// like any other (D68). The one socket this host opens is a take's claim by push (D69): where the
// person has wired the quest's workspace to a remote, a take waits for that remote's answer before
// the session works. A machine that has wired nothing — the default — opens no socket at all.
//
// This process is spawned by its client and lives for the session; the DATABASE is what persists.
// Every session in every repository on this machine spawns over the same file, which is how a quest
// published in one repository's session is waiting when another repository's session starts.
//
//   DAORIS_HOME            where every machine-local file lives (D63) — the installed desktop's own
//                          `data/`, set for the account; with neither it nor the DB named, exit 2
//   DAORIS_KNOWLEDGE_ROOT  where the repositories are      (default: the parent of this workspace)
//   DAORIS_KNOWLEDGE_DB    where the index is kept         (default: $DAORIS_HOME/knowledge.db)
//   DAORIS_REMOTE_CONFIG   the machine's remotes, by workspace (default: $DAORIS_HOME/remotes.json —
//                          D48 §5; DAORIS_REMOTE_URL/_KEY/_WORKSPACE override it whole). Read when a
//                          take claims by push and when a chain is composed.
//   DAORIS_KNOWLEDGE_REPOSITORY  the one checkout served, for a workspace's own server (ORIENT1c):
//                          registered alone, and re-read once its reading is a minute old
//   DAORIS_KNOWLEDGE_DOCUMENTS / _INDEX  repository-relative folders read in each registered checkout:
//                          documents a section each, a generated index a row each (ORIENT1c)

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

// The machine log (LOG1, D94): this host's start and stop and its warnings and errors, in a file of its
// own beside the desktop's, since this standard error belongs to the agent that started it and nobody
// keeps it. With no home it writes nothing.
using var log = MachineLog.Open("mcp");
log.WatchUnhandled();
builder.Logging.AddProvider(new MachineLogProvider(log));
var started = DateTimeOffset.UtcNow;

// The index lives under the Daoris home (D63) unless named directly. No home and no name is a
// refusal on stderr, not a default: an index written somewhere nobody pointed this host is the
// thing removed.
var database = Environment.GetEnvironmentVariable(ServiceOptions.DatabaseVariable)
    ?? HostComposition.DefaultDatabasePath();
if (database is null)
{
    Console.Error.WriteLine(DaorisHome.Sentence);
    return 2;
}

var (serviceOptions, optionsError) = ServiceOptions.FromEnvironment(DefaultRepositoryRoot(), database);
if (optionsError is not null)
{
    Console.Error.WriteLine(optionsError);
    return 2;
}

// The provider is built HOST-SIDE, not in Core: the domain holds `IVectorProvider` and nothing that
// implements one, which is what keeps a model out of it (D22, D24). Everything downstream of that
// choice — which tier is active, what hybrid fuses, what gets reported — is ServiceFactory's, shared
// with the HTTP host so the two cannot disagree about whether semantic recall is on.
var embedder = HostComposition.BuildEmbedder(serviceOptions);

// Every verb commits in this machine's store (D68), and a take on a shared quest claims by push at the
// remote its circle names (D69) — the remotes map read when asked, the same as the HTTP host. The MCP
// host is always a LOCAL door; a shared deployment has no stdio.
// A quest's files are kept under the home of the machine that has them (D65 §2) — this one, always:
// the MCP host is a local door. No home, no keeper, and a publish carrying files is refused (D63).
ComposedService composed;
try
{
    composed = await ServiceFactory.CreateAsync(
        serviceOptions, embedder, remotes: new ConfiguredRemotes(), files: QuestFiles.FromEnvironment());
}
catch (NewerStoreException refusal)
{
    // KSCHEMA1: a store a newer Daoris wrote is refused, left as it was, and said in one sentence a person can act on,
    // as every other start refusal here is. Into the machine log too, since this standard error is the agent's.
    Console.Error.WriteLine(refusal.Message);
    log.Failed("start", refusal);
    return 2;
}

builder.Services.AddSingleton(composed.Service);
builder.Services.AddSingleton(composed.Quests);
builder.Services.AddSingleton(composed.Exchange);
builder.Services.AddSingleton(composed.Asks);
// The ledger, for the one write a session's connector makes on its own record: that it took its quest
// (STANDDOWN2).
builder.Services.AddSingleton(composed.Ledger);

// An intake's connector (D65 §1b): the driver that opened the session names the ask it answers, and a
// quest published here is then asked BY that ask. Every other session names none.
var intake = IntakeScope.FromEnvironment();
builder.Services.AddSingleton(intake);

// Where a session's proposal to change the rules goes (PERM2, D74): the home the driver names on the
// connector it offers — the one its rules live in — else the account's. A file there, never a row.
builder.Services.AddSingleton(RuleProposalBox.FromEnvironment());
// And Ask Daoris's (HELP1c, D89), under the same home: a file each, for the person to apply.
builder.Services.AddSingleton(HelpProposalBox.FromEnvironment());

// The ambient scope (D48 §4): this process is spawned BY a repository's session, so its working
// directory is that repository — which is the one thing that makes "my own circle" answerable without
// asking the agent to know wiring it has no business knowing. Captured at startup, because the
// directory a client launched this from is the fact; anything later is drift. An intake's room is no
// repository: its circle is its ask's, read once here.
var circle = intake.Ask is { } askId ? (await composed.Asks.FindAsync(askId).ConfigureAwait(false))?.Workspace : null;
builder.Services.AddSingleton(new AmbientWorkspace(Directory.GetCurrentDirectory(), circle));

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "daoris-knowledge", Version = "0.1.0" };
        // A server over one checkout says what it reads and which tier answers, before the first search
        // (ORIENT1c); one over a family says nothing its tools do not.
        options.ServerInstructions = KnowledgeTools.Instructions(serviceOptions, composed.SemanticEnabled);
    })
    .WithStdioServerTransport()
    .WithTools<KnowledgeTools>();

log.Info("app.started", ("repository", Path.GetFileName(Directory.GetCurrentDirectory())));
await builder.Build().RunAsync().ConfigureAwait(false);
await composed.DisposeAsync().ConfigureAwait(false);
log.Info("app.stopped", ("uptimeSeconds", (long)(DateTimeOffset.UtcNow - started).TotalSeconds));

return 0;

// The folder holding the repositories — the shared walk-up from the binary (HostComposition), with
// THIS host's fallback: a published binary has no workspace above it, and the working directory here
// is whatever repository the client spawned this from — whose subdirectories are not repositories.
// Falling back silently would index the wrong tree without a word (the ghost shape, again), so the
// fallback says its name. stderr, because stdout is the protocol.
static string DefaultRepositoryRoot()
{
    if (HostComposition.AboveWorkspace() is { } aboveWorkspace) return aboveWorkspace;

    var fallback = Directory.GetCurrentDirectory();
    // This default is computed eagerly even when the environment decides; only warn when it will be used. A
    // server over one checkout never uses it: the checkout is named (ORIENT1c).
    if (Environment.GetEnvironmentVariable(ServiceOptions.RootVariable) is not null
        || Environment.GetEnvironmentVariable(ServiceOptions.RepositoryVariable) is not null) return fallback;
    Console.Error.WriteLine(
        $"daoris-knowledge: no workspace manifest above the binary and {ServiceOptions.RootVariable} is not set — "
        + $"falling back to '{fallback}', which is probably not the family. Set {ServiceOptions.RootVariable} "
        + "in the MCP server entry (the install script prints a ready snippet).");
    return fallback;
}
