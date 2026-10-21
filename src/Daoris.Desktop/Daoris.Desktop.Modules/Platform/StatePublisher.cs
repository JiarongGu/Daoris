using System.Text.Json;
using System.Text.Json.Serialization;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What a route's press can change, as named families of the page's queries (STATEHUB1b, D169 point 3). A route declares
/// them beside its mark; <see cref="StatePublisher"/> names them on the wire in the page's words, in this order.
/// </summary>
/// <remarks>A wire's words: a scope is added, never renamed or renumbered in place (D169 point 3).</remarks>
[Flags]
public enum StateScopes
{
    /// <summary>A read: nothing changed.</summary>
    None = 0,

    /// <summary>The sessions' records and lists.</summary>
    Sessions = 1 << 0,

    /// <summary>Every session's landing plan and gate, its second opinion and what of them waits on the person.</summary>
    Landing = 1 << 1,

    /// <summary>A session's review of its work (its diff), or every session's where the change names none.</summary>
    Diff = 1 << 2,

    /// <summary>Whether a session's landed branch can be handed to a landing plugin.</summary>
    HandOff = 1 << 3,

    /// <summary>The clean-up's list of session branches and landed branches.</summary>
    Sweep = 1 << 4,

    /// <summary>Which repositories hold Daoris's branches, which bringing them up to date reads.</summary>
    TreesSync = 1 << 5,

    /// <summary>Each repository's and workspace's rules: its line, how its work lands, and the rest the lines answer.</summary>
    Lines = 1 << 6,

    // STATEHUB1d (D169 point 10): the rest of the routes' families, after the first slice's, which keep their numbers.

    /// <summary>The quests, and each quest's or ask's work, which sits under their key.</summary>
    Quests = 1 << 7,

    /// <summary>The asks.</summary>
    Asks = 1 << 8,

    /// <summary>The driver's state and everything the page reads under its key: its standing choices, the lines, the plans and the lists.</summary>
    Driver = 1 << 9,

    /// <summary>What an agent Daoris starts may do, and what agents proposed about it.</summary>
    Rules = 1 << 10,

    /// <summary>What a clear of finished history would take and keep, every scope's.</summary>
    History = 1 << 11,

    /// <summary>Where each circle stands with its remote.</summary>
    Sync = 1 << 12,

    /// <summary>The workspaces and repositories this machine registered.</summary>
    Registry = 1 << 13,

    /// <summary>The plugins this machine holds.</summary>
    Plugins = 1 << 14,

    /// <summary>The agents this machine can run, what each start would run on, and their accounts.</summary>
    Harnesses = 1 << 15,

    // STATEHUB1h (D169's STATEHUB1h note): the remaining presses' families, after STATEHUB1d's, which keep their numbers.

    /// <summary>The tools this machine runs, what is downloaded and offered, and where it looks.</summary>
    Tools = 1 << 16,

    /// <summary>Ask Daoris's conversations: their history, and what each proposed.</summary>
    Help = 1 << 17,

    /// <summary>Daoris's browser's settings and favorites.</summary>
    Browser = 1 << 18,

    /// <summary>How this Daoris is wired: its sizes, once a walk has counted them (FABRIC-EVENT5b). Told by the driver, declared by no route.</summary>
    Wiring = 1 << 19,

    // NOTIFY2f (D196 point 6): after STATEHUB1h's, which keep their numbers.

    /// <summary>The notices of Done and failed and whether each is read, which the <c>notices</c> slice pushes.</summary>
    Notices = 1 << 20,
}

/// <summary>
/// What a route declares a successful answer changed (D169 point 2): its scopes, and the payload field naming the session the
/// change was about, where it is one session's.
/// </summary>
/// <param name="Scopes">What a successful answer changed.</param>
/// <param name="Session">The payload field naming the session the change was about.</param>
/// <param name="Subject">The payload field naming the entry or ask the press is for, where it is no one session's (ACCEPTALL1e).</param>
public sealed record StateChange(StateScopes Scopes, string? Session = null, string? Subject = null);

/// <summary>
/// <c>STATE_CHANGED</c> on the wire (D169 point 3): what moved, never what it is now; no words, no answer, no machine path.
/// </summary>
/// <param name="Scopes">The scopes' names, in the page's words.</param>
/// <param name="Sessions">The sessions the change was about, where the route named one; absent is any.</param>
public sealed record StateChanged(
    IReadOnlyList<string> Scopes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Sessions);

/// <summary>One press under way on the wire (STATEHUB1c, D169 point 7): its route, and the session its payload named.</summary>
/// <param name="Route">The route pressed, as the page names it when it sends.</param>
/// <param name="Session">The session the route's declaration names in the payload; absent where it names none.</param>
/// <param name="Subject">The entry or ask the route's declaration names in the payload (ACCEPTALL1e); absent where it names none.</param>
/// <param name="Step">The step its handler says it has reached (PRESSSTEP1, <see cref="PressSteps"/>); absent until it says one.</param>
/// <param name="Behind">
/// The session whose landing or push its <see cref="PressSteps.Waiting"/> step waits behind (LANDHOLD3), where that one said who it is;
/// absent at any other step, and where it did not.
/// </param>
public sealed record PressUnderWay(
    string Route,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Session,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Subject = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Step = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Behind = null);

/// <summary>
/// <c>UNDER_WAY</c> on the wire (STATEHUB1c, D169 point 7): every declared press dispatched and not yet answered, told whole at
/// each change, in the order they were dispatched; no words, no payload but the session, no machine path.
/// </summary>
/// <param name="Seq">Counts each telling, so a window keeps the newest whatever order the bus delivered them in.</param>
/// <param name="Presses">What is under way now; empty is nothing.</param>
public sealed record UnderWay(long Seq, IReadOnlyList<PressUnderWay> Presses);

/// <summary>
/// Every change a route makes, published on the kit's bus as <c>DAORIS.STATE_CHANGED</c> once the route has answered
/// (STATEHUB1b, D169): the dispatcher's middleware, after <see cref="OffTheWindow"/>, reading what the route declares, on the
/// driver's module and the browser's (<see cref="Declared"/>).
/// </summary>
/// <remarks>
/// <para><b>Declared, never emitted by hand.</b> A handler that emitted its own change would be one more list somebody forgets
/// to append to; the route says what it changes beside its mark (<see cref="DriverModule.Declared"/>), and this is the one place that
/// publishes it. A route that declares nothing, a read, publishes nothing.</para>
///
/// <para><b>On success, whatever the answer says</b> (D169 point 5). A refusal answered as data is published as an answer is: a
/// landing refused for a conflict fetched the line its plan reads against, and a list out of date is read again. A refusal
/// thrown as an error is not: the driver refused before it acted.</para>
///
/// <para><b>Not awaited.</b> The bus runs its subscribers (each window's pump, and the plugins' observation, <see cref="Observe"/>)
/// without the press's answer waiting on any of them; a subscriber's failure is the bus's to isolate.</para>
///
/// <para><b>Shell-only</b> (D47): the bus is this machine's, forwarded to its windows and to nothing else.</para>
///
/// <para><b>And what is under way</b> (STATEHUB1c, D169 point 7): a declared press, from its dispatch until it answers or
/// throws, told whole as <c>UNDER_WAY</c> at each change, so *落地中…* is shown by every view and window, not only the
/// component that pressed. Not the kit's request tracker, which names no session (D169's STATEHUB1c note). The list is held where
/// the driver's <c>UNDER_WAY</c> route reads it (<see cref="PressesUnderWay"/>, the loop's in the shell), so a window opened or
/// reloaded mid-press reads it whole as it mounts (STATEHUB1c2).</para>
/// </remarks>
public static class StatePublisher
{
    /// <summary>The module the event is raised under, as every Daoris event is.</summary>
    public const string Module = "DAORIS";

    /// <summary>The event's type.</summary>
    public const string Type = "STATE_CHANGED";

    /// <summary>The type of the event telling what is under way (STATEHUB1c).</summary>
    public const string UnderWayType = "UNDER_WAY";

    /// <summary>
    /// What a request's route declares it changes: the driver's marks, and the browser module's (BROWSERPUBLISH1). Any other
    /// module's request declares nothing: a module is added here when its presses move what a window reads.
    /// </summary>
    public static StateChange? Declared(IpcRequest request) => DriverModule.Declared(request) ?? BrowserModule.Declared(request);

    /// <summary>The middleware over the declared marks, publishing on <paramref name="bus"/>.</summary>
    public static MessageMiddleware Middleware(IEventBus bus) => Middleware(bus, Declared);

    /// <summary>The middleware over the declared marks, holding what is under way in <paramref name="underWay"/>, the loop's in the shell.</summary>
    public static MessageMiddleware Middleware(IEventBus bus, PressesUnderWay underWay) =>
        Middleware(bus, Declared, underWay);

    /// <summary>The middleware over <paramref name="declared"/>, what each request's route declares it changes.</summary>
    /// <param name="bus">Where it publishes.</param>
    /// <param name="declared">What each request's route declares it changes.</param>
    /// <param name="underWay">Where it holds what is under way; one of its own where none is handed, which nothing reads.</param>
    public static MessageMiddleware Middleware(IEventBus bus, Func<IpcRequest, StateChange?> declared, PressesUnderWay? underWay = null)
    {
        // What this pipeline has under way: one per composed dispatcher, as the shell composes one over its loop.
        underWay ??= new PressesUnderWay();
        return async (request, next, _) =>
        {
            if (declared(request) is not { Scopes: not StateScopes.None } change)
            {
                return await next();
            }

            var press = new object();
            bus.Emit(Module, UnderWayType, underWay.Begin(press, new PressUnderWay(
                request.Type, FieldOf(change.Session, request), FieldOf(change.Subject, request))));
            // The handler of this press says its step through PressSteps, which finds this press by the flow of the call.
            using var steps = PressSteps.Within((step, behind) =>
            {
                if (underWay.Reach(press, step, behind) is { } told) bus.Emit(Module, UnderWayType, told);
            });
            try
            {
                // No ConfigureAwait: the kit's pipeline keeps the caller's context, as RefusalLog's says.
                var response = await next();
                // What moved before it is no longer under way: the views ask again while they still say *落地中…*.
                if (response is { Success: true })
                {
                    bus.Emit(Module, Type, Event(change, request));
                }

                return response;
            }
            finally
            {
                bus.Emit(Module, UnderWayType, underWay.End(press));
            }
        };
    }

    /// <summary>
    /// The plugins' observation of the pipeline (STATEHUB1g, D169 point 9): each <c>STATE_CHANGED</c> on <paramref name="bus"/>,
    /// handed to <paramref name="tell"/> as the plugins' frame. Beside the pipeline, never in it: the bus does not wait for its
    /// subscribers, the change is published once the press has answered, and <paramref name="tell"/> (the loop's hook set's)
    /// only queues it, so no plugin holds, changes or refuses an answer. Dispose to stop.
    /// </summary>
    public static IDisposable Observe(IEventBus bus, Action<StateChangedFrame> tell) =>
        bus.Subscribe(Module, Type, message =>
        {
            if (message.Payload is StateChanged changed) tell(new StateChangedFrame(changed.Scopes, changed.Sessions));
            return Task.CompletedTask;
        });

    /// <summary>
    /// The change a press another process told makes (STATEHUB1e, D169 point 8): what the window's press of its route declares, with
    /// the session where the declaration names one, so a terminal's press reads in a window as the window's does. Null where the
    /// route declares nothing (a read, or a route no mark has).
    /// </summary>
    public static StateChanged? Told(PressTold press) => Told(press, DriverModule.Declared);

    /// <summary>The change <paramref name="press"/> makes by <paramref name="declared"/>, what each request's route declares.</summary>
    public static StateChanged? Told(PressTold press, Func<IpcRequest, StateChange?> declared) =>
        declared(new IpcRequest { Id = "told", Module = DriverModule.Name, Type = press.Route }) is { Scopes: not StateScopes.None } change
            ? new StateChanged(Names(change.Scopes), change.Session is not null && press.Session is { Length: > 0 } session ? [session] : null)
            : null;

    /// <summary>
    /// A press another process told, published on <paramref name="bus"/> as a window's press of its route is (STATEHUB1e): what the
    /// shell's loop does with each its request watch hears. Not awaited, as the publisher's own is not.
    /// </summary>
    public static void Publish(IEventBus bus, PressTold press)
    {
        if (Told(press) is { } changed) bus.Emit(Module, Type, changed);
    }

    /// <summary>
    /// A change the driver makes itself, with no press behind it (FABRIC-EVENT5, D169 point 3): words taken up by a conversation, a
    /// proposal written, a size landed. Published as a press's change is, in the same words, so the page's one hub asks again what
    /// it names; <paramref name="session"/> narrows it to the one session it was about. Not awaited, as the publisher's own is not.
    /// </summary>
    public static void Publish(IEventBus bus, StateScopes scopes, string? session = null) =>
        bus.Emit(Module, Type, new StateChanged(Names(scopes), session is { Length: > 0 } ? [session] : null));

    /// <summary>The scopes' names on the wire, in the page's words and in their declared order.</summary>
    public static IReadOnlyList<string> Names(StateScopes scopes) =>
    [
        .. Enum.GetValues<StateScopes>()
            .Where(scope => scope != StateScopes.None && scopes.HasFlag(scope))
            .Select(scope => JsonNamingPolicy.CamelCase.ConvertName(scope.ToString())),
    ];

    /// <summary>The event for <paramref name="change"/>: its scopes, and the session the request's payload named.</summary>
    public static StateChanged Event(StateChange change, IpcRequest request) =>
        new(Names(change.Scopes), SessionOf(change, request) is { } session ? [session] : null);

    private static string? SessionOf(StateChange change, IpcRequest request) => FieldOf(change.Session, request);

    /// <summary>The string the request's payload holds in <paramref name="field"/>, where a declaration names the field and it is there.</summary>
    private static string? FieldOf(string? field, IpcRequest request) =>
        field is not null
        && request.Payload is { ValueKind: JsonValueKind.Object } payload
        && payload.TryGetProperty(field, out var named)
        && named.ValueKind == JsonValueKind.String
        && named.GetString() is { Length: > 0 } session
            ? session
            : null;
}

/// <summary>
/// The presses under way, in dispatch order, each change numbered under the same lock, so the telling is whole and the newest is
/// known whatever order the bus delivers it in (STATEHUB1c). <see cref="StatePublisher"/> writes it; the shell's is the loop's
/// (<see cref="DriverLoop.UnderWay"/>), which the driver's <c>UNDER_WAY</c> route reads for a window opened mid-press (STATEHUB1c2).
/// </summary>
/// <remarks>
/// A step is what the press's own handler says it has reached (<see cref="PressSteps"/>, PRESSSTEP1), a field beside the route and
/// the session, never a change to them (D169 point 3).
/// </remarks>
public sealed class PressesUnderWay
{
    private readonly Lock _gate = new();
    private readonly List<(object Press, PressUnderWay Told)> _underWay = [];
    private long _seq;

    /// <summary><paramref name="press"/> is under way: the telling it makes.</summary>
    public UnderWay Begin(object press, PressUnderWay told)
    {
        lock (_gate)
        {
            _underWay.Add((press, told));
            return Telling(++_seq);
        }
    }

    /// <summary>
    /// <paramref name="press"/> has reached <paramref name="step"/> (PRESSSTEP1), behind <paramref name="behind"/>'s landing where it
    /// waits (LANDHOLD3): the telling it makes, or null where it is not under way or already says that step behind that one, so the same
    /// step told again numbers nothing.
    /// </summary>
    public UnderWay? Reach(object press, string step, string? behind = null)
    {
        lock (_gate)
        {
            var at = _underWay.FindIndex(each => ReferenceEquals(each.Press, press));
            if (at < 0 || (_underWay[at].Told.Step == step && _underWay[at].Told.Behind == behind)) return null;
            _underWay[at] = (press, _underWay[at].Told with { Step = step, Behind = behind });
            return Telling(++_seq);
        }
    }

    /// <summary><paramref name="press"/> answered or threw: the telling it makes.</summary>
    public UnderWay End(object press)
    {
        lock (_gate)
        {
            _underWay.RemoveAll(each => ReferenceEquals(each.Press, press));
            return Telling(++_seq);
        }
    }

    /// <summary>What is under way now, numbered as the last telling: a read, which tells nothing and numbers nothing new.</summary>
    public UnderWay Now()
    {
        lock (_gate)
        {
            return Telling(_seq);
        }
    }

    private UnderWay Telling(long seq) => new(seq, [.. _underWay.Select(each => each.Told)]);
}

/// <summary>
/// Where a declared press's handler says which step it has reached (PRESSSTEP1, D169 point 3), for the press it runs in: the
/// publisher tells every window the press whole with the step beside its route and session. Found by the flow of the call, so a
/// handler holds no bus and no list; said outside a declared press (a read, a terminal's verb, a test of the handler alone) it is
/// said to nobody. The steps are the driver's words on the wire, added to and never renamed (the page words them).
/// </summary>
public static class PressSteps
{
    /// <summary>A landing's gate: the second opinion, then the review's.</summary>
    public const string Gate = "gate";

    /// <summary>A landing's move itself: its merge or replay, and the landing plugin's hand-off the rule names.</summary>
    public const string Landing = "landing";

    /// <summary>A landed branch handed to its plugin.</summary>
    public const string HandOff = "handOff";

    /// <summary>
    /// A landing or hand-off waits behind another one's hold (LANDHOLD2, D145 point 2): said before it waits, with the session it
    /// waits behind where that one said who it is (LANDHOLD3), and the step it was at is said again when it has the hold.
    /// </summary>
    public const string Waiting = "waiting";

    private static readonly AsyncLocal<Action<string, string?>?> Current = new();

    /// <summary>
    /// For the landing or hand-off this call makes (LANDHOLD2): where its hold is waited for, the press says <see cref="Waiting"/>
    /// behind the holder's session once it is known (LANDHOLD3), and <paramref name="back"/> again once the wait is over. A press that
    /// waits for nothing says nothing more.
    /// </summary>
    public static IDisposable WhileWaiting(string back) =>
        LandingHoldNotice.Told((waiting, behind) => Reach(waiting ? Waiting : back, waiting ? behind?.Session : null));

    /// <summary>
    /// The press this call runs in has reached <paramref name="step"/>, behind <paramref name="behind"/>'s landing where it waits;
    /// nothing where it runs in none.
    /// </summary>
    public static void Reach(string step, string? behind = null) => Current.Value?.Invoke(step, behind);

    /// <summary>What the publisher holds for the press it dispatches: its handler's steps reach <paramref name="reach"/> until disposed.</summary>
    internal static IDisposable Within(Action<string, string?> reach)
    {
        var before = Current.Value;
        Current.Value = reach;
        return new Restore(before);
    }

    private sealed class Restore(Action<string, string?>? before) : IDisposable
    {
        public void Dispose() => Current.Value = before;
    }
}
