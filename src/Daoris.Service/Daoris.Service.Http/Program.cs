using System.Text;
using System.Text.Json.Serialization;
using Daoris.Knowledge;
using Daoris.Knowledge.Hosting;
using Daoris.Knowledge.Http;
using Daoris.Knowledge.Http.Routes;
using Daoris.Contracts.Asks;

// The browser's half of the service. The MCP host serves an agent over stdio; a browser cannot speak
// that, so this exists — the same composed service behind a read-only JSON surface.
//
//   DAORIS_HOME            where every machine-local file lives (D63) — the installed desktop's own
//                          `data/`, which a host the install carries takes where none is named
//                          (SERVICEHOME2); in a workspace build, with neither it nor the DB named, exit 2
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
// SUPERVISOR1a-ii (D208 §2.2): the holder key leaves this process first, so no start it builds can copy it.
Environment.SetEnvironmentVariable("DAORIS_ROLE_KEY", null);

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

// A host the install carries finds its home where it runs (SERVICEHOME2, D171 point 1): started from a terminal, where the
// install's launchers set no variable, it takes the install's own `data/` as this process's home where none is named, as the
// desktop and the driver host do (D63), before the index, the keys or the log read one. The desktop names the home for the
// host it starts, and a workspace build is no install, so neither changes.
DaorisHome.Adopt(AppContext.BaseDirectory);

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

// HOSTSTART1: an exception that ends this entry point is written as `error`, at `start` until the host serves. The entry
// point is async, so the `using` above has closed `log` by the time the runtime raises that exception as unhandled, and
// the line WatchUnhandled writes for it was dropped: a host that died at start left nothing of why. The watch, which the
// MCP connector shares (HOSTSTART2), takes that line with a writer of its own.
var entryPoint = log.WatchEntryPoint();

// HOMEID1 (D171, the terminal RPC design §3.2): a local host answers its home's id at /api/status, minted there where it has
// none, so a terminal that found an address takes it only where it is this home's service, never another install's. A file that
// does not read is said and left as it is, and the host answers none. A shared host serves a team, not a home (D47): none.
string? homeId = null;
if (mode == ServiceMode.Local && DaorisHome.Resolve() is { } idHome)
{
    homeId = HomeIdentity.Ensure(idHome, out var idUnread);
    if (idUnread is not null) log.Warn("home.unread", ("why", idUnread));
}

// The bundle travels beside the executable. In development the SDK serves wwwroot from the project
// directory — the default content root — but a PUBLISHED host is launched from anywhere, so when the
// working directory has no bundle and the binary's directory does, the binary's wins. Without this
// the published exe answers every API call and serves a 404 for the page, which reads as "the app is
// broken" rather than "the cwd was wrong".
// The choice and the host's one watch-nothing setting are HostRoot's (HTTPWATCH1): the binary's own bundle first, then the
// working folder's (development), and no settings file watched in either.
var builder = HostRoot.Builder(args);
builder.Logging.AddProvider(new MachineLogProvider(log));
// FLAKE2: a host the shell started waits for its requests in flight less than the shell waits for it (InputEndStop.Drain).
builder.Services.Configure<HostOptions>(host =>
    InputEndStop.BoundDrain(host, Environment.GetEnvironmentVariable(InputEndStop.Variable)));

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
// the one thing stopping it. The declared routes' records first, then the shapes not yet declared beside their routes.
builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.TypeInfoResolverChain.Insert(0, ContractJson.Resolver);
    json.SerializerOptions.TypeInfoResolverChain.Insert(1, ApiJson.Default);
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

// ONEWAY1n-i (D156's note): the host's whole pipeline, routing included, held so a request the person confirms is made through it
// as asked, every gate and handler its own. Routing is placed first, where the host placed it by itself before.
RequestDelegate? pipeline = null;
app.Use(next =>
{
    pipeline = next;
    return next;
});
app.UseRouting();

var hostRun = Guid.NewGuid().ToString("N");
app.Use(async (context, next) =>
{
    context.Response.Headers["x-daoris-run"] = hostRun;
    await next(context);
});

// FABRIC-EVENT3b (the fabric events design §A4.2): which ledger families moved, heard on an observer connection of its own, so
// the driver wakes on a quest an agent published rather than at its pace. A local host's alone: a shared host serves no machine's
// waits, and its circles keep their own cursors. Its waits are bounded on the clock the services hold (TESTCLOCK3).
var changes = mode == ServiceMode.Local
    ? await ChangeStream.OpenAsync(options.DatabasePath, log, clock: app.Services.GetService<TimeProvider>() ?? TimeProvider.System)
    : null;

// The change stream's observer closes with the host, once the stop has answered every wait it held (FABRIC-EVENT3b).
if (changes is not null) app.Lifetime.ApplicationStopped.Register(() => changes.DisposeAsync().AsTask().GetAwaiter().GetResult());

// PERSONDOOR1b (D156 point 4; ONEWAY1n-i): what an asker asks the person to confirm, held in this start's memory only, on the
// clock the services hold (the system's, unless a test moves one), and made at the Confirm through the pipeline held above with
// this start's key, ended with the host's stop. What each door binds is read from its own endpoint when a confirmation is asked,
// so a card shows only what binds.
var doorContracts = new DoorContracts(app.Services);
var confirmations = new PersonConfirmations(
    app.Services.GetService<TimeProvider>() ?? TimeProvider.System, log, holds: personKey is not null, doorContracts.For,
    asked => PersonConfirmations.MakeAsync(pipeline!, app.Services, personKey!, asked, app.Lifetime.ApplicationStopping));
var personGate = new PersonGate(personKey, log);

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
        // A change stream's wait is held by design (FABRIC-EVENT3b), and the desktop's wait on what waits for the person (NOTIFY2j,
        // STARTFAST1b): its time is its wait, never slowness. A failure of either is still written.
        var held = UnhandledRequests.RouteOf(context) is ChangeStream.Route or PersonConfirmations.WaitingRoute;
        if (status >= 500 || (!held && clock.ElapsedMilliseconds > MachineLogProvider.SlowRequestMs))
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
// sentence and a code, and one `person.refused` line. The forms a body makes (respond's `whileOpen`, a set-up naming a
// session or none) are judged by their routes, and a publish reads who called. A request the person confirmed in the window is
// made by the host with this key (ONEWAY1n-i, the confirmations below), so it is judged here like any other. A host handed no
// key has no gate here, and trusts the loopback as before. PersonDoors holds the table and why each door is whose.
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

// Each domain's routes, declared in Daoris.Contracts and mapped by its module (Routes/), found by reflection. The routes
// mapped below are the ones not declared yet.
var wiring = new HostWiring(composed, mode, hostRun, homeId, personKey, personGate);
RouteModules.MapAll(app, wiring);

// The window's confirmation of what an asker asked (PERSONDOOR1b, D156 point 4; ONEWAY1n-i, the oneway design §4 M4). A terminal
// cannot be told from a session, so a keyless call to the person's or the driver's door is refused, and its asker asks here with
// the exact request. It waits with no clock; the window lists it and the person answers with the key, and at the Confirm the host
// makes the request as asked. The asker polls its one, and withdraws it with the token its ask was answered with. LOCAL mode only:
// a shared host has no door that is the person's alone (§6). PersonConfirmations holds the rules and the reasons.
if (mode == ServiceMode.Local)
{
    app.MapGet("/api/confirmations", () => confirmations.Waiting().Select(PersonConfirmations.Wire));

    // NOTIFY2j (D196 point 4, the notification design §2.5): what waits after `since` (`<epoch>.<n>`), answered as soon as it
    // moves, one past its time at its time, or at `wait` seconds (30 at most) as it is; any other cursor at once. The desktop's
    // host holds one and tells every window, so no window polls. The host's stop answers a held wait at once.
    app.MapGet(PersonConfirmations.WaitingRoute, async (string? since, int? wait, CancellationToken ct) =>
    {
        using var ends = CancellationTokenSource.CreateLinkedTokenSource(ct, app.Lifetime.ApplicationStopping);
        var (next, waiting) = await confirmations.WaitAsync(since, TimeSpan.FromSeconds(Math.Clamp(wait ?? 0, 0, 30)), ends.Token);
        return new ConfirmationsWaitingResponse(next, [.. waiting.Select(PersonConfirmations.Wire)]);
    });

    // An id an earlier start held reads `restarted`: what waited ended with that start (§4 M4).
    app.MapGet("/api/confirmations/{id}", (string id) =>
        confirmations.Find(id) is { } found
            ? Results.Ok(PersonConfirmations.Wire(found))
            : ConfirmationAnswer(confirmations.Missing(id)));

    // The ask is read here rather than bound, so it is read no further than its limit: a JSON body, which a page on
    // another origin sends only with CORS's leave (design §4.2), parsed by the host's own options.
    app.MapPost("/api/confirmations", async (HttpContext http, CancellationToken ct) =>
    {
        var (body, unread) = await PersonConfirmations.ReadAskAsync(http, ct);
        if (unread is not null) return unread;

        var (asked, token, refusal, message) = confirmations.Ask(body!.Method, body.Path, body.Body, body.Session);
        return refusal switch
        {
            ConfirmationAskRefusal.None => Results.Ok(new ConfirmationAskedResponse(PersonConfirmations.Wire(asked!), message, token!)),
            // A host handed no key refused nothing, so there is nothing to confirm: a state, the lock's own shape.
            ConfirmationAskRefusal.NoKey => Results.Conflict(new ErrorResponse(message)),
            // Its asker's places taken (PERSONDOOR1j): the gate's refusal, in its shape and with its log line, asking nothing.
            ConfirmationAskRefusal.Full => personGate.Refusal(http, PersonDoors.ConfirmationsFull, message),
            ConfirmationAskRefusal.Bound => personGate.Refusal(http, PersonDoors.AskerFull, message),
            _ => Results.BadRequest(new ErrorResponse(message)),
        };
    });

    // The person's answers, each behind the key (the gate's Person class), and its asker's withdrawal by its token, each while it
    // waits; a state that refuses it is the lock's own shape. The Confirm makes the request, and is answered how its door took it.
    app.MapPost("/api/confirmations/{id}/confirm", async (string id) => ConfirmationAnswer(await confirmations.ConfirmAsync(id)));
    app.MapPost("/api/confirmations/{id}/refuse", (string id) => ConfirmationAnswer(confirmations.Refuse(id)));
    app.MapPost("/api/confirmations/{id}/withdraw", (string id, ConfirmationWithdrawRequest? body) =>
        ConfirmationAnswer(confirmations.Withdraw(id, body?.Token)));
}

static IResult ConfirmationAnswer((Confirmation? Confirmation, ConfirmationAnswerRefusal Refusal, string Message) outcome) =>
    outcome.Refusal switch
    {
        ConfirmationAnswerRefusal.None => Results.Ok(new ConfirmationActionResponse(PersonConfirmations.Wire(outcome.Confirmation!), outcome.Message)),
        ConfirmationAnswerRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
        ConfirmationAnswerRefusal.Restarted => Results.NotFound(new ErrorResponse(outcome.Message, PersonConfirmations.Restarted)),
        // Made, and its door refused or failed it: what moved is its door's own sentence.
        ConfirmationAnswerRefusal.NotMade => Results.Conflict(new ErrorResponse(outcome.Message, PersonConfirmations.NotMade)),
        ConfirmationAnswerRefusal.NotAsker => Results.Json(
            new ErrorResponse(outcome.Message, PersonConfirmations.NotAsker), statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Conflict(new ErrorResponse(outcome.Message)),
    };

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

// The ask doors are AskRoutes'; the channel's doors of the messages to one part, and an intake, are a local host's alone.
if (mode == ServiceMode.Local)
{
    app.MapChannelParts();

    // One quest's handback whole (HANDBACK1, D219), by the quest's id or the id of the session that served it, in any state: what
    // `daoris session handback` prints, in the read's words the connector's `handback_read` answers too. Every workspace, or
    // the one named. Any id is answered, one naming nothing as not found, so a client tells this door from an older host's 404.
    app.MapGet("/api/handbacks/{id}", async (ComposedService s, string id, string? workspace, CancellationToken ct) =>
        Results.Ok(HandbackResponse.Of(await new HandbackReader(s.Quests, s.Ledger).ReadAsync(id, workspace, ct))));

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

    // A first pass's findings, to its working session as another agent's words (design §6.3): once, at a turn's end. A chat run's go to
    // the record of its line the driver names, `to`, between turns (CHATFLOW1e).
    app.MapPost("/api/opinions/{id}/hand", async (ComposedService s, HttpContext http, string id, OpinionHandRequest? body, CancellationToken ct) =>
        MachineLocal(http)
            ? await OpinionReply(await s.Opinions.HandAsync(id, DateTimeOffset.UtcNow, ct, body?.To, settled: body?.Settled == true), s, http, ct)
            : OffMachineOpinion());

    // A helper's record, and its answer's return (MIXAGENT2a-ii, D163 point 2): the driver's helper session opens the record
    // (a chat that serves the asker, in a clone of its own) and hands the helper's answer to the asker as a word marked `helper`.
    app.MapPost("/api/helpers", async (ComposedService s, HttpContext http, HelperOpenRequest body, CancellationToken ct) =>
        MachineLocal(http)
            ? await HelperReply(
                await s.Helpers.OpenAsync(
                    new HelperOpen(body.Asker ?? "", body.Adapter ?? "", body.Tree ?? "")
                    {
                        Account = body.Account, HarnessVersion = body.HarnessVersion, BaseCommit = body.BaseCommit,
                    },
                    DateTimeOffset.UtcNow, ct),
                s, http)
            : OffMachineOpinion());

    // A partner's record (COAGENT1b, D199 point 2; the co-agent design §2.3 step 5): the driver opens it for one piece, idempotent by
    // the piece's id, a chat that serves the lead and carries the piece, in a clone of its own with an entry of its own.
    app.MapPost("/api/partners", async (ComposedService s, HttpContext http, PartnerOpenRequest body, CancellationToken ct) =>
        MachineLocal(http)
            ? await HelperReply(
                await s.Partners.OpenAsync(
                    new PartnerOpen(body.Lead ?? "", body.Piece ?? "", body.Adapter ?? "", body.Tree ?? "")
                    {
                        Account = body.Account, HarnessVersion = body.HarnessVersion, BaseCommit = body.BaseCommit,
                    },
                    DateTimeOffset.UtcNow, ct),
                s, http)
            : OffMachineOpinion());

    // A piece's answer, handed to its lead's line (the co-agent design §2.3 step 9): ordinary, under the piece's key, never urgent.
    app.MapPost("/api/partners/{partner}/hand", async (ComposedService s, HttpContext http, string partner, HelperHandRequest body, CancellationToken ct) =>
        MachineLocal(http)
            ? await HelperReply(await s.Partners.HandAsync(partner, body.Text ?? "", DateTimeOffset.UtcNow, ct, settled: body.Settled == true), s, http)
            : OffMachineOpinion());

    app.MapPost("/api/helpers/{helper}/hand", async (ComposedService s, HttpContext http, string helper, HelperHandRequest body, CancellationToken ct) =>
        MachineLocal(http)
            ? await HelperReply(await s.Helpers.HandAsync(
                helper, body.Text ?? "", DateTimeOffset.UtcNow, ct, running: body.Running == true,
                level: string.IsNullOrWhiteSpace(body.Level) ? null : body.Level.Trim(), settled: body.Settled == true), s, http)
            : OffMachineOpinion());
}

// A brainstorm (BRAINSTORM1a3; D176 points 1, 2 and 8), the door a terminal's `daoris brainstorm` reaches: the person's question
// written as the request the driver's look freezes into a packet, the packet and the document read back, the person's send and
// the clear. LOCAL mode only, and answered only to a caller on this machine: a brainstorm is kept where it was asked, carries the
// asker's records and this machine's accounts, and is never a quest operation. The passes are the driver's; nothing here runs one.
if (mode == ServiceMode.Local)
{
    var brainstorms = BrainstormDesk.FromEnvironment();
    var meetings = MeetingDesk.FromEnvironment();

    app.MapGet("/api/brainstorms", (HttpContext http) =>
        MachineLocal(http) ? Results.Ok(brainstorms.List().Select(ToBrainstorm)) : OffMachineBrainstorm());

    // ONEWAY1t: the one writer of a brainstorm's request, a meeting's included (`kind`); the caller's own `id` makes an ask idempotent.
    app.MapPost("/api/brainstorms", (HttpContext http, BrainstormAskRequest body) =>
    {
        if (!MachineLocal(http)) return OffMachineBrainstorm();
        return BrainstormKindOf(body.Kind) switch
        {
            BrainstormDesk.MeetingKind when body.Sources is { Count: > 0 } =>
                BrainstormReply(new(BrainstormRefusal.BadShape, "A meeting takes no named sources. Nothing was asked.")),
            BrainstormDesk.MeetingKind => BrainstormReply(meetings.Ask(body.Question, body.Repository, body.Agents, DateTimeOffset.UtcNow, body.Id)),
            BrainstormDesk.BrainstormKind => BrainstormReply(
                brainstorms.Ask(body.Question, body.Repository, body.Agents, DateTimeOffset.UtcNow, body.Id, body.Sources)),
            _ => BrainstormReply(BadBrainstormKind(body.Kind)),
        };
    });

    app.MapGet("/api/brainstorms/{id}", (HttpContext http, string id) =>
    {
        if (!MachineLocal(http)) return OffMachineBrainstorm();
        return brainstorms.Show(id) is { } shown
            ? Results.Ok(new BrainstormShownResponse(ToBrainstorm(shown.Brainstorm), shown.Frozen, shown.Document))
            : Results.NotFound(new ErrorResponse(BrainstormDesk.NotFoundSentence(id)));
    });

    // `?kind=meeting` addresses a meeting; `?round=<n>` names the round a meeting's send is for.
    app.MapPost("/api/brainstorms/{id}/send", (HttpContext http, string id, string? kind, int? round) =>
    {
        if (!MachineLocal(http)) return OffMachineBrainstorm();
        return BrainstormKindOf(kind) switch
        {
            BrainstormDesk.MeetingKind => BrainstormReply(meetings.Send(id, round, DateTimeOffset.UtcNow)),
            BrainstormDesk.BrainstormKind when round is not null => BrainstormReply(
                new(BrainstormRefusal.BadShape, "A brainstorm has one packet to send, no rounds; a round is a meeting's.")),
            BrainstormDesk.BrainstormKind => BrainstormReply(brainstorms.Send(id, DateTimeOffset.UtcNow)),
            _ => BrainstormReply(BadBrainstormKind(kind)),
        };
    });

    app.MapPost("/api/brainstorms/{id}/stop", (HttpContext http, string id, string? kind) =>
    {
        if (!MachineLocal(http)) return OffMachineBrainstorm();
        return BrainstormKindOf(kind) switch
        {
            BrainstormDesk.MeetingKind => BrainstormReply(meetings.Stop(id, DateTimeOffset.UtcNow)),
            BrainstormDesk.BrainstormKind => BrainstormReply(brainstorms.Stop(id, DateTimeOffset.UtcNow)),
            _ => BrainstormReply(BadBrainstormKind(kind)),
        };
    });

    app.MapDelete("/api/brainstorms/{id}", (HttpContext http, string id, string? kind) =>
    {
        if (!MachineLocal(http)) return OffMachineBrainstorm();
        return BrainstormKindOf(kind) switch
        {
            BrainstormDesk.MeetingKind => BrainstormReply(meetings.Clear(id)),
            BrainstormDesk.BrainstormKind => BrainstormReply(brainstorms.Clear(id)),
            _ => BrainstormReply(BadBrainstormKind(kind)),
        };
    });
}

// The kind a brainstorm door is asked of: none is a brainstorm; anything but the two is no kind.
static string? BrainstormKindOf(string? kind) => string.IsNullOrWhiteSpace(kind)
    ? BrainstormDesk.BrainstormKind
    : kind.Trim().ToLowerInvariant() is var word and (BrainstormDesk.BrainstormKind or BrainstormDesk.MeetingKind) ? word : null;

static BrainstormOutcome BadBrainstormKind(string? kind) =>
    new(BrainstormRefusal.BadShape, $"A brainstorm's kind is `brainstorm` or `meeting`; `{kind}` is neither. Nothing was done.");

// A door to a brainstorm answers as a second opinion's does: a shape is a bad request, nothing by that name is not found, and a state
// it is in, a clear the disk would not finish or no home is the lock's own shape, 409.
static IResult BrainstormReply(BrainstormOutcome outcome) => outcome.Refusal switch
{
    BrainstormRefusal.None => Results.Ok(new BrainstormActionResponse(outcome.Brainstorm is { } b ? ToBrainstorm(b) : null, outcome.Message)),
    BrainstormRefusal.NotFound => Results.NotFound(new ErrorResponse(outcome.Message)),
    BrainstormRefusal.BadShape => Results.BadRequest(new ErrorResponse(outcome.Message)),
    _ => Results.Conflict(new ErrorResponse(outcome.Message)),
};

static BrainstormResponse ToBrainstorm(BrainstormStanding b) =>
    new(b.Id, b.Question, b.Repository, b.Agents, b.At, b.State, b.Code, b.Packet, b.Sent,
        b.Kind == BrainstormDesk.MeetingKind ? b.Kind : null, b.Kind == BrainstormDesk.MeetingKind ? b.Round : null);

// A door to a helper answers as a second opinion's does: a shape is a bad request, nothing by that name is not found, and a
// state it is in is the lock's own shape, 409.
async Task<IResult> HelperReply(HelperOutcome outcome, ComposedService s, HttpContext http) =>
    outcome.Refusal switch
    {
        HelperRefusal.None => Results.Ok(new HelperActionResponse(
            outcome.Message,
            outcome.Session is { } session ? await SessionAnswers.ToSessionAsync(s, session, MachineLocal(http)) : null,
            outcome.Word is { } word ? SessionAnswers.ToSaid(word) : null,
            outcome.SettledOn,
            outcome.Waiting ? true : null)),
        HelperRefusal.NotFound or HelperRefusal.Shared => Results.NotFound(new ErrorResponse(outcome.Message)),
        HelperRefusal.BadShape => Results.BadRequest(new ErrorResponse(outcome.Message)),
        _ => Results.Conflict(new ErrorResponse(outcome.Message)),
    };

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
        outcome.Session is { } session ? await SessionAnswers.ToSessionAsync(s, session, MachineLocal(http)) : null,
        outcome.Word is { } word ? SessionAnswers.ToSaid(word) : null,
        outcome.SettledOn, outcome.Waiting ? true : null));
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

// FABRIC-EVENT3b (the fabric events design §A4.2): which ledger families moved after `since` (`<epoch>.<n>`), answered as soon
// as one moves or at `wait` seconds (30 at most) with none. A missing, malformed, stale or future cursor is answered at once with
// every family and the current cursor; a ninth wait at once with the cursor. A read every caller may make, as the connector reads
// the same store; LOCAL mode only. The host's stop answers a held wait at once, so a long-poll never holds the shell's wait.
if (changes is not null)
{
    // FOUNDATION1 row k part 2 (D212, the foundation design §4.1): `journal=1` (or `true`) is a reader asking for the event journal, which is then
    // woken by it, told its family after the others and answered its place (the store, the floor and the newest position) with every read. A reader
    // that never asks, every client from before the family, is answered what it always was and woken by nothing a journal-only append does. The
    // host's `journal` protocol mark says a host answers it; a host without the mark ignores the parameter.
    app.MapGet(ChangeStream.Route, async (string? since, int? wait, string? journal, CancellationToken ct) =>
    {
        using var ends = CancellationTokenSource.CreateLinkedTokenSource(ct, app.Lifetime.ApplicationStopping);
        var asked = journal is "1" || string.Equals(journal, "true", StringComparison.OrdinalIgnoreCase);
        var answer = await changes.WaitAsync(since, TimeSpan.FromSeconds(Math.Clamp(wait ?? 0, 0, 30)), ends.Token, asked);
        return new ChangesResponse(
            answer.Since, answer.Moved, answer.Journal is { } place ? new ChangesJournalResponse(place.Store, place.Floor, place.Newest) : null);
    });
}

// FOUNDATION1 l-i (the foundation design §4.1): the journal's rows after a position, oldest first, with where the journal stands, for each process
// that tells from it. The same local-only door as the stream whose `journal` family wakes it.
if (changes is not null)
{
    app.MapGet(JournalRows.Route, async (ComposedService s, long? since, int? limit, CancellationToken ct) =>
        await s.Journal(since ?? 0, limit ?? JournalRows.Most, ct));
}

// Where `daoris repository connect` lands. It accepts a repository's description of ITSELF — the only thing a
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
            $"`{repository}` is not registered here — `daoris repository connect` from inside it, or add it from Repositories."));
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
            : null,
        report.Read.Select(r => new ReadingResponse(r.Repository, r.ReadAt, r.Commit)).ToList()));
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

    // An ask's channel messages, up and down (SESSONE2d, D167 point 8), the session records' two doors in their shape: fed by a keyed
    // caller and kept by that key, and the team's served after a number, every origin but the caller's own. No UI, and no path: the
    // words arrive cleaned and are cleaned again on arrival (ChannelFeed).
    app.MapPost("/api/feed/channel", async (ComposedService s, HttpContext http, CancellationToken ct) =>
    {
        var origin = http.Items["daoris.principal"] as string;
        if (string.IsNullOrWhiteSpace(origin))
        {
            return Results.BadRequest(new ErrorResponse("the feed carries its key's identity — this door answers only keyed callers"));
        }

        using var body = new StreamReader(http.Request.Body);
        if (ChannelWire.ReadFeed(await body.ReadToEndAsync(ct)) is not { } messages)
        {
            return Results.BadRequest(new ErrorResponse("a channel feed is `messages`, each with its id, ask, time and words"));
        }

        var outcome = await new ChannelFeed(s.Service, s.Messages).FeedAsync(origin, messages, ct);
        return outcome.Refusal switch
        {
            SessionFeedRefusal.None => Results.Ok(new FeedResponse(outcome.Records, outcome.Message)),
            SessionFeedRefusal.NotJoined => Results.Conflict(new ErrorResponse(outcome.Message)),
            _ => Results.BadRequest(new ErrorResponse(outcome.Message)),
        };
    });

    app.MapGet("/api/channel/since", async (ComposedService s, HttpContext http, long? since, CancellationToken ct) =>
    {
        if (http.Items["daoris.principal"] is not string caller || string.IsNullOrWhiteSpace(caller))
        {
            return Results.BadRequest(new ErrorResponse("the team's messages are read by a keyed caller — its key says which are its own"));
        }

        return Results.Text(ChannelWire.Page(await s.Messages.TeamSinceAsync(since ?? 0, caller, ct: ct)), "application/json");
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
    // FABRIC-CLOSE1i: both answer the workspace's handshake, which machines here have not declared they know a bound quest.
    app.MapGet("/api/quests/operations", async (ComposedService s, long? since, CancellationToken ct) =>
        Results.Text(QuestWire.Page(await s.Quests.OperationsSinceAsync(since ?? 0, ct: ct) with
        {
            Closure = await s.Quests.ClosureAsync(ct),
        }), "application/json"));

    app.MapPost("/api/quests/operations", async (ComposedService s, HttpRequest request, CancellationToken ct) =>
    {
        using var body = new StreamReader(request.Body);
        var json = await body.ReadToEndAsync(ct);
        if (QuestWire.ReadPush(json) is not var (@base, pushed))
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
        // What the push said of its machine (FABRIC-CLOSE1i), heard whatever was kept of it, since a refused operation's build is still its own.
        await s.Quests.DeclaredAsync(QuestWire.Declares(json), pushed, DateTimeOffset.UtcNow, ct);
        return Results.Text(QuestWire.Pushed(judged with { Closure = await s.Quests.ClosureAsync(ct) }), "application/json");
    });

    // FABRIC-CLOSE1i2: a person's retirement of a machine the handshake counts, asked through their own machine's host. A machine heard
    // from nothing here is answered `retired: false`, so a 404 stays what a remote before the door answers.
    app.MapPost("/api/quests/machines/{machine}/retire", async (ComposedService s, string machine, CancellationToken ct) =>
    {
        var retired = await s.Quests.RetireMachineAsync(machine, DateTimeOffset.UtcNow, ct);
        return Results.Text(QuestWire.Retired(retired, await s.Quests.ClosureAsync(ct)), "application/json");
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
        // An ask's channel travels on the same pass (SESSONE2d); a remote from before it is told once and the channel stays here.
        var channel = await ChannelSync.RunAsync(s.Messages, s.Sessions, s.Service, remote, circle, ct);
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
            quests.Problem ?? sessions.Problem ?? maps.Problem ?? channel.Problem,
            maps.Fetched));
    });

    // Where a circle stands (SYNC6a): read from the store, so it answers without reaching the remote —
    // what the status bar polls and `daoris remote sync status` prints.
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
            standing.Synced, standing.Tried, standing.Problem,
            // FABRIC-CLOSE1i2: the machines the remote last said had not declared, as the last pass heard them; none where it heard none.
            s.Quests.ClosureOf(circle)?.Undeclared));
    });

    // The person's retirement of a machine the circle's remote still counts (FABRIC-CLOSE1i2): asked at the remote, and what it answers
    // kept as a pass keeps it. A wall or a remote before the door is said in the remote's words, and nothing here changes.
    app.MapPost("/api/sync/machines/{machine}/retire", async (ComposedService s, string machine, string? workspace, CancellationToken ct) =>
    {
        var circle = Workspaces.Normalize(workspace);
        if (s.Remotes?.For(circle) is not { } remote)
        {
            return Results.NotFound(new ErrorResponse($"`{circle}` has no remote on this machine, so no machine of it is counted anywhere."));
        }

        try
        {
            var answered = await QuestSync.RetireMachineAsync(s.Quests, remote, circle, machine, ct);
            return Results.Ok(new MachineRetiredResponse(
                circle, machine, answered.Retired, answered.Closure.Undeclared,
                answered.Retired
                    ? $"`{circle}`'s remote no longer counts machine `{machine}`; a push from it counts it again."
                    : $"`{circle}`'s remote has heard nothing from machine `{machine}`, so it counts nothing to retire."));
        }
        catch (RemoteException wall)
        {
            return Results.Json(
                new ErrorResponse(wall.Status == 404
                    ? $"`{circle}`'s remote is older than retiring a machine, so nothing was retired ({wall.Message})."
                    : $"`{circle}`'s remote did not retire machine `{machine}`: {wall.Message}."),
                statusCode: StatusCodes.Status502BadGateway);
        }
    });

    app.MapGet("/api/quests/{id}/claim", async (ComposedService s, string id, CancellationToken ct) =>
    {
        var takes = await s.Quests.ReadTakesAsync(id.TrimStart('#'), ct);
        return Results.Ok(new QuestClaimResponse(
            id.TrimStart('#'), (await s.Quests.ClaimAsync(id.TrimStart('#'), ct)).ToString().ToLowerInvariant(),
            // FABRIC-CLOSE1c18: a take lost to the person's reopen, which the driver's stop says as that.
            await s.Quests.LostToTheReopenAsync(id.TrimStart('#'), ct) ? "source-reopened" : null,
            // SYNCMERGE1b0: the take it holds the quest by, where the person's bring-forward made it, which the driver carries a
            // stood-down session on over (SYNCMERGE1b).
            takes.BroughtForwardBy(s.Quests.Machine) is { } forward ? QuestAnswers.ToForward(forward) : null,
            // CARRY3: when the take it stands taken by was made, which a submission, a reopen or a go-on never moves as they move
            // `updated`: what the driver judges a take made here after a session ended by.
            takes.Take?.At));
    });

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
app.Lifetime.ApplicationStarted.Register(entryPoint.Running);
// KNOWCENTRAL2d: the memory coordinator applies what a session's commit point released, from the start until the stop.
if (composed.Memory is { } memory)
{
    app.Lifetime.ApplicationStarted.Register(() => _ = new MemoryCoordinator(
            composed.Sessions.Memory, memory, composed.Service,
            failed: error => log.Warn("memory.failed", ("error", error.GetType().Name), ("why", error.Message)))
        .RunAsync(app.Lifetime.ApplicationStopping));
}

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
bool MachineLocal(HttpContext http) => wiring.MachineLocal(http);

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

// BRAINSTORM1a3 (D176 point 8): what a brainstorm door says to a caller off this machine.
static IResult OffMachineBrainstorm() =>
    Results.NotFound(new ErrorResponse("A brainstorm is kept on this machine, and answered only to a caller on this machine."));

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

// Walk up from the BINARY to this workspace's manifest — the shared walk-up (HostComposition) — with
// THIS host's last resort: the parent-of-CWD heuristic. Never from the working directory first:
// `dotnet run` sets the CWD to the project directory, so "parent of the current directory" resolves
// to the service tree and its subprojects get scanned as though they were the family. Found by
// launching the host the documented way and reading what it indexed.
static string DefaultRepositoryRoot() =>
    HostComposition.AboveWorkspace()
    ?? Directory.GetParent(Directory.GetCurrentDirectory())?.FullName
    ?? Directory.GetCurrentDirectory();

// The entry point's type, which the host's tests start it by.
public partial class Program;
