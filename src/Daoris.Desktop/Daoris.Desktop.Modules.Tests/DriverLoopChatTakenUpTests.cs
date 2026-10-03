using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// MSG1c2 (D137's MSG1c note): the shell's loop comes up with its conversations, and with them two things no route does. An
/// ended chat whose words were kept while no shell ran is taken up as the loop comes up, judged as words said now are; and
/// a chat taken up by itself announces its end as the page's <c>SESSION_ENDED</c>, in the shape a start's end has. The
/// runner's adapter here spawns nothing, so a chat that goes on ends <c>failed</c> at its spawn, which is an end all the
/// same; the runs that resume a conversation are the driver's <c>ChatGoesOnProcessTests</c>'.
/// </summary>
public sealed class DriverLoopChatTakenUpTests : DriverModuleBridge
{
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

    /// <summary>
    /// 🔴 Words kept on ended chats while no shell ran wait until another word is said, unless the loop takes them up as it
    /// comes up. One whose conversation was kept goes on, and its end reaches the page as <c>SESSION_ENDED</c> with the state
    /// its record took; one whose conversation was not kept says why in its conversation; a driven record is the planner's
    /// and is left.
    /// </summary>
    [Fact]
    public async Task Coming_up_takes_up_the_words_left_on_ended_chats_and_announces_an_end()
    {
        var root = Path.Combine(Home, "engine");
        Directory.CreateDirectory(root);
        var service = new StandIn(root);
        service.Chat("c1", root, ("w1", "Also log the port."));
        service.Chat("c2", root, ("w2", "And the readme."));
        service.Chat("d1", root, ("w3", "One more."), kind: "driven");
        new HarnessConversations(Home).Keep("c1", "talk", "conv-c1");
        var loop = Loop();
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        using var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        using var chat = new ChatRunner(
            client, adapters, loop.Home, loop.Processes, loop.Output, new HarnessRoster(adapters, HarnessSettingsPath), loop.Events);

        await loop.ComeUpAsync(client, chat);

        await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_ENDED"));
        var ended = Assert.Single(Raised, m => m.Type == "SESSION_ENDED");
        Assert.Equal("""{"Session":"c1","State":"failed"}""", JsonSerializer.Serialize(ended.Payload));
        await UntilAsync(() => loop.Events.Page("c2").Events.Any(e => e.Why is not null));
        Assert.Equal(ContinueWhy.Unkept, loop.Events.Page("c2").Events.Single(e => e.Why is not null).Why);
        Assert.Equal(["working", "failed"], service.Moves("c1"));
        Assert.Empty(service.Moves("d1"));
        Assert.Empty(loop.Events.Page("d1").Events);
        Assert.Same(chat, loop.Chat);
    }

    /// <summary>
    /// A start's end and a chat taken up by itself reach the page in one shape, written in one place: the routes that start
    /// a conversation hand their runs the loop's announcement rather than spelling the event again.
    /// </summary>
    [Fact]
    public void A_conversations_end_has_one_writer()
    {
        var modules = Path.Combine(WorkspaceRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Modules");
        var spelled = Directory.EnumerateFiles(modules, "*.cs")
            .Where(path => File.ReadAllText(path).Contains("\"SESSION_ENDED\"", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["DriverLoop.cs"], spelled);
    }

    private static string WorkspaceRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>
    /// The service's doors a chat going on crosses, standing in: the records with their kind, adapter, tree and words
    /// waiting, the registry, and the ledger's moves, each move heard and the record moved.
    /// </summary>
    private sealed class StandIn(string root) : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<JsonObject> _sessions = [];
        private readonly Dictionary<string, List<string>> _moves = new(StringComparer.Ordinal);

        public void Chat(string id, string tree, (string Id, string Text) word, string kind = "chat")
        {
            lock (_gate)
            {
                _sessions.Add(new JsonObject
                {
                    ["id"] = id, ["repository"] = "engine", ["state"] = "completed", ["kind"] = kind, ["adapter"] = "talk",
                    ["harnessVersion"] = "1.0.0", ["tree"] = tree, ["created"] = "2026-10-03T08:00:00Z",
                    ["said"] = new JsonArray(new JsonObject
                    {
                        ["id"] = word.Id, ["text"] = word.Text, ["at"] = "2026-10-03T09:00:00Z", ["files"] = new JsonArray(), ["reopens"] = true,
                    }),
                });
            }
        }

        public IReadOnlyList<string> Moves(string id)
        {
            lock (_gate) return [.. _moves.GetValueOrDefault(id) ?? []];
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
                    return (HttpStatusCode.OK, new JsonArray([.. _sessions.Select(s => s.DeepClone())]).ToJsonString());
                }

                if (method == "GET" && path == "/api/registry")
                {
                    return (HttpStatusCode.OK, new JsonArray(new JsonObject
                    {
                        ["repository"] = "engine", ["adopted"] = true, ["registered"] = true, ["root"] = root, ["workspace"] = "default",
                    }).ToJsonString());
                }

                if (method == "POST" && path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal))
                {
                    var id = Uri.UnescapeDataString(path["/api/sessions/".Length..^"/state".Length]);
                    var to = JsonNode.Parse(body)!["state"]!.GetValue<string>();
                    var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                    session["state"] = to;
                    if (!_moves.TryGetValue(id, out var moves)) _moves[id] = moves = [];
                    moves.Add(to);
                    return (HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "moved" }.ToJsonString());
                }

                return (HttpStatusCode.NotFound, $$"""{"error":"the stand-in has no {{method}} {{path}}"}""");
            }
        }
    }
}
