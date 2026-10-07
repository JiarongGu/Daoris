using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Clearing finished history from this machine, the page's <c>bridge/history.ts</c> (HIST1c, D153; the history-clearing
/// design §6.3): the first press, what a clear would take and keep, and the second, which sends exactly what the first
/// listed. The driver library's <see cref="HistoryClearing"/> is the one implementation, which the terminal's history verbs
/// call too (D50).
/// </summary>
/// <remarks>
/// <para><b>Nothing machine-local comes back</b>: ids, counts and bytes, never a path or a title (§2.4), as the platform
/// language says of every page (§4).</para>
///
/// <para><b>Each reason is a code in <see cref="Refusals"/></b>, the catalogue's words, with the facts its sentence names and
/// which of its sentences is meant as <c>context</c>; read by the driver library from the service's word and the records it
/// names, never from the service's sentence. A word this build does not know is a newer service's, said verbatim as the
/// driver's refusal.</para>
///
/// <para><b>The answers are the driver library's projection</b> (<see cref="HistoryAnswers"/>, HIST1d), which
/// <c>daoris-driver history --json</c> prints too, so the terminal's answer is this route's field for field (D50).</para>
/// </remarks>
public sealed partial class DriverModule
{
    /// <summary>
    /// The first press (§5 step 1): every unit of a workspace, one quest's work, one quest's failed sessions or one ask's work,
    /// each with what it takes, what that holds on the disk and why it would stay; and for a workspace, the reading (§2.4).
    /// Changes nothing. One quest or ask this machine does not hold is refused.
    /// </summary>
    [DriverRoute("HISTORY_PLAN")]
    private async Task<object?> HistoryPlanAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (scope, id) = HistoryNamed(request);
        var plan = await HistoryClearing.PlanAsync(HistoryWorldOf(), scope, id, cancellationToken).ConfigureAwait(false);
        if (scope != HistoryScope.Workspace && plan.Units is [{ Keep.Word: HistoryWords.Unknown } unknown]) throw HistoryKept(unknown.Keep!);

        // HIST1d: the driver library's projection, which `daoris-driver history --json` prints too, field for field.
        return HistoryAnswers.Plan(plan);
    }

    /// <summary>
    /// The second press (§5): exactly the <c>units</c> the first press listed as may go, each judged again, cleared, its files
    /// removed and what names it tidied. What went comes back in counts and bytes, with what changed since the list and stayed;
    /// a clear of one quest, one ask or one quest's failed sessions that stayed is refused in its code.
    /// </summary>
    [DriverRoute("HISTORY_CLEAR")]
    private async Task<object?> HistoryClearAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (scope, id) = HistoryNamed(request);
        var units = HistoryUnits(request);
        var outcome = await HistoryClearing.ClearAsync(HistoryWorldOf(), scope, id, units, PluginEvents.Screen, cancellationToken)
            .ConfigureAwait(false);
        if (scope != HistoryScope.Workspace && outcome is { Cleared.Count: 0, Changed: [var stayed] }) throw HistoryKept(stayed.Keep!);

        _loop.Nudge();
        return HistoryAnswers.Cleared(outcome);
    }

    /// <summary>The scope a history route names: exactly one of <c>workspace</c>, <c>quest</c> and <c>ask</c>; <c>failed</c> only beside a quest.</summary>
    /// <exception cref="DriverException">Neither, more than one, or <c>failed</c> without a quest: the driver's sentence, said verbatim.</exception>
    private static (HistoryScope Scope, string Id) HistoryNamed(IpcRequest request) =>
        (Optional(request, "workspace"), Optional(request, "quest"), Optional(request, "ask"), Flag(request, "failed")) switch
        {
            ({ } workspace, null, null, false) => (HistoryScope.Workspace, workspace.Trim()),
            (null, { } quest, null, var failed) => (failed ? HistoryScope.Failed : HistoryScope.Quest, quest.Trim().TrimStart('#')),
            (null, null, { } ask, false) => (HistoryScope.Ask, ask.Trim().TrimStart('#')),
            _ => throw new DriverException(
                "a clear names one workspace, one quest or one ask, never two; and `failed` only beside a quest."),
        };

    /// <summary>The units the first press listed: each a <c>kind</c> and an <c>id</c>, which a second press must send.</summary>
    /// <exception cref="DriverException">No list: the driver's sentence, said verbatim.</exception>
    private static IReadOnlyList<HistoryUnitName> HistoryUnits(IpcRequest request) =>
        request.Payload is { ValueKind: JsonValueKind.Object } payload
        && payload.TryGetProperty("units", out var units) && units.ValueKind == JsonValueKind.Array
            ? [.. units.EnumerateArray()
                .Where(unit => unit.ValueKind == JsonValueKind.Object)
                .Select(unit => new HistoryUnitName(UnitText(unit, "kind") ?? "", UnitText(unit, "id") ?? ""))]
            : throw new DriverException("a clear sends the units its list held: the first press's `units`, each its `kind` and `id`.");

    private static string? UnitText(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>The loop's world for a clear: its service, its home, its file, its processes, its conversations and its log.</summary>
    private HistoryWorld HistoryWorldOf()
    {
        var service = _loop.Service ?? throw NotReady();
        return new HistoryWorld(service, _loop.Home, _loop.ConfigPath, _loop.Processes) { Events = _loop.Events, Log = _loop.Log };
    }

    /// <summary>
    /// A unit kept, refused in its code with the facts its sentence names and which of its sentences is meant: the driver
    /// library's projection (<see cref="HistoryAnswers.Reason"/>, whose code is <see cref="HistoryCodes.Of"/>'s), the one the
    /// plan's and the clear's answers carry, raised as the IPC's exception (REFAC1). A word this build does not know is a newer
    /// service's: it said no, and its sentence, verbatim, says why.
    /// </summary>
    private static Exception HistoryKept(HistoryKeep keep)
    {
        var reason = HistoryAnswers.Reason(keep);
        if (reason.Code == HistoryCodes.Refused) return Refusals.Because(Refusals.DriverRefused, keep.Message, ("message", keep.Message));

        var facts = new (string Key, string? Value)[]
            {
                ("context", reason.Context), ("quest", reason.Quest), ("ask", reason.Ask), ("session", reason.Session),
                ("machine", reason.Machine), ("workspace", reason.Workspace), ("repository", reason.Repository), ("branch", reason.Branch),
            }
            .Where(fact => fact.Value is not null)
            .Select(fact => (fact.Key, fact.Value!))
            .ToArray();
        return Refusals.Declared(reason.Code, keep.Message, facts);
    }
}
