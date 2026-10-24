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

        var answer = await AnswerAsync(module, "START_HELP").WaitAsync(Poll.Patience);

        Assert.Equal("h1", answer.GetProperty("sessionId").GetString());
        Assert.False(answer.GetProperty("running").GetBoolean());
        Assert.True(answer.GetProperty("starting").GetBoolean());
        Assert.Equal("queued", service.State("h1"));
        Assert.False(File.Exists(room));

        // A second press while it starts (the panel's opening ahead, then the send's own) is handed it, never a second.
        var again = await AnswerAsync(module, "START_HELP").WaitAsync(Poll.Patience);
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

        var answer = await AnswerAsync(module, "HELP_START_FROM", new { id = "h1" }).WaitAsync(Poll.Patience);

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

    /// <summary>
    /// 🔴 ASKSCOPE1a (D158's ASKSCOPE1 note, points 1 and 3): the press names the workspace its window is on, captured there.
    /// Two workspaces' presses each place their own conversation, the other's still starting, and a second press in one is
    /// handed that one's; each answer names its workspace, so a late one is known for the workspace it was asked in. Since
    /// ASKSCOPE1b each starts in its own workspace's room folder, written for that workspace, each reaching its own spawn; none
    /// named is <c>default</c>'s, sends none and starts in <c>help</c>, as before.
    /// </summary>
    [Fact]
    public async Task Each_workspace_s_press_places_its_own_conversation_and_a_second_is_handed_its_own()
    {
        var (loop, module, service) = await UpAsync();
        service.Registry = """
            [{"repository":"engine","workspace":"alpha","adopted":true},{"repository":"site","workspace":"beta","adopted":true}]
            """;
        var reading = new TaskCompletionSource<HelpMachine?>(TaskCreationOptions.RunContinuationsAsynchronously);
        loop.DescribeHelp = _ => reading.Task;

        var alpha = await AnswerAsync(module, "START_HELP", new { workspace = "alpha" }).WaitAsync(Poll.Patience);
        var beta = await AnswerAsync(module, "START_HELP", new { workspace = "beta" }).WaitAsync(Poll.Patience);
        var again = await AnswerAsync(module, "START_HELP", new { workspace = "Alpha" }).WaitAsync(Poll.Patience);
        var unnamed = await AnswerAsync(module, "START_HELP").WaitAsync(Poll.Patience);

        Assert.Equal(("h1", "alpha"), (alpha.GetProperty("sessionId").GetString(), alpha.GetProperty("workspace").GetString()));
        Assert.Equal(("h2", "beta"), (beta.GetProperty("sessionId").GetString(), beta.GetProperty("workspace").GetString()));
        // A name is one workspace whatever its letters' case; the answer names it as it was asked.
        Assert.Equal(("h1", "Alpha"), (again.GetProperty("sessionId").GetString(), again.GetProperty("workspace").GetString()));
        Assert.True(again.GetProperty("starting").GetBoolean());
        Assert.Equal(("h3", "default"), (unnamed.GetProperty("sessionId").GetString(), unnamed.GetProperty("workspace").GetString()));
        Assert.Equal(["alpha", "beta", null], service.Asked);
        Assert.Equal(("alpha", "beta", "default"), (service.Workspace("h1"), service.Workspace("h2"), service.Workspace("h3")));

        // Words said to one go to it alone.
        await AnswerAsync(module, "SESSION_INPUT", new { id = "h2", text = Words });
        Assert.Empty((await AnswerAsync(module, "SESSION_QUEUE", new { id = "h1" })).GetProperty("queued").EnumerateArray());

        reading.SetResult(new HelpMachine { Adapter = "talk", Helper = "talk" });

        await UntilAsync(() => Raised.Count(m => m.Type == "SESSION_ENDED") == 3);
        Assert.All(new[] { "h1", "h2", "h3" }, id => Assert.Contains("this test spawns nothing", service.Note(id)));
        Assert.True(File.Exists(Path.Combine(HelpRoom.PathOf(loop.Home), "AGENTS.md")));
        // ASKSCOPE1b: each workspace's room in its own folder, saying its workspace; `default`'s is `help`, saying none.
        Assert.All(new[] { "alpha", "beta" }, workspace => Assert.Contains(
            $"This conversation is workspace `{workspace}`'s", File.ReadAllText(Path.Combine(HelpRoom.PathOf(loop.Home, workspace), "AGENTS.md"))));
        Assert.DoesNotContain("This conversation is workspace", File.ReadAllText(Path.Combine(HelpRoom.PathOf(loop.Home), "AGENTS.md")));
        await UntilAsync(() => loop.Events.Page("h2").Events.Any(e => e.Kind == SessionEventKind.User && e.Text == Words));
    }

    /// <summary>A workspace this machine does not hold is refused at the press, naming those it does, and nothing is opened.</summary>
    [Fact]
    public async Task A_workspace_this_machine_does_not_hold_is_refused_and_opens_nothing()
    {
        var (loop, module, service) = await UpAsync();
        service.Registry = """[{"repository":"engine","workspace":"alpha","adopted":true}]""";
        service.Seed("h1", "completed");
        loop.Events.Append("h1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = Words });

        var refused = await RefusalAsync(module, "START_HELP", new { workspace = "gamma" });

        Assert.Contains("there is no workspace `gamma` on this machine", refused);
        Assert.Contains("`alpha`", refused);
        Assert.Contains("`default`", refused);
        Assert.Contains("there is no workspace `gamma` on this machine", await RefusalAsync(module, "HELP_START_FROM", new { id = "h1", workspace = "gamma" }));
        Assert.Equal(0, service.Opened);
    }

    /// <summary>
    /// Starting from an earlier conversation is placed in the workspace its press names (ASKSCOPE1a), as <c>START_HELP</c> is,
    /// whatever workspace the earlier one was in.
    /// </summary>
    [Fact]
    public async Task Starting_from_an_earlier_conversation_is_placed_in_the_workspace_its_press_names()
    {
        var (loop, module, service) = await UpAsync();
        service.Registry = """[{"repository":"engine","workspace":"alpha","adopted":true}]""";
        service.Seed("h1", "completed");
        loop.Events.Append("h1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = Words });
        var reading = new TaskCompletionSource<HelpMachine?>(TaskCreationOptions.RunContinuationsAsynchronously);
        loop.DescribeHelp = _ => reading.Task;

        var answer = await AnswerAsync(module, "HELP_START_FROM", new { id = "h1", workspace = "alpha" }).WaitAsync(Poll.Patience);

        Assert.Equal(("h2", "alpha"), (answer.GetProperty("sessionId").GetString(), answer.GetProperty("workspace").GetString()));
        Assert.Equal("alpha", service.Workspace("h2"));
        Assert.Equal(["alpha"], service.Asked);

        reading.SetResult(new HelpMachine { Adapter = "talk", Helper = "talk" });
        await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_ENDED"));
    }

    /// <summary>A room that cannot be read is said in the conversation the press placed, which ends, rather than lost with the request.</summary>
    [Fact]
    public async Task A_room_that_cannot_be_read_ends_the_conversation_saying_why()
    {
        var (loop, module, service) = await UpAsync();
        loop.DescribeHelp = _ => Task.FromException<HelpMachine?>(new DriverException("the service did not answer"));

        var answer = await AnswerAsync(module, "START_HELP").WaitAsync(Poll.Patience);

        Assert.Equal("h1", answer.GetProperty("sessionId").GetString());
        await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_ENDED"));
        Assert.Equal("failed", service.State("h1"));
        Assert.Contains("the service did not answer", service.Note("h1"));
    }

    /// <summary>
    /// PARALLEL1a's proof through the module (D181 point 1): the press reads the facts the judges keep as last judged, so Ask Daoris
    /// starts at once while one repository's line never answers. Its room says that repository not read yet and the other's line,
    /// its conversation says first that some of the machine is not read yet, and the start's path starts no git.
    /// </summary>
    [Fact]
    public async Task A_start_reads_the_facts_at_once_saying_what_is_not_read_yet_and_starts_no_git()
    {
        var (loop, module, service) = await UpAsync();
        var folder = Path.GetDirectoryName(DriverConfigPath)!;
        string Root(string name) => Path.Combine(folder, name).Replace('\\', '/');
        service.Registry = $$"""
            [{"repository":"engine","workspace":"default","adopted":true,"root":"{{Root("engine")}}"},
             {"repository":"site","workspace":"default","adopted":true,"root":"{{Root("site")}}"}]
            """;
        // The lines' judge stands in for git: engine's checkout says `main`, site's never answers, and is never cut off at a bound
        // (TESTCLOCK3): a start that waited for its line would never end, where at the rules' 30 s it ended late.
        var never = HelpFacts.LinesRules with { Bound = Timeout.InfiniteTimeSpan };
        loop.Facts.Topic(HelpFacts.Lines, never, (repository, _) => repository == "site"
            ? new TaskCompletionSource<object?>().Task
            : Task.FromResult<object?>(new HelpLineGuess("main")));
        using (ProcessStarts.Within(_ => null)) loop.Facts.Start();
        await loop.Facts.FirstAsync(HelpFacts.Lines, "engine", CancellationToken.None).WaitAsync(Poll.Patience);
        var gits = 0;

        using (ProcessStarts.Within(info =>
               {
                   if (Path.GetFileNameWithoutExtension(info.FileName).Equals("git", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref gits);
                   return null;
               }))
        {
            var answer = await AnswerAsync(module, "START_HELP").WaitAsync(Poll.Patience);
            Assert.Equal("h1", answer.GetProperty("sessionId").GetString());
            await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_ENDED"));
        }

        Assert.Equal(0, gits);
        var room = File.ReadAllText(Path.Combine(HelpRoom.PathOf(loop.Home), "AGENTS.md"));
        Assert.Contains("| `site` | not driven | not read yet |", room);
        Assert.Contains("| `engine` | not driven | `main` (the checkout's own) |", room);
        Assert.Contains(loop.Events.Page("h1").Events, e => e.Parts?.Any(part => part.Code == "help.room-not-read-yet") == true);
    }

    /// <summary>
    /// HELPNEWCHAT1 (the owner's report, 2026-10-24): on the install the room's session look and its trees took 7 to 15 s against
    /// their 500 ms bound. A new conversation never waits on either: with both never answering, or both failing, the press answers
    /// at once and the conversation starts, on the room as the machine's facts hold it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_new_conversation_opens_with_the_room_s_look_slow_or_failing(bool failing)
    {
        var (loop, module, _) = await UpAsync();
        var never = new TaskCompletionSource<object?>();
        Task<object?> Look(string _, CancellationToken __) => failing ? Task.FromException<object?>(new DriverException("the look failed")) : never.Task;
        loop.Facts.Topic(HelpFacts.Sessions, HelpFacts.WholeRules with { Bound = Timeout.InfiniteTimeSpan }, Look);
        loop.Facts.Topic(HelpFacts.Trees, HelpFacts.TreesRules with { Bound = Timeout.InfiniteTimeSpan }, Look);
        using (ProcessStarts.Within(_ => null)) loop.Facts.Start();

        var answer = await AnswerAsync(module, "START_HELP").WaitAsync(Poll.Patience);

        Assert.Equal("h1", answer.GetProperty("sessionId").GetString());
        Assert.True(answer.GetProperty("starting").GetBoolean());
        // Its agent reached: the stand-in spawns nothing, which the conversation says, rather than wait on the look.
        await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_ENDED"));
        Assert.True(File.Exists(Path.Combine(HelpRoom.PathOf(loop.Home), "AGENTS.md")));
        Assert.False(never.Task.IsCompleted);
    }

    /// <summary>
    /// The room's sessions are the facts' judgement of them (PARALLEL1a), which reads the look this shell took a moment ago, the
    /// Sessions list's, rather than read the service again, and says so in its log line; where the one kept is older than the span
    /// it takes a fresh one, and keeps that in turn. Since FACTSPLIT1b what is kept is the look without its trees, which each
    /// repository's part judges with git, saying its own time; neither the look nor a part whose tree is gone starts git.
    /// </summary>
    [Fact]
    public async Task The_room_reads_a_recent_look_and_takes_a_fresh_one_where_none_is()
    {
        using var log = new MachineLog(Home, "driver");
        var (loop, _, _) = await UpAsync(log);
        // A tree this home opened and no longer has: its part asks it, and git is never started for it.
        var tree = Path.Combine(new SessionTrees(loop.Home).TreesRoot, "default", "engine", "s-c9e1d000");
        var ended = new SessionRecord("c9e1d000", "engine", "completed")
        {
            Kind = "chat", Tree = tree, Created = DateTimeOffset.UtcNow.AddHours(-1), Updated = DateTimeOffset.UtcNow.AddMinutes(-30),
        };
        var judged = new Dictionary<string, TreeWork>(StringComparer.OrdinalIgnoreCase) { [SessionGroups.Normal(tree)] = new TreeWork(3, 0) };
        var look = new SessionLook([ended], [], []) { Trees = judged };

        loop.SessionsLooked.Keep(look, DateTimeOffset.UtcNow);
        Assert.Empty(loop.SessionsLooked.Within(HelpRoomLookLog.Recent, DateTimeOffset.UtcNow)!.Trees);
        var gits = 0;
        using (ProcessStarts.Within(info =>
               {
                   if (Path.GetFileNameWithoutExtension(info.FileName).Equals("git", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref gits);
                   return null;
               }))
        {
            loop.Facts.Start();
        }

        await loop.Facts.FirstAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Poll.Patience);
        await loop.Facts.FirstAsync(HelpFacts.Trees, HelpFacts.NoCheckout, CancellationToken.None).WaitAsync(Poll.Patience);
        var read = await loop.DescribeHelp!(CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Contains(read!.Sessions!.Listed, each => each.Id == ended.Id);
        // The kept look's trees are none of the room's: its part judged the tree gone, so nothing waits to review.
        Assert.Equal(0, read.Sessions.Review);
        Assert.Contains(LogLines(Home), line => line.Contains("\"event\":\"help.room.look\"") && line.Contains("\"kept\":true"));
        // FACTSPLIT1b: a part that walked a tree says its own time and how many; HELPNEWCHAT1: a gone tree is asked, holding nothing,
        // and never walked, so this part says no walk at all.
        Assert.DoesNotContain(LogLines(Home), line => line.Contains("\"event\":\"help.room.look\"") && line.Contains("\"treesMs\":")
            && !line.Contains("recordsMs"));

        loop.SessionsLooked.Keep(look, DateTimeOffset.UtcNow - HelpRoomLookLog.Recent - TimeSpan.FromSeconds(1));
        await loop.Facts.JudgedAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Poll.Patience);
        var fresh = await loop.DescribeHelp!(CancellationToken.None);

        Assert.DoesNotContain(fresh!.Sessions!.Listed, each => each.Id == ended.Id);
        // HELPROOM1: a fresh look says where its time went; a kept one took no steps and says none. The look judges no tree itself.
        Assert.Contains(LogLines(Home), line => line.Contains("\"event\":\"help.room.look\"") && line.Contains("\"kept\":false")
            && line.Contains("\"recordsMs\":") && line.Contains("\"questsMs\":") && line.Contains("\"verdictsMs\":")
            && line.Contains("\"marksMs\":") && line.Contains("\"waitingMs\":") && line.Contains("\"treesMs\":") && line.Contains("\"trees\":0"));
        Assert.DoesNotContain(LogLines(Home), line => line.Contains("\"kept\":true") && line.Contains("Ms\":") && line.Contains("recordsMs"));
        Assert.NotNull(loop.SessionsLooked.Within(HelpRoomLookLog.Recent, DateTimeOffset.UtcNow));
        // The look's verdicts are a fresh plan's here, no loop having looked: it, the parts and the rest of the facts start no git.
        await loop.Facts.JudgedAsync(HelpFacts.Trees, HelpFacts.NoCheckout, CancellationToken.None).WaitAsync(Poll.Patience);
        Assert.Equal(0, Volatile.Read(ref gits));
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

        /// <summary>The workspace each open named in its body, in order: null where it named none (ASKSCOPE1a).</summary>
        public List<string?> Asked { get; } = [];

        /// <summary>What <c>/api/registry</c> answers: none registered unless a test says otherwise.</summary>
        public string Registry { get; set; } = "[]";

        public string? State(string id)
        {
            lock (_gate) return Find(id)?["state"]?.GetValue<string>();
        }

        /// <summary>The workspace a record keeps.</summary>
        public string? Workspace(string id)
        {
            lock (_gate) return Find(id)?["workspace"]?.GetValue<string>();
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
                    case ("GET", "/api/registry"):
                        return (HttpStatusCode.OK, Registry);
                    case ("GET", "/api/quests"):
                    case ("GET", "/api/asks"):
                        return (HttpStatusCode.OK, "[]");
                    case ("POST", "/api/sessions/help"):
                    {
                        // One running per workspace, as the ledger holds it (ASKSCOPE1a); none named is `default`'s.
                        var named = JsonNode.Parse(body)!["workspace"]?.GetValue<string>();
                        Asked.Add(named);
                        var workspace = named ?? "default";
                        if (_sessions.FirstOrDefault(s => s["state"]!.GetValue<string>() is "queued" or "starting" or "working"
                                && string.Equals(s["workspace"]?.GetValue<string>() ?? "default", workspace, StringComparison.OrdinalIgnoreCase)) is { } running)
                        {
                            return (HttpStatusCode.Conflict, $$"""{"error":"Ask Daoris `{{running["id"]}}` is running in `{{workspace}}`."}""");
                        }

                        Opened++;
                        var session = new JsonObject
                        {
                            ["id"] = $"h{_sessions.Count + 1}", ["repository"] = HelpRoom.Repository, ["state"] = "queued", ["kind"] = "chat",
                            ["adapter"] = "talk", ["created"] = "2026-10-15T06:42:13Z", ["said"] = new JsonArray(), ["workspace"] = workspace,
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
