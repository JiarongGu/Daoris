using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service's doors a clear of finished history reads and presses, in-process (HIST1c, HIST1d): the records, the quests and
/// the asks; each scope's listing as set; and a press that clears what it names, taking its records and its kept files as the
/// service does, unless told to refuse it. <see cref="HistoryClearingTests"/> and <see cref="HistoryCommandTests"/> drive both
/// doors through it, so the library and the terminal's verbs read one service.
/// </summary>
internal sealed class HistoryStandIn : HttpMessageHandler
{
    public List<JsonObject> Records { get; init; } = [];

    public List<JsonObject> Quests { get; init; } = [];

    public List<JsonObject> Asks { get; init; } = [];

    /// <summary>Each listing by its query, as the door answers it.</summary>
    public Dictionary<string, JsonArray> Listings { get; } = new(StringComparer.Ordinal);

    /// <summary>A unit the press refuses, by <c>kind:id</c>, with the unit as the service then judges it.</summary>
    public Dictionary<string, JsonObject> Refusing { get; } = new(StringComparer.Ordinal);

    /// <summary>Each unit a press sent, <c>kind:id</c>.</summary>
    public List<string> Pressed { get; } = [];

    /// <summary>A host from before the door: a bare 404.</summary>
    public bool NoDoor { get; init; }

    /// <summary>
    /// Quests and asks whose kept files the press could not remove: they stay, and the answer names them in <c>failed</c>, as the
    /// service's does (HIST1j). Empty is every removal made, and the answer carries no <c>failed</c>, as a host before it.
    /// </summary>
    public HashSet<string> HoldingKept { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The home whose kept files a press takes after the records, as the service's desk does.</summary>
    public string? Home { get; set; }

    public ServiceClient Client() => new("http://stand-in.test", null, new HttpClient(this, disposeHandler: false));

    // ——— The records, as the service answers them.

    public static JsonObject Record(
        string id, string? quest = null, string state = "completed", string? tree = null, string? ask = null, string workspace = "default",
        string kind = "driven") => new()
        {
            ["id"] = id, ["quest"] = quest, ["repository"] = "engine", ["state"] = state, ["kind"] = kind, ["tree"] = tree?.Replace('\\', '/'),
            ["ask"] = ask, ["workspace"] = workspace, ["created"] = "2026-10-03T09:00:00Z", ["updated"] = "2026-10-03T09:05:00Z",
        };

    public static JsonObject Quest(string id, string status = "Done", string workspace = "default", bool held = false, string? awaits = null) => new()
    {
        ["id"] = id, ["from"] = "game", ["to"] = "engine", ["title"] = "a title nobody reads", ["body"] = "", ["status"] = status,
        ["workspace"] = workspace, ["held"] = held, ["awaits"] = awaits,
    };

    public static JsonObject Ask(string id, string state = "Done", string workspace = "default") => new()
    {
        ["id"] = id, ["workspace"] = workspace, ["sentence"] = "a sentence of the person's", ["state"] = state, ["tier"] = "quest",
    };

    public static JsonObject Unit(
        string kind, string id, string workspace = "default", string[]? quests = null, string[]? asks = null, string[]? sessions = null,
        string[]? teammates = null, string[]? forgotten = null, JsonObject? refusal = null, JsonArray? kept = null)
    {
        static JsonArray Of(string[]? ids) => new([.. (ids ?? []).Select(id => (JsonNode)id)]);
        var unit = new JsonObject
        {
            ["kind"] = kind, ["id"] = id, ["workspace"] = workspace, ["clearable"] = refusal is null,
            ["quests"] = Of(quests), ["forgotten"] = Of(forgotten), ["asks"] = Of(asks), ["sessions"] = Of(sessions),
            ["teammates"] = Of(teammates), ["kept"] = kept ?? [],
        };
        if (refusal is not null) unit["refusal"] = refusal;
        return unit;
    }

    /// <param name="waits">What waits beside <c>needs-you</c> (HIST1l); null leaves the field out, as a host before it and every other word do.</param>
    public static JsonObject Refusal(string word, string? quest = null, string? ask = null, string? session = null, string? origin = null,
        string? workspace = null, string? error = null, string? waits = null)
    {
        var refusal = new JsonObject
        {
            ["refusal"] = word, ["error"] = error ?? $"the desk's {word} sentence", ["quest"] = quest, ["ask"] = ask, ["session"] = session,
            ["origin"] = origin, ["workspace"] = workspace,
        };
        if (waits is not null) refusal["waits"] = waits;
        return refusal;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var query = request.RequestUri.Query.TrimStart('?');
        if (request.Method == HttpMethod.Get)
        {
            return path switch
            {
                "/api/sessions" => Answer(HttpStatusCode.OK, new JsonArray([.. Records.Select(record => record.DeepClone())])),
                "/api/quests" => Answer(HttpStatusCode.OK, new JsonArray([.. Quests.Select(quest => quest.DeepClone())])),
                "/api/asks" => Answer(HttpStatusCode.OK, new JsonArray([.. Asks.Select(ask => ask.DeepClone())])),
                "/api/history" when NoDoor => new HttpResponseMessage(HttpStatusCode.NotFound),
                "/api/history" => Answer(HttpStatusCode.OK, new JsonObject
                {
                    ["units"] = Listings.TryGetValue(Uri.UnescapeDataString(query), out var units) ? units.DeepClone() : new JsonArray(),
                }),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
        var answers = new JsonArray();
        foreach (var named in body["units"]!.AsArray())
        {
            var key = $"{(string?)named!["kind"]}:{(string?)named["id"]}";
            Pressed.Add(key);
            if (Refusing.TryGetValue(key, out var refused))
            {
                answers.Add(new JsonObject { ["unit"] = refused.DeepClone(), ["cleared"] = false, ["message"] = "kept" });
                continue;
            }

            var unit = Listings.Values.SelectMany(units => units).OfType<JsonObject>()
                .First(each => $"{(string?)each["kind"]}:{(string?)each["id"]}" == key);
            bool In(string field, JsonObject row) => unit[field]!.AsArray().Any(id => (string?)id == (string?)row["id"]);
            Records.RemoveAll(record => In("sessions", record) || In("teammates", record));
            Quests.RemoveAll(quest => In("quests", quest));
            Asks.RemoveAll(ask => In("asks", ask));
            var failed = new JsonObject { ["quests"] = new JsonArray(), ["asks"] = new JsonArray() };
            foreach (var (field, folder) in new[] { ("quests", "quests"), ("asks", "asks") })
            {
                foreach (var id in unit[field]!.AsArray().Select(id => (string?)id).OfType<string>())
                {
                    if (HoldingKept.Contains(id))
                    {
                        failed[field]!.AsArray().Add(id);
                        continue;
                    }

                    var kept = Path.Combine(Home ?? "", folder, id);
                    if (Home is not null && Directory.Exists(kept)) Directory.Delete(kept, recursive: true);
                }
            }

            var answer = new JsonObject { ["unit"] = unit.DeepClone(), ["cleared"] = true, ["message"] = $"Cleared `#{(string?)unit["id"]}`." };
            if (failed["quests"]!.AsArray().Count + failed["asks"]!.AsArray().Count > 0) answer["failed"] = failed;
            answers.Add(answer);
        }

        return Answer(HttpStatusCode.OK, new JsonObject { ["units"] = answers });
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, JsonNode body) => new(status)
    {
        Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
    };
}
