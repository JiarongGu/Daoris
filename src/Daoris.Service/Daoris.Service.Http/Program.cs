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
//   DAORIS_MODE            local (default) or shared (D47 §3/§7). Shared is the team deployment:
//                          EVERY /api route needs a minted key, no page is served (the remote is an
//                          API until person-auth exists), and no machine path is ever answered.
//                          Binding beyond loopback REQUIRES shared mode — the host refuses to start
//                          otherwise, which is the fail-safe inversion of service design §5.
//
// KEY ADMINISTRATION runs on this same binary, against the same store, and exits without serving:
//   keys mint --name <person@machine> [--days N]    prints the key ONCE; stores only its hash
//   keys list                                       every key's public face — prefix, name, expiry
//   keys revoke <prefix>                            ends one key; the prefix is the audit handle
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

var (mode, modeError) = Access.ParseMode(Environment.GetEnvironmentVariable(Access.ModeVariable));
if (modeError is not null)
{
    Console.Error.WriteLine(modeError);
    return 2;
}

var options = ServiceOptions.FromEnvironment(DefaultRepositoryRoot(), DefaultDatabasePath());

// Key administration is a console verb on the serving binary — same store, no second tool, and it
// exits without binding. Console minting is the whole story until person-auth exists (D47 §7).
if (args is ["keys", .. var keyArgs])
{
    return await KeysVerbAsync(keyArgs, options);
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
// default and the README's port is a lie. An explicit `--urls` or ASPNETCORE_URLS still wins — and
// the RESOLVED value is what both the startup judgement below and the bind itself use, because a
// refusal judged on one address while Kestrel binds another would be a gate on the wrong door.
var urls = builder.Configuration["urls"]
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? "http://localhost:5177";
builder.WebHost.UseUrls(urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

// The startup judgement (D47 §3): local trust bound beyond loopback does not warn — it does not start.
var serviceKey = Environment.GetEnvironmentVariable("DAORIS_SERVICE_KEY");
if (Access.RefuseStartup(mode, urls, !string.IsNullOrWhiteSpace(serviceKey)) is { } refusal)
{
    Console.Error.WriteLine(refusal);
    return 2;
}

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

// A LOCAL host relays verbs on remote-homed quests to the machine's remote, when one is configured
// (D47 §5/§9). A shared host never relays: it is the home the others write through to.
var remoteQuests = mode == ServiceMode.Local && RemoteConfig.Load() is { } remoteConfig
    ? new HttpRemoteQuests(remoteConfig)
    : null;

var composed = await ServiceFactory.CreateAsync(options, embedder, remoteQuests: remoteQuests);
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

// The built UI, when there is one — in local mode. A shared deployment serves the API and nothing
// else: the platform in a browser arrives with person-auth, not before (D47 §7), so until then there
// is no page a network caller could be shown.
if (mode == ServiceMode.Local)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// Shared mode gates EVERY route under /api, reads included (D47 §7): a remote serving the family's
// accumulated knowledge to unauthenticated GETs would be the §5 leak with no key leaked. The message
// never echoes what was presented; the prefix — the audit handle, non-secret by design — names an
// expired or revoked key back to its own holder.
if (mode == ServiceMode.Shared)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var header = context.Request.Headers.Authorization.ToString();
            const string scheme = "Bearer ";
            var presented = header.StartsWith(scheme, StringComparison.Ordinal)
                ? header[scheme.Length..].Trim()
                : "";
            var validation = await composed.Keys.ValidateAsync(
                presented, DateTimeOffset.UtcNow, context.RequestAborted);
            if (validation.Verdict == KeyVerdict.Valid)
            {
                // The key's name IS the caller's identity — per person, per machine — and it is what
                // fed records are attributed to (D47 §7): recorded because the write carried it, not
                // because a second identity model was invented.
                context.Items["daoris.principal"] = validation.Key!.Name;
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ErrorResponse(validation.Verdict switch
                {
                    KeyVerdict.Expired =>
                        $"key `{validation.Key!.Prefix}` expired {validation.Key.Expires:yyyy-MM-dd} — ask an operator to mint a fresh one",
                    KeyVerdict.Revoked =>
                        $"key `{validation.Key!.Prefix}` was revoked — ask an operator to mint a fresh one",
                    _ => "this deployment answers only minted keys — ask an operator for one, sent as a bearer token",
                }));
                return;
            }
        }

        await next();
    });
}

// Writes need the key when one is configured. Absence means local (D21) — on a developer's machine
// the OS account is the boundary and demanding a token would be ceremony. Set, it gates every POST
// under /api; reads stay open because the UI is read-only by design (D31) and this deployment shape
// is a trusted network's. Shared mode refused this variable at startup, so the two gates cannot
// coexist: one credential model per deployment.
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
        // The refused transition is a state conflict, not a bad request: the losing side of the
        // cross-machine race reads 409 as "someone got there first" and stands down (D47 §5).
        QuestRespondRefusal.AlreadyTaken or QuestRespondRefusal.Closed =>
            Results.Conflict(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

// The driver's session records (D46). State only: the service never spawns a process — the record is
// what the platform renders and what survives a driver restart; the process handle stays with the
// driver that owns it. Judgement is the shared ledger's, so this door and any other cannot drift.
app.MapGet("/api/sessions", async (
    ComposedService s, HttpContext http, string? repository, bool? includeClosed, CancellationToken ct) =>
    (await s.Sessions.ListAsync(repository, includeClosed ?? false, ct))
        .Select(session => ToSession(session, MachineLocal(http))));

app.MapPost("/api/sessions", async (
    ComposedService s, HttpContext http, OpenSessionRequest body, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.Quest) || string.IsNullOrWhiteSpace(body.Adapter))
    {
        return Results.BadRequest(new ErrorResponse("quest and adapter are required"));
    }

    var outcome = await s.Ledger.OpenAsync(body.Quest, body.Adapter, DateTimeOffset.UtcNow, ct);

    return outcome.Refusal switch
    {
        SessionOpenRefusal.None => Results.Ok(
            new SessionActionResponse(ToSession(outcome.Session!, MachineLocal(http)), outcome.Message)),
        SessionOpenRefusal.QuestNotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

app.MapPost("/api/sessions/{id}/state", async (
    ComposedService s, HttpContext http, string id, AdvanceSessionRequest body, CancellationToken ct) =>
{
    var outcome = await s.Ledger.AdvanceAsync(
        id, body.State ?? "", body.Note, body.Evidence, body.Transcript, DateTimeOffset.UtcNow, ct);

    return outcome.Refusal switch
    {
        SessionAdvanceRefusal.None => Results.Ok(
            new SessionActionResponse(ToSession(outcome.Session!, MachineLocal(http)), outcome.Message)),
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
        // A shared deployment never stores a machine path, even one a buggy client sent: the feed has
        // no field for it by design (D47 §4), and what must not be served is best not kept.
        Root: mode == ServiceMode.Shared || string.IsNullOrWhiteSpace(body.Root) ? null : body.Root,
        Joined: body.Join ?? false,
        // Knowledge feeds only from a joined repository (D47 §4) — narrowed here as well as in the
        // CLI, because this door also answers clients the CLI never saw.
        SharesKnowledge: (body.Join ?? false) && (body.ShareKnowledge ?? false)), DateTimeOffset.UtcNow, ct);

    return Results.Ok(new RegisteredResponse(body.Repository, DateTimeOffset.UtcNow));
});

app.MapGet("/api/registry", async (ComposedService s, HttpContext http, CancellationToken ct) =>
    (await s.Service.RegistryAsync(ct)).Select(r => new RegistrationResponse(
        r.Repository, r.Adopted, r.Registered, r.Summary, r.Owns, r.Accepts, r.Packs, r.Entries,
        // Machine-local by design (D46): a filesystem path is answered only to a caller on this
        // machine, so a remote deployment never serves anyone's disk layout to the network.
        Root: MachineLocal(http) ? r.Root : null,
        r.Joined, r.SharesKnowledge)));

app.MapPost("/api/refresh", async (ComposedService s, CancellationToken ct) =>
{
    // A shared deployment is fed, not scanned (D47 §4): fed entries are the only entries it has, and a
    // filesystem scan here would ghost-sweep every one of them for repositories it cannot see.
    if (mode == ServiceMode.Shared)
    {
        return Results.Conflict(new ErrorResponse(
            "a shared deployment is fed, not scanned — entries arrive with each desktop's sync"));
    }

    var report = await s.Service.RefreshAsync(ct);
    return Results.Ok(new RefreshResponse(report.Entries, report.Repositories, report.Withheld, report.SemanticError));
});

// ——— The feed doors (D47 §§4–6). Which doors exist depends on the deployment's role: a SHARED host
// is fed by desktops — records and content arrive attributed to the key that carried them — while a
// LOCAL host is never fed by anyone; it feeds, and takes only the quest mirror its own sync loop
// pulls down. A door with no meaning in a mode does not exist in that mode.
if (mode == ServiceMode.Shared)
{
    // Session records, keyed by origin + id — the judgement already ran where the process lived; the
    // record upserts whole and is never re-judged (D47 §6). There is no transcript field to strip,
    // because the DTO carries none. Only joined repositories' records are taken (§4).
    app.MapPost("/api/feed/sessions", async (
        ComposedService s, HttpContext http, FeedSessionsRequest body, CancellationToken ct) =>
    {
        var origin = http.Items["daoris.principal"] as string;
        if (string.IsNullOrWhiteSpace(origin))
        {
            return Results.BadRequest(new ErrorResponse("the feed carries its key's identity — this door answers only keyed callers"));
        }

        var records = body.Records ?? [];
        var joined = (await s.Service.RegistryAsync(ct)).Where(r => r.Joined)
            .Select(r => r.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Id) || string.IsNullOrWhiteSpace(record.Quest)
                || string.IsNullOrWhiteSpace(record.Repository) || !Session.TryParse(record.State ?? "", out _))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"record `{record.Id}` is not a session record — id, quest, repository and a known state are required"));
            }

            if (!joined.Contains(record.Repository))
            {
                return Results.Conflict(new ErrorResponse(
                    $"`{record.Repository}` has not joined this deployment — its records stay home"));
            }
        }

        foreach (var record in records)
        {
            Session.TryParse(record.State!, out var state);
            await s.Sessions.MirrorAsync(new Session(
                $"{origin}/{record.Id}", record.Quest, record.Repository, record.Adapter ?? "unknown",
                state, record.Note, record.Evidence, Transcript: null, record.Created, record.Updated), ct);
        }

        return Results.Ok(new FeedResponse(records.Count, $"{records.Count} session record(s) mirrored from `{origin}`."));
    });

    // Knowledge content — never vectors (D47 §4): each deployment embeds with its own provider, and
    // this one still serves lexical search with none. The disclosure judgement is the service's own
    // (FeedAsync), so any future door shares it.
    app.MapPost("/api/feed/entries", async (ComposedService s, FeedEntriesRequest body, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(body.Repository))
        {
            return Results.BadRequest(new ErrorResponse("repository is required"));
        }

        var entries = new List<KnowledgeEntry>();
        foreach (var entry in body.Entries ?? [])
        {
            if (!Enum.TryParse<EntryKind>(entry.Kind ?? "", ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"unknown kind '{entry.Kind}' — one of: rule, knowledge, skill, decision, fix, taskoutcome"));
            }

            entries.Add(new KnowledgeEntry(
                body.Repository, kind, Provenance.Local, entry.Title ?? "", entry.Body ?? "",
                entry.RelativePath ?? "", entry.Anchor));
        }

        var outcome = await s.Service.FeedAsync(body.Repository, entries, ct);
        return outcome.Accepted
            ? Results.Ok(new FeedResponse(outcome.Entries, outcome.Message))
            : Results.Conflict(new ErrorResponse(outcome.Message));
    });
}
else
{
    // The mirror half of the sync (D47 §5): remote-homed quests land here for reading and planning.
    // MirrorAsync marks every row with its home, which is what makes it immovable locally — verbs on
    // it write through to the remote, and the next mirror carries the result back.
    app.MapPost("/api/feed/quests", async (ComposedService s, FeedQuestsRequest body, CancellationToken ct) =>
    {
        var quests = body.Quests ?? [];
        foreach (var quest in quests)
        {
            if (string.IsNullOrWhiteSpace(quest.Id)
                || !Enum.TryParse<QuestStatus>(quest.Status ?? "", ignoreCase: true, out var status)
                || !Enum.IsDefined(status))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"quest `{quest.Id}` is not mirrorable — an id and a known status are required"));
            }
        }

        foreach (var quest in quests)
        {
            Enum.TryParse<QuestStatus>(quest.Status!, ignoreCase: true, out var status);
            await s.Quests.MirrorAsync(new Quest(
                quest.Id.TrimStart('#'), quest.From, quest.To, quest.Title, quest.Body,
                status, quest.Note, quest.Filed, quest.Updated, Home: "remote"), ct);
        }

        return Results.Ok(new FeedResponse(quests.Count, $"{quests.Count} quest(s) mirrored."));
    });
}

app.Run();
return 0;

// A machine path is visible only to a loopback caller of a LOCAL host (D47 §4). In shared mode the
// answer is no for everyone — structural absence, not a policed permission: the deployment whose
// callers are the network has no branch that serves a path.
bool MachineLocal(HttpContext http) => mode == ServiceMode.Local && IsLoopback(http);

// `keys mint|list|revoke` — the deployment's own console is where machine credentials come from
// until person-auth exists (D47 §7). Exit codes are the contract: 0 clean, 1 the prefix named
// nothing, 2 bad usage.
static async Task<int> KeysVerbAsync(string[] args, ServiceOptions options)
{
    await using var composed = await ServiceFactory.CreateAsync(options);

    switch (args)
    {
        case ["mint", ..]:
        {
            string? name = null;
            var days = 90;
            for (var i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "--name") name = args[i + 1];
                if (args[i] == "--days" && int.TryParse(args[i + 1], out var parsed)) days = parsed;
            }

            if (string.IsNullOrWhiteSpace(name) || days <= 0)
            {
                Console.Error.WriteLine("usage: keys mint --name <person@machine> [--days N]");
                return 2;
            }

            var minted = await composed.Keys.MintAsync(name, TimeSpan.FromDays(days), DateTimeOffset.UtcNow);
            Console.WriteLine(minted.Key);
            Console.WriteLine($"  name {name} · prefix {minted.Record.Prefix} · expires {minted.Record.Expires:yyyy-MM-dd}");
            Console.WriteLine("  Shown once and stored hashed — copy it now. Revoke by the prefix.");
            return 0;
        }

        case ["list"]:
        {
            var keys = await composed.Keys.ListAsync();
            if (keys.Count == 0)
            {
                Console.WriteLine("No keys minted. `keys mint --name <person@machine>` creates one.");
                return 0;
            }

            foreach (var key in keys)
            {
                var state = key.Revoked is not null ? $"revoked {key.Revoked:yyyy-MM-dd}"
                    : key.Expires <= DateTimeOffset.UtcNow ? $"expired {key.Expires:yyyy-MM-dd}"
                    : $"expires {key.Expires:yyyy-MM-dd}";
                Console.WriteLine($"  {key.Prefix}  {key.Name}  {state}");
            }

            return 0;
        }

        case ["revoke", var prefix]:
        {
            if (await composed.Keys.RevokeAsync(prefix, DateTimeOffset.UtcNow))
            {
                Console.WriteLine($"Key `{prefix}` is revoked.");
                return 0;
            }

            Console.Error.WriteLine($"No key with prefix `{prefix}`. `keys list` names them.");
            return 1;
        }

        default:
            Console.Error.WriteLine("usage: keys mint --name <person@machine> [--days N] | keys list | keys revoke <prefix>");
            return 2;
    }
}

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

// The transcript is a machine-local path, guarded exactly as the registration's root is (D47 §4):
// answered only to a caller on this machine. The evidence stays — commits are the reviewable record
// and are meant to travel; the transcript is diagnostics for the machine that ran the session.
static SessionResponse ToSession(Session s, bool loopback) => new(
    s.Id, s.Quest, s.Repository, s.Adapter, s.StateName, s.Note, s.Evidence,
    Transcript: loopback ? s.Transcript : null,
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
    string Repository, IReadOnlyList<string>? Packs, string? CanonSource, DomainRequest? Domain, string? Root,
    bool? Join, bool? ShareKnowledge);
public sealed record RegisteredResponse(string Repository, DateTimeOffset At);
public sealed record RegistrationResponse(
    string Repository, bool Adopted, bool Registered, string? Summary,
    IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts, IReadOnlyList<string> Packs, int Entries,
    string? Root, bool Joined, bool SharesKnowledge);
public sealed record SessionResponse(
    string Id, string Quest, string Repository, string Adapter, string State,
    string? Note, string? Evidence, string? Transcript, DateTimeOffset Created, DateTimeOffset Updated);
public sealed record OpenSessionRequest(string Quest, string Adapter);
public sealed record AdvanceSessionRequest(string? State, string? Note, string? Evidence, string? Transcript);
public sealed record SessionActionResponse(SessionResponse Session, string Message);
public sealed record FeedSessionRecord(
    string Id, string Quest, string Repository, string? Adapter, string? State,
    string? Note, string? Evidence, DateTimeOffset Created, DateTimeOffset Updated);
public sealed record FeedSessionsRequest(IReadOnlyList<FeedSessionRecord>? Records);
public sealed record FeedEntryRecord(string? Kind, string? Title, string? Body, string? RelativePath, string? Anchor);
public sealed record FeedEntriesRequest(string Repository, IReadOnlyList<FeedEntryRecord>? Entries);
public sealed record FeedQuestRecord(
    string Id, string From, string To, string Title, string Body, string? Status, string? Note,
    DateTimeOffset Filed, DateTimeOffset Updated);
public sealed record FeedQuestsRequest(IReadOnlyList<FeedQuestRecord>? Quests);
public sealed record FeedResponse(int Accepted, string Message);
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
[JsonSerializable(typeof(FeedSessionsRequest))]
[JsonSerializable(typeof(FeedEntriesRequest))]
[JsonSerializable(typeof(FeedQuestsRequest))]
[JsonSerializable(typeof(FeedResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class ApiJson : JsonSerializerContext;
