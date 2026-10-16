using System.Collections.Frozen;

namespace Daoris.Desktop;

/// <summary>How the pipe treats a route (RPC1a, D171 point 3, the terminal RPC design §4.2). A wire's words, added to and never renamed.</summary>
public enum PipeClass
{
    /// <summary>A read, or a press D156 §3 leaves to an agent or to the driver: run as asked.</summary>
    Open,

    /// <summary>A press D156 §3 makes the person's alone: refused <c>confirm-in-window</c> until RPC1c confirms it in the window.</summary>
    Person,

    /// <summary>What only a window does (its own commands, the pickers, the file manager, Daoris's browser, its shells): refused.</summary>
    Window,
}

/// <summary>A route the census classes, as <c>ROUTES</c> answers it: a module classed whole names no route.</summary>
public sealed record PipeRoute(string Module, string? Route, PipeClass Class);

/// <summary>
/// The pipe's census (RPC1a, the terminal RPC design §4.2): every route the running Daoris accepts over its pipe, classed
/// <see cref="PipeClass.Open"/>, <see cref="PipeClass.Person"/> or <see cref="PipeClass.Window"/>. The same account opens the
/// pipe as runs <c>daoris-driver</c> (D156 point 4), so the pipe confers no person's authority, and only an open route runs.
/// </summary>
/// <remarks>
/// <para><b>The driver's routes are classed one by one</b>, against D156 §3's table of the local host's doors, and a test fails on
/// a route of <see cref="DriverModule"/> this does not name, or a name that is no route (<c>PipeCensusTests</c>): a route added
/// later is classed in the change that adds it (<c>bridge-presses.md</c>). A route a route-by-route module does not name is a press
/// and the person's; a module the census does not name (the kit's own among them) is refused.</para>
///
/// <para><b>The calls made here, where D156 §3 names no door.</b> A landing is the person's yes; a discard, a sweep, an archive and
/// a delete are their deletes and closes; a stop is a close; words said to a session, a conversation or a channel are theirs; a
/// standing choice (every <c>SET_*</c>, a trust, an account, a tool, a plugin, a rule's answer) is their re-wiring, since one
/// direction of each widens what an agent may do, and a session that set one for itself would be reporting on itself. The
/// driver's own are its looks and syncs: a nudge, a sync, a refresh, a second opinion started, the trees brought up to date.</para>
///
/// <para>Modules are matched as the kit's dispatcher matches them, ignoring case; a route as its module's table does.</para>
/// </remarks>
public sealed class PipeCensus
{
    private readonly FrozenDictionary<string, PipeClass> _whole;
    private readonly FrozenDictionary<string, FrozenDictionary<string, PipeClass>> _byRoute;

    private PipeCensus(IDictionary<string, PipeClass> whole, IDictionary<string, IDictionary<string, PipeClass>> byRoute)
    {
        _whole = whole.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _byRoute = byRoute.ToFrozenDictionary(
            each => each.Key, each => each.Value.ToFrozenDictionary(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The desktop's census.</summary>
    public static PipeCensus Daoris { get; } = new(
        new Dictionary<string, PipeClass>
        {
            // A window's own: its frame and its windows, the person's shells, the page's report into the log and Settings → Logs,
            // the update's banner, and Daoris's browser (the design §4.2 names the browser module).
            ["DAORIS.BROWSER"] = PipeClass.Window,
            ["DAORIS.LOG"] = PipeClass.Window,
            ["DAORIS.TERMINAL"] = PipeClass.Window,
            ["DAORIS.UPDATE"] = PipeClass.Window,
            ["DAORIS.WINDOWS"] = PipeClass.Window,
        },
        new Dictionary<string, IDictionary<string, PipeClass>>
        {
            [DriverModule.Name] = Driver(),
            // The folder picker is a window's; a declaration written into a repository is the person's set-up (D156 §3).
            ["DAORIS.REGISTRY"] = new Dictionary<string, PipeClass>
            {
                ["PICK_FOLDER"] = PipeClass.Window,
                ["WRITE_DECLARATION"] = PipeClass.Person,
            },
            // The machine's wiring: a read is open, a wiring and an unwiring are the person's (D48 §5, D156 §3's re-wiring).
            ["DAORIS.REMOTES"] = new Dictionary<string, PipeClass>
            {
                ["STATE"] = PipeClass.Open,
                ["SET"] = PipeClass.Person,
                ["REMOVE"] = PipeClass.Person,
            },
        });

    /// <summary>
    /// How the pipe treats <paramref name="module"/>'s <paramref name="route"/>: null where the census names no such module, which the
    /// pipe refuses; the person's where a route-by-route module does not name the route, since an unclassed press is theirs.
    /// </summary>
    public PipeClass? Of(string? module, string? route)
    {
        if (module is null) return null;
        if (_whole.TryGetValue(module, out var whole)) return whole;
        if (!_byRoute.TryGetValue(module, out var routes)) return null;
        return route is not null && routes.TryGetValue(route, out var classed) ? classed : PipeClass.Person;
    }

    /// <summary>Whether the census names <paramref name="module"/>, whole or route by route.</summary>
    public bool Names(string module) => _whole.ContainsKey(module) || _byRoute.ContainsKey(module);

    /// <summary>Every route the census classes, as <c>ROUTES</c> answers them: the modules classed whole, then each route, in name order.</summary>
    public IReadOnlyList<PipeRoute> Routes =>
    [
        .. _whole.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => new PipeRoute(each.Key, null, each.Value)),
        .. _byRoute.OrderBy(each => each.Key, StringComparer.Ordinal)
            .SelectMany(module => module.Value.OrderBy(each => each.Key, StringComparer.Ordinal)
                .Select(route => new PipeRoute(module.Key, route.Key, route.Value))),
    ];

    /// <summary>The routes of <paramref name="module"/> the census does not class: what its test fails on.</summary>
    public IReadOnlyList<string> Unclassed(string module, IEnumerable<string> routes) =>
        _byRoute.TryGetValue(module, out var classed)
            ? [.. routes.Where(route => !classed.ContainsKey(route)).Order(StringComparer.Ordinal)]
            : [.. routes.Order(StringComparer.Ordinal)];

    /// <summary>What the census classes for <paramref name="module"/> that is none of its <paramref name="routes"/>: a name left behind.</summary>
    public IReadOnlyList<string> Stale(string module, IEnumerable<string> routes)
    {
        var known = routes.ToHashSet(StringComparer.Ordinal);
        return _byRoute.TryGetValue(module, out var classed)
            ? [.. classed.Keys.Where(route => !known.Contains(route)).Order(StringComparer.Ordinal)]
            : [];
    }

    /// <summary>This census with <paramref name="module"/>'s <paramref name="route"/> classed <paramref name="class"/> beside the rest: a test's.</summary>
    public PipeCensus With(string module, string route, PipeClass @class)
    {
        var byRoute = _byRoute.ToDictionary(
            each => each.Key, each => (IDictionary<string, PipeClass>)new Dictionary<string, PipeClass>(each.Value, StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);
        if (!byRoute.TryGetValue(module, out var routes)) byRoute[module] = routes = new Dictionary<string, PipeClass>(StringComparer.Ordinal);
        routes[route] = @class;
        return new PipeCensus(new Dictionary<string, PipeClass>(_whole, StringComparer.OrdinalIgnoreCase), byRoute);
    }

    /// <summary>
    /// The driver's routes (<see cref="DriverModule.Routes"/>), each classed: <c>PipeCensusTests</c> holds this table to the module's,
    /// both ways.
    /// </summary>
    private static Dictionary<string, PipeClass> Driver()
    {
        var table = new Dictionary<string, PipeClass>(StringComparer.Ordinal);
        Add(PipeClass.Open,
            // Reads (D156 §3's reads stay open): they change nothing and declare nothing (D169 point 2).
            "ACCOUNTS", "ACROSS", "ASKS_ELSEWHERE", "ASK_LANDING", "CHANNEL_READ", "CHECKOUT_BRANCH", "ENTRY_LANDING",
            "GIT_BLAME", "GIT_BRANCHES", "GIT_COMMIT", "GIT_COMPARE", "GIT_GRAPH", "GIT_HISTORY", "HANDOFF_PLAN", "HARNESSES",
            "HELPER_WAITS", "HELP_CONVERSATIONS", "HELP_PROPOSALS", "HISTORY_PLAN", "LANDING", "LINES", "OPINION_GATE",
            "OPINION_WAITS", "PLUGIN", "PLUGINS", "PLUGIN_ACTIVITY", "PLUGIN_READ", "RULES", "SESSION_DIFF", "SESSION_FILE",
            "SESSION_FILES", "SESSION_GROUPS", "SESSION_HELPERS", "SESSION_HISTORY", "SESSION_OPENINGS", "SESSION_OPTIONS",
            "SESSION_PROCESS", "SESSION_QUEUE", "SESSION_SEARCH", "SESSION_STREAMS", "SESSION_WHERE", "SETUP_PLAN", "STARTS",
            "STATE", "SWEEP_PLAN", "TAIL_SESSION", "TOOLS_GIT", "TOOLS_LIST", "TRACE", "TREES_SYNC_PLAN", "TREES_SYNC_SCOPE",
            "UNDER_WAY", "USAGE", "WORKFLOW_CURRENT", "WORKFLOW_RUN", "WORK_PLAN",
            // The driver's own (D156 §3: sync, refresh, a process it starts on the person's press): a look now, a sync pass,
            // the registry and the roster read again, the tools' locations looked at, a second opinion started, and Daoris's
            // own branches brought up to date with their lines.
            "NUDGE", "SYNC_NOW", "REGISTRY_REFRESH", "HARNESSES_REFRESH", "TOOLS_LOOK", "ASK_OPINION", "TREES_SYNC");
        Add(PipeClass.Person,
            // The yes, and what answers for the person's verdict: a landing, a hand-off, an opinion overridden or taken on.
            "LAND_SESSION_TREE", "LAND_ENTRY", "LAND_ASK", "HANDOFF", "OPINION_ANYWAY", "OPINION_MYSELF",
            // Deletes, clears and closes: a discard, a sweep, an archive, a delete, a stop, an end, an abandon.
            "DISCARD_SESSION_TREE", "DISCARD_SESSION_BRANCH", "SWEEP", "HISTORY_CLEAR", "SESSION_ARCHIVE", "SESSION_DELETE",
            "STOP_SESSION", "STOP_OPINION", "STOP_TASK", "CANCEL_TURN", "END_CHAT", "WORK_ABANDON", "WORK_PAUSE", "WORK_RESUME",
            "HELP_DISMISS", "CHANNEL_MARK",
            // A go-ahead's answer, a parked session's yes or no, a helper's, a quest retried after its strikes.
            "SESSION_GO_AHEAD", "RESOLVE_SESSION", "SESSION_HELPER_APPROVE", "SESSION_HELPER_DECLINE", "SESSION_HELPER_ASK",
            "RETRY_QUEST", "HELP_APPLY",
            // The person's words, and a conversation begun or carried on with them.
            "SESSION_INPUT", "SESSION_QUEUE_EDIT", "CHANNEL_POST", "START_CHAT", "START_HELP", "HELP_START_FROM",
            "SESSION_GO_ON_NEW", "SESSION_START_FROM", "SESSION_HAND_OVER", "HELP_RENAME", "HELP_PIN",
            // Their set-up and their standing choices: what a review is set to, how work lands, what agents may reach.
            "SETUP_PRESS", "SET_AGENT_SETTINGS", "SET_COMMITS", "SET_COOLOFF", "SET_DRIVABLE", "SET_HELPER", "SET_HELPERS",
            "SET_HOLD", "SET_INTAKE", "SET_LANDING", "SET_LANGUAGE", "SET_LINE", "SET_NOTIFY", "SET_OPINION", "SET_READ_ACROSS",
            "SET_REVIEW", "SET_SESSION_OPTION", "SET_STANDING", "SET_STRIKES", "SET_TREES", "SET_WRITE_ACROSS", "TRUST_FOLDER",
            "RULE_ACTION", "RULE_PROPOSAL", "ACCOUNT_USE", "HARNESS_ACTION",
            // Code that runs in the driver, and the programs it runs with.
            "PLUGIN_ACTION", "PLUGIN_ADD", "PLUGIN_INSTALL", "PLUGIN_NEW", "PLUGIN_TRY", "PLUGIN_UPDATE",
            "TOOLS_DELETE", "TOOLS_DOWNLOAD", "TOOLS_LOCATION", "TOOLS_STOP", "TOOLS_USE");
        Add(PipeClass.Window,
            // The file manager, the file picker and Daoris's browser are the window's; so is what is typed into an action a
            // window runs, and that action's stop.
            "SESSION_OPEN_FOLDER", "PLUGIN_OPEN_FOLDER", "TOOLS_PICK", "SHOW_REVIEW_AGAIN", "HARNESS_INPUT", "HARNESS_CANCEL");
        return table;

        void Add(PipeClass @class, params string[] routes)
        {
            // A route classed twice is refused here, at first use, as a route marked twice is.
            foreach (var route in routes) table.Add(route, @class);
        }
    }
}
