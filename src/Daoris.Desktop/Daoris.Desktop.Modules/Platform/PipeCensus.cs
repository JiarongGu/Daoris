using System.Collections.Frozen;
using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>How the pipe treats a route. A wire's words, added to and never renamed.</summary>
public enum PipeClass
{
    /// <summary>A read, or a press left to an agent or to the driver: run as asked.</summary>
    Open,

    /// <summary>A press that is the person's alone: shown to the window, and run only on the person's Confirm there.</summary>
    Person,

    /// <summary>What only a window does (its own commands, the pickers, the file manager, Daoris's browser, its shells): refused.</summary>
    Window,

    /// <summary>
    /// A role's act the owner appointed: judged by the standing confirmation's role method, under its caller look and the holder's
    /// proof; run with no card where covered, refused where not, never asked.
    /// </summary>
    Role,
}

/// <summary>
/// What an update's drain does with a person's or a role's press: the drain waits on running work for hours, so only a press that
/// would add to that work, or would be cut by the restart, waits for it.
/// </summary>
public enum PipeDrain
{
    /// <summary>
    /// It starts work the drain waits on, at once or at the next look (every setting asks the look at once); starts a process of its
    /// own that runs on past its answer; or changes a repository through git. Sent during the drain it is refused
    /// <see cref="InstancePipe.Stopping"/>, and confirmed it still waits.
    /// </summary>
    Waits,

    /// <summary>It stops, takes back, answers, records or says a word, and starts no process: it goes on while the update drains.</summary>
    Passes,
}

/// <summary>
/// A route that is a look in one form and a press in another, classed by one field of its payload: <see cref="Class"/> where the
/// field holds one of <see cref="Values"/>, <see cref="Otherwise"/> where it holds another or is absent.
/// </summary>
/// <remarks>
/// The handlers read the field exact-case and once, so a name that matches <see cref="Field"/> only ignoring case, a name said twice,
/// or one of <see cref="Unless"/> beside it is no form the rule names: it takes the strict class of the two (never Open). A value is compared
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

    /// <summary>A route of one class whatever its payload: a rule naming no value.</summary>
    public static PipeRule Whole(PipeClass @class) => new("", [], @class, @class);

    /// <summary>The class <paramref name="payload"/>'s form takes.</summary>
    public PipeClass For(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } named) return Otherwise;
        var properties = named.EnumerateObject().ToList();
        var fields = properties.Where(property => string.Equals(property.Name, Field, StringComparison.OrdinalIgnoreCase)).ToList();
        if (fields.Count == 0) return Otherwise;
        // The handler reads the field exact-case and once: another casing or a second say is no form it would read as named.
        if (fields.Count > 1 || fields[0].Name != Field || properties.Any(property => Unless.Contains(property.Name, StringComparer.OrdinalIgnoreCase)))
        {
            return Class != PipeClass.Open ? Class : Otherwise;
        }

        return Values.Any(value => Same(value, fields[0].Value)) ? Class : Otherwise;
    }

    /// <summary>
    /// Fields that, said beside <see cref="Field"/>, make the payload no form <see cref="Field"/> names: the handler reads one of
    /// these first (a history edit's <c>said</c>, run as the press whatever <c>plan</c> says).
    /// </summary>
    public IReadOnlyList<string> Unless { get; init; } = [];

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
/// The pipe's census: every route the running Daoris accepts over its pipe, classed <see cref="PipeClass.Open"/>,
/// <see cref="PipeClass.Person"/>, <see cref="PipeClass.Window"/> or <see cref="PipeClass.Role"/>. The same account opens the pipe
/// as runs <c>daoris-driver</c>, so the pipe confers no person's authority: only an open route runs as asked, and a person's runs
/// once the person confirms it in the window (<see cref="PipeConfirmations"/>).
/// </summary>
/// <remarks>
/// <para><b>Read off the routes' marks</b> (<see cref="RouteMarks"/>): a route's class and drain sit in its mark, so a route is
/// classed where it is answered. A route a route-by-route module does not name is a press and the person's; a module the census
/// does not name (the kit's own among them) is refused.</para>
///
/// <para><b>How a route is classed.</b> A landing is the person's yes; a discard, a sweep, an archive and a delete are their deletes
/// and closes; a stop is a close; words said to a session, a conversation or a channel are theirs; a standing choice (every
/// <c>SET_*</c>, a trust, an account, a tool, a plugin, a rule's answer) is their re-wiring, since one direction of each widens what an
/// agent may do, and a session that set one for itself would be reporting on itself. The driver's own are its looks: a nudge, a sync
/// with the service, the registry and the accounts' use read again, the tools' locations looked at. A press that moves the person's
/// lines or branches, starts work on an account, or lets a refused account through is theirs, though the driver makes it. Unsure is
/// the person's.</para>
///
/// <para><b>One route, two forms</b>: where a route answers a look and a press by its payload, it is classed by a
/// <see cref="PipeRule"/>, and the look alone runs.</para>
///
/// <para>Modules are matched as the kit's dispatcher matches them, ignoring case; a route as its module's table does.</para>
///
/// <para><b>What an update's drain does with each press</b> (<see cref="PipeDrain"/>): the drain waits for the driven sessions and
/// the conversations with a turn in flight, and its look holds every quest of a drivable repository from starting, resuming or
/// carrying on, and every answered park from going on (<c>InstallUpdate.Drained</c>). So a press whose work is a quest's start or a
/// park's resume goes on; one that would add to what the drain waits on, or that the close's few seconds would cut, waits. A read
/// never waits; a press the drain classes not waits, as an unclassed press is the person's.</para>
/// </remarks>
public sealed class PipeCensus
{
    private readonly FrozenDictionary<string, PipeClass> _whole;
    private readonly FrozenDictionary<string, FrozenDictionary<string, PipeRule>> _byRoute;
    private readonly FrozenDictionary<string, FrozenDictionary<string, PipeDrain>> _drains;

    private PipeCensus(
        IDictionary<string, PipeClass> whole, IDictionary<string, IDictionary<string, PipeRule>> byRoute,
        IDictionary<string, IDictionary<string, PipeDrain>> drains, PipeTargets targets)
    {
        _whole = whole.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _byRoute = byRoute.ToFrozenDictionary(
            each => each.Key, each => each.Value.ToFrozenDictionary(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
        _drains = drains.ToFrozenDictionary(
            each => each.Key, each => each.Value.ToFrozenDictionary(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
        Targets = targets;
    }

    /// <summary>
    /// What each person's press rests on (its grounds, read at its ask and again at its Confirm), and what each person's and window's
    /// route marks <c>owner-set</c> (<see cref="PipeTargets.Mark"/>).
    /// </summary>
    public PipeTargets Targets { get; }

    /// <summary>The desktop's census, read off this library's marks at its first use.</summary>
    public static PipeCensus Daoris { get; } = Read();

    private static PipeCensus Read()
    {
        var (others, windows) = RouteMarks.Modules(typeof(PipeCensus).Assembly);
        IReadOnlyList<RouteMark> marks = [.. DriverModule.Marks, .. others];
        return new PipeCensus(
            windows.ToDictionary(each => each, _ => PipeClass.Window, StringComparer.OrdinalIgnoreCase),
            marks.GroupBy(each => each.Module, StringComparer.OrdinalIgnoreCase).ToDictionary(
                module => module.Key,
                module => (IDictionary<string, PipeRule>)module.ToDictionary(each => each.Route, each => each.Rule, StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase),
            marks.Where(each => each.Drain is not null).GroupBy(each => each.Module, StringComparer.OrdinalIgnoreCase).ToDictionary(
                module => module.Key,
                module => (IDictionary<string, PipeDrain>)module.ToDictionary(each => each.Route, each => each.Drain!.Value, StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase),
            PipeTargets.Of(marks, windows));
    }

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

    /// <summary>
    /// The person's routes whose payload carries a key, a field kept in <c>profiles/</c> alone: <c>DAORIS.REMOTES</c> <c>SET</c>, its
    /// <c>key</c> the workspace's deployment key. A press of one keeps no payload in its record and does not wait across a restart.
    /// </summary>
    public static IReadOnlySet<string> KeyedRoutes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "DAORIS.REMOTES.SET",
        // A git identity's press: a token never arrives over the pipe (SecretRefusal), and an imported key file's path is kept out of the record too.
        $"{DriverModule.Name.ToUpperInvariant()}.GIT_IDENTITY",
    };

    /// <summary>
    /// The refusal for a field a route's payload must never carry over the pipe: a git identity's pasted token (the terminal reads one
    /// from standard input, <c>--token-stdin</c>), which a record of the press and its card would otherwise keep and show. Null where none.
    /// </summary>
    public static string? SecretRefusal(string route, System.Text.Json.JsonElement? payload) =>
        route == "GIT_IDENTITY" && payload is { ValueKind: System.Text.Json.JsonValueKind.Object } sent && sent.TryGetProperty("token", out _)
            ? "a token is never sent over the pipe, where it would be kept and shown; pipe it to `daoris git identity add-https --token-stdin` or `daoris git identity token --token-stdin` at the terminal."
            : null;

    /// <summary>Whether <paramref name="module"/>'s <paramref name="route"/> is one of <see cref="KeyedRoutes"/>, its module matched ignoring case.</summary>
    public static bool Keyed(string module, string route) => KeyedRoutes.Contains($"{module.ToUpperInvariant()}.{route}");

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

    /// <summary>
    /// What an update's drain does with <paramref name="module"/>'s <paramref name="route"/>: the mark's word for a person's or a
    /// role's route, its module matched ignoring case; null where it says none.
    /// </summary>
    public PipeDrain? DrainOf(string? module, string? route) =>
        module is not null && route is not null && _drains.TryGetValue(module, out var routes) && routes.TryGetValue(route, out var drain)
            ? drain
            : null;

    /// <summary>
    /// Whether a press of <paramref name="module"/>'s <paramref name="route"/>, classed <paramref name="class"/>, waits while an update
    /// drains: a read never; any other unless its mark lets it through, so a press the drain classes not waits.
    /// </summary>
    public bool WaitsForDrain(string? module, string? route, PipeClass? @class) =>
        @class != PipeClass.Open && DrainOf(module, route) != PipeDrain.Passes;

    /// <summary>
    /// This census with <paramref name="module"/>'s <paramref name="route"/> classed <paramref name="class"/> beside the rest: a test's. Its
    /// targets are this census's, so a person's route added here has no grounds row; nor has it a drain's word, so it waits.
    /// </summary>
    public PipeCensus With(string module, string route, PipeClass @class)
    {
        var byRoute = _byRoute.ToDictionary(
            each => each.Key, each => (IDictionary<string, PipeRule>)new Dictionary<string, PipeRule>(each.Value, StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);
        if (!byRoute.TryGetValue(module, out var routes)) byRoute[module] = routes = new Dictionary<string, PipeRule>(StringComparer.Ordinal);
        routes[route] = PipeRule.Whole(@class);
        var drains = _drains.ToDictionary(
            each => each.Key, each => (IDictionary<string, PipeDrain>)new Dictionary<string, PipeDrain>(each.Value, StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);
        return new PipeCensus(new Dictionary<string, PipeClass>(_whole, StringComparer.OrdinalIgnoreCase), byRoute, drains, Targets);
    }
}
