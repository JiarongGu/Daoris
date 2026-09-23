using System.Text;
using System.Text.Json.Serialization;
using Daoris.Knowledge;
using Daoris.Knowledge.Hosting;
using Daoris.Knowledge.Http;

// The browser's half of the service. The MCP host serves an agent over stdio; a browser cannot speak
// that, so this exists — the same composed service behind a read-only JSON surface.
//
//   DAORIS_HOME            where every machine-local file lives (D63) — the installed desktop's own
//                          `data/`, set for the account; with neither it nor the DB named, exit 2
//   DAORIS_KNOWLEDGE_ROOT  where the repositories are      (default: the parent of this workspace)
//   DAORIS_KNOWLEDGE_DB    where the index is kept         (default: $DAORIS_HOME/knowledge.db)
//   DAORIS_EMBED_MODEL     naming one turns semantic on    (absent: lexical only, and it says so)
//   DAORIS_EMBED_URL       the endpoint                    (default: http://localhost:11434)
//   DAORIS_WEB_ORIGIN      the dev UI's origin for CORS    (absent: same-origin only)
//   DAORIS_WORKSPACE       which circle a SHARED host serves    (default: `default`; D48 §5)
//                          Refused on a local host, which holds every workspace this machine wired.
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
// carry it. There are exactly two trust shapes (D47 §7, as amended): LOCAL trusts the loopback — the
// OS account is the boundary (D21) — and SHARED gates every route with minted keys. Binding beyond
// loopback without shared mode refuses to start, so no third shape can exist by accident.
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

// A shared deployment is a WORKSPACE's deployment (D48 §5): it carries one circle's identity, takes
// every registration into it, and refuses one that declares another. A local host has no identity —
// it holds every circle the person wired — and naming one there is refused rather than ignored.
var (hostWorkspace, workspaceError) = Access.ParseWorkspace(
    mode, Environment.GetEnvironmentVariable(Access.WorkspaceVariable));
if (workspaceError is not null)
{
    Console.Error.WriteLine(workspaceError);
    return 2;
}

// The index lives under the Daoris home (D63) unless named directly. No home and no name is a
// refusal, not a default: an index written somewhere nobody pointed this host is the thing removed.
var database = Environment.GetEnvironmentVariable(ServiceOptions.DatabaseVariable)
    ?? HostComposition.DefaultDatabasePath();
if (database is null)
{
    Console.Error.WriteLine(DaorisHome.Sentence);
    return 2;
}

var options = ServiceOptions.FromEnvironment(DefaultRepositoryRoot(), database);

// Key administration is a console verb on the serving binary — same store, no second tool, and it
// exits without binding. Console minting is the whole story until person-auth exists (D47 §7).
if (args is ["keys", .. var keyArgs])
{
    return await KeysConsole.RunAsync(keyArgs, options);
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
if (Access.RefuseStartup(mode, urls) is { } refusal)
{
    Console.Error.WriteLine(refusal);
    return 2;
}

// The provider is built HOST-SIDE, not in Core: the domain holds `IVectorProvider` and nothing that
// implements one, so a model never reaches it (D22, D24). What tier that produces is Core's business;
// the construction itself is HostComposition's, shared with the MCP host so the two cannot drift.
var embedder = HostComposition.BuildEmbedder(options);

// Every verb commits in this host's store and the driver's sync carries it (D68). What a LOCAL host
// needs of the remotes map is only whether a circle is wired, which a chain's composition asks — read
// from the map when asked. A shared host IS a remote, and wires nothing.
// A shared deployment is fed, not scanned (D47 §4) — and not only at the refresh route: the service
// indexes on first use when its store is empty, so a shared host composed with the filesystem source
// would scan the server's own disk on its first request and serve what it found to keyed callers.
var composed = await ServiceFactory.CreateAsync(
    options, embedder, wired: mode == ServiceMode.Local ? RemoteConfig.IsWired : null,
    source: mode == ServiceMode.Shared ? new EmptyKnowledgeSource() : null,
    // A quest's files are kept by the machine that has them (D65 §2): a local host keeps them under
    // its home, and a shared host keeps none — it holds names, and its door refuses bytes outright.
    files: mode == ServiceMode.Local ? QuestFiles.FromEnvironment() : null);
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
    app.UseStaticFiles(new StaticFileOptions
    {
        // 🔴 The page is UNHASHED and names the hashed assets, so it decides what the window runs —
        // and a browser told nothing reuses it without asking. Seen on the second deployment: with
        // no Cache-Control here, a page the window had loaded once from a stale host was answered
        // from the WebView2 profile's cache on every start after, with the right host running and
        // never asked. A hashed name is a promise, so an asset may be kept for good; the page must
        // be asked about every time, which with the ETag already sent is a 304.
        OnPrepareResponse = prepared =>
        {
            var path = prepared.Context.Request.Path.Value ?? string.Empty;
            prepared.Context.Response.Headers.CacheControl =
                path.StartsWith("/assets/", StringComparison.Ordinal)
                    ? "public, max-age=31536000, immutable"
                    : "no-cache";
        },
    });
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

// There is deliberately no third gate. D36's interim single-key write gate was retired with nothing
// deployed (D47 §7, as amended): local mode trusts the loopback outright — the OS account is the
// boundary (D21), and the startup refusal above keeps local mode ON the loopback — while shared mode
// gates everything with minted keys. Two credential stories would drift, and the weaker would win.

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

// Every cross-repository answer below takes `workspace` — the unit of sharing (D48 §4). Absent spans
// every circle this deployment holds, which for a shared host is one by construction and for a local
// one is the person's own machine (D21). The DOOR never invents a default: a caller that wants its
// own circle says so, because only the caller knows which repository it is speaking for.
app.MapGet("/api/repositories", async (ComposedService s, string? workspace, CancellationToken ct) =>
    (await s.Service.SummarizeAsync(workspace, ct))
        .Select(r => new RepositoryResponse(
            r.Repository, r.Total, r.Local, r.Canonical, r.Workspace,
            // Staleness a person can SEE beats freshness they must assume (D48 §6).
            r.Fed is null
                ? null
                : new ProvenanceResponse(
                    r.Fed.Commit, r.Fed.ShortCommit, r.Fed.CommittedAt, r.Fed.Branch, r.Fed.Origin))));

app.MapGet("/api/search", async (
    ComposedService s, string q, string? kinds, string? repositories, bool? localOnly, int? limit,
    string? workspace, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new ErrorResponse("q is required"));

    var hits = await s.Service.SearchAsync(new KnowledgeQuery(q)
    {
        Kinds = KnowledgeQuery.ParseKinds(kinds),
        Repositories = KnowledgeQuery.ParseSet(repositories),
        Provenance = (localOnly ?? true) ? Provenance.Local : null,
        Limit = Math.Clamp(limit ?? 20, 1, 100),
        Workspace = workspace,
    }, ct);

    return Results.Ok(hits.Select(h => new HitResponse(
        h.Entry.Id, h.Entry.Repository, h.Entry.Kind.ToString(), h.Entry.Title,
        h.Entry.RelativePath, h.Excerpt, h.Score, h.Entry.Workspace)));
});

app.MapGet("/api/entry", async (ComposedService s, string id, CancellationToken ct) =>
{
    var entry = await s.Service.FindAsync(id, ct);
    return entry is null
        ? Results.NotFound(new ErrorResponse($"no entry with id '{id}'"))
        : Results.Ok(ToEntry(entry));
});

// One repository's own knowledge, whole — what a sync loop feeds from (D47 §4). Local provenance
// only, because that is the only class a feed may carry: canonical doctrine is distributed by `sync`.
app.MapGet("/api/entries", async (ComposedService s, string repository, CancellationToken ct) =>
    (await s.Service.LocalEntriesAsync(repository, ct)).Select(ToEntry));

// The landing view's endpoint (D30). Convergence is the centre of this UI, not a feature on a menu:
// search answers a question you have, and comparison tells you which question to ask.
app.MapGet("/api/convergence", async (
    ComposedService s, double? minimumSimilarity, string? kinds, int? limit, string? workspace,
    CancellationToken ct) =>
{
    var found = await s.Service.FindConvergenceAsync(
        new ConvergenceOptions(
            Math.Clamp(minimumSimilarity ?? 0.82, 0, 1), KnowledgeQuery.ParseKinds(kinds),
            Math.Clamp(limit ?? 25, 1, 100), workspace),
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
    ComposedService s, HttpContext http, string? repository, bool? includeClosed, string? workspace,
    CancellationToken ct) =>
    (await s.Quests.ListAsync(repository, includeClosed ?? false, workspace, ct))
        .Select(q => ToQuest(q, s.Files, MachineLocal(http))));

// Publish and respond go through the same exchange the MCP host uses, so the two doors cannot drift
// on who may be addressed or what declining requires. This pair is what makes a REMOTE deployment a
// transfer of work rather than a read-only mirror — and it needs no model at all (D24).
app.MapPost("/api/quests", async (
    ComposedService s, HttpContext http, PublishQuestRequest body, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.From) || string.IsNullOrWhiteSpace(body.To)
        || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Body))
    {
        return Results.BadRequest(new ErrorResponse("from, to, title and body are all required"));
    }

    // Which shape a file may arrive in is this deployment's mode (D65 §2). A LOCAL host is on the
    // machine that has the file, so a file comes with its content and is kept here. A SHARED host is
    // where machines' quests meet and keeps names only — a content field reaching it is bytes leaving
    // a machine, which is refused rather than quietly dropped, because dropped looks kept.
    var uploads = new List<QuestUpload>();
    var named = new List<QuestAttachment>();
    foreach (var file in body.Attachments ?? [])
    {
        if (string.IsNullOrWhiteSpace(file.Name))
        {
            return Results.BadRequest(new ErrorResponse("every attachment needs a name"));
        }

        if (mode == ServiceMode.Shared)
        {
            if (file.Content is not null)
            {
                return Results.BadRequest(new ErrorResponse(
                    $"`{file.Name}` arrived with its content, and a shared deployment keeps names, never bytes "
                    + "(D65 §2) — the file stays on the machine that has it, which sends its name and hash."));
            }

            if (file.Sha256 is null || file.Bytes is null)
            {
                return Results.BadRequest(new ErrorResponse(
                    $"`{file.Name}` needs its sha256 and its size — by name is the only way a file reaches a shared deployment."));
            }

            named.Add(new QuestAttachment(file.Name, file.Sha256, file.Bytes.Value));
        }
        else
        {
            if (file.Content is null)
            {
                return Results.BadRequest(new ErrorResponse(
                    $"`{file.Name}` arrived without its content — this host is on the machine that keeps a quest's "
                    + "files, so it takes them whole."));
            }

            uploads.Add(new QuestUpload(file.Name, file.Content));
        }
    }

    var outcome = await s.Exchange.PublishAsync(
        new QuestAsk(body.From, body.To, body.Title, body.Body)
        {
            Links = body.Links ?? [],
            Uploads = uploads,
            Named = named,
            // The chain (D65 §4). A step missing its words arrives blank and is refused by the
            // exchange naming which step — the same sentence every door gives.
            Then = (body.Then ?? []).Select(step => new QuestStep(step.To ?? "", step.Title ?? "", step.Body ?? "")).ToList(),
        },
        DateTimeOffset.UtcNow, ct);

    return outcome.Refusal switch
    {
        QuestPublishRefusal.None => Results.Ok(
            new QuestActionResponse(ToQuest(outcome.Quest!, s.Files, MachineLocal(http)), outcome.Message)),
        // Two circles that were never joined is a state conflict, not a malformed ask — the same 409
        // shape the quest lock teaches. The sentence names both sides (D48 §4).
        QuestPublishRefusal.CrossWorkspace => Results.Conflict(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

app.MapPost("/api/quests/{id}/respond", async (
    ComposedService s, HttpContext http, string id, RespondQuestRequest body, CancellationToken ct) =>
{
    var outcome = await s.Exchange.RespondAsync(
        id, body.Action ?? "", body.Reason, DateTimeOffset.UtcNow, ct);

    return outcome.Refusal switch
    {
        QuestRespondRefusal.None => Results.Ok(
            new QuestActionResponse(ToQuest(outcome.Quest!, s.Files, MachineLocal(http)), outcome.Message)),
        QuestRespondRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        // The refused transition is a state conflict, not a bad request: the losing side of the
        // cross-machine race reads 409 as "someone got there first" and stands down (D47 §5).
        QuestRespondRefusal.AlreadyTaken or QuestRespondRefusal.Closed =>
            Results.Conflict(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

// A quest's file, whole — so a person reading the drawer can open the screenshot the quest carries.
// LOCAL mode only and to a caller on this machine only: the bytes never left it (D65 §2), and a shared
// deployment has no door here because it has none of them. Found by hash, never by a path a caller
// names, so nothing outside the quest's own directory is reachable through it.
if (mode == ServiceMode.Local)
{
    app.MapGet("/api/quests/{id}/attachments/{sha256}", async (
        ComposedService s, HttpContext http, string id, string sha256, CancellationToken ct) =>
    {
        var quest = MachineLocal(http) ? await s.Quests.FindAsync(id.TrimStart('#'), ct) : null;
        var attachment = quest?.Attachments.FirstOrDefault(a => string.Equals(a.Sha256, sha256, StringComparison.Ordinal));
        if (quest is null || attachment is null || s.Files is null || !s.Files.Has(quest.Id, attachment))
        {
            return Results.NotFound(new ErrorResponse(
                quest is not null && attachment is not null
                    ? $"`{attachment.Name}` is named on quest `#{quest.Id}` and not kept on this machine — its bytes "
                      + "are on the machine that published it."
                    : $"quest `#{id.TrimStart('#')}` carries no file with that hash"));
        }

        // 🔴 An attached page must not run as the platform. The file is served from the platform's own
        // origin, so an HTML or SVG a session attached would otherwise be a script with every route
        // this host answers: `sandbox` makes it an opaque origin with no script, `nosniff` stops a
        // browser promoting a text file into a page, and anything that is not plainly an image, a
        // PDF or text is a download rather than a document.
        http.Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; img-src 'self'; style-src 'unsafe-inline'";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        var type = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider()
            .TryGetContentType(attachment.Name, out var known) ? known : "application/octet-stream";
        var inline = type is "image/png" or "image/jpeg" or "image/gif" or "image/webp" or "application/pdf"
            || type.StartsWith("text/plain", StringComparison.Ordinal);

        return Results.File(
            s.Files.PathOf(quest.Id, attachment), type, fileDownloadName: inline ? null : attachment.Name);
    });
}

// Asks (D65 §1a): a sentence entered at a WORKSPACE, answered by the tier this machine has — the
// declarations with no harness, a named receiver at once. LOCAL mode only: the intake is this
// machine's, like a quest file's bytes, and a shared deployment has no door here.
if (mode == ServiceMode.Local)
{
    app.MapGet("/api/asks", async (
        ComposedService s, HttpContext http, string? workspace, bool? includeClosed, CancellationToken ct) =>
        (await s.Asks.ListAsync(workspace, includeClosed ?? false, ct)).Select(a => ToAsk(a, s.Files, MachineLocal(http))));

    app.MapPost("/api/asks", async (ComposedService s, HttpContext http, AskRequestBody body, CancellationToken ct) =>
    {
        var uploads = new List<QuestUpload>();
        foreach (var file in body.Attachments ?? [])
        {
            if (string.IsNullOrWhiteSpace(file.Name) || file.Content is null)
            {
                return Results.BadRequest(new ErrorResponse(
                    "every attachment needs its name and its content — an ask is made on the machine that has the file"));
            }

            uploads.Add(new QuestUpload(file.Name, file.Content));
        }

        var outcome = await s.Asks.AskAsync(
            new AskRequest(body.Workspace ?? "", body.Sentence ?? "")
            {
                Links = body.Links ?? [],
                Uploads = uploads,
                To = body.To,
            },
            DateTimeOffset.UtcNow, ct);
        return AskAnswer(outcome, s, http);
    });

    app.MapPost("/api/asks/{id}/publish", async (
        ComposedService s, HttpContext http, string id, AskPublishRequest body, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(body.To))
        {
            return Results.BadRequest(new ErrorResponse("to is required — the repository this ask becomes a quest for"));
        }

        return AskAnswer(await s.Asks.PublishAsync(id, body.To, DateTimeOffset.UtcNow, ct), s, http);
    });

    app.MapPost("/api/asks/{id}/close", async (
        ComposedService s, HttpContext http, string id, AskCloseRequest body, CancellationToken ct) =>
        AskAnswer(await s.Asks.CloseAsync(id, body.Reason ?? "", DateTimeOffset.UtcNow, ct), s, http));
}

// The driver's session records (D46). State only: the service never spawns a process — the record is
// what the platform renders and what survives a driver restart; the process handle stays with the
// driver that owns it. Judgement is the shared ledger's, so this door and any other cannot drift.
app.MapGet("/api/sessions", async (
    ComposedService s, HttpContext http, string? repository, bool? includeClosed, string? workspace,
    CancellationToken ct) =>
    (await s.Sessions.ListAsync(repository, includeClosed ?? false, workspace, ct))
        .Select(session => ToSession(session, MachineLocal(http))));

app.MapPost("/api/sessions", async (
    ComposedService s, HttpContext http, OpenSessionRequest body, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.Quest) || string.IsNullOrWhiteSpace(body.Adapter))
    {
        return Results.BadRequest(new ErrorResponse("quest and adapter are required"));
    }

    // The harness version and the profile are observed by the DRIVER before it spawns (D49 §4) — the
    // service has no binaries to look at, which is exactly the D46 §7 split: records here, processes
    // there. It records what it is told and judges none of it.
    var outcome = await s.Ledger.OpenAsync(
        body.Quest, body.Adapter, DateTimeOffset.UtcNow, body.HarnessVersion, body.Profile, body.Tree,
        body.BaseCommit, ct);

    return outcome.Refusal switch
    {
        SessionOpenRefusal.None => Results.Ok(
            new SessionActionResponse(ToSession(outcome.Session!, MachineLocal(http)), outcome.Message)),
        SessionOpenRefusal.QuestNotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        // State conflicts wear 409, the same shape the quest door teaches (D47 §5): a driver racing
        // another machine for a repository slot got beaten, not malformed.
        SessionOpenRefusal.QuestNotOpen or SessionOpenRefusal.RepositoryBusy =>
            Results.Conflict(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

// A chat is a session a person entered rather than the driver planned (D49 §3) — same record, same
// lock, no quest required. The process is the driver's, as ever: this opens the RECORD, and only a
// driver on this machine can put a harness behind it.
app.MapPost("/api/sessions/chat", async (
    ComposedService s, HttpContext http, OpenChatRequest body, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.Repository))
    {
        return Results.BadRequest(new ErrorResponse("repository is required"));
    }

    var outcome = await s.Ledger.OpenChatAsync(
        body.Repository,
        // The adapter is the harness, never a model (D24). Silence takes the supported one.
        string.IsNullOrWhiteSpace(body.Adapter) ? "claude-code" : body.Adapter,
        DateTimeOffset.UtcNow, body.HarnessVersion, body.Profile, body.Tree, body.BaseCommit, ct);

    return outcome.Refusal switch
    {
        SessionOpenRefusal.None => Results.Ok(
            new SessionActionResponse(ToSession(outcome.Session!, MachineLocal(http)), outcome.Message)),
        SessionOpenRefusal.RepositoryUnknown => Results.NotFound(new ErrorResponse(outcome.Message)),
        // Busy is a state conflict, the same shape the driven door and the quest lock teach.
        SessionOpenRefusal.RepositoryBusy => Results.Conflict(new ErrorResponse(outcome.Message)),
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
        // Terminal and an illegal move are conflicts with the record's current state — 409 like the
        // quest door. Only an unknown state name is a bad request.
        SessionAdvanceRefusal.Terminal or SessionAdvanceRefusal.InvalidMove =>
            Results.Conflict(new ErrorResponse(outcome.Message)),
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

    // One shared deployment serves one workspace (D48 §5). A row declaring another is not a
    // permission failure — it is a message delivered to the wrong building, so it says so naming both
    // sides. A conflict, not a bad request: the payload is well-formed and the deployment is wrong.
    if (Access.RefuseForeignWorkspace(hostWorkspace, body.Repository, body.Workspace) is { } foreign)
    {
        return Results.Conflict(new ErrorResponse(foreign));
    }

    var registered = await s.Service.RegisterAsync(new Registration(
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
        SharesKnowledge: (body.Join ?? false) && (body.ShareKnowledge ?? false),
        // A SHARED host puts every row in its own circle — it is that workspace's deployment, and the
        // receiving deployment's wiring is what decides where fed material lands (D48 §2/§5). On a
        // LOCAL host silence PRESERVES the row: an ordinary re-registration runs on every sync tick
        // and says nothing about the wiring, so a null must not re-point the repository to `default`.
        Workspace: hostWorkspace ?? body.Workspace,
        // The canonical line, as the checkout that registered knows it (D48 §6) — unstated preserves.
        DefaultBranch: body.DefaultBranch), DateTimeOffset.UtcNow, ct);

    // Answered with the workspace that TOOK, not the one that was asked for — the client learns which
    // circle it is actually wired to, including when it said nothing and the existing row held.
    return Results.Ok(new RegisteredResponse(body.Repository, DateTimeOffset.UtcNow, registered.InWorkspace));
});

// The registration lifecycle's other two verbs (D48 §3/§7). Neither touches a file: retiring ends a
// registration and re-wiring edits one field, which is what makes "remove" safe to put on a screen.
// The machine-path half of adding stays with the shell — a browser is never told, and never tells, a
// filesystem path (D46/D47 §4).
app.MapDelete("/api/registry/{repository}", async (
    ComposedService s, string repository, CancellationToken ct) =>
{
    var retired = await s.Service.RetireAsync(repository, ct);
    return Results.Ok(new RetiredResponse(
        repository, retired,
        retired
            ? $"`{repository}` is no longer registered here. Nothing was deleted: its files, its history "
              + "and its doctrine are its own — it has simply stopped being addressable and indexed on "
              + "this machine, and its entries leave the index on the next refresh."
            : $"`{repository}` was not registered here, so there was nothing to retire."));
});

// Re-wiring only — the workspace, and nothing else about the row (design §7: the two updates are kept
// visibly apart, because one is local instant wiring and the other edits a tracked file).
app.MapPost("/api/registry/{repository}/workspace", async (
    ComposedService s, string repository, WireRequest body, CancellationToken ct) =>
{
    // Re-wiring across a shared deployment's own boundary is the same refusal the registration door
    // gives (D48 §5) — a host that serves one circle cannot hold a row belonging to another.
    if (Access.RefuseForeignWorkspace(hostWorkspace, repository, body.Workspace) is { } foreign)
    {
        return Results.Conflict(new ErrorResponse(foreign));
    }

    var existing = (await s.Service.RegistryAsync(ct: ct))
        .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));
    if (existing is null)
    {
        return Results.NotFound(new ErrorResponse(
            $"`{repository}` is not registered here — `daoris connect` from inside it, or add it from Projects."));
    }

    var wired = await s.Service.RegisterAsync(
        existing with { Workspace = Workspaces.Normalize(body.Workspace) }, DateTimeOffset.UtcNow, ct);

    return Results.Ok(new RegisteredResponse(wired.Repository, DateTimeOffset.UtcNow, wired.InWorkspace));
});

// The bootstrap, as a verb a person runs (D48 §3). Local only: a shared deployment is fed, and has no
// disk of its own to read — the same sentence that keeps it off its own filesystem.
app.MapPost("/api/registry/import", async (ComposedService s, ImportRequest body, CancellationToken ct) =>
{
    if (mode == ServiceMode.Shared)
    {
        return Results.Conflict(new ErrorResponse(
            "a shared deployment is fed, not scanned — it has no checkouts of its own to import"));
    }

    var folder = string.IsNullOrWhiteSpace(body.Folder) ? options.RepositoryRoot : body.Folder;
    if (!Directory.Exists(folder))
    {
        return Results.BadRequest(new ErrorResponse($"no such folder: '{folder}'"));
    }

    var imported = await s.Service.ImportAsync(folder, DateTimeOffset.UtcNow, ct);
    var names = imported.Select(r => r.Repository).ToList();

    return Results.Ok(new ImportedResponse(
        folder, names.Count, names,
        names.Count == 0
            ? $"Nothing under '{folder}' — an import registers a folder's immediate subdirectories."
            : $"Registered {names.Count} from '{folder}': {string.Join(", ", names)}. Existing rows kept "
              + "their workspace: an import states none, and unstated wiring is preserved."));
});

app.MapGet("/api/registry", async (
    ComposedService s, HttpContext http, string? workspace, CancellationToken ct) =>
    (await s.Service.RegistryAsync(workspace, ct)).Select(r => new RegistrationResponse(
        r.Repository, r.Adopted, r.Registered, r.Summary, r.Owns, r.Accepts, r.Packs, r.Entries,
        // Machine-local by design (D46): a filesystem path is answered only to a caller on this
        // machine, so a remote deployment never serves anyone's disk layout to the network.
        Root: MachineLocal(http) ? r.Root : null,
        r.Joined, r.SharesKnowledge, r.InWorkspace, r.DefaultBranch)));

// A repository's code map (MAP3a): its modules and how they depend on each other, read from its own
// committed file — never written to (D32). Local mode reads the checkout on each ask; a repository
// with no checkout here answers with no file, until a feed brings one (MAP3b). The file's content is
// repository-relative by its own rules, so nothing machine-local rides this door.
app.MapGet("/api/code-map/{repository}", async (ComposedService s, string repository, CancellationToken ct) =>
    await s.Service.CodeMapAsync(repository, ct) is { } read
        ? Results.Ok(new CodeMapResponse(
            repository, read.File, read.Problem,
            read.Map?.Modules.Select(m => new CodeModuleResponse(m.Id, m.Path, m.Summary)).ToList() ?? [],
            read.Map?.Dependencies.Select(d => new CodeDependencyResponse(d.From, d.To, d.Kind)).ToList() ?? []))
        : Results.NotFound(new ErrorResponse($"`{repository}` is not a repository this service has registered.")));

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
    return Results.Ok(new RefreshResponse(
        report.Entries, report.Repositories, report.Withheld, report.SemanticError, report.Absent));
});

// ——— The feed and sync doors (D47 §§4–6, D68). Which doors exist depends on the deployment's role: a
// SHARED host is fed by desktops — records and content arrive attributed to the key that carried
// them — and orders the quest operations they push; a LOCAL host is fed by nobody, and its sync doors
// answer only its own driver. A door with no meaning in a mode does not exist in that mode.
const string OperationShape =
    "every operation names its machine, sequence, quest, kind and time; a publish carries from, to, title and "
    + "body, every file its name, sha256 and size, and every step its to, title and body; a conflict names "
    + "what it attempted";

if (mode == ServiceMode.Shared)
{
    // Session records, keyed by origin + id — the judgement already ran where the process lived; the
    // record upserts whole and is never re-judged (D47 §6). There is no transcript field to strip,
    // because the DTO carries none. Only joined repositories' records are taken (§4).
    app.MapPost("/api/feed/sessions", async (
        ComposedService s, HttpContext http, FeedSessionsRequest body, CancellationToken ct) =>
    {
        // Unreachable while the shared gate stamps every /api caller — kept deliberately: the origin
        // is the record's attribution, and a gate refactor that dropped the stamp must fail HERE,
        // loudly, not mirror records under an empty name.
        var origin = http.Items["daoris.principal"] as string;
        if (string.IsNullOrWhiteSpace(origin))
        {
            return Results.BadRequest(new ErrorResponse("the feed carries its key's identity — this door answers only keyed callers"));
        }

        var outcome = await new SessionFeed(s.Service, s.Sessions).FeedAsync(
            origin,
            (body.Records ?? []).Select(r => new FedSessionRecord(
                r.Id, r.Quest, r.Repository, r.Adapter, r.State, r.Note, r.Evidence, r.Created, r.Updated,
                r.Kind, r.HarnessVersion))
                .ToList(),
            ct);

        return outcome.Refusal switch
        {
            SessionFeedRefusal.None => Results.Ok(new FeedResponse(outcome.Records, outcome.Message)),
            SessionFeedRefusal.NotJoined => Results.Conflict(new ErrorResponse(outcome.Message)),
            _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
        };
    });

    // Knowledge content — never vectors (D47 §4): each deployment embeds with its own provider, and
    // this one still serves lexical search with none. The disclosure judgement and the provenance
    // judgement are both the service's own (FeedAsync), so any future door shares them.
    app.MapPost("/api/feed/entries", async (
        ComposedService s, HttpContext http, FeedEntriesRequest body, CancellationToken ct) =>
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

        // The commit this feed speaks for (D48 §6). Whole or nothing, like every pair in this system:
        // half a provenance cannot be compared with what is held, and the door must not assemble a
        // plausible one out of the half it got.
        var provenance = body.Commit is { Length: > 0 } commit
            && body.CommittedAt is { } committedAt
            && body.Branch is { Length: > 0 } branch
                // The origin is the key's own identity — recorded because the write carried it, not
                // because a second identity model was invented (D47 §7).
                ? new FeedProvenance(commit, committedAt, branch, http.Items["daoris.principal"] as string)
                : null;

        var outcome = await s.Service.FeedAsync(body.Repository, entries, provenance, ct);
        if (outcome.Accepted) return Results.Ok(new FeedResponse(outcome.Entries, outcome.Message));

        // A feed with no commit is a malformed request; everything else is a state conflict — this
        // deployment holds something the feed cannot replace. The `information` flag is what lets a
        // sync report a stale or branch feed as news rather than as a wall (§6).
        return outcome.Refusal == FeedRefusal.NoProvenance
            ? Results.BadRequest(new FeedRefusalResponse(outcome.Message, outcome.Information))
            : Results.Conflict(new FeedRefusalResponse(outcome.Message, outcome.Information));
    });

    // The remote's quest doors (D68, sync design §8): what it accepted after a number, in its order,
    // and a push rebased on one, judged quest by quest through the same table every machine uses.
    app.MapGet("/api/quests/operations", async (ComposedService s, long? since, CancellationToken ct) =>
    {
        var fetched = await s.Quests.OperationsSinceAsync(since ?? 0, ct: ct);
        return Results.Ok(new QuestOperationsResponse(
            fetched.Operations.Select(ToOperationWire).ToList(), fetched.Through, fetched.More));
    });

    app.MapPost("/api/quests/operations", async (ComposedService s, QuestPushRequest body, CancellationToken ct) =>
    {
        if (FromOperationWires(body.Operations, numbered: false) is not { } pushed)
        {
            return Results.BadRequest(new ErrorResponse(OperationShape));
        }

        // The remote re-judges a publish as its own door would (JudgeReceived), and files it by the
        // receiver's registration HERE — the push names no workspace (SYNC0a).
        var registered = await s.Service.RegistryAsync(ct: ct);
        var pushedTo = await s.Quests.ReceiveAsync(
            body.Base ?? 0, pushed,
            asked => s.Exchange.JudgeReceived(asked, registered),
            asked => registered.FirstOrDefault(r =>
                string.Equals(r.Repository, asked.To, StringComparison.OrdinalIgnoreCase))?.InWorkspace ?? Workspaces.Default,
            ct);

        return Results.Ok(new QuestPushResponse(
            pushedTo.Accepted.Select(a => new QuestAcceptanceWire(a.Machine, a.Sequence, a.Number)).ToList(),
            pushedTo.Behind,
            pushedTo.Refused.Select(r => new QuestPushRefusalWire(r.Quest, r.Reason)).ToList()));
    });
}
else
{
    // The machine's quest doors (D68, sync design §8) — what the driver's sync moves bytes through.
    // Replaying and rebasing happen in the store, through the one table; the driver judges nothing.
    app.MapGet("/api/quests/sync", async (ComposedService s, string? workspace, CancellationToken ct) =>
        Results.Ok(new QuestCursorResponse(
            Workspaces.Normalize(workspace), await s.Quests.CursorAsync(Workspaces.Normalize(workspace), ct))));

    app.MapPost("/api/quests/sync", async (ComposedService s, QuestIntegrateRequest body, CancellationToken ct) =>
    {
        if (FromOperationWires(body.Operations, numbered: true) is not { } fetched)
        {
            return Results.BadRequest(new ErrorResponse(OperationShape + " — and a fetched one carries the number the remote gave it"));
        }

        var circle = Workspaces.Normalize(body.Workspace);
        var integrated = await s.Quests.IntegrateAsync(circle, fetched, body.Through ?? 0, ct);

        // What may leave follows the receiver (sync design §8): joined in this circle, whether its
        // checkout is here or a teammate's row came down without one. Silence means local.
        var shared = (await s.Service.RegistryAsync(ct: ct))
            .Where(r => r.Joined && Workspaces.Same(r.InWorkspace, circle))
            .Select(r => r.Repository)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = await s.Quests.PendingAsync(circle, shared.Contains, ct);

        return Results.Ok(new QuestIntegrateResponse(
            integrated.Cursor,
            pending.Select(ToOperationWire).ToList(),
            integrated.Conflicts.Select(ToOperationWire).ToList()));
    });

    app.MapPost("/api/quests/sync/accepted", async (ComposedService s, QuestAcceptedRequest body, CancellationToken ct) =>
    {
        var accepted = (body.Accepted ?? []).ToList();
        if (accepted.Any(a => string.IsNullOrWhiteSpace(a.Machine) || a.Sequence is null || a.Number is null))
        {
            return Results.BadRequest(new ErrorResponse("every acceptance names its machine, its sequence and its number"));
        }

        await s.Quests.AcceptedAsync(
            accepted.Select(a => new QuestAcceptance(a.Machine!, a.Sequence!.Value, a.Number!.Value)).ToList(), ct);
        return Results.Ok(new FeedResponse(accepted.Count, $"{accepted.Count} operation(s) accepted."));
    });
}

app.Run();
return 0;

// A machine path is visible only to a loopback caller of a LOCAL host (D47 §4). In shared mode the
// answer is no for everyone — structural absence, not a policed permission: the deployment whose
// callers are the network has no branch that serves a path.
bool MachineLocal(HttpContext http) => mode == ServiceMode.Local && IsLoopback(http);

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

// Links and attachment names travel with every read; a kept file's PATH answers only to a caller on
// this machine, and only when the bytes are here (D47 §4, D65 §2) — so null says "named, not held".
static QuestResponse ToQuest(Quest q, QuestFiles? files, bool machineLocal) => new(
    q.Id, q.From, q.To, q.Title, q.Body, q.Status.ToString(), q.Note, q.Filed, q.Updated, q.Workspace,
    q.Links,
    q.Attachments.Select(a => new QuestAttachmentResponse(
        a.Name, a.Sha256, a.Bytes,
        Path: machineLocal && files is not null && files.Has(q.Id, a) ? files.PathOf(q.Id, a) : null)).ToList(),
    q.Then.Select(s => new QuestStepWire(s.To, s.Title, s.Body)).ToList(),
    q.Parent,
    q.Conflicts.Select(c => new QuestConflictResponse(c.Machine, c.Attempted.ToString(), c.Note, c.At)).ToList());

// An operation as both quest doors carry it (sync design §8): a publish's ask without its workspace,
// files by name, and nothing a record may not name.
static QuestOperationWire ToOperationWire(QuestOperation o) => new(
    o.Number, o.Machine, o.Sequence, o.Quest, o.Kind.ToString().ToLowerInvariant(), o.At, o.Note,
    o.Attempted?.ToString(),
    o.Published is not { } asked
        ? null
        : new QuestAskedWire(
            asked.From, asked.To, asked.Title, asked.Body, asked.Links,
            asked.Attachments.Select(a => new QuestFileWire(a.Name, a.Sha256, a.Bytes)).ToList(),
            asked.Then.Select(s => new QuestStepWire(s.To, s.Title, s.Body)).ToList(),
            asked.Parent));

// Operations off the wire — null when any is not whole, because an operation half-read is one the
// store would replay as something nobody made. A publish arrives in the default circle; the store
// files it by the receiving side's wiring (SYNC0a).
static IReadOnlyList<QuestOperation>? FromOperationWires(IReadOnlyList<QuestOperationWire>? wires, bool numbered)
{
    var operations = new List<QuestOperation>();
    foreach (var w in wires ?? [])
    {
        if (string.IsNullOrWhiteSpace(w.Machine) || w.Sequence is null || string.IsNullOrWhiteSpace(w.Quest)
            || w.At is null || (numbered && w.Number is null)
            || !Enum.TryParse<QuestOperationKind>(w.Kind ?? "", ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            return null;
        }

        QuestStatus? attempted = null;
        if (kind == QuestOperationKind.Conflict)
        {
            if (!Enum.TryParse<QuestStatus>(w.Attempted ?? "", ignoreCase: true, out var lost) || !Enum.IsDefined(lost)) return null;
            attempted = lost;
        }

        Quest? published = null;
        if (kind == QuestOperationKind.Published)
        {
            if (w.Asked is not { From: { } from, To: { } to, Title: { } title, Body: { } body } asked
                || (asked.Attachments ?? []).Any(a => a.Name is null || a.Sha256 is null || a.Bytes is null)
                || (asked.Then ?? []).Any(s => s.To is null || s.Title is null || s.Body is null))
            {
                return null;
            }

            published = new Quest(w.Quest, from, to, title, body, QuestStatus.Open, null, w.At.Value, w.At.Value)
            {
                Links = asked.Links ?? [],
                Attachments = (asked.Attachments ?? []).Select(a => new QuestAttachment(a.Name!, a.Sha256!, a.Bytes!.Value)).ToList(),
                Then = (asked.Then ?? []).Select(s => new QuestStep(s.To!, s.Title!, s.Body!)).ToList(),
                Parent = asked.Parent,
            };
        }

        operations.Add(new QuestOperation(
            w.Quest, kind, w.Machine, w.Sequence.Value, w.At.Value, w.Note, published, attempted, numbered ? w.Number : null));
    }

    return operations;
}

// An ask's answer. A refusal is the desk's sentence, whole — including a named receiver the exchange
// refused, whose message already says the ask was kept and where it was proposed instead.
IResult AskAnswer(AskOutcome outcome, ComposedService s, HttpContext http) => outcome.Refusal switch
{
    AskRefusal.None => Results.Ok(new AskActionResponse(
        ToAsk(outcome.Ask!, s.Files, MachineLocal(http)), outcome.Message,
        outcome.Quest is null ? null : ToQuest(outcome.Quest, s.Files, MachineLocal(http)))),
    AskRefusal.UnknownWorkspace or AskRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
    AskRefusal.Closed or AskRefusal.QuestRefused => Results.Conflict(new ErrorResponse(outcome.Message)),
    _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
};

// The ask's record — its kept files' paths answered to this machine only, the quest files' rule.
static AskResponse ToAsk(Ask a, QuestFiles? files, bool machineLocal)
{
    var kept = files?.For(AskDesk.Folder);
    return new(
        a.Id, a.Workspace, a.Sentence, a.State.ToString(), a.Tier, a.Asked, a.Updated, a.Asker, a.Note, a.Links,
        a.Attachments.Select(f => new QuestAttachmentResponse(
            f.Name, f.Sha256, f.Bytes,
            Path: machineLocal && kept is not null && kept.Has(a.Id, f) ? kept.PathOf(a.Id, f) : null)).ToList(),
        a.Proposal.Select(m => new DeclarationMatchResponse(m.Repository, m.Score, m.Matched)).ToList(),
        a.Quests);
}

static EntryResponse ToEntry(KnowledgeEntry entry) => new(
    entry.Id, entry.Repository, entry.Kind.ToString(), entry.Provenance.ToString(),
    entry.Title, entry.RelativePath, entry.Body, entry.Anchor, entry.Workspace);

// The transcript is a machine-local path, guarded exactly as the registration's root is (D47 §4):
// answered only to a caller on this machine. The evidence stays — commits are the reviewable record
// and are meant to travel; the transcript is diagnostics for the machine that ran the session.
// The PROFILE NAME is guarded the same way and for the same reason (D49 §4): which account a session
// ran as is this machine's wiring, and it is the one field a person is likely to name after
// themselves. So is the TREE (D51), which is a filesystem path outright. The harness version is a
// fact about a tool and travels with the record.
static SessionResponse ToSession(Session s, bool loopback) => new(
    s.Id, s.Quest, s.Repository, s.Adapter, s.StateName, s.Note, s.Evidence,
    Transcript: loopback ? s.Transcript : null,
    s.Created, s.Updated, s.Workspace, s.Kind.ToString().ToLowerInvariant(),
    s.HarnessVersion,
    Profile: loopback ? s.Profile : null,
    Tree: loopback ? s.Tree : null,
    BaseCommit: loopback ? s.BaseCommit : null);

// A caller on this machine — which is what "the root never leaves the machine" means in practice. A
// null remote address is the in-process test server, which is this process and therefore local.
static bool IsLoopback(HttpContext http) =>
    http.Connection.RemoteIpAddress is null || System.Net.IPAddress.IsLoopback(http.Connection.RemoteIpAddress);

// Walk up from the BINARY to this workspace's manifest — the shared walk-up (HostComposition) — with
// THIS host's last resort: the parent-of-CWD heuristic. Never from the working directory first:
// `dotnet run` sets the CWD to the project directory, so "parent of the current directory" resolves
// to the service tree and its subprojects get scanned as though they were the family. Found by
// launching the host the documented way and reading what it indexed.
static string DefaultRepositoryRoot() =>
    HostComposition.AboveWorkspace()
    ?? Directory.GetParent(Directory.GetCurrentDirectory())?.FullName
    ?? Directory.GetCurrentDirectory();
