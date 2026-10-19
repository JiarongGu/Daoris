using System.Collections.Frozen;
using System.Reflection;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The session-control surface's host half (D46 §6): the page asks, this answers. A standing choice is
/// an edit to its file under the home (`driver.json`, `harnesses.json`, `permissions.json`,
/// `plugins.json`) plus a nudge, because the file is the truth and the loop re-reads it every tick —
/// the same files the terminal's verbs edit (D50). The controls that touch a process or a tree (stop,
/// a parked session's answer, a conversation, a merge or a discard) go through the shared registry and
/// the service's ledger, and the record says whose act it was. (REV3 corrected "every mutation is an
/// edit to `driver.json`", written when that was true.)
/// </summary>
/// <remarks>
/// <b>A handler class per domain, and a table built from their list</b> (MOD5, PART10d). Every route was a case of one
/// switch here, so every feature that added a driver door edited this file. A domain's routes live in its handler class,
/// named as the page's own bridge file is (`RulesRoutes.cs` beside `bridge/rules.ts`), each handler marked
/// <see cref="DriverRouteAttribute"/>; the class is one line in <see cref="Domains"/>, and <see cref="RouteAsync"/> looks a
/// request up in the table the marks make. Every domain has moved (PART10f), so this class holds the dispatch and nothing of a
/// domain's, and is no longer partial.
/// </remarks>
public sealed class DriverModule : ModuleBase
{
    private readonly DriverContext _context;

    /// <summary>
    /// The folder holding the install's offers — Daoris's own example plugins, none installed until a press
    /// (PLUG9 d, D103). Null finds them beside the running application, else beside the home; a test names
    /// its own, since every test's home shares one parent. Held by the context, which Plugins reads at each request.
    /// </summary>
    public string? Offers
    {
        get => _context.Offers;
        init => _context.Offers = value;
    }

    /// <summary>
    /// The world a set-up press reads (LAYOUT8): null is the real one; a test names a stand-in, since the real one starts the
    /// doctrine tool for its version. Held by the context, which the set-up and help routes read at each request.
    /// </summary>
    public Func<ServiceClient, ISetupWorld>? SetupWorlds
    {
        get => _context.SetupWorlds;
        init => _context.SetupWorlds = value;
    }

    /// <summary>
    /// What This Daoris's read is handed, changed (WIRING1b): null is the window's own; a test names the account's and the
    /// machine's variables and sizes of its own. Held by the context, which the system domain reads at each request.
    /// </summary>
    public Func<WiringWorld, WiringWorld>? WiringWorlds
    {
        get => _context.WiringWorlds;
        init => _context.WiringWorlds = value;
    }

    // Each domain's handler, built over the context, in the order of Domains: a route's entry names its domain's place.
    private readonly object[] _handlers;

    // The reads under way that an identical ask joins (SWEEPFLIGHT1), forgotten as a press answers.
    private readonly RouteFlights _flights = new();

    /// <remarks>
    /// The bus is held as well as handed to the base: this module both ANSWERS requests and, since
    /// D49 §3, raises one of its own — a conversation ending is news the page wants without asking.
    /// </remarks>
    /// <param name="openFolder">
    /// The file manager, for a plugin's Open folder (PLUGUI1e): the application hands in the window kit's launcher,
    /// as it does to the log's module. Null where the host carries none: then nothing opens, and the answer says so.
    /// </param>
    /// <param name="pickFile">
    /// The system's file picker, for a tool's *Browse…* (TOOLS7): the application hands in its window's dialog. Null
    /// where the host carries none, and the answer says so.
    /// </param>
    public DriverModule(IEventBus events, DriverLoop loop, OpenFolder? openFolder = null, PickFile? pickFile = null)
        : base(events: events)
    {
        _context = new DriverContext(events, loop, openFolder, pickFile);
        _handlers = [.. Domains.Select(domain => domain.Build(_context))];
        // Ask Daoris's room as START_HELP writes it, for a conversation going on in it (ASKHIST1). Since PARALLEL1a from the facts the
        // judges keep on the loop: their judges registered here, and the room written again as the facts it said not read yet land.
        HelpRoutes.KeepFacts(_context);
        loop.DescribeHelp = cancellationToken => HelpRoutes.HelpMachineNowAsync(_context, cancellationToken);
        // A change told from elsewhere (the terminal's press, the loop's own) overtakes the reads under way as this module's
        // presses do (SWEEPFLIGHT1). The bus does not wait for its subscribers, so this is the backstop; a press made here
        // forgets them before it answers.
        _ = events.Subscribe(StatePublisher.Module, StatePublisher.Type, _ =>
        {
            _flights.Forget();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// The doors Ask Daoris's Apply goes through (HELP6): each the code a screen's own route runs. The module's one seam to
    /// them, since the tests build doors through a module (<c>DriverModuleBridge.Module()</c>) rather than a context.
    /// </summary>
    public IHelpDoors HelpDoors(ServiceClient? service) => HelpRoutes.Doors(_context, service);

    /// <summary>The module's name on the bridge, which the page's calls and <see cref="Declared"/> read.</summary>
    public const string Name = "DAORIS.DRIVER";

    public override string ModuleName => Name;

    /// <summary>
    /// Route, and let the driver's own refusals reach the person.
    /// </summary>
    /// <remarks>
    /// <b>An unhandled exception becomes a generic `UNKNOWN_ERROR` carrying only its TYPE</b>, so every
    /// sentence `DriverException` was written to deliver — "unknown adapter 'x' — one of: …", "that
    /// harness declares no installer" — used to be dropped on the floor and shown as a bare failure.
    /// Mapped once, here, rather than at each throw site: the driver library is where those sentences
    /// live, and a per-site wrapping is a list somebody eventually forgets to append to.
    /// </remarks>
    protected override async Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await RouteAsync(request, cancellationToken);
        }
        catch (DriverException error)
        {
            throw Refusals.Because(Refusals.DriverRefused, error.Message, ("message", error.Message));
        }
    }

    /// <summary>
    /// The route's handler from the table, or the framework's own answer for a route nobody declared.
    /// </summary>
    /// <remarks>
    /// <para>A route that is not in the table answers <c>NO_ROUTE</c> through <c>UnknownType</c>, exactly as the
    /// switch's default did. The table is case-sensitive, as the switch's labels were (MOD5).</para>
    /// <para><b>A read marked <see cref="OneFlightAttribute"/></b> joins the identical ask under way (SWEEPFLIGHT1): the same
    /// route with the same payload, answered to every caller that joined it (<see cref="RouteFlights"/>).</para>
    /// <para><b>A press</b> (one whose route declares a change) forgets every read under way once it has answered or thrown,
    /// before the publisher tells the windows, so the page's ask again after it never joins a look begun before it.</para>
    /// </remarks>
    private Task<object?> RouteAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        if (request.Type is not { } type || !Table.TryGetValue(type, out var route)) throw UnknownType(request);
        var handler = _handlers[route.Domain];
        if (route.OneFlight)
        {
            return _flights.JoinAsync(FlightOf(type, request), token => route.Answer(handler, request, token), cancellationToken);
        }

        return route.Change is null ? route.Answer(handler, request, cancellationToken) : PressAsync(route, handler, request, cancellationToken);
    }

    private async Task<object?> PressAsync(Handler route, object handler, IpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // No ConfigureAwait: the kit's pipeline keeps the caller's context, as the publisher's says.
            return await route.Answer(handler, request, cancellationToken);
        }
        finally
        {
            _flights.Forget();
        }
    }

    /// <summary>What makes two asks identical: the route and its payload as the page sent it.</summary>
    private static string FlightOf(string type, IpcRequest request) =>
        request.Payload is { ValueKind: not System.Text.Json.JsonValueKind.Undefined } payload ? $"{type}\n{payload.GetRawText()}" : type;

    /// <summary>Every route this module answers, by name: what the page's bridge may call (MOD5).</summary>
    public static IReadOnlyCollection<string> Routes => Table.Keys;

    /// <summary>The reads whose identical asks share the one under way (SWEEPFLIGHT1), by name.</summary>
    public static IReadOnlyCollection<string> OneFlightRoutes => [.. Table.Where(entry => entry.Value.OneFlight).Select(entry => entry.Key)];

    /// <summary>
    /// What a request's route declares a successful answer changed (STATEHUB1b, D169 point 2), read off the
    /// <see cref="PublishesAttribute"/> beside its mark: null for a read, a route nobody declared, or a request to another
    /// module. <see cref="StatePublisher"/> publishes it.
    /// </summary>
    public static StateChange? Declared(IpcRequest request) =>
        request.Module == Name && request.Type is { } type && Table.TryGetValue(type, out var route) ? route.Change : null;

    /// <summary>
    /// The domains this module answers for, one line each, in alphabetical order (PART10d, D166): a domain's handler class and
    /// how it is built over the context, each taking only what it uses: the loop, or the <see cref="DriverContext"/> where it
    /// uses more. A new domain is its <c>&lt;Domain&gt;Routes.cs</c> and its line here, and a new door in a domain is a marked
    /// handler in its class: nothing is found by scanning.
    /// </summary>
    private static readonly Domain[] Domains =
    [
        Domain.Of(context => new AccountsRoutes(context.Loop)),
        Domain.Of(context => new AcrossRoutes(context)),
        Domain.Of(context => new AgentsRoutes(context)),
        Domain.Of(context => new AsksRoutes(context.Loop)),
        Domain.Of(context => new ConsoleRoutes(context.Loop)),
        Domain.Of(context => new ConversationRoutes(context)),
        Domain.Of(context => new DriverRoutes(context)),
        Domain.Of(context => new EntriesRoutes(context)),
        Domain.Of(context => new GitRoutes(context.Loop)),
        Domain.Of(context => new HelpRoutes(context)),
        Domain.Of(context => new HistoryRoutes(context.Loop)),
        Domain.Of(context => new LinesRoutes(context)),
        Domain.Of(context => new PluginsRoutes(context)),
        Domain.Of(context => new RegistryRoutes(context.Loop)),
        Domain.Of(context => new RulesRoutes(context.Loop)),
        Domain.Of(context => new SessionsRoutes(context)),
        Domain.Of(context => new SetupRoutes(context)),
        Domain.Of(context => new SystemRoutes(context)),
        Domain.Of(context => new ToolsRoutes(context)),
        Domain.Of(context => new TraceRoutes(context.Loop)),
        Domain.Of(context => new TreesRoutes(context.Loop)),
        Domain.Of(context => new WorkRoutes(context)),
        Domain.Of(context => new WorkflowRoutes(context.Loop)),
    ];

    // The handlers, read once off their marks. A route marked twice fails here, at the module's first use, as a switch with
    // two equal labels would not have compiled; a handler of neither shape fails here too. Declared after Domains, which it reads.
    private static readonly FrozenDictionary<string, Handler> Table = ReadTable();

    /// <summary>The table the domains' marks make: each route, the place of its domain in <see cref="Domains"/>, and its handler.</summary>
    private static FrozenDictionary<string, Handler> ReadTable()
    {
        var table = new Dictionary<string, Handler>(StringComparer.Ordinal);
        for (var place = 0; place < Domains.Length; place++)
        {
            foreach (var (route, answer, change, oneFlight) in Domains[place].Routes) table.Add(route, new Handler(place, answer, change, oneFlight));
        }

        return table.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// A route's entry in the table: its domain's place in <see cref="Domains"/>, the handler over that domain's instance, what
    /// it declares a successful answer changed (D169), null for a read, and whether its identical asks share one under way.
    /// </summary>
    private readonly record struct Handler(
        int Domain, Func<object, IpcRequest, CancellationToken, Task<object?>> Answer, StateChange? Change, bool OneFlight);

    /// <summary>One line of <see cref="Domains"/>: how a domain's handler class is built over the context, and the routes that class marks.</summary>
    private sealed class Domain(
        Func<DriverContext, object> build,
        IReadOnlyList<(string Route, Func<object, IpcRequest, CancellationToken, Task<object?>> Answer, StateChange? Change, bool OneFlight)> routes)
    {
        public IReadOnlyList<(string Route, Func<object, IpcRequest, CancellationToken, Task<object?>> Answer, StateChange? Change, bool OneFlight)> Routes => routes;

        public object Build(DriverContext context) => build(context);

        /// <summary>
        /// A domain whose handler class is <typeparamref name="T"/>: each <see cref="DriverRouteAttribute"/> on one of its
        /// non-public instance methods, by its route, with what its <see cref="PublishesAttribute"/> beside it declares and
        /// whether its <see cref="OneFlightAttribute"/> shares it. A handler answers at once, <c>object? M(IpcRequest)</c>, or
        /// later, <c>Task&lt;object?&gt; M(IpcRequest, CancellationToken)</c>.
        /// </summary>
        public static Domain Of<T>(Func<DriverContext, T> build) where T : class
        {
            var routes = new List<(string, Func<object, IpcRequest, CancellationToken, Task<object?>>, StateChange?, bool)>();
            foreach (var method in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var marks = method.GetCustomAttributes<DriverRouteAttribute>().ToList();
                var publishes = method.GetCustomAttributes<PublishesAttribute>().ToDictionary(each => each.Route, StringComparer.Ordinal);
                var shared = method.GetCustomAttributes<OneFlightAttribute>().Select(each => each.Route).ToHashSet(StringComparer.Ordinal);
                // A declaration for a route this method does not mark is a typo that would publish nothing: refused here, at the
                // module's first use, as a route marked twice is.
                if (publishes.Keys.Concat(shared).FirstOrDefault(route => marks.All(mark => mark.Type != route)) is { } stray)
                {
                    throw new InvalidOperationException($"{typeof(T).Name}.{method.Name} declares `{stray}`, and marks no such route.");
                }

                // Two presses are two acts, each the person's: only a read is ever shared (SWEEPFLIGHT1).
                if (shared.FirstOrDefault(publishes.ContainsKey) is { } press)
                {
                    throw new InvalidOperationException($"{typeof(T).Name}.{method.Name} shares `{press}`, a press: only a read is shared.");
                }

                foreach (var route in marks)
                {
                    routes.Add((route.Type, method.GetParameters().Length == 1
                        ? Now(method.CreateDelegate<Func<T, IpcRequest, object?>>())
                        : Later(method.CreateDelegate<Func<T, IpcRequest, CancellationToken, Task<object?>>>()),
                        publishes.TryGetValue(route.Type, out var declared) ? new StateChange(declared.Changes, declared.Session, declared.Subject) : null,
                        shared.Contains(route.Type)));
                }
            }

            return new Domain(build, routes);

            // A handler that answers at once throws at once, which the router awaits the same way as a fault.
            static Func<object, IpcRequest, CancellationToken, Task<object?>> Now(Func<T, IpcRequest, object?> answer) =>
                (handler, request, _) => Task.FromResult(answer((T)handler, request));

            static Func<object, IpcRequest, CancellationToken, Task<object?>> Later(
                Func<T, IpcRequest, CancellationToken, Task<object?>> answer) =>
                (handler, request, cancellationToken) => answer((T)handler, request, cancellationToken);
        }
    }
}

/// <summary>
/// Marks a method of a domain's handler class as the handler of one of <see cref="DriverModule"/>'s routes (MOD5, PART10d).
/// A route is added in its domain's class by marking its handler, so no central switch is edited per route.
/// </summary>
/// <param name="type">The route's name, as the page's bridge sends it.</param>
/// <remarks>A route that changes the driver's state says so beside its mark, with <see cref="PublishesAttribute"/>.</remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class DriverRouteAttribute(string type) : Attribute
{
    public string Type { get; } = type;
}

/// <summary>
/// What one of a handler's routes changes when it answers successfully (STATEHUB1b, D169 point 2), declared beside its
/// <see cref="DriverRouteAttribute"/>: <see cref="StatePublisher"/> publishes it, so no handler emits its own. A read declares
/// nothing. Named by its route, since a handler may answer a read and a press (a plan and the press it plans), and a route the
/// method does not mark fails the module's first use.
/// </summary>
/// <remarks>
/// Its own mark rather than a property of the route's, and on one line: the index reads a route's mark as written
/// (`tools/orient-index.mjs`), and skips the marks under it to find the handler.
/// </remarks>
/// <param name="route">The route it declares for, one this method marks.</param>
/// <param name="changes">What a successful answer changed, as the page's query families.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class PublishesAttribute(string route, StateScopes changes) : Attribute
{
    public string Route { get; } = route;

    public StateScopes Changes { get; } = changes;

    /// <summary>The payload's field naming the session the change was about, where it is one session's.</summary>
    public string? Session { get; set; }

    /// <summary>The payload's field naming the entry or ask the press is for, where it is no one session's (ACCEPTALL1e).</summary>
    public string? Subject { get; set; }
}

/// <summary>
/// One of a handler's reads whose identical asks share the one under way (SWEEPFLIGHT1): an ask of the same route with the same
/// payload, made while one runs, is answered what that one answers rather than starting another (<see cref="RouteFlights"/>).
/// For a read whose answer is a fan-out over every checkout, which the page asks again at each change it hears and each window
/// asks for itself. A press is never shared, refused at the module's first use; named by its route, as
/// <see cref="PublishesAttribute"/> is, since a handler may answer a read and the press it plans.
/// </summary>
/// <param name="route">The read it shares, one this method marks.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class OneFlightAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}
