using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A unit as a press names it (HIST1b's door): its kind's word (<c>quest</c>, <c>ask</c> or <c>failed</c>) and its id.</summary>
public sealed record HistoryUnitName(string Kind, string Id);

/// <summary>
/// Why the service keeps a unit, or a piece of one, as its history door says it (HIST1b): its word, which the driver acts on,
/// the desk's sentence, which only a terminal prints, and what the word names.
/// </summary>
/// <param name="Word"><c>unknown</c>, <c>open</c>, <c>asked</c>, <c>live</c>, <c>needs-you</c>, <c>awaited</c>, <c>unpushed</c> or <c>not-ours</c>.</param>
public sealed record HistoryRefusalView(string Word, string Message)
{
    public string? Quest { get; init; }

    public string? Ask { get; init; }

    public string? Session { get; init; }

    /// <summary>The machine a teammate's record ran on.</summary>
    public string? Origin { get; init; }

    public string? Workspace { get; init; }
}

/// <summary>One unit as the service's history door judged it (HIST1b's <c>HistoryDesk</c>, the history-clearing design §6.3).</summary>
/// <param name="Workspace">Its records' workspace; null for a unit this machine does not hold.</param>
public sealed record HistoryUnitView(string Kind, string Id, string? Workspace)
{
    public bool Clearable { get; init; }

    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>Of <see cref="Quests"/>, those a remote numbered: forgotten here, and kept there.</summary>
    public IReadOnlyList<string> Forgotten { get; init; } = [];

    public IReadOnlyList<string> Asks { get; init; } = [];

    /// <summary>This machine's session records it takes.</summary>
    public IReadOnlyList<string> Sessions { get; init; } = [];

    /// <summary>This machine's copies of a teammate's records, keyed <c>origin/id</c>.</summary>
    public IReadOnlyList<string> Teammates { get; init; } = [];

    /// <summary>Why the whole unit stays; null when the service would clear it.</summary>
    public HistoryRefusalView? Refusal { get; init; }

    /// <summary>Pieces listed and kept while the unit goes: a teammate's failed session.</summary>
    public IReadOnlyList<HistoryRefusalView> Kept { get; init; } = [];

    public HistoryUnitName Name => new(Kind, Id);
}

/// <summary>What the service's press did to one unit: the unit as judged where it was cleared, whether it went, and its sentence.</summary>
public sealed record HistoryClearedView(HistoryUnitView Unit, bool Cleared, string Message);

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
        using var response = await _http.GetAsync($"{_base}/api/history?{query}", ct).ConfigureAwait(false);
        var root = Answered(response.IsSuccessStatusCode, (int)response.StatusCode, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
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
            .. Units(root, "units").Select(each => new HistoryClearedView(
                each.TryGetProperty("unit", out var unit) ? ReadHistoryUnit(unit) : new HistoryUnitView("", "", null),
                Flag(each, "cleared"),
                Text(each, "message") ?? "")),
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
    };
}
