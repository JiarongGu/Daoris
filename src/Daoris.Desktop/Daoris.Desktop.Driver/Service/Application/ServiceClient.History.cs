using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The service's history doors, a local host's only (HIST1b, D153; the history-clearing design §6.3): what a clear of finished
/// history would take, and the clear itself. The driver's <see cref="HistoryClearing"/> calls them, judging this machine's half
/// before and removing the home's files after.
/// </summary>
public sealed partial class ServiceClient
{
    /// <summary>
    /// The units a scope holds, each judged by the service, deleting nothing: <paramref name="query"/> is the door's one scope,
    /// <c>workspace=</c>, <c>quest=</c>, <c>ask=</c> or <c>quest=…&amp;failed=true</c>, escaped.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, or one that refused the scope, in its words.</exception>
    public async Task<IReadOnlyList<HistoryUnitView>> HistoryAsync(string query, CancellationToken ct = default)
    {
        var answer = await _doors.SendAsync(ServiceVerb.Get, $"/api/history?{query}", null, ct).ConfigureAwait(false);
        var root = Answered(answer.Ok, answer.Status, answer.Payload);
        return [.. Units(root, "units").Select(ReadHistoryUnit)];
    }

    /// <summary>
    /// The second press: exactly <paramref name="units"/>, each judged again by the service and cleared in one transaction of
    /// its own, or kept with its word.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, or one that refused the press, in its words.</exception>
    public async Task<IReadOnlyList<HistoryClearedView>> ClearHistoryAsync(IReadOnlyList<HistoryUnitName> units, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("units");
            foreach (var unit in units)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", unit.Kind);
                writer.WriteString("id", unit.Id);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
        var (ok, status, payload, _) = await PostJsonAsync("/api/history/clear", body, ct).ConfigureAwait(false);
        var root = Answered(ok, status, payload);
        return
        [
            .. Units(root, "units").Select(each =>
            {
                // Additive (HIST1j): absent where every removal was made, and from a host before it.
                var failed = each.TryGetProperty("failed", out var named) && named.ValueKind == JsonValueKind.Object;
                return new HistoryClearedView(
                    each.TryGetProperty("unit", out var unit) ? ReadHistoryUnit(unit) : new HistoryUnitView("", "", null),
                    Flag(each, "cleared"),
                    Text(each, "message") ?? "")
                {
                    FailedQuests = failed ? Strings(named, "quests") : [],
                    FailedAsks = failed ? Strings(named, "asks") : [],
                };
            }),
        ];
    }

    /// <summary>The door's answer read, or its refusal said: in its words where it gave some, else as a host older than the door.</summary>
    private JsonElement Answered(bool ok, int status, string payload)
    {
        JsonElement root = default;
        try
        {
            using var document = JsonDocument.Parse(payload);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not JSON: a host older than the door answers a bare 404 or 405.
        }

        if (ok && root.ValueKind == JsonValueKind.Object) return root;
        if (Text(root, "error") is { Length: > 0 } error) throw new DriverException($"the service refused the history door ({status}): {error}");
        throw new DriverException($"the service at {_base} has no history door ({status}) — is it older than this driver?");
    }

    private static IEnumerable<JsonElement> Units(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var units) && units.ValueKind == JsonValueKind.Array
            ? units.EnumerateArray().Where(unit => unit.ValueKind == JsonValueKind.Object)
            : [];

    private static HistoryUnitView ReadHistoryUnit(JsonElement unit) => new(Text(unit, "kind") ?? "", Text(unit, "id") ?? "", Text(unit, "workspace"))
    {
        Clearable = Flag(unit, "clearable"),
        Quests = Strings(unit, "quests"),
        Forgotten = Strings(unit, "forgotten"),
        Asks = Strings(unit, "asks"),
        Sessions = Strings(unit, "sessions"),
        Teammates = Strings(unit, "teammates"),
        Refusal = unit.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.Object ? ReadHistoryRefusal(refusal) : null,
        Kept = [.. Units(unit, "kept").Select(ReadHistoryRefusal)],
    };

    private static HistoryRefusalView ReadHistoryRefusal(JsonElement refusal) => new(Text(refusal, "refusal") ?? "", Text(refusal, "error") ?? "")
    {
        Quest = Text(refusal, "quest"),
        Ask = Text(refusal, "ask"),
        Session = Text(refusal, "session"),
        Origin = Text(refusal, "origin"),
        Workspace = Text(refusal, "workspace"),
        // Additive (HIST1l, HIST1m): each absent beside every other word, and from a host before it.
        Waits = Text(refusal, "waits"),
        Stands = Text(refusal, "stands"),
        By = Text(refusal, "by"),
    };
}
