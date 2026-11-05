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
// The inbox's variables leave with it (the event hook design §6.3).
Environment.SetEnvironmentVariable("CLAUDE_CODE_MESSAGING_SOCKET", null);
Environment.SetEnvironmentVariable("CLAUDE_CODE_MESSAGING_TOKEN", null);

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
// the one thing stopping it. The declared routes' records.
builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.TypeInfoResolverChain.Insert(0, ContractJson.Resolver);
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

// Each domain's routes, declared in Daoris.Contracts and mapped by its module (Routes/), found by reflection.
var wiring = new HostWiring(
    composed, mode, hostRun, homeId, personKey, personGate, confirmations, changes, hostWorkspace, options.RepositoryRoot,
    app.Lifetime.ApplicationStopping);
RouteModules.MapAll(app, wiring);

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
