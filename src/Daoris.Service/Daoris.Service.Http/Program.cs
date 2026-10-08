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
//   DAORIS_EMBED_WINDOW    the most characters one embedded text carries, title included; a longer
//                          entry is embedded in pieces (D123)   (default: 2000; under 200, exit 2)
//   DAORIS_WEB_ORIGIN      the dev UI's origin, for CORS and the write gate   (absent: same-origin only)
//   DAORIS_WORKSPACE       which circle a SHARED host serves    (default: `default`; D48 §5)
//                          Refused on a local host, which holds every workspace this machine wired.
//   DAORIS_STOP_ON_INPUT_END  `1`: stop cleanly when standard input ends — how the desktop stops a
//                          host it started (LOG2a). Unset, standard input is never opened.
//   DAORIS_PERSON_KEY_ON_INPUT  `1`: the first line of standard input is this start's person key, which
//                          gates the person's and the driver's doors (PERSONDOOR1a, D156). Unset, the
//                          input is not read for one, and the loopback is trusted as before. Local only.
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
// loopback without shared mode refuses to start, so no third shape can exist by accident. Neither shape
// takes a write from a web page it does not allow (ORIGIN1): a browser on this machine is on the loopback. And a
// local host answers only its loopback names (ORIGIN2), since a website's name can be pointed at the loopback too.
// Within the loopback, a local host its starter handed a person key opens the person's and the driver's doors only to
// that key (PERSONDOOR1a, D156): every session Daoris starts is on the loopback too, as the same account.
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

var (options, optionsError) = ServiceOptions.FromEnvironment(DefaultRepositoryRoot(), database);
if (optionsError is not null)
{
    Console.Error.WriteLine(optionsError);
    return 2;
}

// Key administration is a console verb on the serving binary — same store, no second tool, and it
// exits without binding. Console minting is the whole story until person-auth exists (D47 §7).
if (args is ["keys", .. var keyArgs])
{
    return await KeysConsole.RunAsync(keyArgs, options);
}

// The machine log (LOG1, D94): this host's start and stop, the framework's warnings and errors, every
// exception nothing caught, what a request's route threw (HOSTLOG1), and each request that failed or was
// slow — by route and status, never its query or body. In a file of its own beside the desktop's; with no
// home it writes nothing.
using var log = MachineLog.Open("host");
log.WatchUnhandled();
var started = DateTimeOffset.UtcNow;

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
builder.Logging.AddProvider(new MachineLogProvider(log));

// The documented address, made true by construction: with nothing configured, Kestrel binds its own
// default and the README's port is a lie. An explicit `--urls` or ASPNETCORE_URLS still wins — and
// the RESOLVED value is what both the startup judgement below and the bind itself use, because a
// refusal judged on one address while Kestrel binds another would be a gate on the wrong door.
var urls = builder.Configuration["urls"]
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? "http://localhost:5177";
builder.WebHost.UseUrls(urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

// The startup judgement (D47 §3): local trust bound beyond loopback does not warn — it does not start.
// Kestrel's own endpoints are judged too: they override the urls above when the host binds (REV3).
var endpoints = builder.Configuration.GetSection("Kestrel:Endpoints").GetChildren()
    .Select(endpoint => endpoint["Url"])
    .OfType<string>();
if (Access.RefuseStartup(mode, urls, endpoints) is { } refusal)
{
    Console.Error.WriteLine(refusal);
    return 2;
}

// PERSONDOOR1a (D156): the person key, read from the first line of standard input when the starter asks, before the host
// answers anyone. Its doors are gated by it below; a host asked and handed no key does not start, and one not asked keeps
// today's trust (the person-door design §2.1). PersonKey says why the input, and why nothing ever writes the key down.
var (personKey, personKeyRefusal) = PersonKey.FromInput(
    mode, Environment.GetEnvironmentVariable(PersonKey.InputVariable), () => Console.In, PersonKey.Wait);
if (personKeyRefusal is not null)
{
    Console.Error.WriteLine(personKeyRefusal);
    return 2;
}

var personGate = new PersonGate(personKey, log);

// The provider is built HOST-SIDE, not in Core: the domain holds `IVectorProvider` and nothing that
// implements one, so a model never reaches it (D22, D24). What tier that produces is Core's business;
// the construction itself is HostComposition's, shared with the MCP host so the two cannot drift.
var embedder = HostComposition.BuildEmbedder(options);

// Every verb commits in this host's store (D68). A LOCAL host syncs its quests with the remote each
// circle names — when its driver asks, and when a take on a shared quest claims by push (D69) — reading
// the remotes map when asked. A shared host IS a remote, and wires nothing.
// A shared deployment is fed, not scanned (D47 §4) — and not only at the refresh route: the service
// indexes on first use when its store is empty, so a shared host composed with the filesystem source
// would scan the server's own disk on its first request and serve what it found to keyed callers.
var composed = await ServiceFactory.CreateAsync(
    options, embedder, remotes: mode == ServiceMode.Local ? new ConfiguredRemotes() : null,
    source: mode == ServiceMode.Shared ? new EmptyKnowledgeSource() : null,
    // A quest's files are kept by the machine that has them (D65 §2): a local host keeps them under
    // its home, and a shared host keeps none — it holds names, and its door refuses bytes outright.
    files: mode == ServiceMode.Local ? QuestFiles.FromEnvironment() : null,
    // The rule proposals a clear of history reads and tidies (HIST1b) are this machine's files; a shared host has none.
    proposals: mode == ServiceMode.Local ? RuleProposalBox.FromEnvironment() : null,
    // A second opinion is this machine's alone (XAGENT1c): a shared host's desk refuses every door with its sentence.
    mode: mode);
builder.Services.AddSingleton(composed);

// KSCHEMA1: an index a newer Daoris wrote is left as it is; the host starts, and what is not derived works. Said once,
// here: on stderr with the folder to update, and in the log by its versions. Each route over the index refuses below.
if (composed.IndexRefusal is { } refusedIndex)
{
    Console.Error.WriteLine($"{refusedIndex.Message} This build runs from '{refusedIndex.Build}'.");
    log.Warn("index.refused", ("found", refusedIndex.Found), ("known", refusedIndex.Known));
}

// Source-generated serialization: this host publishes AOT-friendly and reflection-based JSON would be
// the one thing stopping it.
builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJson.Default);
    json.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Who may call this host from another origin: the development UI on another port, when that variable
// names it; and, on a LOCAL host only, the desktop's own page (D92), which lives on its engine's app
// origin and reaches this host at the loopback address. One list, which the write gate below reads too
// (ORIGIN1): CORS only keeps a page from reading an answer.
var origins = BrowserOrigins.Allowed(mode, Environment.GetEnvironmentVariable("DAORIS_WEB_ORIGIN"));
if (origins.Count > 0)
{
    builder.Services.AddCors(cors => cors.AddDefaultPolicy(p =>
        p.WithOrigins([.. origins]).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

// A request that failed or took over two seconds, into the log (LOG1a). By its route's pattern, so an
// id in the path and a search in the query never reach the file. Outermost, so a throw from anything
// after it (the gate, the page, a route) is the host's to write and answer (HOSTLOG1): the exception
// by its type, message and first frames, and the caller a sentence that names none of it, where the
// server alone wrote a line with no cause and answered an empty 500.
app.Use(async (context, next) =>
{
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var status = 0;
    try
    {
        await next();
        status = context.Response.StatusCode;
    }
    catch (NewerIndexException refused) when (!context.Response.HasStarted)
    {
        // KSCHEMA1: an index a newer Daoris wrote is a refusal the caller shows, not an error nobody expected. 409 in the
        // house's shape, as every refusal here is: nothing about it passes by waiting, so it is no busy host to try
        // again. Its sentence names no path, and nothing is written as an error: the host said it once, at its start.
        status = StatusCodes.Status409Conflict;
        context.Response.Clear();
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(refused.Message));
    }
    catch (Exception error) when (!UnhandledRequests.CallerLeft(context, error))
    {
        status = StatusCodes.Status500InternalServerError;
        var logged = UnhandledRequests.Write(log, context, error);
        if (!await UnhandledRequests.AnswerAsync(context, logged)) throw;
    }
    catch
    {
        status = StatusCodes.Status500InternalServerError;
        throw;
    }
    finally
    {
        if (status >= 500 || clock.ElapsedMilliseconds > MachineLogProvider.SlowRequestMs)
        {
            log.Warn("request.failed",
                ("method", context.Request.Method),
                ("route", UnhandledRequests.RouteOf(context)),
                ("status", status),
                ("ms", clock.ElapsedMilliseconds));
        }
    }
});

// ORIGIN2: a local host answers only its loopback names. A website whose DNS name is pointed at 127.0.0.1 is its own
// origin here, so neither CORS nor the origin gate below can tell its page from the host's own, and every read a
// loopback caller is given would be its: the index, the quests, the registration roots. So any request, read, write or
// preflight, under a name that is not `localhost` or a loopback address is refused before anything else runs. A request
// that names no host is no browser's (every browser names the host it asked for) and passes as a program's. Local mode
// only: a shared host is reached by whatever name its deployment gives it, and its key gate is what keeps a page off it
// (D47 §7), since a browser never sends a bearer key on its own.
if (mode == ServiceMode.Local)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Host.HasValue && !BrowserOrigins.IsLoopbackName(context.Request.Host.Host))
        {
            // Once per request, by the route's pattern: never the name the request used, its address, or the body.
            log.Warn(BrowserOrigins.HostEvent,
                ("method", context.Request.Method),
                ("route", UnhandledRequests.RouteOf(context)));
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(BrowserOrigins.HostSentence, BrowserOrigins.HostCode));
            return;
        }

        await next();
    });
}

if (origins.Count > 0) app.UseCors();

// ORIGIN1: no website presses a door. CORS keeps a page from reading an answer, never from sending a simple
// request (a POST with no body, a text body or a form body), so a write from a page this host does not allow is
// refused here, before any route runs: by its Origin, or with none by what its browser says of the site. A program
// sends neither header and passes as before. In both modes: a browser reaches a shared host too, and that host's
// key gate stops a page only for as long as a browser holds no credential for it. BrowserOrigins says why each
// header decides what it does, and why the shell's page, another site by its browser's reckoning, is answered.
var ownOrigin = mode == ServiceMode.Local;
app.Use(async (context, next) =>
{
    if (BrowserOrigins.Refused(context.Request, origins, ownOrigin) is { } by)
    {
        // Once per request, by the route's pattern and which header refused it: never the page's address, a header's
        // value or the body (machine-log design §5).
        log.Warn(BrowserOrigins.Event,
            ("method", context.Request.Method),
            ("route", UnhandledRequests.RouteOf(context)),
            ("by", by),
            ("site", BrowserOrigins.SiteOf(context.Request)));
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(BrowserOrigins.Sentence, BrowserOrigins.Code));
        return;
    }

    await next();
});

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

// PERSONDOOR1a (D156 point 3): a local host handed its person key gates its doors by the route's class, before any route
// runs: a read answers anyone; an agent's door answers a keyless call, judged as an agent's; the driver's own and the
// person's alone want the key; a key from another start is refused at every door but a read. Every refusal is 403 with a
// sentence and a code, and one `person.refused` line. The four forms a body makes are judged by their routes. A host handed
// no key has no gate here, and trusts the loopback as before. PersonDoors holds the table and why each door is whose.
if (personGate.Holds)
{
    app.Use(async (context, next) => await personGate.InvokeAsync(context, next));
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

// There is deliberately no third credential gate. D36's interim single-key write gate was retired with
// nothing deployed (D47 §7, as amended): local mode trusts the loopback — the OS account is the
// boundary (D21), and the startup refusal above keeps local mode ON the loopback — while shared mode
// gates everything with minted keys. Two credential stories would drift, and the weaker would win. The
// origin gate above is no credential: it tells a website's page from Daoris's own, which the loopback
// trust never asked, since a browser on this machine is on the loopback too (ORIGIN1). The person key
// (D156) narrows the local trust rather than adding a story beside it: minted for each start, never
// configured, read only by a local host, and enforced only by a host its starter handed one (PERSONDOOR1a).

app.MapGet("/api/status", (ComposedService s, string? prove) => new StatusResponse(
    Semantic: s.SemanticEnabled,
    Tier: s.SemanticEnabled ? "lexical + semantic" : "lexical only",
    // Said on every response, not only when it is absent. A caller with results has no way to know the
    // semantic half was missing, and will read "these are the matches" as complete rather than as
    // complete-for-word-overlap (D24).
    Note: s.SemanticEnabled
        ? null
        : $"Set {ServiceOptions.ModelVariable} to enable semantic recall — it is what finds two "
          + "repositories that reached the same conclusion in different words.",
    // The proof of possession (PERSONDOOR1a, design §2.3): the shell hands its modules and the page the key only once the
    // host it started proves it holds it, so a process that took the port first never learns it. None without a key.
    Proof: string.IsNullOrEmpty(prove) ? null : personKey?.Prove(prove)));

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
    ComposedService s, HttpContext http, string q, string? kinds, string? repositories, bool? localOnly, int? limit,
    string? workspace, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new ErrorResponse("q is required"));
    if (KindsOrRefusal(kinds) is { Refusal: { } refused }) return Results.BadRequest(new ErrorResponse(refused));

    var answer = await s.Service.AnswerAsync(new KnowledgeQuery(q)
    {
        Kinds = KnowledgeQuery.ParseKinds(kinds),
        Repositories = KnowledgeQuery.ParseSet(repositories),
        Provenance = (localOnly ?? true) ? Provenance.Local : null,
        Limit = Math.Clamp(limit ?? 20, 1, 100),
        Workspace = workspace,
    }, ct);
    var hits = answer.Hits;

    // Which tier ANSWERED (TIER1, D24), as a token in a header, so the body stays the array every
    // client already reads. The failure's own words are not sent here: a header is ASCII, and the
    // sentence is the status door's and the refresh's to say.
    http.Response.Headers["x-daoris-tier"] = answer.Tier;

    return Results.Ok(hits.Select(h => new HitResponse(
        h.Entry.Id, h.Entry.Repository, h.Entry.Kind.ToString(), h.Entry.Title,
        h.Entry.RelativePath, h.Excerpt, h.Score, h.Entry.Workspace,
        h.Entry.Lines?.First, h.Entry.Lines?.Last, h.ExcerptLine)));
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
    if (KindsOrRefusal(kinds) is { Refusal: { } refused }) return Results.BadRequest(new ErrorResponse(refused));

    var found = await s.Service.FindConvergenceAsync(
        new ConvergenceOptions(
            Math.Clamp(minimumSimilarity ?? 0.82, 0, 1), KnowledgeQuery.ParseKinds(kinds),
            Math.Clamp(limit ?? 25, 1, 100), workspace),
        ct);

    return Results.Ok(found.Select(c => new ConvergenceResponse(
        c.Method.ToString(),
        c.Similarity,
        c.Repositories,
        c.Entries.Select(e => new ConvergenceEntryResponse(
            e.Id, e.Repository, e.Kind.ToString(), e.Title, e.RelativePath)).ToList(),
        // The command, not an edit box (D31). The UI shows where the change belongs; the person makes it
        // in the repository that owns the file, where review happens.
        Suggestion: SuggestionFor(c))));
});

// What each repository OWES, as opposed to what it knows. Held by the service rather than written
// into anyone's files: repositories here are not developed across, so a quest is published and pulled,
// never pushed into a sibling's tree.
app.MapGet("/api/quests", async (
    ComposedService s, HttpContext http, string? repository, bool? includeClosed, string? workspace,
    CancellationToken ct) =>
{
    var quests = await s.Quests.ListAsync(repository, includeClosed ?? false, workspace, ct);
    var deletable = await DeletableAsync(s, quests, ct);
    return quests.Select(q => ToQuest(q, s.Files, MachineLocal(http), deletable.Contains(q.Id)));
});

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
            Then = StepsOf(body.Then),
            Requirements = RequirementsOf(body.Requirements),
            // The person's short title from the composer (SESSUX1j), judged by the exchange with every other door's.
            Short = body.Short,
            // The chain's review choice (REVIEWENV1b): the person's own at this door, with any words they give.
            Review = ReviewOf(body.Review),
            // An agent's publish where the host holds a person key and the call carried none (PERSONDOOR1a, design §3.2):
            // its review choice is judged as a connector's is (REVIEWENV1b3). With the key, the person's.
            ByAgent = personGate.IsAgent(http),
        },
        DateTimeOffset.UtcNow, ct);

    return outcome.Refusal switch
    {
        QuestPublishRefusal.None => Results.Ok(
            new QuestActionResponse(await QuestAnswerAsync(s, http, outcome.Quest!, ct), outcome.Message)),
        // Two circles that were never joined is a state conflict, not a malformed ask — the same 409
        // shape the quest lock teaches. The sentence names both sides (D48 §4).
        QuestPublishRefusal.CrossWorkspace => Results.Conflict(new ErrorResponse(outcome.Message)),
        // The words of a quest this machine forgot (HIST1b): the remote holds it closed, a state conflict as a closed quest's move is.
        QuestPublishRefusal.Cleared => Results.Conflict(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

app.MapPost("/api/quests/{id}/respond", async (
    ComposedService s, HttpContext http, string id, RespondQuestRequest body, CancellationToken ct) =>
{
    // `whileOpen` (PAUSE1c) is an abandon's decline, which the driver sends on the person's press: the person's form of an
    // agent's door, so it wants the key where the host holds one (PERSONDOOR1a, design §3.2).
    if (personGate.Refused(http, PersonDoors.Respond, form: body.WhileOpen == true) is { } refusedForm) return refusedForm;

    // A done's answers (DRIFT1d): a number left out arrives as 0, a half left out blank, and the exchange refuses
    // each naming which — the same sentence every door gives.
    var answers = (body.Answers ?? [])
        .Select(a => new QuestAnswer(a?.Requirement ?? 0, a?.Met, a?.Departed, a?.Quote))
        .ToList();
    // `whileOpen` (PAUSE1c) is an abandon's decline, sent by the driver; the exchange judges it like any respond.
    var outcome = await s.Exchange.RespondAsync(
        id, body.Action ?? "", body.Reason, DateTimeOffset.UtcNow, ct, on: body.On, answers: answers,
        whileOpen: body.WhileOpen == true);

    return outcome.Refusal switch
    {
        QuestRespondRefusal.None => Results.Ok(
            new QuestActionResponse(await QuestAnswerAsync(s, http, outcome.Quest!, ct), outcome.Message)),
        QuestRespondRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        // The refused transition is a state conflict, not a bad request: the losing side of the
        // cross-machine race reads 409 as "someone got there first" and stands down (D47 §5).
        QuestRespondRefusal.AlreadyTaken or QuestRespondRefusal.Closed =>
            Results.Conflict(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

// The person's yes to a done's departure from what they required (DRIFT1d, D133 §4): what it held goes on — the
// chain's next step is published, and a quest waiting on it resumes. LOCAL mode only, as a delete is: the person
// says yes on their own machine, and the operation travels from there like any verb (D68). No connector tool reaches
// it, since the yes is the person's, never an agent's.
if (mode == ServiceMode.Local)
{
    app.MapPost("/api/quests/{id}/accept", async (ComposedService s, HttpContext http, string id, CancellationToken ct) =>
    {
        var outcome = await s.Exchange.AcceptAsync(id, DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            QuestRespondRefusal.None => Results.Ok(
                new QuestActionResponse(await QuestAnswerAsync(s, http, outcome.Quest!, ct), outcome.Message)),
            QuestRespondRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
            // Nothing waits for a yes: a state, the lock's own shape.
            _ => Results.Conflict(new ErrorResponse(outcome.Message)),
        };
    });

    // The person marks a quest done (QUESTCLOSE1, D126's note): their done, which answers none of their requirements and
    // says it was theirs, with their words. LOCAL mode only, as the yes is: said on their own machine, travelling from there
    // as a done (D68). No connector tool reaches it, since an agent's done answers each requirement through respond.
    app.MapPost("/api/quests/{id}/done", async (
        ComposedService s, HttpContext http, string id, PersonDoneRequest? body, CancellationToken ct) =>
    {
        var outcome = await s.Exchange.PersonDoneAsync(id, body?.Note, DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            QuestRespondRefusal.None => Results.Ok(
                new QuestActionResponse(await QuestAnswerAsync(s, http, outcome.Quest!, ct), outcome.Message)),
            QuestRespondRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
            // A closed quest, or a take this machine lost: a state, the lock's own shape.
            _ => Results.Conflict(new ErrorResponse(outcome.Message)),
        };
    });

    // What the driver read of a done's evidence (EVID1a, D144 §3): its verdict at the end of the session that made the
    // done, the sweep's, or the person's check at the terminal. LOCAL mode only, as the yes is: the commit is read on the
    // machine whose tree holds it, and the verdict travels from there as an operation (D68). No connector tool reaches it,
    // since the session a requirement judges never writes its verdict.
    app.MapPost("/api/quests/{id}/evidence", async (
        ComposedService s, HttpContext http, string id, QuestEvidenceVerdictWire body, CancellationToken ct) =>
    {
        // A field left out arrives blank and is refused by the exchange naming which; when and where are the operation's.
        var verdict = new QuestEvidenceVerdict(
            body.Commit ?? "", body.How ?? "",
            (body.Items ?? []).Select(i => new QuestEvidenceRead(i?.Requirement ?? 0, i?.Path, i?.Gate, i?.Result ?? "")
            {
                Object = i?.Object, Changed = i?.Changed, Spelled = i?.Spelled,
            }).ToList())
        {
            Session = body.Session,
        };
        var outcome = await s.Exchange.EvidenceAsync(id, verdict, DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            QuestRespondRefusal.None => Results.Ok(
                new QuestActionResponse(await QuestAnswerAsync(s, http, outcome.Quest!, ct), outcome.Message)),
            QuestRespondRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
            // Nothing waits on evidence: a state, the lock's own shape.
            QuestRespondRefusal.NotAwaitingEvidence => Results.Conflict(new ErrorResponse(outcome.Message)),
            _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
        };
    });

    // A set-up on a set-up step (REVIEWENV1b, design §2.6, §3.3): the driver posts each set-up a session said, with the commit it
    // read from the step's tree, or the person records their own. LOCAL mode only, as the evidence door is: the commit is read
    // on the machine whose tree holds it, and the set-up travels from there as an operation (D68). No connector tool reaches
    // it, since a session never reports a commit.
    // Both forms want the key where the host holds one (PERSONDOOR1a, design §3.2): naming a session, the driver's post of what
    // that session said, which the session never reports of itself; naming none, the person's own.
    app.MapPost("/api/quests/{id}/set-up", async (
        ComposedService s, HttpContext http, string id, QuestSetUpRequest body, CancellationToken ct) =>
    {
        if (personGate.Refused(http, PersonDoors.SetUp, form: body.Session is null) is { } refusedForm) return refusedForm;

        return await ReviewAnswer(await s.Exchange.PostSetUpAsync(
            id, new QuestSetUpPost(body.Commit, body.Kind, body.Session, body.Look, body.Shows, body.Again), DateTimeOffset.UtcNow, ct),
            s, http, ct);
    });

    // The person's verdict on a set-up step, or their skip of a review (REVIEWENV1b, D154 point 8): LOCAL mode only, as the yes
    // is, and no connector tool reaches it, since the verdict is a look only the person has taken. A set-up named by half its
    // reference is refused here, never read as none (REVIEWENV1b3): none once meant the newest, which the person may not have seen.
    app.MapPost("/api/quests/{id}/review", async (
        ComposedService s, HttpContext http, string id, QuestReviewRequest body, CancellationToken ct) =>
    {
        if (body.SetUp is { } half && (string.IsNullOrWhiteSpace(half.Machine) || half.Sequence is null))
        {
            return Results.BadRequest(new ErrorResponse(
                "`setUp` names a set-up by both its `machine` and its `sequence`, as the quest's `setUps` answer them. Nothing was kept."));
        }

        return await ReviewAnswer(await s.Exchange.ReviewAsync(
            id, body.Verdict, body.Words,
            body.SetUp is { Machine: { } machine, Sequence: { } sequence } ? new QuestOperationRef(machine, sequence) : null,
            DateTimeOffset.UtcNow, ct),
            s, http, ct);
    });

    // The person's *Set it up in `<environment>`* (REVIEWENV1b, design §2.1, §3.6): a set-up step published following a done
    // quest, in Daoris's words. LOCAL mode only: the person's press, on their own machine.
    app.MapPost("/api/quests/{id}/set-up-step", async (
        ComposedService s, HttpContext http, string id, SetUpStepRequest body, CancellationToken ct) =>
        await ReviewAnswer(await s.Exchange.PublishSetUpStepAsync(id, body.Environment, DateTimeOffset.UtcNow, ct), s, http, ct));
}

// A review door's answer (REVIEWENV1b): a state the review refuses is the lock's own shape, 409, and a shape is a bad request.
async Task<IResult> ReviewAnswer(QuestRespondOutcome outcome, ComposedService s, HttpContext http, CancellationToken ct) =>
    outcome.Refusal switch
    {
        QuestRespondRefusal.None => Results.Ok(new QuestActionResponse(await QuestAnswerAsync(s, http, outcome.Quest!, ct), outcome.Message)),
        QuestRespondRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        QuestRespondRefusal.BadSetUp or QuestRespondRefusal.BadReviewVerdict => Results.BadRequest(new ErrorResponse(outcome.Message)),
        _ => Results.Conflict(new ErrorResponse(outcome.Message)),
    };

// A person dismissing a conflict (SYNC6c): committed here like any verb, and carried by the next pass,
// so the conflict goes on every machine. It moves no status, which is why it is not a `respond`
// action — every one of those is a move through the table.
app.MapPost("/api/quests/{id}/conflicts/dismiss", async (
    ComposedService s, HttpContext http, string id, DismissConflictRequest body, CancellationToken ct) =>
{
    var quest = id.TrimStart('#');
    var dismissal = await s.Quests.DismissAsync(quest, body.Machine, body.Sequence, DateTimeOffset.UtcNow, ct);
    if (dismissal.Quest is null)
    {
        return Results.NotFound(new ErrorResponse($"quest `#{quest}` is not one this service holds."));
    }

    return Results.Ok(new QuestActionResponse(
        await QuestAnswerAsync(s, http, dismissal.Quest, ct),
        dismissal.Dismissed switch
        {
            0 => $"quest `#{quest}` carries no such conflict — nothing to dismiss.",
            1 => $"Dismissed one conflict on quest `#{quest}`; every machine drops it on its next sync.",
            var count => $"Dismissed {count} conflicts on quest `#{quest}`; every machine drops them on its next sync.",
        }));
});

// Deleting a quest made by mistake (D95): the exchange's judgement and sentence, verbatim. LOCAL mode
// only — a person deletes on their own machine, and a shared quest's tombstone travels from there; a
// deployment answering the network has no door that removes a record.
if (mode == ServiceMode.Local)
{
    app.MapDelete("/api/quests/{id}", async (ComposedService s, string id, CancellationToken ct) =>
    {
        var outcome = await s.Exchange.DeleteAsync(id, DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            QuestDeleteRefusal.None => Results.Ok(new DeletedResponse(outcome.Quest!.Id, outcome.Message)),
            QuestDeleteRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
            // Something stands on it, or a take won the race: a state conflict, the lock's own shape.
            _ => Results.Conflict(new ErrorResponse(outcome.Message)),
        };
    });
}

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
                // The person's review choice from the composer (REVIEWENV1b), judged by the desk.
                Review = body.Review,
                ReviewWords = body.ReviewWords,
            },
            DateTimeOffset.UtcNow, ct);
        return AskAnswer(outcome, s, http);
    });

    // The person sets the ask's review choice on its page, or applies its intake's proposal (REVIEWENV1b, design §1.5): the
    // latest stands. Local like every ask route.
    app.MapPost("/api/asks/{id}/review", async (
        ComposedService s, HttpContext http, string id, AskReviewRequest body, CancellationToken ct) =>
        AskAnswer(await s.Asks.ChooseReviewAsync(id, body.Choice, body.Words, DateTimeOffset.UtcNow, ct), s, http));

    // One ask, whole — how the driver observes what its intake made of it (D65 §1b).
    app.MapGet("/api/asks/{id}", async (ComposedService s, HttpContext http, string id, CancellationToken ct) =>
        await s.Asks.FindAsync(id, ct) is { } ask
            ? Results.Ok(ToAsk(ask, s.Files, MachineLocal(http)))
            : Results.NotFound(new ErrorResponse($"No ask `#{id.TrimStart('#')}`.")));

    app.MapPost("/api/asks/{id}/publish", async (
        ComposedService s, HttpContext http, string id, AskPublishRequest body, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(body.To))
        {
            return Results.BadRequest(new ErrorResponse("to is required — the repository this ask becomes a quest for"));
        }

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

        // A person's publish names a receiver and nothing else; an intake's carries its own words (D65
        // §1b). Anything beyond `to` makes a draft — words, links, files or a chain alone included.
        var drafted = body.Title is not null || body.Body is not null || body.Links is { Count: > 0 }
            || uploads.Count > 0 || body.Then is { Count: > 0 } || body.Requirements is { Count: > 0 } || body.Short is not null
            || body.Review is not null || body.ReviewProposal is not null;
        var draft = drafted
            ? new AskDraft(body.Title, body.Body)
            {
                Links = body.Links ?? [],
                Uploads = uploads,
                Then = StepsOf(body.Then),
                Requirements = RequirementsOf(body.Requirements),
                // The intake's short title (SESSUX1j), judged by the exchange with every other door's.
                Short = body.Short,
                // The chain's review choice and an intake's proposal (REVIEWENV1b), judged by the exchange and the desk.
                Review = ReviewOf(body.Review),
                ReviewProposal = body.ReviewProposal is { } proposal ? new ReviewProposed(proposal.Choice, proposal.Reason) : null,
            }
            : null;

        // Keyless where the host holds a person key, an agent's publish onto the ask, crediting the session it names, if any;
        // with the key and no session, the person's choice of receiver (PERSONDOOR1a, design §3.2).
        return AskAnswer(
            await s.Asks.PublishAsync(id, body.To, DateTimeOffset.UtcNow, ct, draft, body.Session, byAgent: personGate.IsAgent(http)),
            s, http);
    });

    app.MapPost("/api/asks/{id}/close", async (
        ComposedService s, HttpContext http, string id, AskCloseRequest body, CancellationToken ct) =>
        AskAnswer(await s.Asks.CloseAsync(id, body.Reason ?? "", DateTimeOffset.UtcNow, ct), s, http));

    // The person answers a go-ahead a session asked on their ask (KNOWUSE1a, D135 §2): yes or no, with their words where
    // they give any. Local like every ask route; no connector tool answers one, since the production acts stay theirs.
    // The answer that leaves none of a parked session's go-aheads waiting is that park's answer, and the sentence says which
    // parks go on and which still wait (GOAHEAD2), unless the caller answers the park itself (`goesOn: false`).
    app.MapPost("/api/asks/{id}/go-aheads/{number}", async (
        ComposedService s, HttpContext http, string id, string number, GoAheadAnswerRequest body, CancellationToken ct) =>
    {
        if (!int.TryParse(number, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) || n < 1)
        {
            return Results.NotFound(new ErrorResponse($"`{number}` is no go-ahead's number — they are numbered from 1 on their ask."));
        }

        if (body.Answer is not ("approved" or "refused"))
        {
            return Results.BadRequest(new ErrorResponse("answer is `approved` or `refused` — the person's yes or no to the act."));
        }

        var now = DateTimeOffset.UtcNow;
        var outcome = await s.Asks.AnswerGoAheadAsync(id, n, body.Answer == "approved", body.Words, now, ct);
        var parks = body.GoesOn == false ? GoAheadParks.None : await s.Ledger.GoOnWithGoAheadsAsync(outcome, now, ct);
        return outcome.Refusal switch
        {
            GoAheadAnswerRefusal.None => Results.Ok(new AskActionResponse(
                ToAsk(outcome.Ask!, s.Files, MachineLocal(http)), outcome.Message + parks.Said, null)),
            GoAheadAnswerRefusal.NotFound or GoAheadAnswerRefusal.NoGoAhead => Results.NotFound(new ErrorResponse(outcome.Message)),
            _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
        };
    });

    // Deleting an ask made by mistake, with every quest asked by it, or none of it (D95).
    app.MapDelete("/api/asks/{id}", async (ComposedService s, string id, CancellationToken ct) =>
    {
        var outcome = await s.Asks.DeleteAsync(id, DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            AskRefusal.None => Results.Ok(new DeletedResponse(id.TrimStart('#'), outcome.Message)),
            AskRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
            _ => Results.Conflict(new ErrorResponse(outcome.Message)),
        };
    });

    // An intake (D65 §1b): the record of a conversation the driver opens for an ask, in a room under
    // its home. Local like the asks it answers; the process is the driver's, as ever.
    app.MapPost("/api/sessions/intake", async (
        ComposedService s, HttpContext http, OpenIntakeRequest body, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(body.Ask) || string.IsNullOrWhiteSpace(body.Room))
        {
            return Results.BadRequest(new ErrorResponse("ask and room are required — which ask, and where its intake runs"));
        }

        var outcome = await s.Ledger.OpenIntakeAsync(
            body.Ask,
            // The adapter is the harness, never a model (D24). Silence takes the supported one.
            string.IsNullOrWhiteSpace(body.Adapter) ? "claude-code" : body.Adapter,
            body.Room, DateTimeOffset.UtcNow, body.HarnessVersion, body.Profile, ct);

        return outcome.Refusal switch
        {
            SessionOpenRefusal.None => Results.Ok(
                new SessionActionResponse(ToSession(outcome.Session!, MachineLocal(http)), outcome.Message)),
            SessionOpenRefusal.AskNotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
            // Answered and busy are state conflicts, the shape every open door teaches.
            SessionOpenRefusal.AskAnswered or SessionOpenRefusal.RepositoryBusy =>
                Results.Conflict(new ErrorResponse(outcome.Message)),
            _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
        };
    });

    // Ask Daoris (HELP1a, D89): the record of the conversation about Daoris itself, in the room the
    // driver keeps for it. Local like the intake's; the process is the driver's.
    app.MapPost("/api/sessions/help", async (
        ComposedService s, HttpContext http, OpenHelpRequest body, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(body.Room))
        {
            return Results.BadRequest(new ErrorResponse("room is required — where Ask Daoris's conversation runs"));
        }

        var outcome = await s.Ledger.OpenHelpAsync(
            // The adapter is the harness, never a model (D24). Silence takes the supported one.
            string.IsNullOrWhiteSpace(body.Adapter) ? "claude-code" : body.Adapter,
            body.Room, DateTimeOffset.UtcNow, body.HarnessVersion, body.Profile, ct);

        return outcome.Refusal switch
        {
            SessionOpenRefusal.None => Results.Ok(
                new SessionActionResponse(ToSession(outcome.Session!, MachineLocal(http)), outcome.Message)),
            SessionOpenRefusal.RepositoryBusy => Results.Conflict(new ErrorResponse(outcome.Message)),
            _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
        };
    });

    // Deleting a conversation that served no quest (SESSUX1f, D126 §5.4): the ledger judges the record's half and removes
    // it, and the driver, which judged the machine's half first, removes what this machine kept of it after this yes. LOCAL
    // mode only, as D95's deletes are: a person deletes on their own machine, and a session record does not travel as a
    // deletion. A refusal is the ledger's sentence with its word and facts, so the driver reads the word, never the sentence.
    app.MapDelete("/api/sessions/{id}", async (ComposedService s, string id, CancellationToken ct) =>
    {
        var outcome = await s.Ledger.DeleteAsync(id, ct);
        return outcome.Refusal switch
        {
            SessionDeleteRefusal.None => Results.Ok(new DeletedResponse(id, outcome.Message)),
            SessionDeleteRefusal.NotFound => Results.NotFound(ToDeletion(outcome)),
            // Live, another machine's, a record something names: a conflict with the record as it stands, D95's shape.
            _ => Results.Conflict(ToDeletion(outcome)),
        };
    });

    // The same judgement, deleting nothing: what the driver asks before it judges the tree and the landing, so a refusal is
    // said in the order a person meets it.
    app.MapGet("/api/sessions/{id}/deletable", async (ComposedService s, string id, CancellationToken ct) =>
    {
        var outcome = await s.Ledger.JudgeDeleteAsync(id, ct);
        return outcome.Refusal == SessionDeleteRefusal.NotFound
            ? Results.NotFound(ToDeletion(outcome))
            : Results.Ok(ToDeletion(outcome));
    });

    // What the person added to a running session (DRIFT1a, D133 §1), as its driver reports it: the words
    // reach the session through the driver, which never passes them here otherwise, so this is where they
    // are kept on the ask its work is for. Local like the asks; a session on no ask is answered `kept:
    // false` with the reason, which is no error.
    app.MapPost("/api/sessions/{id}/added", async (
        ComposedService s, HttpContext http, string id, AddedRequest body, CancellationToken ct) =>
    {
        var outcome = await s.Ledger.KeepOnAskAsync(id, AskWordKind.Added, body.Text, DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            AskWordRefusal.None => Results.Ok(new AddedResponse(
                true, outcome.Message,
                await s.Asks.FindAsync(outcome.Ask!, ct) is { } ask ? ToAsk(ask, s.Files, MachineLocal(http)) : null)),
            AskWordRefusal.NoAsk => Results.Ok(new AddedResponse(false, outcome.Message, null)),
            AskWordRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
            _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
        };
    });

    // What the person says to a parked or ended session of this machine's (MSG1a, D137 §5.3): kept on its record for it
    // to go on with, the record staying as it is. LOCAL mode only: a session goes on only where its process and its
    // conversation are, and a shared host runs none. What never goes on is refused by its word.
    app.MapPost("/api/sessions/{id}/say", async (
        ComposedService s, HttpContext http, string id, SayRequest body, CancellationToken ct) =>
    {
        var now = DateTimeOffset.UtcNow;
        var outcome = await s.Ledger.SayAsync(id, body.Text, body.Files, now, ct);
        if (outcome.Refusal == SessionSayRefusal.None)
        {
            // Words to a park are its answer, kept on its ask at once as the answer door keeps one (DRIFT1a); words to an
            // ended session are kept there once a session took them (the taken door, D137 §2.4).
            if (!outcome.Word!.Reopens)
            {
                await s.Ledger.KeepOnAskAsync(id, AskWordKind.Answered, outcome.Word.Text, now, ct);
            }

            var local = MachineLocal(http);
            return Results.Ok(new SaidResponse(ToSession(outcome.Session!, local), outcome.Message, local ? ToSaid(outcome.Word) : null));
        }

        var refused = new SessionSayRefusalResponse(
            outcome.Message, SayRefusal(outcome.Refusal), outcome.Quest, outcome.Ask, outcome.Origin, outcome.Opinion);
        return outcome.Refusal switch
        {
            SessionSayRefusal.NotFound => Results.NotFound(refused),
            SessionSayRefusal.Empty => Results.BadRequest(refused),
            // A teammate's, an intake, a stand-down, a running session: a conflict with the record as it stands.
            _ => Results.Conflict(refused),
        };
    });

    // The words a session took off its record (MSG1a, D137 §2.4): the resumed run's first prompt went, or the session a
    // fallback handed them to took them. Each of the person's said after the record ended is kept on the ask its work is for,
    // as `reopened`, said to the session that took it, beside the take as the answer door keeps an answer (DRIFT1a). Another
    // agent's claims never are (XAGENT1c): on the ask they would reach every later session as the person's requirements.
    app.MapPost("/api/sessions/{id}/taken", async (
        ComposedService s, HttpContext http, string id, TakenRequest body, CancellationToken ct) =>
    {
        var ids = (body.Said ?? []).OfType<string>().Where(word => word.Length > 0).ToList();
        var outcome = await s.Ledger.TakeSaidAsync(id, ids, string.IsNullOrWhiteSpace(body.By) ? null : body.By, ct);
        if (outcome.Refusal == SessionSayRefusal.None)
        {
            foreach (var word in outcome.Taken.Where(word => word.Reopens && word.Persons))
            {
                await s.Ledger.KeepOnAskAsync(
                    string.IsNullOrWhiteSpace(body.By) ? id : body.By, AskWordKind.Reopened, word.Text, word.At, ct);
            }

            return Results.Ok(new SessionActionResponse(ToSession(outcome.Session!, MachineLocal(http)), outcome.Message));
        }

        var refused = new SessionSayRefusalResponse(outcome.Message, SayRefusal(outcome.Refusal));
        return outcome.Refusal switch
        {
            SessionSayRefusal.NotFound => Results.NotFound(refused),
            SessionSayRefusal.Empty => Results.BadRequest(refused),
            _ => Results.Conflict(refused),
        };
    });
}

// Second opinions (XAGENT1c, D155 point 11; the second agent design §6.2–§6.3): the driver asks a pass, which opens its
// reviewer's record beside it, reads it back, and hands its findings to the working session. LOCAL mode only, and answered
// only to a caller on this machine, as a record's `said` is (MSG1a): the candidate is this machine's commits until they land,
// and the reviewer names an account. What the reviewer says and what the working session answers come through each one's
// own connector, and no door here answers a dispute for the person.
if (mode == ServiceMode.Local)
{
    app.MapPost("/api/opinions", async (ComposedService s, HttpContext http, OpinionAskRequest body, CancellationToken ct) =>
    {
        if (!MachineLocal(http)) return OffMachineOpinion();

        // A part left out arrives blank, refused by the desk naming which; `minutes` left out is 0, which is no bound.
        var candidate = body.Candidate;
        var reviewer = body.Reviewer;
        var ask = new OpinionAsk(
            body.Occasion ?? "", body.Pass ?? "", body.Working ?? "",
            new OpinionCandidate(
                candidate?.Repository ?? "", candidate?.Base ?? "", candidate?.Tip ?? "", [.. (candidate?.Commits ?? []).Select(commit => commit ?? "")]),
            new OpinionReviewer(reviewer?.Adapter ?? "", reviewer?.Label ?? "")
            {
                Product = reviewer?.Product, Maker = reviewer?.Maker, Account = reviewer?.Account,
            },
            body.Posture ?? "", body.Minutes ?? 0, body.Tree ?? "")
        {
            Rechecks = body.Rechecks,
            Families = [.. (body.Families ?? []).Select(family => family ?? "")],
            HarnessVersion = body.HarnessVersion,
        };
        return await OpinionReply(await s.Opinions.AskAsync(ask, DateTimeOffset.UtcNow, ct), s, http, ct);
    });

    // The opinions on one session's work, the one a reviewer's record reads for, or those of a repository, each where its
    // pass stands, oldest first.
    app.MapGet("/api/opinions", async (
        ComposedService s, HttpContext http, string? working, string? session, string? repository, CancellationToken ct) =>
        MachineLocal(http)
            ? Results.Ok((await s.Opinions.ListAsync(working, session, repository, DateTimeOffset.UtcNow, ct)).Select(ToOpinion))
            : OffMachineOpinion());

    app.MapGet("/api/opinions/{id}", async (ComposedService s, HttpContext http, string id, CancellationToken ct) =>
    {
        if (!MachineLocal(http)) return OffMachineOpinion();
        return await s.Opinions.ReadAsync(id, DateTimeOffset.UtcNow, ct) is { } standing
            ? Results.Ok(ToOpinion(standing))
            : Results.NotFound(new ErrorResponse($"There is no second opinion `{id.Trim()}` on this machine."));
    });

    // A first pass's findings, to its working session as another agent's words (design §6.3): once, at a turn's end.
    app.MapPost("/api/opinions/{id}/hand", async (ComposedService s, HttpContext http, string id, CancellationToken ct) =>
        MachineLocal(http)
            ? await OpinionReply(await s.Opinions.HandAsync(id, DateTimeOffset.UtcNow, ct), s, http, ct)
            : OffMachineOpinion());
}

// A door to a second opinion answers (XAGENT1c): a shape is a bad request, nothing by that name is not found, and a bound or a
// state the opinion is in is the lock's own shape, 409. Done, the opinion as it now stands with the desk's sentence.
async Task<IResult> OpinionReply(OpinionOutcome outcome, ComposedService s, HttpContext http, CancellationToken ct)
{
    if (outcome.Refusal != OpinionRefusal.None)
    {
        return outcome.Refusal switch
        {
            OpinionRefusal.NotFound or OpinionRefusal.Shared => Results.NotFound(new ErrorResponse(outcome.Message)),
            OpinionRefusal.BadShape => Results.BadRequest(new ErrorResponse(outcome.Message)),
            _ => Results.Conflict(new ErrorResponse(outcome.Message)),
        };
    }

    var standing = (await s.Opinions.ReadAsync(outcome.Opinion!.Id, DateTimeOffset.UtcNow, ct))!;
    return Results.Ok(new OpinionActionResponse(
        ToOpinion(standing), outcome.Message,
        outcome.Session is { } session ? ToSession(session, MachineLocal(http)) : null,
        outcome.Word is { } word ? ToSaid(word) : null));
}

// Clearing finished history from this machine (HIST1b, D153; the history-clearing design §6.3), listed first and then
// pressed (D88): the desk judges the records' half for both doors, and the driver, which judges the trees, the landings
// and the processes, calls these and then removes what the home kept (HIST1c). LOCAL mode only, as D95's and D126's
// deletes are: a person clears their own machine, and a remote's history is the team's, so a shared deployment maps
// neither. Nothing here travels: a quest a remote numbered is forgotten here, and the team keeps its copy.
if (mode == ServiceMode.Local)
{
    // What each unit holds and would take, or why it stays: a workspace's finished history, one quest's work, one quest's
    // failed sessions, or one ask's work. Deletes nothing. Every unit is answered, a refused one with its word.
    app.MapGet("/api/history", async (
        ComposedService s, string? workspace, string? quest, string? ask, bool? failed, CancellationToken ct) =>
    {
        var named = new[] { workspace, quest, ask }.Count(scope => !string.IsNullOrWhiteSpace(scope));
        if (named != 1 || (failed == true && string.IsNullOrWhiteSpace(quest)))
        {
            return Results.BadRequest(new ErrorResponse(
                "name one scope: `workspace`, `quest` or `ask`, and `failed=true` only beside a quest"));
        }

        IReadOnlyList<HistoryUnit> units = !string.IsNullOrWhiteSpace(workspace)
            ? await s.History.PlanWorkspaceAsync(workspace, ct)
            : [await s.History.PlanAsync(
                !string.IsNullOrWhiteSpace(quest)
                    ? new HistoryUnitRef(failed == true ? HistoryUnitKind.Failed : HistoryUnitKind.Quest, quest)
                    : new HistoryUnitRef(HistoryUnitKind.Ask, ask!),
                ct)];
        return Results.Ok(new HistoryPlanResponse([.. units.Select(ToHistoryUnit)]));
    });

    // The second press: exactly the units named, each judged again where it is cleared, and cleared or kept with its word.
    app.MapPost("/api/history/clear", async (ComposedService s, HistoryClearRequest body, CancellationToken ct) =>
    {
        var units = new List<HistoryUnitRef>();
        foreach (var unit in body.Units ?? [])
        {
            if (HistoryUnitRef.Parse(unit?.Kind) is not { } kind || string.IsNullOrWhiteSpace(unit!.Id))
            {
                return Results.BadRequest(new ErrorResponse(
                    "each unit names its `kind` (`quest`, `ask` or `failed`) and its `id`, as the listing gave them"));
            }

            units.Add(new HistoryUnitRef(kind, unit.Id.Trim()));
        }

        if (units.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse("`units` names what to clear, as the listing gave them; it named none"));
        }

        var outcomes = await s.History.ClearAsync(units, DateTimeOffset.UtcNow, ct);
        return Results.Ok(new HistoryClearResponse(
            [.. outcomes.Select(outcome => new HistoryClearedResponse(
                ToHistoryUnit(outcome.Unit), outcome.Cleared, outcome.Message,
                outcome.Failed.Any ? new HistoryFailedResponse(outcome.Failed.Quests, outcome.Failed.Asks) : null))]));
    });
}

// The driver's session records (D46). State only: the service never spawns a process — the record is
// what the platform renders and what survives a driver restart; the process handle stays with the
// driver that owns it. Judgement is the shared ledger's, so this door and any other cannot drift.
app.MapGet("/api/sessions", async (
    ComposedService s, HttpContext http, string? repository, bool? includeClosed, string? workspace,
    CancellationToken ct) =>
{
    var sessions = await s.Sessions.ListAsync(repository, includeClosed ?? false, workspace, ct);
    // Which records the ledger would delete (SESSUX1f), D95's way; none at a shared deployment, which has no delete door.
    var deletable = mode == ServiceMode.Local ? await s.Ledger.DeletableAsync(sessions, ct) : new HashSet<string>();
    return sessions.Select(session => ToSession(session, MachineLocal(http)) with { Deletable = deletable.Contains(session.Id) });
});

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
        SessionOpenRefusal.QuestNotOpen or SessionOpenRefusal.TakenElsewhere or SessionOpenRefusal.RepositoryBusy =>
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
        id, body.State ?? "", body.Note, body.Evidence, body.Transcript, DateTimeOffset.UtcNow, ct,
        interrupted: body.Interrupted == true, limit: body.Limit == true,
        // The note's lines by code (LANG1a), kept only as what a part is; none with no note.
        noteParts: body.Note is null ? null : NoteParts.Normalize(body.NoteParts));

    return outcome.Refusal switch
    {
        SessionAdvanceRefusal.None => Results.Ok(
            new SessionActionResponse(ToSession(outcome.Session!, MachineLocal(http)), outcome.Message)),
        SessionAdvanceRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        // Terminal and an illegal move are conflicts with the record's current state — 409 like the
        // quest door, and so is a tree another session holds when an ended record would go on (MSG1a).
        // Only an unknown state name is a bad request.
        SessionAdvanceRefusal.Terminal or SessionAdvanceRefusal.InvalidMove or SessionAdvanceRefusal.Busy =>
            Results.Conflict(new ErrorResponse(outcome.Message)),
        _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
    };
});

// The person answers a driven session that parked to ask them (STANDDOWN2): the record stays parked with
// their words, and the reply is the session as it now stands, for the driver's next look to go on with
// (ANSWER1b, D131) — its own conversation resumed, or its quest carried on in the same tree.
app.MapPost("/api/sessions/{id}/answer", async (
    ComposedService s, HttpContext http, string id, AnswerSessionRequest body, CancellationToken ct) =>
{
    var now = DateTimeOffset.UtcNow;
    var outcome = await s.Ledger.AnswerAsync(id, body.Answer, now, ct);
    // DRIFT1a (D133 §1): the answer is the person's word on the ask the session's quest was asked by, so
    // it is kept there too, beside the answer and never inside it. A blank one says nothing to keep, and a
    // session on no ask has nowhere to keep it; either way the answer stands as it was given.
    if (outcome.Refusal == SessionAdvanceRefusal.None)
    {
        await s.Ledger.KeepOnAskAsync(id, AskWordKind.Answered, body.Answer, now, ct);
    }

    return outcome.Refusal switch
    {
        SessionAdvanceRefusal.None => Results.Ok(
            new SessionActionResponse(ToSession(outcome.Session!, MachineLocal(http)), outcome.Message)),
        SessionAdvanceRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        _ => Results.Conflict(new ErrorResponse(outcome.Message)),
    };
});

// Where `daoris connect` lands. It accepts a repository's description of ITSELF — the only thing a
// repository is authoritative about — and persists it, because for a remote service the pushed
// registrations ARE the family: one that forgot them on restart would drop every connected
// repository off the map without anyone being told.
app.MapPost("/api/registry", async (ComposedService s, HttpContext http, RegisterRequest body, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.Repository)) return Results.BadRequest(new ErrorResponse("repository is required"));

    // One shared deployment serves one workspace (D48 §5). A row declaring another is not a
    // permission failure — it is a message delivered to the wrong building, so it says so naming both
    // sides. A conflict, not a bad request: the payload is well-formed and the deployment is wrong.
    if (Access.RefuseForeignWorkspace(hostWorkspace, body.Repository, body.Workspace) is { } foreign)
    {
        return Results.Conflict(new ErrorResponse(foreign));
    }

    // Registered is addressable; adopted is disciplined (D70) — so the difference is recorded as it is.
    // Only a door that looked says false: the desktop's *add* of a folder with no manifest. Silence is
    // `connect`, which is adoption; a shared deployment holds no roots and so no unadopted row.
    var adopted = mode == ServiceMode.Shared || (body.Adopted ?? true);
    var declared = new Registration(
        body.Repository,
        Adopted: adopted,
        body.Domain?.Summary,
        body.Domain?.Owns ?? [],
        body.Domain?.Accepts ?? [],
        body.Packs ?? [],
        Entries: 0,
        // A shared deployment never stores a machine path, even one a buggy client sent: the feed has
        // no field for it by design (D47 §4), and what must not be served is best not kept.
        Root: mode == ServiceMode.Shared || string.IsNullOrWhiteSpace(body.Root) ? null : body.Root,
        // A join is a manifest's declaration (D47 §4), so a repository without one joins nothing.
        Joined: adopted && (body.Join ?? false),
        // Knowledge feeds only from a joined repository (D47 §4) — narrowed here as well as in the
        // CLI, because this door also answers clients the CLI never saw.
        SharesKnowledge: adopted && (body.Join ?? false) && (body.ShareKnowledge ?? false),
        // A SHARED host puts every row in its own circle — it is that workspace's deployment, and the
        // receiving deployment's wiring is what decides where fed material lands (D48 §2/§5). On a
        // LOCAL host silence PRESERVES the row: an ordinary re-registration runs on every sync tick
        // and says nothing about the wiring, so a null must not re-point the repository to `default`.
        Workspace: hostWorkspace ?? body.Workspace,
        // The canonical line, as the checkout that registered knows it (D48 §6) — unstated preserves.
        DefaultBranch: body.DefaultBranch,
        // What it says it uses (D91), part of the declaration and replaced with it.
        Uses: Declared.Uses(body.Domain?.Uses, body.Repository),
        // Its lanes' words (D115 §2.2); unstated preserves what the row holds.
        Lanes: body.Lanes is null
            ? null
            : Declared.Lanes(body.Lanes.Select(lane => lane is null
                ? null
                : new DeclaredLane(lane.Id ?? "", lane.Title ?? "", lane.Summary ?? "", lane.Steward ?? false))));

    // A SHARED deployment holds many machines' copies of one declaration, so it orders them by the
    // commit each was read at, as it orders their knowledge (SYNC5b) — the last writer no longer wins.
    // A LOCAL host's registration is this machine's own, from its own checkout: nothing to order.
    Registration registered;
    if (mode == ServiceMode.Shared)
    {
        var (outcome, taken) = await s.Service.RegisterFedAsync(
            declared, FedFrom(body.Commit, body.CommittedAt, body.Branch, body.Base, http), DateTimeOffset.UtcNow, ct);
        if (taken is null) return FeedAnswer(outcome);
        registered = taken;
    }
    else
    {
        registered = await s.Service.RegisterAsync(declared, DateTimeOffset.UtcNow, ct);
    }

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
    // Read before it goes: a joined checkout's retire is owed to its circle (SYNC5b), and a person
    // removing it should hear that the team's deployment will too, not only this machine.
    var leaving = (await s.Service.RegistryAsync(ct: ct))
        .Named(repository);
    var retired = await s.Service.RetireAsync(repository, ct);
    var circle = retired && mode == ServiceMode.Local && leaving is { Joined: true, Root: not null }
        && s.Remotes?.For(leaving.InWorkspace) is not null
            ? leaving.InWorkspace
            : null;

    return Results.Ok(new RetiredResponse(
        repository, retired,
        retired
            ? $"`{repository}` is no longer registered here. Nothing was deleted: its files, its history "
              + "and its doctrine are its own — it has simply stopped being addressable and indexed on "
              + "this machine, and its entries have left the index."
              + (circle is null ? "" : $" It leaves the `{circle}` workspace's deployment too, on the next sync.")
            : $"`{repository}` was not registered here, so there was nothing to retire."));
});

// Re-wiring only — the workspace, and nothing else about the row (design §7: the two updates are kept
// visibly apart, because one is local instant wiring and the other edits a tracked file).
app.MapPost("/api/registry/{repository}/workspace", async (
    ComposedService s, string repository, WireRequest body, CancellationToken ct) =>
{
    // Re-wiring across a shared deployment's own boundary is the same refusal the registration door
    // gives (D48 §5) — a host that serves one circle cannot hold a row belonging to another. And a
    // re-wire must name where to: silence became `default`, out of a shared host's circle (REV3).
    if (Access.RefuseRewire(hostWorkspace, repository, body.Workspace) is { } refused)
    {
        return Results.Conflict(new ErrorResponse(refused));
    }

    var existing = (await s.Service.RegistryAsync(ct: ct))
        .Named(repository);
    if (existing is null)
    {
        return Results.NotFound(new ErrorResponse(
            $"`{repository}` is not registered here — `daoris connect` from inside it, or add it from Repositories."));
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

    // A named workspace is the person setting the folder up AS one (D77): every row lands there.
    var stated = string.IsNullOrWhiteSpace(body.Workspace) ? null : body.Workspace.Trim();
    var imported = await s.Service.ImportAsync(folder, DateTimeOffset.UtcNow, stated, ct);
    var names = imported.Select(r => r.Repository).ToList();

    return Results.Ok(new ImportedResponse(
        folder, names.Count, names,
        names.Count == 0
            ? $"Nothing under '{folder}' — an import registers a folder's immediate subdirectories."
            : stated is null
                ? $"Registered {names.Count} from '{folder}': {string.Join(", ", names)}. Existing rows kept "
                  + "their workspace: an import states none, and unstated wiring is preserved."
                : $"Registered {names.Count} from '{folder}' into workspace `{stated}`: {string.Join(", ", names)}.")
    {
        Workspace = stated,
    });
});

app.MapGet("/api/registry", async (
    ComposedService s, HttpContext http, string? workspace, CancellationToken ct) =>
    (await s.Service.RegistryAsync(workspace, ct)).Select(r => new RegistrationResponse(
        r.Repository, r.Adopted, r.Registered, r.Summary, r.Owns, r.Accepts, r.Packs, r.Entries,
        // Machine-local by design (D46): a filesystem path is answered only to a caller on this
        // machine, so a remote deployment never serves anyone's disk layout to the network.
        Root: MachineLocal(http) ? r.Root : null,
        r.Joined, r.SharesKnowledge, r.InWorkspace, r.DefaultBranch, r.Addressable, r.DependsOn,
        // What an asker may address there (D115 §2.2): each lane's words, never its paths.
        r.DeclaredLanes.Select(lane => new LaneWire(lane.Id, lane.Title, lane.Summary, lane.Steward)).ToList())));

// A repository's code map (MAP3a): its modules and how they depend on each other, read from its own
// committed file — never written to (D32). A repository with a checkout here is read from it on each
// ask; one without answers with what was fed (MAP3b) or brought down from the circle's remote (MAP3e),
// saying where it came from, or with no file when nothing was. The file's content is
// repository-relative by its own rules, and the provenance names a commit and a key, never a path, so
// nothing machine-local rides this door. The shape is Core's (CodeMapWire), the one a machine's host
// reads when it brings a teammate's map down — two hands, one shape.
app.MapGet("/api/code-map/{repository}", async (ComposedService s, string repository, CancellationToken ct) =>
    await s.Service.CodeMapAsync(repository, ct) is { } read
        ? Results.Text(CodeMapWire.Answer(repository, read), "application/json")
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
        report.Entries, report.Repositories, report.Withheld, report.SemanticError, report.Absent,
        report.Embedded is { } embedded
            ? new EmbeddedResponse(embedded.Entries, embedded.Pieces, embedded.Split, embedded.Window)
            : null));
});

// ——— The feed and sync doors (D47 §§4–6, D68). Which doors exist depends on the deployment's role: a
// SHARED host is fed by desktops — records and content arrive attributed to the key that carried
// them — and orders the quest operations they push; a LOCAL host is fed by nobody, and its sync doors
// answer only its own driver. A door with no meaning in a mode does not exist in that mode.
if (mode == ServiceMode.Shared)
{
    // Session records, keyed by origin + id — the judgement already ran where the process lived; the
    // record upserts whole and is never re-judged (D47 §6). There is no transcript field to strip,
    // because the wire (Core's SessionWire, the one the machines' client writes) has none. Only
    // joined repositories' records are taken (§4).
    app.MapPost("/api/feed/sessions", async (ComposedService s, HttpContext http, CancellationToken ct) =>
    {
        // Unreachable while the shared gate stamps every /api caller — kept deliberately: the origin
        // is the record's attribution, and a gate refactor that dropped the stamp must fail HERE,
        // loudly, not mirror records under an empty name.
        var origin = http.Items["daoris.principal"] as string;
        if (string.IsNullOrWhiteSpace(origin))
        {
            return Results.BadRequest(new ErrorResponse("the feed carries its key's identity — this door answers only keyed callers"));
        }

        using var body = new StreamReader(http.Request.Body);
        if (SessionWire.ReadFeed(await body.ReadToEndAsync(ct)) is not { } records)
        {
            return Results.BadRequest(new ErrorResponse("a feed is `records`, each with its created and updated times"));
        }

        var outcome = await new SessionFeed(s.Service, s.Sessions).FeedAsync(origin, records, ct);

        return outcome.Refusal switch
        {
            SessionFeedRefusal.None => Results.Ok(new FeedResponse(outcome.Records, outcome.Message)),
            SessionFeedRefusal.NotJoined => Results.Conflict(new ErrorResponse(outcome.Message)),
            _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
        };
    });

    // The team's records, down (SYNC4): what this deployment holds after a revision, in order — every
    // origin but the caller's own, which the caller already has. Keyed callers only, like the feed:
    // the caller's key is what says which records are its own.
    app.MapGet("/api/sessions/since", async (ComposedService s, HttpContext http, long? since, CancellationToken ct) =>
    {
        if (http.Items["daoris.principal"] is not string caller || string.IsNullOrWhiteSpace(caller))
        {
            return Results.BadRequest(new ErrorResponse("the team's records are read by a keyed caller — its key says which are its own"));
        }

        return Results.Text(SessionWire.Page(await s.Sessions.TeamSinceAsync(since ?? 0, caller, ct: ct)), "application/json");
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
                    $"unknown kind '{entry.Kind}' — one of: rule, knowledge, skill, decision, fix, taskoutcome, index"));
            }

            entries.Add(new KnowledgeEntry(
                body.Repository, kind, Provenance.Local, entry.Title ?? "", entry.Body ?? "",
                entry.RelativePath ?? "", entry.Anchor,
                Lines: LineSpan.Of(entry.FirstLine, entry.LastLine, entry.Body ?? "")));
        }

        var outcome = await s.Service.FeedAsync(
            body.Repository, entries,
            FedFrom(body.Commit, body.CommittedAt, body.Branch, body.Base, http), ct);
        return FeedAnswer(outcome);
    });

    // A repository's code map (MAP3b) — what this deployment answers `GET /api/code-map` with, having
    // no checkout. Judged whole again by the reader that judges the file on disk, and ordered exactly
    // as the knowledge feed is, at its own commit.
    app.MapPost("/api/feed/code-map", async (
        ComposedService s, HttpContext http, FeedCodeMapRequest body, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(body.Repository))
        {
            return Results.BadRequest(new ErrorResponse("repository is required"));
        }

        return FeedAnswer(await s.Service.FeedCodeMapAsync(
            body.Repository, body.File, body.Map,
            FedFrom(body.Commit, body.CommittedAt, body.Branch, body.Base, http), ct));
    });

    // Which commit each feed of a repository stands on here — what a feeding machine asks git about
    // before it feeds (SYNC5a): the deployment cannot run git, so the question travels to the checkout.
    app.MapGet("/api/feed/held", async (ComposedService s, string? repository, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(repository))
        {
            return Results.BadRequest(new ErrorResponse("repository is required"));
        }

        var held = await s.Service.HeldAsync(repository, ct);
        return Results.Ok(new FeedHeldResponse(repository, held.Knowledge, held.CodeMap, held.Registration));
    });

    // The remote's quest doors (D68, sync design §8): what it accepted after a number, in its order,
    // and a push rebased on one, judged quest by quest through the same table every machine uses. The
    // wire is Core's (QuestWire), the one the machines' client writes — two hands, one shape.
    app.MapGet("/api/quests/operations", async (ComposedService s, long? since, CancellationToken ct) =>
        Results.Text(QuestWire.Page(await s.Quests.OperationsSinceAsync(since ?? 0, ct: ct)), "application/json"));

    app.MapPost("/api/quests/operations", async (ComposedService s, HttpRequest request, CancellationToken ct) =>
    {
        using var body = new StreamReader(request.Body);
        if (QuestWire.ReadPush(await body.ReadToEndAsync(ct)) is not var (@base, pushed))
        {
            return Results.BadRequest(new ErrorResponse(QuestWire.Shape));
        }

        // The remote re-judges a publish as its own door would (JudgeReceived), and files it by the
        // receiver's registration HERE — the push names no workspace (SYNC0a).
        var registered = await s.Service.RegistryAsync(ct: ct);
        var judged = await s.Quests.ReceiveAsync(
            @base, pushed,
            asked => s.Exchange.JudgeReceived(asked, registered),
            asked => registered.Named(asked.To)?.InWorkspace ?? Workspaces.Default,
            ct);
        return Results.Text(QuestWire.Pushed(judged), "application/json");
    });
}
else
{
    // The machine's sync door (D69, SYNC4): one pass for one circle — the quests' fetch, rebase and
    // push, then the session records both ways — what the driver's tick asks for. The quest half is
    // the same pass a take on a shared quest runs as it claims. And where THIS machine's claim on a
    // quest stands, which is how its driver knows a session to stop.
    app.MapPost("/api/sync", async (ComposedService s, string? workspace, CancellationToken ct) =>
    {
        var circle = Workspaces.Normalize(workspace);
        if (s.Remotes?.For(circle) is not { } remote)
        {
            return Results.Ok(new SyncResponse(circle, s.Quests.Machine, Wired: false, 0, [], [], [], 0, 0, null, 0));
        }

        var quests = await QuestSync.RunAsync(s.Quests, s.Service, remote, circle, ct);
        var sessions = await SessionSync.RunAsync(s.Sessions, s.Quests, s.Service, remote, circle, ct);
        // The team's code maps come down on the same pass (MAP3e): pulling one needs no git, so it is
        // the host's to do, and the host is what answers the page for a repository with no checkout here.
        var maps = await CodeMapSync.RunAsync(s.Service, remote, circle, ct);
        return Results.Ok(new SyncResponse(
            circle, s.Quests.Machine, Wired: true, quests.Pushed,
            quests.Conflicts.Select(c => new QuestConflictNote(c.Quest, (c.Attempted ?? QuestStatus.Open).ToString())).ToList(),
            quests.Refused.Select(r => new QuestPushRefusalWire(r.Quest, r.Reason)).ToList(),
            quests.Behind,
            sessions.Pushed,
            sessions.Fetched,
            quests.Problem ?? sessions.Problem ?? maps.Problem,
            maps.Fetched));
    });

    // Where a circle stands (SYNC6a): read from the store, so it answers without reaching the remote —
    // what the status bar polls and `daoris-driver sync status` prints.
    app.MapGet("/api/sync", async (ComposedService s, string? workspace, CancellationToken ct) =>
    {
        var circle = Workspaces.Normalize(workspace);
        if (s.Remotes?.For(circle) is null)
        {
            return Results.Ok(new SyncStandingResponse(circle, Wired: false, 0, [], [], null, null, null));
        }

        var standing = await QuestSync.StandingAsync(s.Quests, s.Service, circle, ct);
        return Results.Ok(new SyncStandingResponse(
            circle, Wired: true, standing.Ahead, standing.Behind, standing.Conflicts,
            standing.Synced, standing.Tried, standing.Problem));
    });

    app.MapGet("/api/quests/{id}/claim", async (ComposedService s, string id, CancellationToken ct) =>
        Results.Ok(new QuestClaimResponse(
            id.TrimStart('#'), (await s.Quests.ClaimAsync(id.TrimStart('#'), ct)).ToString().ToLowerInvariant())));

    // The retires this machine's checkouts owe a circle (SYNC5b): what the driver's pass carries to
    // that circle's deployment, then clears. The store writes them as the row leaves, so no door that
    // retires, re-wires or re-registers has to remember to.
    app.MapGet("/api/registry/retired", async (ComposedService s, string? workspace, CancellationToken ct) =>
    {
        var circle = Workspaces.Normalize(workspace);
        return Results.Ok(new RetiredPendingResponse(circle, await s.Service.RetiredAsync(circle, ct)));
    });

    app.MapDelete("/api/registry/retired/{repository}", async (
        ComposedService s, string repository, string? workspace, CancellationToken ct) =>
    {
        var circle = Workspaces.Normalize(workspace);
        var cleared = await s.Service.ClearRetiredAsync(repository, circle, ct);
        return Results.Ok(new RetiredResponse(
            repository, cleared,
            cleared
                ? $"`{circle}` no longer owes a retire of `{repository}`."
                : $"`{circle}` owed no retire of `{repository}`, so there was nothing to clear."));
    });
}

log.Info("app.started",
    ("version", typeof(MachineLogProvider).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion),
    ("mode", mode.ToString().ToLowerInvariant()));
app.Lifetime.ApplicationStopping.Register(() =>
    log.Info("app.stopped", ("uptimeSeconds", (long)(DateTimeOffset.UtcNow - started).TotalSeconds)));
// LOG2a: the shell stops a host it started by closing its standard input, so the stop is the lifetime's
// and the line above is written. Only when the shell asked (the variable): a terminal's host never opens
// its input. Watched once the host is up, so a stop is never asked of a host still starting.
app.Lifetime.ApplicationStarted.Register(() => _ = InputEndStop.WatchWhenAsked(
    Environment.GetEnvironmentVariable(InputEndStop.Variable), Console.OpenStandardInput, app.Lifetime));
app.Run();
return 0;

// A machine path is visible only to a loopback caller of a LOCAL host (D47 §4). In shared mode the
// answer is no for everyone — structural absence, not a policed permission: the deployment whose
// callers are the network has no branch that serves a path.
bool MachineLocal(HttpContext http) => mode == ServiceMode.Local && IsLoopback(http);

// The commit a feed speaks for (D48 §6). Whole or nothing, like every pair in this system: half a
// provenance cannot be compared with what is held, and the door must not assemble a plausible one out
// of the half it got. The origin is the key's own identity — recorded because the write carried it,
// not because a second identity model was invented (D47 §7).
static FeedProvenance? FedFrom(
    string? commit, DateTimeOffset? committedAt, string? branch, string? checkedAgainst, HttpContext http) =>
    commit is { Length: > 0 } && committedAt is { } at && branch is { Length: > 0 }
        ? new FeedProvenance(commit, at, branch, http.Items["daoris.principal"] as string)
        {
            Base = string.IsNullOrWhiteSpace(checkedAgainst) ? null : checkedAgainst.Trim(),
        }
        : null;

// A feed with no commit or a malformed map is a bad request; everything else is a state conflict —
// this deployment holds something the feed cannot replace. The `information` flag is what lets a sync
// report a stale, moved or branch feed as news rather than as a wall (§6).
static IResult FeedAnswer(FeedOutcome outcome) =>
    outcome.Accepted
        ? Results.Ok(new FeedResponse(outcome.Entries, outcome.Message))
        : outcome.Refusal is FeedRefusal.NoProvenance or FeedRefusal.Malformed
            ? Results.BadRequest(new FeedRefusalResponse(outcome.Message, outcome.Information))
            : Results.Conflict(new FeedRefusalResponse(outcome.Message, outcome.Information));

// A convergence is a prompt to look, so the suggestion says what to read and where the change goes —
// never "apply this". Doctrine that appeared without anyone choosing it is the failure this project
// exists to prevent (D21).
// A kind nobody has is the caller's mistake, said back as a 400 rather than widened to every kind (REV3).
static (IReadOnlySet<EntryKind>? Kinds, string? Refusal) KindsOrRefusal(string? kinds)
{
    try
    {
        return (KnowledgeQuery.ParseKinds(kinds), null);
    }
    catch (ArgumentException refused)
    {
        return (null, refused.Message);
    }
}

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

// Which quests this host would delete (D95) — the exchange's judgement, so the page offers the verb only
// where the door would take it. None at a shared deployment, which has no delete door.
async Task<IReadOnlySet<string>> DeletableAsync(ComposedService s, IEnumerable<Quest> quests, CancellationToken ct) =>
    mode == ServiceMode.Local ? await s.Exchange.DeletableAsync(quests, ct) : new HashSet<string>();

// One quest as a door answers it, with whether it may be deleted (D95).
async Task<QuestResponse> QuestAnswerAsync(ComposedService s, HttpContext http, Quest quest, CancellationToken ct) =>
    ToQuest(quest, s.Files, MachineLocal(http), (await DeletableAsync(s, [quest], ct)).Contains(quest.Id));

// Links and attachment names travel with every read; a kept file's PATH answers only to a caller on
// this machine, and only when the bytes are here (D47 §4, D65 §2) — so null says "named, not held".
static QuestResponse ToQuest(Quest q, QuestFiles? files, bool machineLocal, bool deletable = false) => new(
    q.Id, q.From, q.To, q.Title, q.Body, q.Status.ToString(), q.Note, q.Filed, q.Updated, q.Workspace,
    q.Links,
    q.Attachments.Select(a => new QuestAttachmentResponse(
        a.Name, a.Sha256, a.Bytes,
        Path: machineLocal && files is not null && files.Has(q.Id, a) ? files.PathOf(q.Id, a) : null)).ToList(),
    q.Then.Select(s => new QuestStepWire(s.To, s.Title, s.Body, s.SetUpIn)).ToList(),
    q.Parent,
    q.Conflicts.Select(c => new QuestConflictResponse(c.Machine, c.Attempted.ToString(), c.Note, c.At, c.Sequence)).ToList(),
    q.Awaits,
    q.PublishedBy,
    deletable,
    q.Lanes,
    q.Requirements.Select(r => new QuestRequirementWire(
        r.Quote, r.Check, r.Evidence.Select(e => (QuestEvidenceWire?)new QuestEvidenceWire(e.Path, e.Gate)).ToList())).ToList(),
    q.Answers.Select(a => new QuestAnswerWire(a.Requirement, a.Met, a.Departed, a.Quote)).ToList(),
    q.Held,
    q.Accepted,
    // What a list calls it (SESSUX1j): the publisher's short title, else the name read from its words, from this host.
    q.Name,
    // Why it waits, and what was read of its evidence (EVID1a): codes and names, which every door may answer.
    q.Hold is { } hold ? QuestEvidenceCodes.Spell(hold) : null,
    q.AwaitsEvidence,
    q.Evidence is { } read
        ? new QuestEvidenceVerdictWire(
            read.Commit, read.How, read.Session,
            read.Items.Select(i => (QuestEvidenceReadWire?)new QuestEvidenceReadWire(i.Requirement, i.Path, i.Gate, i.Result, i.Object, i.Changed, i.Spelled)).ToList(),
            read.At, read.Machine)
        : null,
    // The review on the record (REVIEWENV1b): names, words and codes, which every door may answer; a local set-up's address is
    // absent where it came from another machine, as the wire left it.
    q.Review is { } chosen ? new QuestReviewWire(chosen.Choice, chosen.Words) : null,
    q.SetUpIn,
    q.SetUps.Count == 0
        ? null
        : q.SetUps.Select(s => new QuestSetUpWire(s.Commit, s.Look, s.Shows, s.Again, s.Served, s.Run, s.Session, s.Local, s.At, s.Machine, s.Sequence)).ToList(),
    q.Verdicts.Count == 0
        ? null
        : q.Verdicts.Select(v => new QuestReviewVerdictWire(
            v.Said, v.SetUp is { } named ? new QuestSetUpRefWire(named.Machine, named.Sequence) : null, v.Commit, v.Words, v.At, v.Machine)).ToList());

// Requirements as a door hands them to the exchange (DRIFT1c): a half left out, or a whole one, arrives
// blank and is refused there naming which — the same sentence every door gives. So does an evidence item
// naming both or neither (EVID1a).
static IReadOnlyList<QuestRequirement> RequirementsOf(IReadOnlyList<QuestRequirementWire?>? given) =>
    (given ?? []).Select(r => new QuestRequirement(r?.Quote ?? "", r?.Check ?? "")
    {
        Evidence = [.. (r?.Evidence ?? []).Select(e => new QuestEvidence(e?.Path, e?.Gate))],
    }).ToList();

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
        a.Quests, a.Intake, a.Deletable,
        // The person's words, served as the sentence is: these routes are a local host's alone (DRIFT1a).
        Words: a.Words.Select(w => new AskWordResponse(AskWord.Spell(w.Kind), w.Text, w.At, w.Session, w.Quest)).ToList(),
        WordsKeptFrom: a.WordsKeptFrom,
        // The go-aheads its sessions asked for (KNOWUSE1a), every one, served as the words are.
        GoAheads: a.GoAheads.Select(g => new GoAheadResponse(
            g.Number, g.Kind, g.On, g.Act, GoAhead.Spell(g.State),
            g.Asked.Select(r => new GoAheadRequestResponse(r.Session, r.Quest, r.At, r.Why)).ToList(),
            g.Answer is { } answer ? new GoAheadAnswerResponse(answer.Approved, answer.Words, answer.At) : null,
            g.Near)).ToList(),
        // The person's review choices and the intake's proposals (REVIEWENV1b), each absent where there is none.
        ReviewChoices: a.ReviewChoices.Count == 0
            ? null
            : a.ReviewChoices.Select(c => new AskReviewChoiceResponse(c.Choice, c.At, c.Words)).ToList(),
        ReviewProposals: a.ReviewProposals.Count == 0
            ? null
            : a.ReviewProposals.Select(p => new AskReviewProposalResponse(p.Choice, p.Reason, p.At, p.Session, p.Quest)).ToList());
}

// A chain's steps as a door hands them to the exchange (D65 §4, REVIEWENV1b): a set-up step's environment rides with its words.
// A step sent as null arrives blank, refused by the exchange naming its number as a step missing its words is (REVIEWENV1b3).
static IReadOnlyList<QuestStep> StepsOf(IReadOnlyList<QuestStepWire?>? given) =>
    (given ?? []).Select(step => new QuestStep(step?.To ?? "", step?.Title ?? "", step?.Body ?? "") { SetUpIn = step?.SetUpIn }).ToList();

// A chain's review choice as a door hands it to the exchange (REVIEWENV1b): a choice left out arrives blank, refused there.
static QuestReview? ReviewOf(QuestReviewWire? given) => given is null ? null : new QuestReview(given.Choice ?? "", given.Words);

static EntryResponse ToEntry(KnowledgeEntry entry) => new(
    entry.Id, entry.Repository, entry.Kind.ToString(), entry.Provenance.ToString(),
    entry.Title, entry.RelativePath, entry.Body, entry.Anchor, entry.Workspace,
    entry.Lines?.First, entry.Lines?.Last);

// The transcript is a machine-local path, guarded exactly as the registration's root is (D47 §4):
// answered only to a caller on this machine. The evidence stays — commits are the reviewable record
// and are meant to travel; the transcript is diagnostics for the machine that ran the session.
// The PROFILE NAME is guarded the same way and for the same reason (D49 §4): which account a session
// ran as is this machine's wiring, and it is the one field a person is likely to name after
// themselves. So is the TREE (D51), which is a filesystem path outright. The harness version is a
// fact about a tool and travels with the record.
static SessionResponse ToSession(Session s, bool loopback) => new(
    s.Id, s.Quest, s.Repository, s.Adapter, s.StateName,
    // The note is free text the driver wrote, and it named the tree and the account (REV3).
    loopback ? s.Note : SessionNote.ForAnotherMachine(s.Note, s), s.Evidence,
    Transcript: loopback ? s.Transcript : null,
    s.Created, s.Updated, s.Workspace, s.Kind.ToString().ToLowerInvariant(),
    s.HarnessVersion,
    Profile: loopback ? s.Profile : null,
    Tree: loopback ? s.Tree : null,
    BaseCommit: loopback ? s.BaseCommit : null,
    Ask: s.Ask,
    Took: s.Took,
    // The person's own words, which may name anything on this machine: answered to it only, like a transcript.
    Answer: loopback ? s.Answer : null,
    Interrupted: s.Interrupted,
    // A limit names no account (TOOL4c), so it is answered to every caller, as the state beside it is.
    Limit: s.Limit,
    // The same words, one by one (MSG1a), under the same guard.
    Said: loopback ? s.Said.Select(ToSaid).ToList() : null,
    // The note's lines by code (LANG1a), beside it and cleaned as it is for any other caller.
    NoteParts: NoteParts.Element(loopback ? s.NoteParts : NoteParts.ForAnotherMachine(s.NoteParts, s)),
    // The second opinion a reviewer's record reads for (XAGENT1c): this machine's, like the opinion, so answered to it only.
    Opinion: loopback ? s.Opinion : null);

// A word with whose it is (XAGENT1c): `by` names the second opinion whose findings these are, and is absent for the person's.
static SaidWordResponse ToSaid(SaidWord word) => new(word.Id, word.Text, word.At, word.Files, word.Reopens, word.By);

// An opinion as this machine's doors answer it (XAGENT1c), with where its pass stands, derived when read.
static OpinionResponse ToOpinion(OpinionStanding standing)
{
    var opinion = standing.Opinion;
    return new(
        opinion.Id, opinion.Occasion, opinion.Pass, opinion.Working, opinion.Session, opinion.Rechecks,
        new OpinionCandidateWire(opinion.Candidate.Repository, opinion.Candidate.Base, opinion.Candidate.Tip, [.. opinion.Candidate.Commits]),
        new OpinionReviewerWire(opinion.Reviewer.Adapter, opinion.Reviewer.Label, opinion.Reviewer.Product, opinion.Reviewer.Maker, opinion.Reviewer.Account),
        opinion.Families, opinion.Posture, opinion.Minutes, opinion.Tier, opinion.Asked, opinion.Due, standing.State, standing.Why,
        opinion.Given is { } given
            ? new OpinionGivenResponse(
                [.. given.Findings.Select(finding => new OpinionFindingResponse(
                    finding.Number, finding.Weight, finding.Where, finding.Claim, finding.Consequence, finding.Reproduce, finding.Sure,
                    finding.Proposal))],
                given.Read, given.Limits,
                given.Rechecked.Count == 0 ? null : [.. given.Rechecked.Select(recheck => new OpinionRecheckResponse(recheck.Finding, recheck.Says))],
                given.At)
            : null,
        opinion.Handed is { } handed ? new OpinionHandedResponse(handed.Session, handed.Word, handed.At) : null,
        [.. opinion.Answers.Select(answer => new OpinionAnswerResponse(answer.Finding, answer.Said, answer.Commit, answer.Evidence, answer.Why, answer.At))]);
}

// An opinion is this machine's (D47 §4): a caller off it is answered none, as a quest's file is not served to one.
static IResult OffMachineOpinion() =>
    Results.NotFound(new ErrorResponse("A second opinion is kept on this machine, and answered only to a caller on this machine."));

// A say or a take refused (MSG1a), its word as the wire spells it, kebab-case like a session's state.
static string SayRefusal(SessionSayRefusal refusal) => refusal switch
{
    SessionSayRefusal.Empty => "no-words",
    SessionSayRefusal.NotFound => "not-found",
    SessionSayRefusal.NotOurs => "not-ours",
    SessionSayRefusal.Intake => "intake",
    SessionSayRefusal.StoodDown => "stood-down",
    SessionSayRefusal.Opinion => "opinion",
    _ => "running",
};

// A session delete's judgement as its doors answer it (SESSUX1f): the sentence as `error`, beside its word and the facts
// the word names. The words are the wire's, kebab-case like a session's state.
static SessionDeletionResponse ToDeletion(SessionDeleteOutcome outcome) => outcome.Refusal == SessionDeleteRefusal.None
    ? new SessionDeletionResponse(Deletable: true)
    : new SessionDeletionResponse(
        Deletable: false, Error: outcome.Message,
        Refusal: outcome.Refusal switch
        {
            SessionDeleteRefusal.NotFound => "not-found",
            SessionDeleteRefusal.NotOurs => "not-ours",
            SessionDeleteRefusal.Live => "live",
            SessionDeleteRefusal.ServedQuest => "served-quest",
            SessionDeleteRefusal.Named => "named",
            _ => "on-remote",
        },
        outcome.Quest, outcome.Ask, outcome.Origin, outcome.Workspace);

// A unit of finished history as its doors answer it (HIST1b): ids only, never a path or a title, and a refusal as its word
// beside the desk's sentence, as a session delete's is (SESSUX1f).
static HistoryUnitResponse ToHistoryUnit(HistoryUnit unit) => new(
    HistoryUnitRef.Spell(unit.Kind), unit.Id, unit.Workspace, unit.Clearable, unit.Quests, unit.Forgotten, unit.Asks,
    unit.Sessions, unit.Teammates, unit.Refusal is { } refusal ? ToHistoryRefusal(refusal) : null,
    [.. unit.Kept.Select(ToHistoryRefusal)]);

static HistoryRefusalResponse ToHistoryRefusal(HistoryKept kept) => new(
    HistoryKept.Spell(kept.Refusal), kept.Message, kept.Quest, kept.Ask, kept.Session, kept.Origin, kept.Workspace,
    kept.Waits is { } waits ? HistoryKept.Spell(waits) : null,
    kept.Stands is { } stands ? HistoryKept.Spell(stands) : null,
    kept.By is { } by ? HistoryKept.Spell(by) : null);

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
