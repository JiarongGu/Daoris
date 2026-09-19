using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Daoris.Knowledge;
using Lyntai;
using Lyntai.Inference;
using Lyntai.Providers.Http;
using Lyntai.Providers.Ollama;

// The browser's half of the service. The MCP host serves an agent over stdio; a browser cannot speak
// that, so this exists — the same composed service behind a read-only JSON surface.
//
//   DAORIS_KNOWLEDGE_ROOT  where the repositories are      (default: the parent of this workspace)
//   DAORIS_KNOWLEDGE_DB    where the index is kept         (default: ~/.daoris/knowledge.db)
//   DAORIS_EMBED_MODEL     naming one turns semantic on    (absent: lexical only, and it says so)
//   DAORIS_EMBED_URL       the endpoint                    (default: http://localhost:11434)
//   DAORIS_WEB_ORIGIN      the dev UI's origin for CORS    (absent: same-origin only)
//   DAORIS_SERVICE_KEY     set ⇒ every POST under /api needs it as a bearer token
//                          (absent: local trust — the OS account is the boundary, D21)
//
// DOCTRINE IS READ-ONLY HERE (D31). No endpoint writes a rule, a knowledge document or a skill:
// `upstream` routes an improvement through the repository that found it, where it meets that
// repository's review, and a web editor would win against that path for the wrong reason.
//
// SERVICE STATE IS WRITABLE, NARROWLY (D32, D34, D46). A repository may register what it owns, a quest
// may be published and answered, and a driver may record its sessions — the transfer of request and
// task is the whole point of a remote deployment, which may run with no model at all (D24) and still
// carry it. Those writes are exactly what DAORIS_SERVICE_KEY gates; a deployment reachable beyond a
// trusted network needs the fuller credential model in
// docs/2026-08-05-knowledge-service-design.md §5 before it exists.
//
// A REGISTRATION'S ROOT NEVER LEAVES THE MACHINE (D46). The filesystem path a repository registers is
// answered only to loopback callers — the local driver — so a remote deployment never serves anyone's
// disk layout to the network.
if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}

// The bundle travels beside the executable. In development the SDK serves wwwroot from the project
// directory — the default content root — but a PUBLISHED host is launched from anywhere, so when the
// working directory has no bundle and the binary's directory does, the binary's wins. Without this
// the published exe answers every API call and serves a 404 for the page, which reads as "the app is
// broken" rather than "the cwd was wrong".
var contentRoot = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"))
    ? Directory.GetCurrentDirectory()
    : Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot"))
        ? AppContext.BaseDirectory
        : Directory.GetCurrentDirectory();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = contentRoot });

// The documented address, made true by construction: with nothing configured, Kestrel binds its own
// default and the README's port is a lie. An explicit ASPNETCORE_URLS still wins.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:5177");
}

var options = ServiceOptions.FromEnvironment(DefaultRepositoryRoot(), DefaultDatabasePath());

// The provider is built HERE, not in Core: the domain holds `IVectorProvider` and nothing that
// implements one, so a model never reaches it (D22, D24). What tier that produces is Core's business.
// An Ollama ROOT speaks Ollama's own wire — the same judgement the sibling's DI door applies (its
// D160), replicated because this composition root builds by hand; `Produces` must say Vector, or the
// default is a chat backend posting /chat/completions (its D130).
IVectorProvider? embedder = null;
if (!string.IsNullOrWhiteSpace(options.EmbedModel))
{
    var embedUrl = options.EmbedUrl ?? "http://localhost:11434";
    embedder = IsOllamaRoot(embedUrl)
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

var composed = await ServiceFactory.CreateAsync(options, embedder);
builder.Services.AddSingleton(composed);

// Source-generated serialization: this host publishes AOT-friendly and reflection-based JSON would be
// the one thing stopping it.
builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJson.Default);
    json.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var origin = Environment.GetEnvironmentVariable("DAORIS_WEB_ORIGIN");
if (!string.IsNullOrWhiteSpace(origin))
{
    // Named, never wildcarded. The UI is served from this host in a real deployment; the variable
    // exists for the development server on another port, and a wildcard would quietly make a
    // local-only index readable by any page the browser happens to have open.
    builder.Services.AddCors(cors => cors.AddDefaultPolicy(p =>
        p.WithOrigins(origin).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();
if (!string.IsNullOrWhiteSpace(origin)) app.UseCors();

// The built UI, when there is one. Serving it from the same origin is what makes CORS unnecessary in
// a real deployment, and what lets the desktop shell host exactly the same bytes.
app.UseDefaultFiles();
app.UseStaticFiles();

// Writes need the key when one is configured. Absence means local (D21) — on a developer's machine
// the OS account is the boundary and demanding a token would be ceremony. Set, it gates every POST
// under /api; reads stay open because the UI is read-only by design (D31) and this deployment shape
// is a trusted network's.
var serviceKey = Environment.GetEnvironmentVariable("DAORIS_SERVICE_KEY");
if (!string.IsNullOrWhiteSpace(serviceKey))
{
    app.Use(async (context, next) =>
    {
        var isWrite = HttpMethods.IsPost(context.Request.Method)
                      && context.Request.Path.StartsWithSegments("/api");
        if (isWrite && !PresentsKey(context.Request.Headers.Authorization.ToString(), serviceKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            // Through the configured options, not the raw context — the raw metadata skips the
            // camelCase policy and answers `Error` where every endpoint answers `error`.
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse("this write needs the service key — send DAORIS_SERVICE_KEY as a bearer token"));
            return;
        }

        await next();
    });
}

app.MapGet("/api/status", (ComposedService s) => new StatusResponse(
    Semantic: s.SemanticEnabled,
    Tier: s.SemanticEnabled ? "lexical + semantic" : "lexical only",
    // Said on every response, not only when it is absent. A caller with results has no way to know the
    // semantic half was missing, and will read "these are the matches" as complete rather than as
    // complete-for-word-overlap (D24).
    Note: s.SemanticEnabled
        ? null
        : $"Set {ServiceOptions.ModelVariable} to enable semantic recall — it is what finds two "
          + "repositories that reached the same conclusion in different words."));

app.MapGet("/api/repositories", async (ComposedService s, CancellationToken ct) =>
    (await s.Service.SummarizeAsync(ct)).Select(r => new RepositoryResponse(r.Repository, r.Total, r.Local, r.Canonical)));

app.MapGet("/api/search", async (
    ComposedService s, string q, string? kinds, string? repositories, bool? localOnly, int? limit, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new ErrorResponse("q is required"));

    var hits = await s.Service.SearchAsync(new KnowledgeQuery(q)
    {
        Kinds = ParseKinds(kinds),
        Repositories = ParseSet(repositories),
        Provenance = (localOnly ?? true) ? Provenance.Local : null,
        Limit = Math.Clamp(limit ?? 20, 1, 100),
    }, ct);

    return Results.Ok(hits.Select(h => new HitResponse(
        h.Entry.Id, h.Entry.Repository, h.Entry.Kind.ToString(), h.Entry.Title,
        h.Entry.RelativePath, h.Excerpt, h.Score)));
});

app.MapGet("/api/entry", async (ComposedService s, string id, CancellationToken ct) =>
{
    var entry = await s.Service.FindAsync(id, ct);
    return entry is null
        ? Results.NotFound(new ErrorResponse($"no entry with id '{id}'"))
        : Results.Ok(new EntryResponse(
            entry.Id, entry.Repository, entry.Kind.ToString(), entry.Provenance.ToString(),
            entry.Title, entry.RelativePath, entry.Body));
});

// The landing view's endpoint (D30). Convergence is the centre of this UI, not a feature on a menu:
// search answers a question you have, and comparison tells you which question to ask.
app.MapGet("/api/convergence", async (
    ComposedService s, double? minimumSimilarity, string? kinds, int? limit, CancellationToken ct) =>
{
    var found = await s.Service.FindConvergenceAsync(
        new ConvergenceOptions(
            Math.Clamp(minimumSimilarity ?? 0.82, 0, 1), ParseKinds(kinds), Math.Clamp(limit ?? 25, 1, 100)),
        ct);

    return found.Select(c => new ConvergenceResponse(
        c.Method.ToString(),
        c.Similarity,
        c.Repositories,
        c.Entries.Select(e => new ConvergenceEntryResponse(
            e.Id, e.Repository, e.Kind.ToString(), e.Title, e.RelativePath)).ToList(),
        // The command, not an edit box (D31). The UI shows where the change belongs; the person makes it
        // in the repository that owns the file, where review happens.
        Suggestion: SuggestionFor(c)));
});

// What each repository OWES, as opposed to what it knows. Held by the service rather than written
// into anyone's files: repositories here are not developed across, so a quest is published and pulled,
// never pushed into a sibling's tree.
app.MapGet("/api/quests", async (
    ComposedService s, string? repository, bool? includeClosed, CancellationToken ct) =>
    (await s.Quests.ListAsync(repository, includeClosed ?? false, ct)).Select(ToQuest));

// Publish and respond go through the same exchange the MCP host uses, so the two doors cannot drift
// on who may be addressed or what declining requires. This pair is what makes a REMOTE deployment a
// transfer of work rather than a read-only mirror — and it needs no model at all (D24).
app.MapPost("/api/quests", async (ComposedService s, PublishQuestRequest body, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.From) || string.IsNullOrWhiteSpace(body.To)
        || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Body))
    {
        return Results.BadRequest(new ErrorResponse("from, to, title and body are all required"));
    }

    var outcome = await s.Exchange.PublishAsync(
        body.From, body.To, body.Title, body.Body, DateTimeOffset.UtcNow, ct);

    return outcome.Refusal == QuestPublishRefusal.None
        ? Results.Ok(new QuestActionResponse(ToQuest(outcome.Quest!), outcome.Message))
        : Results.BadRequest(new ErrorResponse(outcome.Message));
});

app.MapPost("/api/quests/{id}/respond", async (
    ComposedService s, string id, RespondQuestRequest body, CancellationToken ct) =>
{
    var outcome = await s.Exchange.RespondAsync(
        id, body.Action ?? "", body.Reason, DateTimeOffset.UtcNow, ct);

    return outcome.Refusal switch
    {
        QuestRespondRefusal.None => Results.Ok(new QuestActionResponse(ToQuest(outcome.Quest!), outcome.Message)),
        QuestRespondRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

// The driver's session records (D46). State only: the service never spawns a process — the record is
// what the platform renders and what survives a driver restart; the process handle stays with the
// driver that owns it. Judgement is the shared ledger's, so this door and any other cannot drift.
app.MapGet("/api/sessions", async (
    ComposedService s, string? repository, bool? includeClosed, CancellationToken ct) =>
    (await s.Sessions.ListAsync(repository, includeClosed ?? false, ct)).Select(ToSession));

app.MapPost("/api/sessions", async (ComposedService s, OpenSessionRequest body, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.Quest) || string.IsNullOrWhiteSpace(body.Adapter))
    {
        return Results.BadRequest(new ErrorResponse("quest and adapter are required"));
    }

    var outcome = await s.Ledger.OpenAsync(body.Quest, body.Adapter, DateTimeOffset.UtcNow, ct);

    return outcome.Refusal switch
    {
        SessionOpenRefusal.None => Results.Ok(new SessionActionResponse(ToSession(outcome.Session!), outcome.Message)),
        SessionOpenRefusal.QuestNotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

app.MapPost("/api/sessions/{id}/state", async (
    ComposedService s, string id, AdvanceSessionRequest body, CancellationToken ct) =>
{
    var outcome = await s.Ledger.AdvanceAsync(
        id, body.State ?? "", body.Note, body.Evidence, body.Transcript, DateTimeOffset.UtcNow, ct);

    return outcome.Refusal switch
    {
        SessionAdvanceRefusal.None => Results.Ok(new SessionActionResponse(ToSession(outcome.Session!), outcome.Message)),
        SessionAdvanceRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

// Where `daoris connect` lands. It accepts a repository's description of ITSELF — the only thing a
// repository is authoritative about — and persists it, because for a remote service the pushed
// registrations ARE the family: one that forgot them on restart would drop every connected
// repository off the map without anyone being told.
app.MapPost("/api/registry", async (ComposedService s, RegisterRequest body, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.Repository)) return Results.BadRequest(new ErrorResponse("repository is required"));

    await s.Service.RegisterAsync(new Registration(
        body.Repository,
        Adopted: true,
        body.Domain?.Summary,
        body.Domain?.Owns ?? [],
        body.Domain?.Accepts ?? [],
        body.Packs ?? [],
        Entries: 0,
        Root: string.IsNullOrWhiteSpace(body.Root) ? null : body.Root), DateTimeOffset.UtcNow, ct);

    return Results.Ok(new RegisteredResponse(body.Repository, DateTimeOffset.UtcNow));
});

app.MapGet("/api/registry", async (ComposedService s, HttpContext http, CancellationToken ct) =>
    (await s.Service.RegistryAsync(ct)).Select(r => new RegistrationResponse(
        r.Repository, r.Adopted, r.Registered, r.Summary, r.Owns, r.Accepts, r.Packs, r.Entries,
        // Machine-local by design (D46): a filesystem path is answered only to a caller on this
        // machine, so a remote deployment never serves anyone's disk layout to the network.
        Root: IsLoopback(http) ? r.Root : null)));

app.MapPost("/api/refresh", async (ComposedService s, CancellationToken ct) =>
{
    var report = await s.Service.RefreshAsync(ct);
    return new RefreshResponse(report.Entries, report.Repositories, report.Withheld, report.SemanticError);
});

app.Run();
return;

// A convergence is a prompt to look, so the suggestion says what to read and where the change goes —
// never "apply this". Doctrine that appeared without anyone choosing it is the failure this project
// exists to prevent (D21).
static string SuggestionFor(ConvergenceCandidate candidate) => candidate.Method switch
{
    ConvergenceMethod.Identical =>
        "The same document in more than one repository. If it should be doctrine, promote one copy with "
        + "`daoris upstream <file>` from the repository that owns it, then let the others sync.",
    ConvergenceMethod.Restatement =>
        "A copy that has drifted. Read both, decide which wording is right, and promote that one with "
        + "`daoris upstream <file>`.",
    _ =>
        "The same lesson in different words — the case no text comparison finds. Read both: what they "
        + "share may be canonical, and what differs is usually each repository's own and must stay local.",
};

static QuestResponse ToQuest(Quest q) => new(
    q.Id, q.From, q.To, q.Title, q.Body, q.Status.ToString(), q.Note, q.Filed, q.Updated);

static SessionResponse ToSession(Session s) => new(
    s.Id, s.Quest, s.Repository, s.Adapter, s.StateName, s.Note, s.Evidence, s.Transcript,
    s.Created, s.Updated);

// A caller on this machine — which is what "the root never leaves the machine" means in practice. A
// null remote address is the in-process test server, which is this process and therefore local.
static bool IsLoopback(HttpContext http) =>
    http.Connection.RemoteIpAddress is null || System.Net.IPAddress.IsLoopback(http.Connection.RemoteIpAddress);

// The sibling's own root test (internal there): Ollama's well-known port with no /v1 suffix — a /v1
// base targets its OpenAI-shaped surface, where the native wire would 404 on every call.
static bool IsOllamaRoot(string baseUrl) =>
    Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
    && uri.Port == 11434
    && !uri.AbsolutePath.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase);

// Fixed-time, so the comparison itself cannot leak how much of a guessed key matched.
static bool PresentsKey(string header, string key)
{
    const string scheme = "Bearer ";
    if (!header.StartsWith(scheme, StringComparison.Ordinal)) return false;
    var presented = Encoding.UTF8.GetBytes(header[scheme.Length..].Trim());
    var expected = Encoding.UTF8.GetBytes(key);
    return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(presented, expected);
}

static IReadOnlySet<EntryKind>? ParseKinds(string? value)
{
    var names = ParseSet(value);
    if (names is null) return null;

    var kinds = new HashSet<EntryKind>();
    foreach (var name in names)
    {
        var normalized = name.Equals("task", StringComparison.OrdinalIgnoreCase) ? "TaskOutcome" : name;
        if (Enum.TryParse<EntryKind>(normalized, ignoreCase: true, out var kind)) kinds.Add(kind);
    }

    return kinds.Count > 0 ? kinds : null;
}

static IReadOnlySet<string>? ParseSet(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return null;
    var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return items.Length > 0 ? new HashSet<string>(items, StringComparer.OrdinalIgnoreCase) : null;
}

// Walk up from the BINARY to this workspace's manifest, exactly as the MCP host does — never from the
// working directory: `dotnet run` sets the CWD to the project directory, so "parent of the current
// directory" resolves to the service tree and its subprojects get scanned as though they were the
// family. Found by launching the host the documented way and reading what it indexed.
static string DefaultRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json")))
    {
        directory = directory.Parent;
    }

    return directory?.Parent?.FullName
        ?? Directory.GetParent(Directory.GetCurrentDirectory())?.FullName
        ?? Directory.GetCurrentDirectory();
}

static string DefaultDatabasePath() => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris", "knowledge.db");

public sealed record StatusResponse(bool Semantic, string Tier, string? Note);
public sealed record RepositoryResponse(string Name, int Total, int Local, int Canonical);
public sealed record HitResponse(
    string Id, string Repository, string Kind, string Title, string Path, string? Excerpt, double Score);
public sealed record EntryResponse(
    string Id, string Repository, string Kind, string Provenance, string Title, string Path, string Body);
public sealed record ConvergenceEntryResponse(
    string Id, string Repository, string Kind, string Title, string Path);
public sealed record ConvergenceResponse(
    string Method, double Similarity, IReadOnlyList<string> Repositories,
    IReadOnlyList<ConvergenceEntryResponse> Entries, string Suggestion);
public sealed record QuestResponse(
    string Id, string From, string To, string Title, string Body,
    string Status, string? Note, DateTimeOffset Filed, DateTimeOffset Updated);
public sealed record PublishQuestRequest(string From, string To, string Title, string Body);
public sealed record RespondQuestRequest(string? Action, string? Reason);
public sealed record QuestActionResponse(QuestResponse Quest, string Message);
public sealed record RefreshResponse(int Entries, int Repositories, int Withheld, string? SemanticError);
public sealed record DomainRequest(string? Summary, IReadOnlyList<string>? Owns, IReadOnlyList<string>? Accepts);
public sealed record RegisterRequest(
    string Repository, IReadOnlyList<string>? Packs, string? CanonSource, DomainRequest? Domain, string? Root);
public sealed record RegisteredResponse(string Repository, DateTimeOffset At);
public sealed record RegistrationResponse(
    string Repository, bool Adopted, bool Registered, string? Summary,
    IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts, IReadOnlyList<string> Packs, int Entries,
    string? Root);
public sealed record SessionResponse(
    string Id, string Quest, string Repository, string Adapter, string State,
    string? Note, string? Evidence, string? Transcript, DateTimeOffset Created, DateTimeOffset Updated);
public sealed record OpenSessionRequest(string Quest, string Adapter);
public sealed record AdvanceSessionRequest(string? State, string? Note, string? Evidence, string? Transcript);
public sealed record SessionActionResponse(SessionResponse Session, string Message);
public sealed record ErrorResponse(string Error);

[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(IEnumerable<RepositoryResponse>))]
[JsonSerializable(typeof(IEnumerable<HitResponse>))]
[JsonSerializable(typeof(EntryResponse))]
[JsonSerializable(typeof(IEnumerable<ConvergenceResponse>))]
[JsonSerializable(typeof(IEnumerable<QuestResponse>))]
[JsonSerializable(typeof(PublishQuestRequest))]
[JsonSerializable(typeof(RespondQuestRequest))]
[JsonSerializable(typeof(QuestActionResponse))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(RegisteredResponse))]
[JsonSerializable(typeof(IEnumerable<RegistrationResponse>))]
[JsonSerializable(typeof(IEnumerable<SessionResponse>))]
[JsonSerializable(typeof(OpenSessionRequest))]
[JsonSerializable(typeof(AdvanceSessionRequest))]
[JsonSerializable(typeof(SessionActionResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class ApiJson : JsonSerializerContext;
