using System.Collections.Frozen;
using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>How the pipe treats a route (RPC1a, D171 point 3, the terminal RPC design §4.2). A wire's words, added to and never renamed.</summary>
public enum PipeClass
{
    /// <summary>A read, or a press D156 §3 leaves to an agent or to the driver: run as asked.</summary>
    Open,

    /// <summary>A press D156 §3 makes the person's alone: shown to the window, and run only on the person's Confirm there (RPC1c).</summary>
    Person,

    /// <summary>What only a window does (its own commands, the pickers, the file manager, Daoris's browser, its shells): refused.</summary>
    Window,
}

/// <summary>
/// A route that is a look in one form and a press in another, classed by one field of its payload: <see cref="Class"/> where the
/// field holds one of <see cref="Values"/>, <see cref="Otherwise"/> where it holds another or is absent.
/// </summary>
/// <remarks>
/// Every property whose name is <see cref="Field"/> ignoring case is read, and the strictest class any of them gives is the
/// route's, so neither another casing nor a name said twice opens what the handler would read as the press. A value is compared
/// as the handler reads it: a string by its text, whatever its escapes; <c>true</c>, <c>false</c> and <c>null</c> by kind.
/// </remarks>
/// <param name="Field">The payload's field.</param>
/// <param name="Values">The field's values that make the form <see cref="Class"/>.</param>
/// <param name="Class">The class of the form those values name.</param>
/// <param name="Otherwise">The class of every other form, an absent field and no payload included.</param>
public sealed record PipeRule(string Field, IReadOnlyList<JsonElement> Values, PipeClass Class, PipeClass Otherwise)
{
    /// <summary>A rule over string or boolean values, written as JSON (<c>"system"</c>, <c>true</c>).</summary>
    public static PipeRule Of(string field, PipeClass @class, PipeClass otherwise, params string[] values) =>
        new(field, [.. values.Select(value => JsonDocument.Parse(value).RootElement.Clone())], @class, otherwise);

    /// <summary>The class <paramref name="payload"/>'s form takes.</summary>
    public PipeClass For(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } named) return Otherwise;
        var classes = named.EnumerateObject()
            .Where(property => string.Equals(property.Name, Field, StringComparison.OrdinalIgnoreCase))
            .Select(property => Values.Any(value => Same(value, property.Value)) ? Class : Otherwise)
            .ToList();
        if (classes.Count == 0) return Otherwise;
        return classes.FirstOrDefault(each => each != PipeClass.Open, PipeClass.Open);
    }

    private static bool Same(JsonElement expected, JsonElement actual) =>
        expected.ValueKind == actual.ValueKind
        && (expected.ValueKind != JsonValueKind.String || expected.GetString() == actual.GetString())
        && (expected.ValueKind is JsonValueKind.String or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null
            || expected.GetRawText() == actual.GetRawText());
}

/// <summary>
/// A route the census classes, as <c>ROUTES</c> answers it: a module classed whole names no route, and a route classed by its
/// payload says the rule (<see cref="When"/>) beside the class every other form takes.
/// </summary>
public sealed record PipeRoute(string Module, string? Route, PipeClass Class, PipeRule? When = null);

/// <summary>
/// The pipe's census (RPC1a, the terminal RPC design §4.2): every route the running Daoris accepts over its pipe, classed
/// <see cref="PipeClass.Open"/>, <see cref="PipeClass.Person"/> or <see cref="PipeClass.Window"/>. The same account opens the
/// pipe as runs <c>daoris-driver</c> (D156 point 4), so the pipe confers no person's authority: only an open route runs as asked,
/// and a person's runs once the person confirms it in the window (RPC1c, <see cref="PipeConfirmations"/>).
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
/// driver's own are its looks: a nudge, a sync with the service, the registry and the accounts' use read again, the tools'
/// locations looked at. A press that moves the person's lines or branches, starts work on an account, or lets a refused account
/// through is theirs, though the driver makes it (a second agent's review of RPC1a).</para>
///
/// <para><b>One route, two forms</b>: where a route answers a look and a press by its payload, it is classed by a
/// <see cref="PipeRule"/>, and the look alone runs.</para>
///
/// <para>Modules are matched as the kit's dispatcher matches them, ignoring case; a route as its module's table does.</para>
/// </remarks>
public sealed class PipeCensus
{
    private readonly FrozenDictionary<string, PipeClass> _whole;
    private readonly FrozenDictionary<string, FrozenDictionary<string, PipeRule>> _byRoute;

    private PipeCensus(IDictionary<string, PipeClass> whole, IDictionary<string, IDictionary<string, PipeRule>> byRoute)
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
        new Dictionary<string, IDictionary<string, PipeRule>>
        {
            [DriverModule.Name] = Driver(),
            // The folder picker is a window's; a declaration written into a repository is the person's set-up (D156 §3).
            ["DAORIS.REGISTRY"] = Classed(("PICK_FOLDER", PipeClass.Window), ("WRITE_DECLARATION", PipeClass.Person)),
            // The machine's wiring: a read is open, a wiring and an unwiring are the person's (D48 §5, D156 §3's re-wiring).
            ["DAORIS.REMOTES"] = Classed(("STATE", PipeClass.Open), ("SET", PipeClass.Person), ("REMOVE", PipeClass.Person)),
        });

    /// <summary>
    /// How the pipe treats <paramref name="module"/>'s <paramref name="route"/> sent with <paramref name="payload"/>: null where the
    /// census names no such module, which the pipe refuses; the person's where a route-by-route module does not name the route,
    /// since an unclassed press is theirs.
    /// </summary>
    public PipeClass? Of(string? module, string? route, JsonElement? payload = null)
    {
        if (module is null) return null;
        if (_whole.TryGetValue(module, out var whole)) return whole;
        if (!_byRoute.TryGetValue(module, out var routes)) return null;
        return route is not null && routes.TryGetValue(route, out var classed) ? classed.For(payload) : PipeClass.Person;
    }

    /// <summary>Whether the census names <paramref name="module"/>, whole or route by route.</summary>
    public bool Names(string module) => _whole.ContainsKey(module) || _byRoute.ContainsKey(module);

    /// <summary>Every route the census classes, as <c>ROUTES</c> answers them: the modules classed whole, then each route, in name order.</summary>
    public IReadOnlyList<PipeRoute> Routes =>
    [
        .. _whole.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => new PipeRoute(each.Key, null, each.Value)),
        .. _byRoute.OrderBy(each => each.Key, StringComparer.Ordinal)
            .SelectMany(module => module.Value.OrderBy(each => each.Key, StringComparer.Ordinal)
                .Select(route => new PipeRoute(module.Key, route.Key, route.Value.Otherwise, route.Value.Values.Count > 0 ? route.Value : null))),
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
            each => each.Key, each => (IDictionary<string, PipeRule>)new Dictionary<string, PipeRule>(each.Value, StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);
        if (!byRoute.TryGetValue(module, out var routes)) byRoute[module] = routes = new Dictionary<string, PipeRule>(StringComparer.Ordinal);
        routes[route] = Whole(@class);
        return new PipeCensus(new Dictionary<string, PipeClass>(_whole, StringComparer.OrdinalIgnoreCase), byRoute);
    }

    /// <summary>A route of one class whatever its payload: a rule naming no value.</summary>
    private static PipeRule Whole(PipeClass @class) => new("", [], @class, @class);

    private static Dictionary<string, PipeRule> Classed(params (string Route, PipeClass Class)[] routes) =>
        routes.ToDictionary(each => each.Route, each => Whole(each.Class), StringComparer.Ordinal);

    /// <summary>
    /// The driver's routes (<see cref="DriverModule.Routes"/>), each classed: <c>PipeCensusTests</c> holds this table to the module's,
    /// both ways.
    /// </summary>
    private static Dictionary<string, PipeRule> Driver()
    {
        var table = new Dictionary<string, PipeRule>(StringComparer.Ordinal);
        Add(PipeClass.Open,
            // Reads (D156 §3's reads stay open): they change nothing and declare nothing (D169 point 2).
            "ACCOUNTS", "ACROSS", "ANSWER_WAITS", "ASKS_ELSEWHERE", "ASK_LANDING", "CHANNEL_READ", "CHECKOUT_BRANCH", "ENTRY_LANDING",
            "GIT_BLAME", "GIT_BRANCHES", "GIT_COMMIT", "GIT_COMPARE", "GIT_GRAPH", "GIT_HISTORY", "HANDOFF_PLAN", "HARNESSES",
            "HELPER_WAITS", "HELP_CONVERSATIONS", "HELP_PROPOSALS", "HELP_WIRED", "HEAVY_MARKS", "HEAVY_SLOT", "HISTORY_PLAN", "LANDING", "LINES", "OPINION_GATE",
            "OPINION_WAITS", "PERMISSION_WAITS", "PLUGIN", "PLUGINS", "PLUGIN_ACTIVITY", "PLUGIN_READ", "RULES", "SESSION_DIFF", "SESSION_FILE",
            "SESSION_FILES", "SESSION_GROUPS", "SESSION_HAND_OVER_ASK", "SESSION_HELPERS", "SESSION_HISTORY", "SESSION_OPENINGS", "SESSION_OPTIONS",
            "SESSION_PROCESS", "SESSION_QUEUE", "SESSION_SEARCH", "SESSION_SPENT", "SESSION_STREAMS", "SESSION_WHERE", "SETUP_PLAN", "STARTS",
            "STATE", "STATE_SLICE", "SWEEP_PLAN", "SYSTEM_DOWNLOADS", "SYSTEM_SPACE", "SYSTEM_WIRING", "TAIL_SESSION", "TOOLS_LIST", "TRACE", "TREES_SYNC_PLAN", "TREES_SYNC_SCOPE",
            "UNDER_WAY", "USAGE", "WORKFLOW_CURRENT", "WORKFLOW_RUN", "WORK_PLAN", "WORK_STATE",
            // The driver's own (D156 §3: sync, refresh): a look now, a sync pass with the service, the registry and the
            // accounts' use read again, the tools' locations looked at.
            "NUDGE", "SYNC_NOW", "REGISTRY_REFRESH", "ACCOUNTS_READ", "TOOLS_LOOK");
        Add(PipeClass.Person,
            // The yes, and what answers for the person's verdict: a landing, a hand-off, an opinion overridden or taken on.
            "LAND_SESSION_TREE", "LAND_ENTRY", "LAND_ASK", "HANDOFF", "OPINION_ANYWAY", "OPINION_MYSELF",
            // Deletes, clears and closes: a discard, a sweep, an archive, a delete, a stop, an end, an abandon.
            "DISCARD_SESSION_TREE", "DISCARD_SESSION_BRANCH", "RECOVERY_DROP", "SWEEP", "HISTORY_CLEAR", "SYSTEM_DOWNLOADS_CLEAR", "SESSION_ARCHIVE", "SESSION_DELETE",
            "STOP_SESSION", "STOP_OPINION", "STOP_TASK", "CANCEL_TURN", "END_CHAT", "WORK_ABANDON", "WORK_PAUSE", "WORK_RESUME",
            // FABRIC-PERFORM1g: the person's word on a plugin step's attempt held for them, which settles what nothing else can.
            "WORK_HELD",
            "HELP_DISMISS", "CHANNEL_MARK",
            // NOTIFY2f: the person's news read or dismissed, which a terminal marking would hide from them.
            "NOTICES_READ", "NOTICE_DISMISS",
            // A go-ahead's answer, a parked session's yes or no, a helper's, a quest retried after its strikes, and the answer to a
            // permission a session holds for the person (AUTONOMY1b3a), whose allow is the person's alone.
            "SESSION_GO_AHEAD", "RESOLVE_SESSION", "SESSION_HELPER_APPROVE", "SESSION_HELPER_DECLINE", "SESSION_HELPER_ASK", "SESSION_PERMISSION_ANSWER",
            // MIXAGENT1c: the yes and the no to a session's agent's own ask to hand it over.
            "SESSION_HAND_OVER_APPROVE", "SESSION_HAND_OVER_DECLINE",
            // COWORK1e: the person's word on a third answer alone held for them, which only they may let go on.
            "SESSION_ANSWER_ALONE",
            "RETRY_QUEST", "HELP_APPLY",
            // The person's words, and a conversation begun or carried on with them.
            "SESSION_INPUT", "SESSION_QUEUE_EDIT", "CHANNEL_POST", "CHANNEL_FORWARD", "START_CHAT", "START_HELP", "HELP_START_FROM",
            "SESSION_GO_ON_NEW", "SESSION_START_FROM", "SESSION_HAND_OVER", "HELP_RENAME", "HELP_PIN",
            // SESSCTX1c part B: *Go on with Daoris's brief*, the person's words carried into a new part of their session.
            "SESSION_GO_ON_BRIEF",
            // Their set-up and their standing choices: what a review is set to, how work lands, what agents may reach.
            "SETUP_PRESS", "SET_ACCESS", "SET_AGENT_SETTINGS", "SET_CAP", "SET_COMMITS", "SET_COOLOFF", "SET_DRIVABLE", "SET_HAND_OVER", "SET_HELPER", "SET_HELPERS", "SET_HELP_MODE",
            // HELPMODE1c2b: what Ask Daoris's housekeeping may do, the person's grant alone.
            "SET_HELP_GRANT",
            // FABRIC-PIPELINE1a: the landed route and the gates it runs unasked, accepted by their blob (D156).
            "WORKFLOW_LANDED",
            // HEAVYRUN1b2b: the commands this machine runs for a repository's heavy gates, which the person writes.
            "SET_HEAVY_GATES", "SET_HOLD", "SET_INTAKE", "SET_LANDING", "SET_LANGUAGE", "SET_LINE", "SET_NOTIFY", "SET_OPINION", "SET_READ_ACROSS",
            "SET_REVIEW", "SET_SESSION_OPTION",
            // FABRIC-PIPELINE1b: which checks a repository's sessions run, the person's standing choice; light lowers their part.
            "SET_SESSION_CHECKS", "SET_STANDING", "SET_STRIKES", "SET_TREES", "SET_WRITE_ACROSS", "TRUST_FOLDER",
            "RULE_ACTION", "RULE_PROPOSAL", "ACCOUNT_USE", "HARNESS_ACTION",
            // Code that runs in the driver, and the programs it runs with.
            "PLUGIN_ACTION", "PLUGIN_ADD", "PLUGIN_INSTALL", "PLUGIN_NEW", "PLUGIN_TRY",
            "TOOLS_DELETE", "TOOLS_DOWNLOAD", "TOOLS_LOCATION", "TOOLS_STOP", "TOOLS_USE",
            // Made by the driver, and still the person's (a second agent's review): bringing Daoris's branches up to date
            // fast-forwards the lines and replays and deletes branches, every repository's at once; a second opinion starts a
            // review on one of their accounts; Read again lets a refused account through and ends the tool's own cool-off.
            "TREES_SYNC", "ASK_OPINION", "HARNESSES_REFRESH");
        Add(PipeClass.Window,
            // The file manager, the file picker and Daoris's browser are the window's; so is what is typed into an action a
            // window runs, and that action's stop.
            "SESSION_OPEN_FOLDER", "PLUGIN_OPEN_FOLDER", "WORK_RECORDS_FOLDER", "TOOLS_PICK", "SHOW_REVIEW_AGAIN", "HARNESS_INPUT", "HARNESS_CANCEL",
            // RPC1c, PERSONDOOR1f: what waits for the person's Confirm is the window's to read and answer, so a terminal can ask and never
            // answer; and the person key is the window's, which the pipe never answers (D156 point 2, the terminal RPC design §4.3).
            "PERSON_CONFIRMATIONS", "PERSON_CONFIRM", "PERSON_KEY",
            // NOTIFY2j: and what waits at the service's door, told to the windows, read beside the pipe's.
            "SERVICE_CONFIRMATIONS",
            // CONFIRMSEEN1: that a card is in front of the person is the window's to say; a terminal saying it would start its two minutes.
            "PERSON_CONFIRM_SHOWN");
        // One route, two forms. What a switch of git would change compares programs Daoris already knows (the PATH's, a
        // managed version), and with `way: "file"` runs whatever file the payload names: that form is the person's.
        table.Add("TOOLS_GIT", PipeRule.Of("way", PipeClass.Open, PipeClass.Person, "\"system\"", "\"managed\""));
        // An update's preview reads what it would change; with `apply: true` it replaces the plugin.
        table.Add("PLUGIN_UPDATE", PipeRule.Of("apply", PipeClass.Person, PipeClass.Open, "true"));
        return table;

        void Add(PipeClass @class, params string[] routes)
        {
            // A route classed twice is refused here, at first use, as a route marked twice is.
            foreach (var route in routes) table.Add(route, Whole(@class));
        }
    }
}
