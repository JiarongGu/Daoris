using Daoris.Knowledge;

namespace Daoris.Knowledge.Http;

/// <summary>Whose a door of the local host is (PERSONDOOR1a, D156 point 3; the person-door design §3.1–§3.2).</summary>
public enum DoorClass
{
    /// <summary>A read: answered to every caller, as the connector reads the same store.</summary>
    Open,

    /// <summary>
    /// What an agent may do: answered without the key and judged as an agent's (a publish is <c>byAgent</c>); with the key,
    /// the person's.
    /// </summary>
    Agent,

    /// <summary>What the driver read for itself: only with the key, and recorded as the driver's own facts (D46, D144).</summary>
    Driver,

    /// <summary>The person's alone: only with the key, and recorded as theirs.</summary>
    Person,

    /// <summary>Mapped on a shared host only, whose gate is minted keys (D47 §7), so no local host judges it.</summary>
    Shared,
}

/// <summary>
/// One route of the host and its class. <paramref name="Act"/> is the route's own name, which a refusal says (*Only the
/// person can give a review's verdict*) and a confirmation shows (PERSONDOOR1b); a read has none.
/// </summary>
public sealed record Door(string Method, string Pattern, DoorClass Class, string? Act = null)
{
    /// <summary>
    /// A form of a call that moves it to another class, by a field of the body its route binds (design §3.2; the
    /// publishes' person's form is the key's, which the route reads through <see cref="PersonGate.IsAgent"/>). The route
    /// judges it with <see cref="PersonGate.Refused"/>, since only the route binds the body as the exchange reads it; the
    /// gate before the route leaves such a door to it, refusing only a stale key.
    /// </summary>
    public DoorForm? Form { get; init; }
}

/// <summary>A door's form (<see cref="Door.Form"/>): the field that makes it, the class it moves to, and its own name.</summary>
public sealed record DoorForm(string Field, DoorClass Class, string Act);

/// <summary>
/// The local host's doors, every route by its class (design §3.2), and the refusal each says (§5.2). A route the table
/// does not class is the person's: a write with no class is refused without the key, so a door added tomorrow is the
/// person's until someone classes it otherwise (§3.1). <c>PersonDoorTableTests</c> lists every route, as
/// <c>docs/index/routes.md</c> does, and holds this table to it and to every route each host maps.
/// </summary>
public static class PersonDoors
{
    /// <summary>The refusal's code at a door that is the person's alone, without the key.</summary>
    public const string PersonOnly = "person-only";

    /// <summary>The refusal's code at the driver's own door, without the key.</summary>
    public const string DriverOnly = "driver-only";

    /// <summary>The refusal's code for a key from another start of the host.</summary>
    public const string Stale = "stale";

    /// <summary>The machine log's event for a refusal: one warning per refused call, codes only.</summary>
    public const string Event = "person.refused";

    /// <summary>The name a write the table does not class is refused by.</summary>
    public const string Unclassified = "this change";

    /// <summary>The refusal of a key from another start (§5.2).</summary>
    public const string StaleSentence =
        "This call carried a key from an earlier start of this service, and each start has its own. Nothing was kept. "
        + "Daoris's window asks for the new one by itself; a terminal takes the one this start's starter holds.";

    /// <summary>The refusal at a door that is the person's alone (§5.2).</summary>
    public static string PersonSentence(string act) =>
        $"Only the person can give {act}, and this call carried no key of theirs. Nothing was kept. In Daoris's window, "
        + "press it there; from a terminal, run it again and confirm it in the window. An agent asks the person instead: "
        + "a go-ahead, or its closing note.";

    /// <summary>The refusal at the driver's own door (§5.2).</summary>
    public static string DriverSentence(string act) =>
        $"Only Daoris's driver posts {act}: it is what the driver read for itself, never what a session says of its own "
        + "work. Nothing was kept.";

    /// <summary>The respond door: an agent's take, done or decline; with <c>whileOpen</c>, an abandon's decline.</summary>
    public static readonly Door Respond = new("POST", "/api/quests/{id}/respond", DoorClass.Agent, "a quest's take, done or decline")
    {
        // PAUSE1c: the driver declines an open quest on the person's press to abandon it.
        Form = new DoorForm("whileOpen", DoorClass.Person, "an abandon's decline"),
    };

    /// <summary>The set-up door: a set-up a session said, naming it; or the person's own, with where to look.</summary>
    public static readonly Door SetUp = new("POST", "/api/quests/{id}/set-up", DoorClass.Driver, "a set-up a session said, with the commit Daoris read")
    {
        // REVIEWENV1b: a set-up that names no session is the person's own, which the exchange takes only with `look`.
        Form = new DoorForm("look", DoorClass.Person, "a set-up of their own"),
    };

    /// <summary>Every route either host maps, each with its class: 63 at PERSONDOOR1a.</summary>
    public static IReadOnlyList<Door> Table { get; } =
    [
        // Open: reads (design §3.2, 18), and the second opinion's two (D155's XAGENT1c note), answered to loopback only.
        new("GET", "/api/status", DoorClass.Open),
        new("GET", "/api/repositories", DoorClass.Open),
        new("GET", "/api/search", DoorClass.Open),
        new("GET", "/api/entry", DoorClass.Open),
        new("GET", "/api/entries", DoorClass.Open),
        new("GET", "/api/convergence", DoorClass.Open),
        new("GET", "/api/quests", DoorClass.Open),
        new("GET", "/api/quests/{id}/attachments/{sha256}", DoorClass.Open),
        new("GET", "/api/quests/{id}/claim", DoorClass.Open),
        new("GET", "/api/asks", DoorClass.Open),
        new("GET", "/api/asks/{id}", DoorClass.Open),
        new("GET", "/api/sessions", DoorClass.Open),
        new("GET", "/api/sessions/{id}/deletable", DoorClass.Open),
        // A plan, which deletes nothing.
        new("GET", "/api/history", DoorClass.Open),
        new("GET", "/api/registry", DoorClass.Open),
        new("GET", "/api/registry/retired", DoorClass.Open),
        new("GET", "/api/code-map/{repository}", DoorClass.Open),
        new("GET", "/api/sync", DoorClass.Open),
        new("GET", "/api/opinions", DoorClass.Open),
        new("GET", "/api/opinions/{id}", DoorClass.Open),

        // What an agent may do (3): the exchange the connector's tools use, so the two doors cannot drift.
        Respond,
        new("POST", "/api/quests", DoorClass.Agent, "a quest's publish"),
        new("POST", "/api/asks/{id}/publish", DoorClass.Agent, "a quest's publish from an ask"),

        // The driver's own (11 and the set-up's session form), and the second opinion's two posts (D155's XAGENT1c note).
        new("POST", "/api/quests/{id}/evidence", DoorClass.Driver, "a done's evidence"),
        SetUp,
        new("POST", "/api/sessions", DoorClass.Driver, "a driven session's record"),
        new("POST", "/api/sessions/chat", DoorClass.Driver, "a conversation's record"),
        new("POST", "/api/sessions/intake", DoorClass.Driver, "an intake's record"),
        new("POST", "/api/sessions/help", DoorClass.Driver, "Ask Daoris's record"),
        new("POST", "/api/sessions/{id}/state", DoorClass.Driver, "a session's state"),
        new("POST", "/api/sessions/{id}/taken", DoorClass.Driver, "the words a session took"),
        new("POST", "/api/sync", DoorClass.Driver, "a sync pass"),
        new("DELETE", "/api/registry/retired/{repository}", DoorClass.Driver, "a retire carried to a workspace's deployment"),
        // Its root and workspace decide where sessions run and which circle sees a quest (WSP1, WSP2), so no agent sets it.
        new("POST", "/api/registry", DoorClass.Driver, "a registration"),
        // The machine's job, which is why the connector's default leaves `knowledge_refresh` out.
        new("POST", "/api/refresh", DoorClass.Driver, "a rebuild of the index"),
        new("POST", "/api/opinions", DoorClass.Driver, "a second opinion's pass"),
        new("POST", "/api/opinions/{id}/hand", DoorClass.Driver, "a second opinion's findings, handed to the working session"),

        // The person's alone (19, and the forms above): the review, what a review is set to, the yes and the done, a
        // go-ahead's answer, their words, closing and deleting, and the registry's verbs (D48 §3).
        new("POST", "/api/quests/{id}/review", DoorClass.Person, "a review's verdict"),
        new("POST", "/api/quests/{id}/set-up-step", DoorClass.Person, "a set-up step"),
        new("POST", "/api/asks", DoorClass.Person, "an ask"),
        new("POST", "/api/asks/{id}/review", DoorClass.Person, "an ask's review choice"),
        new("POST", "/api/quests/{id}/accept", DoorClass.Person, "the yes to a departure"),
        new("POST", "/api/quests/{id}/done", DoorClass.Person, "a quest's done of their own"),
        new("POST", "/api/asks/{id}/go-aheads/{number}", DoorClass.Person, "a go-ahead's answer"),
        new("POST", "/api/sessions/{id}/added", DoorClass.Person, "words added to a running session"),
        new("POST", "/api/sessions/{id}/say", DoorClass.Person, "words to a session"),
        new("POST", "/api/sessions/{id}/answer", DoorClass.Person, "an answer to a parked session"),
        new("POST", "/api/asks/{id}/close", DoorClass.Person, "an ask's close"),
        new("DELETE", "/api/asks/{id}", DoorClass.Person, "an ask's delete"),
        new("DELETE", "/api/quests/{id}", DoorClass.Person, "a quest's delete"),
        new("DELETE", "/api/sessions/{id}", DoorClass.Person, "a session's delete"),
        new("POST", "/api/history/clear", DoorClass.Person, "a clear of finished history"),
        new("POST", "/api/quests/{id}/conflicts/dismiss", DoorClass.Person, "a conflict's dismissal"),
        new("DELETE", "/api/registry/{repository}", DoorClass.Person, "a repository's retire"),
        new("POST", "/api/registry/{repository}/workspace", DoorClass.Person, "a repository's re-wiring"),
        new("POST", "/api/registry/import", DoorClass.Person, "a folder's import"),

        // A shared host's alone (design §6, 7): its feed and sync doors, gated by minted keys.
        new("POST", "/api/feed/sessions", DoorClass.Shared),
        new("GET", "/api/sessions/since", DoorClass.Shared),
        new("POST", "/api/feed/entries", DoorClass.Shared),
        new("POST", "/api/feed/code-map", DoorClass.Shared),
        new("GET", "/api/feed/held", DoorClass.Shared),
        new("GET", "/api/quests/operations", DoorClass.Shared),
        new("POST", "/api/quests/operations", DoorClass.Shared),
    ];

    private static readonly Dictionary<(string Method, string Pattern), Door> ByRoute =
        Table.ToDictionary(door => (door.Method, door.Pattern));

    /// <summary>The door for a route, by its method and its pattern as the host mapped it; null for one the table does not class.</summary>
    public static Door? Find(string method, string pattern) =>
        ByRoute.GetValueOrDefault((method.ToUpperInvariant(), pattern));
}

/// <summary>What a call presented of the person key.</summary>
public enum Presented
{
    /// <summary>No key: on a host that holds one, the caller is an agent (design §3.1).</summary>
    None,

    /// <summary>This start's key: the person, or the driver acting for itself or on the person's press.</summary>
    Key,

    /// <summary>A key that is not this start's: one from an earlier start, as the only way a caller comes to hold one.</summary>
    Stale,
}

/// <summary>
/// The gate the person key keeps (PERSONDOOR1a; the person-door design §3, §5): before a local host's routes, it refuses
/// a call to the person's or the driver's door that carried no key, and any call but a read that carried a key from
/// another start, each <c>403</c> with a sentence and a code, never a <c>500</c>; it tells an agent's door who called, so
/// the route judges a keyless publish as an agent's (<see cref="IsAgent"/>); and the forms a body makes are judged by their
/// routes through <see cref="Refused"/>.
/// </summary>
/// <remarks>
/// <para><b>A host handed no key keeps today's trust</b> (design §2.1): every call is the person's, nothing is refused, and
/// a keyless publish is the person's as before, until the shell and the gates hand their hosts a key (PERSONDOOR1d–h).</para>
///
/// <para><b>Authority is the key's, attribution the body's</b> (§3.1): a body may narrow who acted (a session named, an
/// agent's publish) and never widen it, so nothing a body says reads as the key.</para>
///
/// <para><b>The log</b> (§5.1): one <see cref="PersonDoors.Event"/> warning per refusal, by the method, the route's
/// pattern, what was presented (<c>none</c> or <c>stale</c>) and the code. Never the key, the body or the caller.</para>
/// </remarks>
public sealed class PersonGate(PersonKey? key, MachineLog log)
{
    private static readonly object PresentedItem = new();

    /// <summary>Whether this start was handed a key, and so enforces it.</summary>
    public bool Holds => key is not null;

    /// <summary>
    /// What a request presented of the key. Read from a caller on this machine only (design §2.2): a local host binds the
    /// loopback alone, so a key that arrives from anywhere else has left the machine, and its call is judged as keyless.
    /// </summary>
    public Presented PresentedBy(HttpContext context)
    {
        var values = context.Request.Headers[PersonKey.Header];
        if (!FromThisMachine(context.Connection) || values.All(string.IsNullOrWhiteSpace)) return Presented.None;
        return values.Count == 1 && key is not null && key.Matches(values[0]) ? Presented.Key : Presented.Stale;
    }

    /// <summary>A caller on this machine, as the host's machine paths are answered (D46): no address is the in-process server.</summary>
    private static bool FromThisMachine(ConnectionInfo connection) =>
        connection.RemoteIpAddress is null || System.Net.IPAddress.IsLoopback(connection.RemoteIpAddress);

    /// <summary>
    /// The gate's judgement of a call (design §3.1), pure so it can be held without a host: the code it is refused with,
    /// or null when its route is to answer it. An agent's door answers a keyless call, and a door with a form leaves its
    /// class to its route. A write the table does not class is the person's; anything else it does not class (a read, a
    /// preflight) is answered.
    /// </summary>
    public static (string Code, string Act)? Judge(Door? door, string method, Presented presented)
    {
        var @class = door?.Class ?? (BrowserOrigins.IsWrite(method) ? DoorClass.Person : DoorClass.Open);
        if (@class is DoorClass.Open) return null;
        if (presented == Presented.Stale) return (PersonDoors.Stale, door?.Act ?? PersonDoors.Unclassified);
        if (presented == Presented.Key || @class == DoorClass.Agent || door?.Form is not null) return null;
        return Required(@class, door?.Act ?? PersonDoors.Unclassified);
    }

    /// <summary>The middleware: judge the call before its route runs, and keep what it presented for the route.</summary>
    public async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        if (key is not null && context.GetEndpoint() is RouteEndpoint endpoint)
        {
            var presented = PresentedBy(context);
            context.Items[PresentedItem] = presented;
            var door = PersonDoors.Find(context.Request.Method, endpoint.RoutePattern.RawText ?? "");
            if (Judge(door, context.Request.Method, presented) is { } refused)
            {
                Write(context, presented, refused.Code);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new ErrorResponse(Sentence(refused.Code, refused.Act), refused.Code));
                return;
            }
        }

        await next();
    }

    /// <summary>
    /// Whether this call is an agent's (design §3.1): no key, on a host that holds one. On a host handed no key, never: its
    /// trust is today's, and a keyless call is the person's as before.
    /// </summary>
    public bool IsAgent(HttpContext http) => key is not null && http.Items[PresentedItem] is Presented.None;

    /// <summary>
    /// A door with a form, judged by its route (design §3.2): <paramref name="form"/> is whether the body the route bound
    /// makes the form. The refusal to answer, or null when the call may go on. A stale key never reaches here: the gate
    /// refused it before the route ran.
    /// </summary>
    public IResult? Refused(HttpContext http, Door door, bool form)
    {
        var (@class, act) = form && door.Form is { } made ? (made.Class, made.Act) : (door.Class, door.Act ?? PersonDoors.Unclassified);
        if (!IsAgent(http) || @class is DoorClass.Open or DoorClass.Agent) return null;

        var (code, _) = Required(@class, act);
        Write(http, Presented.None, code);
        return Results.Json(new ErrorResponse(Sentence(code, act), code), statusCode: StatusCodes.Status403Forbidden);
    }

    private static (string Code, string Act) Required(DoorClass @class, string act) =>
        (@class == DoorClass.Driver ? PersonDoors.DriverOnly : PersonDoors.PersonOnly, act);

    private static string Sentence(string code, string act) => code switch
    {
        PersonDoors.Stale => PersonDoors.StaleSentence,
        PersonDoors.DriverOnly => PersonDoors.DriverSentence(act),
        _ => PersonDoors.PersonSentence(act),
    };

    // Once per refused call, codes only (design §5.1): the route's pattern, never its values, the key or the body.
    private void Write(HttpContext context, Presented presented, string code) =>
        log.Warn(PersonDoors.Event,
            ("method", context.Request.Method),
            ("route", UnhandledRequests.RouteOf(context)),
            ("presented", presented == Presented.Stale ? "stale" : "none"),
            ("code", code));
}
