using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// HELPSTART1 (D89's note): <c>START_HELP</c> answers once Ask Daoris's conversation is placed, its record open, and the room is
/// read and its agent spawned after, off the request; the person's words said to it meanwhile wait for it as its queue, opening.
/// On a loaded machine the room's session look took 24.9 s and the page stopped waiting at 30 s (SENDLOST1), its words lost. The
/// room's reading is held here by the loop's <c>DescribeHelp</c>, the seam the module hands it; the runner's adapter spawns
/// nothing, so the conversation ends <c>failed</c> at its spawn, which is said in it, its words kept there as the person's.
/// </summary>
public sealed class DriverModuleHelpStartTests : DriverModuleBridge
{
    private const string Words = "how do I drive engine?";

    /// <summary>
    /// The press answers within a short bound while the room's reading is held: the conversation placed (its record queued, no
    /// room written), a second press handed the same one, the person's words held as its queue. Once read, the room is written
    /// and the conversation starts; its end reaches the page as a start's does.
    /// </summary>
    [Fact]
    public async Task The_press_answers_before_the_room_is_read_and_the_conversation_starts_after()
    {
        var (loop, module, service) = await UpAsync();
        var reading = new TaskCompletionSource<HelpMachine?>(TaskCreationOptions.RunContinuationsAsynchronously);
        loop.DescribeHelp = _ => reading.Task;
        var room = Path.Combine(HelpRoom.PathOf(loop.Home), "AGENTS.md");

        var answer = await AnswerAsync(module, "START_HELP").WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("h1", answer.GetProperty("sessionId").GetString());
        Assert.False(answer.GetProperty("running").GetBoolean());
        Assert.True(answer.GetProperty("starting").GetBoolean());
        Assert.Equal("queued", service.State("h1"));
        Assert.False(File.Exists(room));

        // A second press while it starts (the panel's opening ahead, then the send's own) is handed it, never a second.
        var again = await AnswerAsync(module, "START_HELP").WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("h1", again.GetProperty("sessionId").GetString());
        Assert.True(again.GetProperty("starting").GetBoolean());
        Assert.Equal(1, service.Opened);

        // The person's words wait for it, said as waiting on its opening.
        var said = await AnswerAsync(module, "SESSION_INPUT", new { id = "h1", text = Words });
        Assert.True(said.GetProperty("sent").GetBoolean());
        var queue = await AnswerAsync(module, "SESSION_QUEUE", new { id = "h1" });
        Assert.True(queue.GetProperty("opening").GetBoolean());
        Assert.False(queue.GetProperty("taking").GetBoolean());
        Assert.Equal(Words, Assert.Single(queue.GetProperty("queued").EnumerateArray()).GetProperty("text").GetString());

        reading.SetResult(new HelpMachine { Adapter = "talk", Helper = "talk" });

        await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_ENDED"));
        Assert.Equal("""{"Session":"h1","State":"failed"}""", JsonSerializer.Serialize(Raised.Single(m => m.Type == "SESSION_ENDED").Payload));
        Assert.True(File.Exists(room));
        // Its failure is said in the conversation, and the words it held are there as the person's: never lost.
        Assert.Contains("this test spawns nothing", service.Note("h1"));
        await UntilAsync(() => loop.Events.Page("h1").Events.Any(e => e.Kind == SessionEventKind.User && e.Text == Words));
        Assert.Empty((await AnswerAsync(module, "SESSION_QUEUE", new { id = "h1" })).GetProperty("queued").EnumerateArray());
    }

    /// <summary>
    /// HELPSTART1b: starting from an earlier conversation answers as <c>START_HELP</c> does, the new conversation placed with its
    /// record open, while the room's reading is held; the earlier one's words are kept for it, and once the room is read it starts.
    /// </summary>
    [Fact]
    public async Task Starting_from_an_earlier_conversation_answers_before_the_room_is_read()
    {
        var (loop, module, service) = await UpAsync();
        service.Seed("h1", "completed");
        loop.Events.Append("h1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = Words });
        loop.Events.Append("h1", new SessionEvent { Kind = SessionEventKind.Message, Text = "with a go." });
        var reading = new TaskCompletionSource<HelpMachine?>(TaskCreationOptions.RunContinuationsAsynchronously);
        loop.DescribeHelp = _ => reading.Task;
        var room = Path.Combine(HelpRoom.PathOf(loop.Home), "AGENTS.md");

        var answer = await AnswerAsync(module, "HELP_START_FROM", new { id = "h1" }).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("h2", answer.GetProperty("sessionId").GetString());
        Assert.Equal("h1", answer.GetProperty("from").GetString());
        Assert.Equal("queued", service.State("h2"));
        Assert.False(File.Exists(room));
        Assert.Single(Directory.GetFiles(ChatFiles.Folder(loop.Home, "h2")));

        reading.SetResult(new HelpMachine { Adapter = "talk", Helper = "talk" });

        await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_ENDED"));
        Assert.True(File.Exists(room));
        Assert.Equal("failed", service.State("h2"));
    }

    /// <summary>A room that cannot be read is said in the conversation the press placed, which ends, rather than lost with the request.</summary>
    [Fact]
    public async Task A_room_that_cannot_be_read_ends_the_conversation_saying_why()
    {
        var (loop, module, service) = await UpAsync();
        loop.DescribeHelp = _ => Task.FromException<HelpMachine?>(new DriverException("the service did not answer"));

        var answer = await AnswerAsync(module, "START_HELP").WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("h1", answer.GetProperty("sessionId").GetString());
        await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_ENDED"));
        Assert.Equal("failed", service.State("h1"));
        Assert.Contains("the service did not answer", service.Note("h1"));
    }

    /// <summary>
    /// The room reads the whole look this shell took a moment ago, the Sessions list's, rather than judge every tree with git again,
    /// and says so in its log line; where the one kept is older than the span it takes a fresh one, and keeps that in turn.
    /// </summary>
    [Fact]
    public async Task The_room_reads_a_recent_look_and_takes_a_fresh_one_where_none_is()
    {
        var logs = Path.GetDirectoryName(DriverConfigPath)!;
        using var log = new MachineLog(logs, "driver");
        var (loop, _, _) = await UpAsync(log);
        var ended = new SessionRecord("c9e1d000", "engine", "completed")
        {
            Kind = "chat", Created = DateTimeOffset.UtcNow.AddHours(-1), Updated = DateTimeOffset.UtcNow.AddMinutes(-30),
        };
        var look = new SessionLook([ended], [], []);

        loop.SessionsLooked.Keep(look, DateTimeOffset.UtcNow);
        var read = await loop.DescribeHelp!(CancellationToken.None);

        Assert.Contains(read!.Sessions!.Listed, each => each.Id == ended.Id);
        Assert.Contains(LogLines(logs), line => line.Contains("\"event\":\"help.room.look\"") && line.Contains("\"kept\":true"));

        loop.SessionsLooked.Keep(look, DateTimeOffset.UtcNow - HelpRoomLookLog.Recent - TimeSpan.FromSeconds(1));
        var fresh = await loop.DescribeHelp!(CancellationToken.None);

        Assert.DoesNotContain(fresh!.Sessions!.Listed, each => each.Id == ended.Id);
        // HELPROOM1: a fresh look says where its time went; a kept one took no steps and says none.
        Assert.Contains(LogLines(logs), line => line.Contains("\"event\":\"help.room.look\"") && line.Contains("\"kept\":false")
            && line.Contains("\"recordsMs\":") && line.Contains("\"questsMs\":") && line.Contains("\"verdictsMs\":")
            && line.Contains("\"marksMs\":") && line.Contains("\"waitingMs\":") && line.Contains("\"treesMs\":") && line.Contains("\"trees\":0"));
        Assert.DoesNotContain(LogLines(logs), line => line.Contains("\"kept\":true") && line.Contains("Ms\":") && line.Contains("recordsMs"));
        Assert.NotNull(loop.SessionsLooked.Within(HelpRoomLookLog.Recent, DateTimeOffset.UtcNow));
    }

    /// <summary>The machine log's lines under a home, read while the log may still be writing them.</summary>
    private static string[] LogLines(string home)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        })];
    }

    private async Task<(DriverLoop Loop, DriverModule Module, StandIn Service)> UpAsync(MachineLog? log = null)
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "talk", "helperAdapter": "talk" }""");
        var service = new StandIn();
        var loop = new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0", log: log);
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        var chat = new ChatRunner(
            client, adapters, loop.Home, loop.Processes, loop.Output, new HarnessRoster(adapters, HarnessSettingsPath), loop.Events);
        await loop.ComeUpAsync(client, chat);
        return (loop, new DriverModule(Bus, loop), service);
    }

    /// <summary>A conversation's adapter on the protocol door with no toolchain to ask, so nothing is probed or spawned.</summary>
    private sealed class Talk : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("this test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("this test spawns nothing");
    }

    /// <summary>The service's doors a help start crosses, standing in: the records, an empty registry, no quests, the room's open and its moves.</summary>
    private sealed class StandIn : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<JsonObject> _sessions = [];

        public int Opened { get; private set; }

        public string? State(string id)
        {
            lock (_gate) return Find(id)?["state"]?.GetValue<string>();
        }

        public string Note(string id)
        {
            lock (_gate) return Find(id)?["note"]?.ToJsonString() ?? "";
        }

        /// <summary>An earlier conversation of the room, already in the records.</summary>
        public void Seed(string id, string state)
        {
            lock (_gate)
            {
                _sessions.Add(new JsonObject
                {
                    ["id"] = id, ["repository"] = HelpRoom.Repository, ["state"] = state, ["kind"] = "chat",
                    ["adapter"] = "talk", ["created"] = "2026-10-15T06:42:13Z", ["said"] = new JsonArray(),
                });
            }
        }

        private JsonObject? Find(string id) => _sessions.FirstOrDefault(s => s["id"]!.GetValue<string>() == id);

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
                switch (method, path)
                {
                    case ("GET", "/api/sessions"):
                        return (HttpStatusCode.OK, new JsonArray([.. _sessions.Select(s => s.DeepClone())]).ToJsonString());
                    case ("GET", "/api/quests"):
                    case ("GET", "/api/registry"):
                    case ("GET", "/api/asks"):
                        return (HttpStatusCode.OK, "[]");
                    case ("POST", "/api/sessions/help"):
                    {
                        // One running per room, as the ledger holds it.
                        if (_sessions.Any(s => s["state"]!.GetValue<string>() is "queued" or "starting" or "working"))
                        {
                            return (HttpStatusCode.Conflict, """{"error":"Ask Daoris `h1` is running."}""");
                        }

                        Opened++;
                        var session = new JsonObject
                        {
                            ["id"] = $"h{_sessions.Count + 1}", ["repository"] = HelpRoom.Repository, ["state"] = "queued", ["kind"] = "chat",
                            ["adapter"] = "talk", ["created"] = "2026-10-15T06:42:13Z", ["said"] = new JsonArray(),
                        };
                        _sessions.Add(session);
                        return (HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "opened" }.ToJsonString());
                    }
                }

                if (method == "POST" && path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal))
                {
                    var id = Uri.UnescapeDataString(path["/api/sessions/".Length..^"/state".Length]);
                    var session = Find(id)!;
                    var moved = JsonNode.Parse(body)!;
                    session["state"] = moved["state"]!.GetValue<string>();
                    if (moved["note"] is { } note) session["note"] = note.DeepClone();
                    return (HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "moved" }.ToJsonString());
                }

                return (HttpStatusCode.NotFound, $$"""{"error":"the stand-in has no {{method}} {{path}}"}""");
            }
        }
    }
}
