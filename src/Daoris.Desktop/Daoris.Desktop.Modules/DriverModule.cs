using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
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
/// <b>A partial per domain, and a route table they add to</b> (MOD5). Every route was a case of one switch
/// here, so every feature that added a driver door edited this file. A domain's routes now live in the
/// partial named as the page's own bridge file is (`DriverModule.Plugins.cs` beside `bridge/plugins.ts`),
/// each handler marked <see cref="DriverRouteAttribute"/>, and <see cref="RouteAsync"/> looks a request up
/// in the table the marks make. This file keeps the dispatch and what every domain shares.
/// </remarks>
public sealed partial class DriverModule : ModuleBase
{
    private readonly IEventBus _events;
    private readonly DriverLoop _loop;
    private readonly OpenFolder? _openFolder;
    private readonly PickFile? _pickFile;

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
        _events = events;
        _loop = loop;
        _openFolder = openFolder;
        _pickFile = pickFile;
    }

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
            ? route(this, request, cancellationToken)
            : throw UnknownType(request);

    /// <summary>Every route this module answers, by name: what the page's bridge may call (MOD5).</summary>
    public static IReadOnlyCollection<string> Routes => Table.Keys;

    // The handlers, read once off their marks. A route marked twice fails here, on the first request, as a
    // switch with two equal labels would not have compiled; a handler of neither shape fails here too.
    private static readonly FrozenDictionary<string, Func<DriverModule, IpcRequest, CancellationToken, Task<object?>>> Table =
        ReadTable();

    /// <summary>
    /// The table the partials add to: each <see cref="DriverRouteAttribute"/> on a handler of this class, by
    /// its route. A handler answers at once, <c>object? M(IpcRequest)</c>, or later,
    /// <c>Task&lt;object?&gt; M(IpcRequest, CancellationToken)</c>.
    /// </summary>
    private static FrozenDictionary<string, Func<DriverModule, IpcRequest, CancellationToken, Task<object?>>> ReadTable()
    {
        var table = new Dictionary<string, Func<DriverModule, IpcRequest, CancellationToken, Task<object?>>>(StringComparer.Ordinal);
        foreach (var method in typeof(DriverModule).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            foreach (var route in method.GetCustomAttributes<DriverRouteAttribute>())
            {
                table.Add(route.Type, method.GetParameters().Length == 1
                    ? Now(method.CreateDelegate<Func<DriverModule, IpcRequest, object?>>())
                    : method.CreateDelegate<Func<DriverModule, IpcRequest, CancellationToken, Task<object?>>>());
            }
        }

        return table.ToFrozenDictionary(StringComparer.Ordinal);

        // A handler that answers at once throws at once, which the router awaits the same way as a fault.
        static Func<DriverModule, IpcRequest, CancellationToken, Task<object?>> Now(Func<DriverModule, IpcRequest, object?> answer) =>
            (module, request, _) => Task.FromResult(answer(module, request));
    }

    // What the page may leave out, read one way (REV3 CLEAN1). The routes read optional values by hand,
    // and differently: one took an empty `adapter` as a name where this takes it as absent.

    /// <summary>A string the page may send, or null — blank is absent.</summary>
    private static string? Optional(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>A flag the page may send: true only when it says true.</summary>
    private static bool Flag(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;

    /// <summary>A whole number the page may send, or null.</summary>
    private static long? Number(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : null;

    /// <summary>What every route that needs the loop's service says before it answers (REV3 CLEAN1: five wrote it).</summary>
    private static Exception NotReady() => Refusals.Because(
        Refusals.DriverNotReady, "the driver is still coming up — its service is not answering yet. A moment.");

    private void Change(Func<DriverConfig, DriverConfig> change)
    {
        change(DriverConfig.Load(_loop.ConfigPath)).Save(_loop.ConfigPath);
        _loop.Nudge();
    }
}

/// <summary>
/// Marks a method of <see cref="DriverModule"/> as the handler of one of its routes (MOD5). A route is added
/// in its domain's partial by marking its handler, so no central switch is edited per route.
/// </summary>
/// <param name="type">The route's name, as the page's bridge sends it.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class DriverRouteAttribute(string type) : Attribute
{
    public string Type { get; } = type;
}
