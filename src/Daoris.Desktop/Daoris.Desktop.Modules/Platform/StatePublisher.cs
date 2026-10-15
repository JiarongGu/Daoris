using System.Text.Json;
using System.Text.Json.Serialization;
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
}

/// <summary>
/// What a route declares a successful answer changed (D169 point 2): its scopes, and the payload field naming the session the
/// change was about, where it is one session's.
/// </summary>
public sealed record StateChange(StateScopes Scopes, string? Session = null);

/// <summary>
/// <c>STATE_CHANGED</c> on the wire (D169 point 3): what moved, never what it is now; no words, no answer, no machine path.
/// </summary>
/// <param name="Scopes">The scopes' names, in the page's words.</param>
/// <param name="Sessions">The sessions the change was about, where the route named one; absent is any.</param>
public sealed record StateChanged(
    IReadOnlyList<string> Scopes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Sessions);

/// <summary>
/// Every change a route makes, published on the kit's bus as <c>DAORIS.STATE_CHANGED</c> once the route has answered
/// (STATEHUB1b, D169): the dispatcher's middleware, after <see cref="OffTheWindow"/>, reading what the route declares.
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
/// <para><b>Not awaited.</b> The bus runs its subscribers (each window's pump, later a plugin's observer) without the press's
/// answer waiting on any of them; a subscriber's failure is the bus's to isolate.</para>
///
/// <para><b>Shell-only</b> (D47): the bus is this machine's, forwarded to its windows and to nothing else.</para>
/// </remarks>
public static class StatePublisher
{
    /// <summary>The module the event is raised under, as every Daoris event is.</summary>
    public const string Module = "DAORIS";

    /// <summary>The event's type.</summary>
    public const string Type = "STATE_CHANGED";

    /// <summary>The middleware over the driver's marks, publishing on <paramref name="bus"/>.</summary>
    public static MessageMiddleware Middleware(IEventBus bus) => Middleware(bus, DriverModule.Declared);

    /// <summary>The middleware over <paramref name="declared"/>, what each request's route declares it changes.</summary>
    public static MessageMiddleware Middleware(IEventBus bus, Func<IpcRequest, StateChange?> declared) => async (request, next, _) =>
    {
        // No ConfigureAwait: the kit's pipeline keeps the caller's context, as RefusalLog's says.
        var response = await next();
        if (response is { Success: true } && declared(request) is { Scopes: not StateScopes.None } change)
        {
            bus.Emit(Module, Type, Event(change, request));
        }

        return response;
    };

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

    private static string? SessionOf(StateChange change, IpcRequest request) =>
        change.Session is { } field
        && request.Payload is { ValueKind: JsonValueKind.Object } payload
        && payload.TryGetProperty(field, out var named)
        && named.ValueKind == JsonValueKind.String
        && named.GetString() is { Length: > 0 } session
            ? session
            : null;
}
