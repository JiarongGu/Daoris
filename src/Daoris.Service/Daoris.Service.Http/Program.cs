using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Daoris.Knowledge;
using Lyntai.Embeddings;
using Lyntai.Memory;
using Lyntai;
using Lyntai.Providers.OpenAiCompatible;

// The browser's half of the service. The MCP host serves an agent over stdio; a browser cannot speak
// that, so this exists — the same composed service behind a read-only JSON surface.
//
//   DAORIS_KNOWLEDGE_ROOT  where the repositories are      (default: the parent of this workspace)
//   DAORIS_KNOWLEDGE_DB    where the index is kept         (default: ~/.daoris/knowledge.db)
//   DAORIS_EMBED_MODEL     naming one turns semantic on    (absent: lexical only, and it says so)
//   DAORIS_EMBED_URL       the endpoint                    (default: http://localhost:11434)
//   DAORIS_WEB_ORIGIN      the dev UI's origin for CORS    (absent: same-origin only)
//
// READ-ONLY BY CONSTRUCTION (D31). There is no endpoint that writes doctrine, which is a design
// decision rather than an unfinished feature: `upstream` routes an improvement through the repository
// that found it, where it meets that repository's review, and a web editor would win against that path
// for the wrong reason. It also means there is nothing here to authenticate for the first version.
if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}

var builder = WebApplication.CreateBuilder(args);

var options = ServiceOptions.FromEnvironment(DefaultRepositoryRoot(), DefaultDatabasePath());

// The provider is built HERE, not in Core: the domain holds `IEmbedder` and nothing that implements
// one, so a model never reaches it (D22, D24). What tier that produces is Core's business.
IEmbedder? embedder = null;
if (!string.IsNullOrWhiteSpace(options.EmbedModel))
{
    embedder = new HttpEmbedder(
        id: "daoris-embed",
        config: new OpenAiCompatibleEmbedderOptions
        {
            BaseUrl = options.EmbedUrl ?? "http://localhost:11434",
            Model = options.EmbedModel,
        },
        httpFactory: () => new HttpClient(),
        options: new LyntaiOptions());
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
    (await s.Quests.ListAsync(repository, includeClosed ?? false, ct)).Select(q => new QuestResponse(
        q.Id, q.From, q.To, q.Title, q.Body, q.Status.ToString(), q.Note, q.Filed, q.Updated)));

// Where `daoris connect` lands. The one endpoint that accepts anything, and it accepts a repository's
// description of ITSELF — which is the only thing a repository is authoritative about.
app.MapPost("/api/registry", (ComposedService s, RegisterRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body.Repository)) return Results.BadRequest(new ErrorResponse("repository is required"));

    s.Service.Register(new Registration(
        body.Repository,
        Adopted: true,
        body.Domain?.Summary,
        body.Domain?.Owns ?? [],
        body.Domain?.Accepts ?? [],
        body.Packs ?? [],
        Entries: 0));

    return Results.Ok(new RegisteredResponse(body.Repository, DateTimeOffset.UtcNow));
});

app.MapGet("/api/registry", async (ComposedService s, CancellationToken ct) =>
    (await s.Service.RegistryAsync(ct)).Select(r => new RegistrationResponse(
        r.Repository, r.Adopted, r.Registered, r.Summary, r.Owns, r.Accepts, r.Packs, r.Entries)));

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

static string DefaultRepositoryRoot() =>
    Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? Directory.GetCurrentDirectory();

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
public sealed record RefreshResponse(int Entries, int Repositories, int Withheld, string? SemanticError);
public sealed record DomainRequest(string? Summary, IReadOnlyList<string>? Owns, IReadOnlyList<string>? Accepts);
public sealed record RegisterRequest(
    string Repository, IReadOnlyList<string>? Packs, string? CanonSource, DomainRequest? Domain);
public sealed record RegisteredResponse(string Repository, DateTimeOffset At);
public sealed record RegistrationResponse(
    string Repository, bool Adopted, bool Registered, string? Summary,
    IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts, IReadOnlyList<string> Packs, int Entries);
public sealed record ErrorResponse(string Error);

[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(IEnumerable<RepositoryResponse>))]
[JsonSerializable(typeof(IEnumerable<HitResponse>))]
[JsonSerializable(typeof(EntryResponse))]
[JsonSerializable(typeof(IEnumerable<ConvergenceResponse>))]
[JsonSerializable(typeof(IEnumerable<QuestResponse>))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(RegisteredResponse))]
[JsonSerializable(typeof(IEnumerable<RegistrationResponse>))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class ApiJson : JsonSerializerContext;
