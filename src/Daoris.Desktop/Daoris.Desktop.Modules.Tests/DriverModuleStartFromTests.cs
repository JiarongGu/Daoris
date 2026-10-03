using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// MSG1f2 (D137 §5.3's <c>SESSION_START_FROM</c>): *Start a conversation with these words* over the bridge, one press the
/// page's start and first message used to compose. The route hands the loop's chat runner the session and answers
/// <c>{sessionId, sent, why, message}</c>; the new conversation's end reaches the page as any start's does. The judgement
/// and the act are the driver's <c>StartFromTests</c>'; the runner's adapter here spawns nothing, so a conversation that
/// opens ends <c>failed</c> at its spawn.
/// </summary>
public sealed class DriverModuleStartFromTests : DriverModuleBridge
{
    /// <summary>Before the loop's conversations come up, nothing can start one: a sentence the person can wait out.</summary>
    [Fact]
    public async Task A_press_before_the_loop_is_up_says_so()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_START_FROM", new { id = "c1" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>A refusal is the answer, by its code, with nothing started: here no words wait on the record.</summary>
    [Fact]
    public async Task A_refusal_is_the_answer_by_its_code()
    {
        var (_, module, service) = await UpAsync(words: false);

        var started = await AnswerAsync(module, "SESSION_START_FROM", new { id = "c1" });

        Assert.Equal(JsonValueKind.Null, started.GetProperty("sessionId").ValueKind);
        Assert.False(started.GetProperty("sent").GetBoolean());
        Assert.Equal(StartFrom.NoWords, started.GetProperty("why").GetString());
        Assert.False(string.IsNullOrWhiteSpace(started.GetProperty("message").GetString()));
        Assert.Equal(1, service.Count);
    }

    /// <summary>
    /// The press starts a conversation through the runner, on the machine's adapter, and its end reaches the page as a start's
    /// does; one that failed at its spawn leaves the words on the record they were said to, and the answer says why.
    /// </summary>
    [Fact]
    public async Task The_press_starts_a_conversation_whose_end_reaches_the_page()
    {
        var (loop, module, service) = await UpAsync(words: true);

        var started = await AnswerAsync(module, "SESSION_START_FROM", new { id = "c1" });

        Assert.Equal(JsonValueKind.Null, started.GetProperty("sessionId").ValueKind);
        Assert.False(started.GetProperty("sent").GetBoolean());
        Assert.Contains("this test spawns nothing", started.GetProperty("message").GetString());
        await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_ENDED"));
        Assert.Equal("""{"Session":"c2","State":"failed"}""", JsonSerializer.Serialize(Raised.Single(m => m.Type == "SESSION_ENDED").Payload));
        Assert.Equal(["w1"], service.Said("c1"));
        Assert.True(loop.Nudges >= 1);
    }

    private async Task<(DriverLoop Loop, DriverModule Module, StandIn Service)> UpAsync(bool words)
    {
        var root = Path.Combine(Home, "engine");
        Directory.CreateDirectory(root);
        File.WriteAllText(DriverConfigPath, """{ "adapter": "talk" }""");
        var service = new StandIn(root);
        service.Chat("c1", root, words);
        var loop = Loop();
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        var chat = new ChatRunner(
            client, adapters, loop.Home, loop.Processes, loop.Output, new HarnessRoster(adapters, HarnessSettingsPath), loop.Events);
        await loop.ComeUpAsync(client, chat);
        // The loop takes up an ended chat's words as it comes up (MSG1c2); this one kept no conversation, so it says it cannot
        // go on, which is the line the press stands under. Waited for, so the press does not cross that take-up.
        if (words) await UntilAsync(() => loop.Events.Page("c1").Events.Any(e => e.Why is not null));
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

    /// <summary>The service's doors a start crosses, standing in: the records, the registry, no quests, a chat's open and its moves.</summary>
    private sealed class StandIn(string root) : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<JsonObject> _sessions = [];

        public int Count
        {
            get { lock (_gate) return _sessions.Count; }
        }

        public void Chat(string id, string tree, bool words)
        {
            lock (_gate)
            {
                _sessions.Add(new JsonObject
                {
                    ["id"] = id, ["repository"] = "engine", ["state"] = "completed", ["kind"] = "chat", ["adapter"] = "talk",
                    ["tree"] = tree, ["created"] = "2026-10-03T08:00:00Z",
                    ["said"] = words
                        ? new JsonArray(new JsonObject
                        {
                            ["id"] = "w1", ["text"] = "Also log the port.", ["at"] = "2026-10-03T09:00:00Z", ["files"] = new JsonArray(),
                            ["reopens"] = true,
                        })
                        : new JsonArray(),
                });
            }
        }

        public IReadOnlyList<string> Said(string id)
        {
            lock (_gate)
            {
                return [.. _sessions.Single(s => s["id"]!.GetValue<string>() == id)["said"]!.AsArray().Select(word => word!["id"]!.GetValue<string>())];
            }
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
                switch (method, path)
                {
                    case ("GET", "/api/sessions"):
                        return (HttpStatusCode.OK, new JsonArray([.. _sessions.Select(s => s.DeepClone())]).ToJsonString());
                    case ("GET", "/api/quests"):
                        return (HttpStatusCode.OK, "[]");
                    case ("GET", "/api/registry"):
                        return (HttpStatusCode.OK, new JsonArray(new JsonObject
                        {
                            ["repository"] = "engine", ["adopted"] = true, ["registered"] = true, ["root"] = root, ["workspace"] = "default",
                        }).ToJsonString());
                    case ("POST", "/api/sessions/chat"):
                    {
                        var session = new JsonObject
                        {
                            ["id"] = $"c{_sessions.Count + 1}", ["repository"] = "engine", ["state"] = "queued", ["kind"] = "chat",
                            ["adapter"] = "talk", ["created"] = "2026-10-03T10:00:00Z", ["said"] = new JsonArray(),
                        };
                        _sessions.Add(session);
                        return (HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "opened" }.ToJsonString());
                    }
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
