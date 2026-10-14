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

    // Each domain's handler, built over the context, in the order of Domains: a route's entry names its domain's place.
    private readonly object[] _handlers;

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
        // Ask Daoris's room as START_HELP writes it, for a conversation going on in it (ASKHIST1).
        loop.DescribeHelp = cancellationToken => HelpRoutes.HelpMachineNowAsync(_context, cancellationToken);
    }

    /// <summary>
    /// The doors Ask Daoris's Apply goes through (HELP6): each the code a screen's own route runs. The module's one seam to
    /// them, since the tests build doors through a module (<c>DriverModuleBridge.Module()</c>) rather than a context.
    /// </summary>
    public IHelpDoors HelpDoors(ServiceClient? service) => HelpRoutes.Doors(_context, service);

    public override string ModuleName => "DAORIS.DRIVER";

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
    /// A route that is not in the table answers <c>NO_ROUTE</c> through <c>UnknownType</c>, exactly as the
    /// switch's default did. The table is case-sensitive, as the switch's labels were (MOD5).
    /// </remarks>
    private Task<object?> RouteAsync(IpcRequest request, CancellationToken cancellationToken) =>
        request.Type is { } type && Table.TryGetValue(type, out var route)
            ? route.Answer(_handlers[route.Domain], request, cancellationToken)
            : throw UnknownType(request);

    /// <summary>Every route this module answers, by name: what the page's bridge may call (MOD5).</summary>
    public static IReadOnlyCollection<string> Routes => Table.Keys;

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
        Domain.Of(context => new GitRoutes(context.Loop)),
        Domain.Of(context => new HelpRoutes(context)),
        Domain.Of(context => new HistoryRoutes(context.Loop)),
        Domain.Of(context => new LinesRoutes(context)),
        Domain.Of(context => new PluginsRoutes(context)),
        Domain.Of(context => new RegistryRoutes(context.Loop)),
        Domain.Of(context => new RulesRoutes(context.Loop)),
        Domain.Of(context => new SessionsRoutes(context)),
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
            foreach (var (route, answer) in Domains[place].Routes) table.Add(route, new Handler(place, answer));
        }

        return table.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>A route's entry in the table: its domain's place in <see cref="Domains"/>, and the handler over that domain's instance.</summary>
    private readonly record struct Handler(int Domain, Func<object, IpcRequest, CancellationToken, Task<object?>> Answer);

    /// <summary>One line of <see cref="Domains"/>: how a domain's handler class is built over the context, and the routes that class marks.</summary>
    private sealed class Domain(
        Func<DriverContext, object> build,
        IReadOnlyList<(string Route, Func<object, IpcRequest, CancellationToken, Task<object?>> Answer)> routes)
    {
        public IReadOnlyList<(string Route, Func<object, IpcRequest, CancellationToken, Task<object?>> Answer)> Routes => routes;

        public object Build(DriverContext context) => build(context);

        /// <summary>
        /// A domain whose handler class is <typeparamref name="T"/>: each <see cref="DriverRouteAttribute"/> on one of its
        /// non-public instance methods, by its route. A handler answers at once, <c>object? M(IpcRequest)</c>, or later,
        /// <c>Task&lt;object?&gt; M(IpcRequest, CancellationToken)</c>.
        /// </summary>
        public static Domain Of<T>(Func<DriverContext, T> build) where T : class
        {
            var routes = new List<(string, Func<object, IpcRequest, CancellationToken, Task<object?>>)>();
            foreach (var method in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                foreach (var route in method.GetCustomAttributes<DriverRouteAttribute>())
                {
                    routes.Add((route.Type, method.GetParameters().Length == 1
                        ? Now(method.CreateDelegate<Func<T, IpcRequest, object?>>())
                        : Later(method.CreateDelegate<Func<T, IpcRequest, CancellationToken, Task<object?>>>())));
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
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class DriverRouteAttribute(string type) : Attribute
{
    public string Type { get; } = type;
}
