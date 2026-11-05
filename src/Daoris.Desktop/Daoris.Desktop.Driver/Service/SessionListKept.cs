using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Contracts;
using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>
/// The service's widest session list (every record, closed ones and every workspace included) held on this side and kept by its change
/// since the mark it last read (<c>GET /api/sessions/changes</c>): a reader of every record is handed the list in the list's order,
/// as the service writes each record, and asks the service only what moved. Reached as <see cref="SessionListDoors.Kept"/>.
/// </summary>
/// <remarks>
/// Readers at once take turns. What a reader is handed is never changed under it: a change makes a new list, and a record unmoved is
/// the same object in both, which a reader only reads. An order naming a record not held, which only a lost answer leaves, is read
/// again whole.
/// </remarks>
public sealed class SessionListKept
{
    private readonly ServiceClient _service;
    private readonly SemaphoreSlim _turn = new(1, 1);
    private string? _mark;
    private Dictionary<string, JsonObject> _held = new(StringComparer.Ordinal);
    private IReadOnlyList<JsonObject> _list = [];
    private string? _json;
    private SessionResponse[]? _typed;

    internal SessionListKept(ServiceClient service) => _service = service;

    /// <summary>Every record in the list's order, each as the service wrote it. A refused read throws with the service's own sentence.</summary>
    /// <exception cref="DriverException">A refused read.</exception>
    public Task<IReadOnlyList<JsonObject>> ReadAsync(CancellationToken ct = default) => CurrentAsync(() => _list, ct);

    /// <summary>
    /// Every record as the list's text, what a reader of the list's answer parses (<see cref="SessionRecords.Parse"/>): the one read for a
    /// reader that needs records beside the one it names (a line's other parts, a quest's earlier sessions, every tree). The text is
    /// written once for each state of the list.
    /// </summary>
    /// <exception cref="DriverException">A refused read.</exception>
    public Task<string> JsonAsync(CancellationToken ct = default) => CurrentAsync(() => _json ??= Written(_list), ct);

    /// <summary>Every record as the declaration types it, for the readers of its fields; read once for each state of the list.</summary>
    /// <exception cref="DriverException">A refused read.</exception>
    public Task<SessionResponse[]> ListedAsync(CancellationToken ct = default) => CurrentAsync(
        () => _typed ??= [.. _list.Select(record => record.Deserialize(ContractJson.Options.GetTypeInfo(typeof(SessionResponse))) as SessionResponse
            ?? throw new JsonException("a session record did not read as the list declares it"))], ct);

    /// <summary>One record as the declaration types it, found by its id (letters of either case); null where the list holds none.</summary>
    /// <exception cref="DriverException">A refused read.</exception>
    public Task<SessionResponse?> OneAsync(string id, CancellationToken ct = default) => CurrentAsync(
        () => (_held.TryGetValue(id, out var found) ? found
            : _list.FirstOrDefault(record => string.Equals(Id(record), id, StringComparison.OrdinalIgnoreCase)))
            ?.Deserialize(ContractJson.Options.GetTypeInfo(typeof(SessionResponse))) as SessionResponse, ct);

    private static string? Id(JsonObject record) => record["id"] is JsonValue id && id.TryGetValue<string>(out var named) ? named : null;

    private async Task<T> CurrentAsync<T>(Func<T> read, CancellationToken ct)
    {
        await _turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!Lay(await AskAsync(_mark, ct).ConfigureAwait(false))) Lay(await AskAsync(null, ct).ConfigureAwait(false));
            return read();
        }
        finally
        {
            _turn.Release();
        }
    }

    private static string Written(IReadOnlyList<JsonObject> records)
    {
        using var written = new MemoryStream();
        using (var writer = new Utf8JsonWriter(written))
        {
            writer.WriteStartArray();
            foreach (var record in records) record.WriteTo(writer);
            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(written.ToArray());
    }

    private async Task<JsonObject> AskAsync(string? since, CancellationToken ct)
    {
        var sent = await _service.SendAsync(new ListSessionChanges(since), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return await WholeAsync(ct).ConfigureAwait(false);
        sent.Answered();
        return JsonNode.Parse(sent.Reply.Payload) as JsonObject ?? throw new JsonException("the service answered the sessions' change as no object");
    }

    // A host from before the change door has only the whole list, which is a change from nothing; this ends when no host older than the door is read.
    private async Task<JsonObject> WholeAsync(CancellationToken ct)
    {
        var sent = await _service.SendAsync(new ListSessions(IncludeClosed: true), ct).ConfigureAwait(false);
        sent.Answered();
        var records = JsonNode.Parse(sent.Reply.Payload) as JsonArray ?? throw new JsonException("the service answered the session list as no array");
        var order = new JsonArray();
        foreach (var record in records)
        {
            if (record?["id"] is JsonValue id && id.TryGetValue<string>(out var named)) order.Add(named);
        }

        return new JsonObject { ["whole"] = true, ["changed"] = records, ["order"] = order, ["mark"] = null };
    }

    // The change laid over what is held: false where the order names a record neither held nor changed.
    private bool Lay(JsonObject change)
    {
        var whole = change["whole"] is JsonValue flag && flag.TryGetValue<bool>(out var all) && all;
        var held = whole ? new Dictionary<string, JsonObject>(StringComparer.Ordinal) : new Dictionary<string, JsonObject>(_held, StringComparer.Ordinal);
        if (change["changed"] is JsonArray changed)
        {
            var records = changed.OfType<JsonObject>().ToList();
            changed.Clear();
            foreach (var record in records)
            {
                if (record["id"] is JsonValue id && id.TryGetValue<string>(out var named)) held[named] = record;
            }
        }

        var order = change["order"] as JsonArray ?? throw new JsonException("the service answered the sessions' change with no order");
        var list = new List<JsonObject>(order.Count);
        var kept = new Dictionary<string, JsonObject>(order.Count, StringComparer.Ordinal);
        foreach (var node in order)
        {
            if (node is not JsonValue value || !value.TryGetValue<string>(out var id) || !held.TryGetValue(id, out var record)) return false;
            list.Add(record);
            kept[id] = record;
        }

        _mark = change["mark"]?.GetValue<string>();
        _held = kept;
        // A change that moved nothing keeps the list, its text and its typed records as they are.
        if (list.Count == _list.Count && list.Zip(_list).All(pair => ReferenceEquals(pair.First, pair.Second))) return true;
        _list = list;
        _json = null;
        _typed = null;
        return true;
    }
}
