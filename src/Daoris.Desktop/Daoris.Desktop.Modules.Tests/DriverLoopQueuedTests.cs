using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// MSG1f2 (D137 §5.3): <c>SESSION_QUEUED</c> carries what a word said now would do, <c>reach</c> {reaches, why}, so the page
/// follows the box it offers live rather than asking <c>SESSION_QUEUE</c> again on each move. It is told when the words a
/// session's door holds change, said at once where the session runs here; when a record of this machine's moves through the
/// loop's client; and, for a quest's earlier sessions, when a later one opens. <c>reach</c> is an object, present whenever
/// the shell says it, because the bridge leaves a null out: an older shell, which carries none, is told apart from a word
/// that would start a turn at once.
/// </summary>
public sealed class DriverLoopQueuedTests : DriverModuleBridge
{
    /// <summary>A running driven session's door: its words' reach rides the queue it tells, at once, with no record read.</summary>
    [Fact]
    public async Task A_doors_change_tells_its_reach_with_the_queue()
    {
        var (loop, service) = await UpAsync(Record("d1", "working"));
        var inbox = loop.Processes.OpenInbox("d1");

        inbox.Attach(interrupt: null);

        var told = await QueuedAsync("d1", queued => queued.TryGetProperty("Reach", out _));
        Assert.True(told.GetProperty("Listening").GetBoolean());
        Assert.Equal("turn-end", told.GetProperty("Reach").GetProperty("Reaches").GetString());
        Assert.Equal(JsonValueKind.Null, told.GetProperty("Reach").GetProperty("Why").ValueKind);
        Assert.Equal(0, service.Reads);
    }

    /// <summary>
    /// A door that closes leaves the session winding up, which only its record can answer for: the queue is told at once
    /// without a reach, and then again, whole, with the reach the record gives: its words wait for it to end (<c>resume</c>).
    /// </summary>
    [Fact]
    public async Task A_door_that_closes_tells_the_reach_its_record_gives()
    {
        var (loop, _) = await UpAsync(Record("d1", "working"));
        var inbox = loop.Processes.OpenInbox("d1");
        inbox.Attach(interrupt: null);
        await QueuedAsync("d1", queued => queued.TryGetProperty("Reach", out _));

        inbox.Close();

        var told = await QueuedAsync("d1", queued => Reaches(queued) == "resume");
        Assert.False(told.GetProperty("Taking").GetBoolean());
        Assert.False(told.GetProperty("Listening").GetBoolean());
        Assert.Empty(told.GetProperty("Queued").EnumerateArray());
    }

    /// <summary>A record of this machine's that moves through the loop's client tells the reach it now gives.</summary>
    [Fact]
    public async Task A_record_that_moves_tells_its_reach()
    {
        var (loop, service) = await UpAsync(Record("s1", "working"));

        service.State("s1", "completed");
        await loop.Service!.AdvanceAsync("s1", "completed", note: "the quest reached done.");

        var told = await QueuedAsync("s1", queued => Reaches(queued) == "resume");
        Assert.False(told.GetProperty("Taking").GetBoolean());
    }

    /// <summary>
    /// A quest's earlier session no longer takes words once a later one opens here (<c>superseded</c>): its reach is told as
    /// the later one opens, since nothing moved the earlier record.
    /// </summary>
    [Fact]
    public async Task A_later_session_of_a_quest_tells_the_earlier_one_it_is_superseded()
    {
        var (loop, _) = await UpAsync(Record("s1", "failed", created: "2026-10-03T08:00:00Z"));

        var (opened, _) = await loop.Service!.OpenSessionAsync("q1", "claude-code");

        Assert.Equal("s2", opened);
        var told = await QueuedAsync("s1", queued => Why(queued) == WordsNever.Superseded);
        Assert.Equal(JsonValueKind.Null, told.GetProperty("Reach").GetProperty("Reaches").ValueKind);
    }

    /// <summary>The queue's answer says the reach the same way, so a page asking once knows the shell tells it live.</summary>
    [Fact]
    public async Task The_queues_answer_carries_the_reach_as_the_event_does()
    {
        var (loop, _) = await UpAsync(Record("s1", "completed"));

        var queue = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_QUEUE", new { id = "s1" });

        Assert.Equal("resume", queue.GetProperty("reach").GetProperty("reaches").GetString());
        Assert.Equal("resume", queue.GetProperty("reaches").GetString());
    }

    private static string? Reaches(JsonElement queued) =>
        queued.TryGetProperty("Reach", out var reach) && reach.ValueKind == JsonValueKind.Object
        && reach.TryGetProperty("Reaches", out var reaches) && reaches.ValueKind == JsonValueKind.String
            ? reaches.GetString()
            : null;

    private static string? Why(JsonElement queued) =>
        queued.TryGetProperty("Reach", out var reach) && reach.ValueKind == JsonValueKind.Object
        && reach.TryGetProperty("Why", out var why) && why.ValueKind == JsonValueKind.String
            ? why.GetString()
            : null;

    /// <summary>The first <c>SESSION_QUEUED</c> for this session the condition holds for, waited for: it is told from the loop's threads.</summary>
    private async Task<JsonElement> QueuedAsync(string session, Func<JsonElement, bool> holds)
    {
        JsonElement found = default;
        await UntilAsync(() =>
        {
            foreach (var message in Raised.Where(m => m.Type == "SESSION_QUEUED"))
            {
                var payload = JsonSerializer.SerializeToElement(message.Payload);
                if (payload.GetProperty("Session").GetString() == session && holds(payload))
                {
                    found = payload;
                    return true;
                }
            }

            return false;
        });
        return found;
    }

    private async Task<(DriverLoop Loop, StandIn Service)> UpAsync(params JsonObject[] records)
    {
        var service = new StandIn(records);
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        return (loop, service);
    }

    private static JsonObject Record(string id, string state, string quest = "q1", string created = "2026-10-03T08:00:00Z") => new()
    {
        ["id"] = id, ["repository"] = "engine", ["state"] = state, ["quest"] = quest, ["kind"] = "driven", ["created"] = created,
    };

    /// <summary>The records as given, the state door moving them, and the open door adding a session of the quest asked for.</summary>
    private sealed class StandIn(JsonObject[] records) : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<JsonObject> _sessions = [.. records];

        /// <summary>How many times the records were read.</summary>
        public int Reads { get; private set; }

        public void State(string id, string state)
        {
            lock (_gate) _sessions.Single(s => s["id"]!.GetValue<string>() == id)["state"] = state;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var (status, answer) = Answer(request.Method.Method, path, body);
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }

        private (HttpStatusCode, string) Answer(string method, string path, string body)
        {
            lock (_gate)
            {
                if (method == "GET" && path == "/api/sessions")
                {
                    Reads++;
                    return (HttpStatusCode.OK, new JsonArray([.. _sessions.Select(s => s.DeepClone())]).ToJsonString());
                }

                if (method == "POST" && path == "/api/sessions")
                {
                    var asked = JsonNode.Parse(body)!;
                    var session = new JsonObject
                    {
                        ["id"] = $"s{_sessions.Count + 1}", ["repository"] = "engine", ["state"] = "queued",
                        ["quest"] = asked["quest"]!.GetValue<string>(), ["kind"] = "driven", ["adapter"] = "claude-code",
                        ["created"] = "2026-10-03T10:00:00Z",
                    };
                    _sessions.Add(session);
                    return (HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "opened" }.ToJsonString());
                }

                if (method == "POST" && path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal))
                {
                    var id = Uri.UnescapeDataString(path["/api/sessions/".Length..^"/state".Length]);
                    var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                    session["state"] = JsonNode.Parse(body)!["state"]!.GetValue<string>();
                    return (HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "moved" }.ToJsonString());
                }

                return (HttpStatusCode.NotFound, $$"""{"error":"the stand-in has no {{method}} {{path}}"}""");
            }
        }
    }
}
