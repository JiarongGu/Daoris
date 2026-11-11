using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris quest|ask clear &lt;id&gt; --yes</c>, <c>daoris quest clear &lt;id&gt; --failed --yes</c> and <c>daoris history clear --workspace
/// &lt;name&gt; --yes</c> as the card shows them (<c>HISTORY_CLEAR</c>): what would go, counted from the first press's list
/// (<c>HISTORY_PLAN</c>), the screen's own. A press the verb's list would keep or refuse is refused at the ask in the verb's own words, so
/// the person never sees it; a workspace or a quest's failed sessions with nothing that may go is said, as the list says it.
/// </summary>
public sealed class HistoryClearView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "HISTORY_CLEAR";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var (workspace, quest, ask) = (Named(payload, "workspace"), Named(payload, "quest")?.TrimStart('#'), Named(payload, "ask")?.TrimStart('#'));
        var failed = payload is { } body && PressPayload.Flag(body, "failed");
        if (new[] { workspace, quest, ask }.Count(name => name is not null) != 1 || (failed && quest is null))
        {
            return PressViewed.Refused("a clear names one workspace, one quest or one ask, never two; and `failed` only beside a quest.");
        }

        // The verb's list, in its words: what it would keep, or a name it does not know, is its exit 1.
        var asked = new { workspace, quest, ask, failed = failed ? true : (bool?)null };
        var listed = TreesSaid.Read(await world.ReadAsync(Module, "HISTORY_PLAN", new { asked.workspace, asked.quest, asked.ask, asked.failed, clear = true, said = true },
            cancellationToken).ConfigureAwait(false));
        if (listed is { Code: not 0 } refused) return PressViewed.Refused(string.Join(' ', refused.Out.Concat(refused.Error).Where(line => line.Length > 0)));

        var plan = await world.ReadAsync(Module, "HISTORY_PLAN", asked, cancellationToken).ConfigureAwait(false);
        return PressViewed.Shown(workspace is not null
            ? Workspace(workspace, plan)
            : One(quest is null ? "ask" : failed ? "failed" : "quest", (quest ?? ask)!, plan));
    }

    /// <summary>One quest's work, one ask's with every quest it became, or one closed quest's failed sessions.</summary>
    private static PressView One(string scope, string id, JsonElement plan)
    {
        var unit = PressPayload.Rows(plan, "units").FirstOrDefault();
        var (sessions, quests) = (Count(unit, "sessions"), Count(unit, "quests"));
        var command = scope switch
        {
            "ask" => $"daoris ask clear {ShellWord.Of(id, ShellWord.Name)} --yes",
            "failed" => $"daoris quest clear {ShellWord.Of(id, ShellWord.Name)} --failed --yes",
            _ => $"daoris quest clear {ShellWord.Of(id, ShellWord.Name)} --yes",
        };
        var facts = Facts((scope == "ask" ? "ask" : "quest", id), ("quests", Number(quests)), ("sessions", Number(sessions)),
            ("copies", Number(Count(unit, "teammates"))), ("size", HistoryCommand.Size(Bytes(unit))));
        if (scope == "failed" && sessions + Count(unit, "teammates") == 0) return new PressView("history-clear-failed-none", facts, command);

        var also = new List<PressSaid>();
        if (Count(unit, "forgotten") > 0) also.Add(new PressSaid("history-clear-remote", ("workspace", PressPayload.Field(unit, "workspace"))));
        if (Count(unit, "kept") is > 0 and var kept) also.Add(new PressSaid("history-clear-pieces", ("count", Number(kept))));
        return new PressView($"history-clear-{scope}", facts, command, also.Count == 0 ? null : also);
    }

    /// <summary>A workspace's finished history: what its list takes now, counted, and what it keeps.</summary>
    private static PressView Workspace(string workspace, JsonElement plan)
    {
        var name = PressPayload.Field(plan, "id") is { Length: > 0 } spelled ? spelled : workspace;
        var command = $"daoris history clear --workspace {ShellWord.Of(name, ShellWord.Workspace)} --yes";
        var reading = Object(plan, "reading");
        var takes = Object(reading, "takes");
        var leftOver = Number(Object(reading, "leftOver"), "count");
        var units = PressPayload.Rows(plan, "units").ToList();
        var facts = Facts(("workspace", name), ("quests", Number(Number(takes, "quests"))), ("asks", Number(Number(takes, "asks"))),
            ("sessions", Number(Number(takes, "sessions"))), ("copies", Number(Number(takes, "teammates"))), ("size", HistoryCommand.Size(Long(takes, "bytes"))));
        if (Number(takes, "quests") + Number(takes, "asks") + Number(takes, "sessions") + Number(takes, "teammates") + leftOver == 0)
        {
            return new PressView("history-clear-workspace-nothing", facts, command);
        }

        var also = new List<PressSaid>();
        if (leftOver > 0) also.Add(new PressSaid("history-clear-leftover", ("count", Number(leftOver))));
        if (units.Count(unit => !(unit.TryGetProperty("clearable", out var may) && may.ValueKind == JsonValueKind.True)) is > 0 and var kept)
        {
            also.Add(new PressSaid("history-clear-workspace-keeps", ("count", Number(kept))));
        }

        if (units.Where(unit => unit.TryGetProperty("clearable", out var may) && may.ValueKind == JsonValueKind.True).Sum(unit => Count(unit, "forgotten")) is > 0 and var forgotten)
        {
            also.Add(new PressSaid("history-clear-workspace-remote", ("workspace", name), ("count", Number(forgotten))));
        }

        return new PressView("history-clear-workspace", facts, command, also.Count == 0 ? null : also);
    }

    private static string? Named(JsonElement? payload, string name) => PressPayload.Text(payload, name) is { Length: > 0 } text ? text : null;

    private static Dictionary<string, string> Facts(params (string Name, string Value)[] facts) =>
        facts.ToDictionary(fact => fact.Name, fact => fact.Value, StringComparer.Ordinal);

    private static JsonElement Object(JsonElement read, string name) =>
        read.ValueKind == JsonValueKind.Object && read.TryGetProperty(name, out var value) ? value : default;

    private static int Count(JsonElement unit, string name) => PressPayload.Rows(unit, name).Count();

    private static int Number(JsonElement read, string name) =>
        Object(read, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number) ? number : 0;

    private static long Long(JsonElement read, string name) =>
        Object(read, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var number) ? number : 0;

    private static long Bytes(JsonElement unit) => Long(Object(unit, "bytes"), "total");

    private static string Number(int count) => count.ToString(CultureInfo.InvariantCulture);
}
