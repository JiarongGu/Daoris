using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MSG1b (D137 §2.1–§2.3) through REAL ticks. On the protocol door: a session closes its quest, the person writes to it,
/// and at the next look the same record goes on, its own conversation resumed with every word as its own block, its quest
/// staying as it closed; where the agent's other client holds the conversation, the record goes back to how it ended, its
/// words waiting and marked, and no later look tries them again until more are said. On the native door: words said while
/// the run works go on in its own conversation (<c>--resume</c>) before the record concludes.
/// </summary>
/// <remarks>
/// The stand-in service models MSG1a: words kept on a parked or ended record (<c>say</c>), taken off by their ids
/// (<c>taken</c>), and the ledger's one move out of an ended state, to working with words waiting.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class SessionMessagesTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-msg-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public SessionMessagesTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "msg@example.com");
        Git("config", "user.name", "MSG1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private IReadOnlyList<SessionEvent> Events(string session) => new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events;

    /// <param name="command">What the protocol door runs in place of the stand-in agent: a file that is no program, so its spawn fails.</param>
    /// <param name="goesOnLost">The shell's chat runner's seam a closed quest's lost session goes on through (SESSCTX1c part B); none headless.</param>
    private Daoris.Driver.Driver Protocol(
        SaidStandIn service, string mode, SessionProcesses? processes = null, IReadOnlyList<string>? command = null,
        Func<string, ContinueReason, CancellationToken, Task<StartedFrom>>? goesOnLost = null)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = command ?? ["node", Agent(), Log, mode] },
        };
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: processes ?? new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            GoesOnLost = goesOnLost,
        };
    }

    /// <summary>
    /// 🔴 SESSCTX1c part B (D189 point 3; the steward's choice: a closed quest opens no driven record): a closed quest's session whose
    /// agent no longer has its conversation (<c>resource_not_found</c>) has lost it, so it goes on by itself as a conversation in its tree,
    /// handed to the shell's chat runner, which conversations are; its words are not marked and its conversation says nothing of waiting.
    /// A host with no runner (the headless one) has no conversation to open, so there it waits for the person, marked by the reason, as
    /// before, the page's press ready.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_closed_quests_session_whose_conversation_was_lost_goes_on_as_a_conversation_where_a_runner_is(bool shell)
    {
        await using var service = SaidStandIn.Start(_repository);
        await Protocol(service, "resumes").RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));
        Assert.Equal("w1", await service.SayAsync("s1", "Also log the port."));
        var handed = new List<(string Session, string Why)>();
        Task<StartedFrom> GoesOn(string session, ContinueReason why, CancellationToken _)
        {
            lock (handed) handed.Add((session, why.Code));
            return Task.FromResult(new StartedFrom("c9", true, null, $"`{session}` goes on in conversation `c9`."));
        }

        var look = await Protocol(service, "gone", goesOnLost: shell ? GoesOn : null).RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True((service.State("s1"), service.Status("q1")) == ("completed", "Done"), seen);
        Assert.EndsWith("It cannot go on in this session, because the agent no longer has its conversation.", service.Note("s1"));
        if (shell)
        {
            lock (handed) Assert.Equal([("s1", ContinueWhy.Gone)], handed);
            Assert.Null(new GoOnMarks(_home).Read("s1"));
            Assert.DoesNotContain(Events("s1"), e => e.Why is not null && e.To is null);
            Assert.Contains(look.Events, line => line.Contains("conversation `c9`", StringComparison.Ordinal));
        }
        else
        {
            lock (handed) Assert.Empty(handed);
            Assert.Equal(ContinueWhy.Gone, new GoOnMarks(_home).Read("s1")!.Why);
            Assert.Contains(Events("s1"), e => e.Words is ["w1"] && e.Why == ContinueWhy.Gone);
        }
    }

    /// <summary>
    /// 🔴 The owner's case: a session in *To review* the person writes to goes on in its own row. Its record leaves
    /// `completed` for working, the stub is sent <c>session/resume</c> on the conversation it named with both words as their
    /// own blocks, the words leave the record by their ids, the archive mark goes, and the record ends `completed` again with
    /// its quest still done — one record from start to end.
    /// </summary>
    [Fact]
    public async Task Words_to_a_session_whose_quest_closed_go_on_in_the_same_session()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Protocol(service, "resumes");

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));
        Assert.Equal(new HarnessConversation("acp-stub", "conv-s1"), new HarnessConversations(_home).Read("s1"));

        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions,SessionArchive.FileName), """{"archived":[{"session":"s1","at":"2026-10-03T09:00:00Z"}]}""");
        Assert.Equal("w1", await service.SayAsync("s1", "Also log the port."));
        Assert.Equal("w2", await service.SayAsync("s1", "And use 9090."));
        var before = service.Record("s1");

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        // Said whole on failure: the record before the look, the plan, what the look did, and the record after it.
        var seen = $"before: {before}\nplan: {string.Join(" | ", look.Considerations.Select(c => $"{c.Quest.Id} {c.Verdict}: {c.Reason}"))}"
                   + $"\nlook: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nquest: {service.Status("q1")}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True((service.State("s1"), service.Status("q1")) == ("completed", "Done"), seen);
        Assert.True(service.MovesAfterWords("s1").SequenceEqual(["working", "completed"]), seen);
        Assert.True(service.Said("s1").Count == 0, seen);
        Assert.Equal([("s1", "w1,w2", (string?)null)], service.Taken);

        var heard = Heard();
        Assert.Single(heard, line => line["method"]!.GetValue<string>() == "session/new");
        var resumed = Assert.Single(heard, line => line["method"]!.GetValue<string>() == "session/resume");
        Assert.Equal("conv-s1", resumed["conversation"]!.GetValue<string>());
        var prompt = heard.Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.Equal(["Also log the port.", "And use 9090."], prompt["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));
        Assert.True(prompt["resumed"]!.GetValue<bool>());

        Assert.False(new SessionArchive(_home).Marks().ContainsKey("s1"));
        Assert.Contains(Events("s1"), e => e is { Kind: SessionEventKind.User, Origin: "person", Id: "w1", Text: "Also log the port." });
        Assert.Contains(Events("s1"), e => e is { Kind: SessionEventKind.User, Origin: "person", Id: "w2", Text: "And use 9090." });

        // Nothing waits now, so the next look plans nothing for it.
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");
    }

    /// <summary>
    /// ANSWER2b (D131's ANSWER2 note, "no real resumed run met a second look"), the install's case through real ticks: a session
    /// takes its quest and parks to ask, the person answers through the say door, and the next look resumes its conversation. That
    /// run is still in its turn, its record working with the answer still on it, when another look comes. That look begins nothing
    /// on it: the conversation is resumed once, the record moves to working once, and it ends as its run does, its quest done and
    /// its answer taken off once.
    /// </summary>
    [Fact]
    public async Task An_answered_parks_resumed_run_still_in_its_turn_at_the_next_look_goes_on_once()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Protocol(service, "parks");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("awaiting-person", "Taken"), (service.State("s1"), service.Status("q1")));
        Assert.Equal("w1", await service.SayAsync("s1", "Port 8080."));
        using var closing = new CancellationTokenSource(Poll.Patience);
        int Resumed(string method) => Heard().Count(line => line["method"]!.GetValue<string>() == method && line["resumed"]?.GetValue<bool>() != false);
        string Seen(TickReport look) =>
            $"record: {service.Record("s1")}\nquest: {service.Status("q1")}\nsessions: {service.SessionCount}"
            + $"\nmoves since the words: {string.Join(", ", service.MovesAfterWords("s1"))}\ntaken: {string.Join(" | ", service.Taken)}"
            + $"\nplan: {string.Join(" | ", look.Considerations.Select(c => $"{c.Quest.Id} {c.Verdict}: {c.Reason}"))}"
            + $"\nlook: {string.Join(" | ", look.Events)}\nheard: {string.Join(" | ", Heard().Select(line => line.ToJsonString()))}";

        var first = await driver.TickAsync(closing.Token).WaitAsync(Poll.Patience);
        await Poll.Until(() => Resumed("session/prompt") == 1 && service.State("s1") == "working", () => Seen(first), Poll.Patience);
        var second = await driver.TickAsync(closing.Token).WaitAsync(Poll.Patience);
        Assert.Equal(["w1"], service.Said("s1"));
        File.WriteAllText(Path.Combine(_home, "go.flag"), "");
        await Poll.Until(() => service.State("s1") != "working", () => Seen(second), Poll.Patience);
        await driver.Running.SettledAsync().WaitAsync(Poll.Patience);

        Assert.True(first.Considerations.Any(c => c is { Verdict: StartVerdict.Start, GoesOn: true }), Seen(first));
        Assert.False(second.Considerations.Any(c => c.Verdict == StartVerdict.Start || c.GoesOn), Seen(second));
        Assert.True(service.SessionCount == 1, Seen(second));
        Assert.True((service.State("s1"), service.Status("q1")) == ("completed", "Done"), Seen(second));
        Assert.True(service.MovesAfterWords("s1").SequenceEqual(["working", "completed"]), Seen(second));
        Assert.True(service.Taken.SequenceEqual([("s1", "w1", (string?)null)]), Seen(second));
        Assert.Equal(1, Resumed("session/resume"));
        Assert.Equal(1, Resumed("session/prompt"));
        Assert.DoesNotContain("cannot move", service.Note("s1") ?? "", StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 SAYHELD1 (A), the owner's case on the install (2026-10-21): the quest's session asked another repository and ended waiting
    /// on its answer (D79); the person wrote to it meanwhile, and the words waited with the quest. Once the answer came the quest
    /// resumed in a new session, which was never handed them: they stayed on the first record, unread. Now the resume's session is
    /// handed them beneath the answer, its conversation shows each under its id, and they leave the first record naming it, which
    /// says where they went.
    /// </summary>
    [Fact]
    public async Task Words_said_to_the_session_that_asked_while_its_quest_waits_reach_the_session_its_resume_opens()
    {
        await using var service = SaidStandIn.Start(_repository);
        service.Question();
        await Protocol(service, "asks").RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("completed", "Taken"), (service.State("s1"), service.Status("q1")));
        Assert.Equal("w1", await service.SayAsync("s1", "Use the staging database, not production."));

        // While the question is open, the words wait with the quest and nothing starts.
        await Protocol(service, "resumes").RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(1, service.SessionCount);
        Assert.Equal(["w1"], service.Said("s1"));

        service.Close("q2", "Done", "POST /notes takes { text }.");
        var look = await Protocol(service, "resumes").RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"plan: {string.Join(" | ", look.Considerations.Select(c => $"{c.Quest.Id} {c.Verdict}: {c.Reason}"))}"
                   + $"\nlook: {string.Join(" | ", look.Events)}\ns1: {service.Record("s1")}\ntaken: {string.Join(" | ", service.Taken)}";
        Assert.True(service.SessionCount == 2, seen);
        Assert.True(service.Said("s1").Count == 0, seen);
        Assert.True(service.Taken.SequenceEqual([("s1", "w1", (string?)"s2")]), seen);
        var instruction = string.Join("\n", Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt")["blocks"]!.AsArray()
            .Select(block => block!.GetValue<string>()));
        Assert.Contains("resuming quest `#q1`", instruction);
        Assert.Contains("POST /notes takes { text }.", instruction);
        Assert.Contains("> Use the staging database, not production.", instruction);
        Assert.Contains(Events("s2"), e => e is { Kind: SessionEventKind.User, Origin: "person", Id: "w1", Text: "Use the staging database, not production." });
        Assert.Contains(Events("s1"), e => e is { Kind: SessionEventKind.Note, Words: ["w1"], To: "s2", Why: ContinueWhy.Resumed });
        Assert.Equal("Done", service.Status("q1"));
    }

    /// <summary>
    /// 🔴 SAYHELD1 (B), the owner's case on the install (2026-10-21): the quest resumed in a new session once its question was answered,
    /// and that session parked to ask the person. Their answer sat for hours: the service keeps naming the question while the quest is
    /// taken, so the look read the quest as still waiting on it, planned its resume again, and found the park itself holding the
    /// quest. The answer goes on at the next look in the park's own conversation, and the session ends as its run does.
    /// </summary>
    [Fact]
    public async Task The_persons_answer_to_a_park_its_resume_made_goes_on_though_the_quest_still_names_its_question()
    {
        await using var service = SaidStandIn.Start(_repository);
        service.Question();
        await Protocol(service, "asks").RunOnceAsync().WaitAsync(Poll.Patience);
        service.Close("q2", "Done", "POST /notes takes { text }.");
        // The answered park's turn closes the quest at once.
        File.WriteAllText(Path.Combine(_home, "go.flag"), "");
        await Protocol(service, "parks").RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("awaiting-person", "Taken", 2), (service.State("s2"), service.Status("q1"), service.SessionCount));
        Assert.Equal("w1", await service.SayAsync("s2", "Not production; use staging."));

        var look = await Protocol(service, "parks").RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"plan: {string.Join(" | ", look.Considerations.Select(c => $"{c.Quest.Id} {c.Verdict}: {c.Reason}"))}"
                   + $"\nlook: {string.Join(" | ", look.Events)}\ns2: {service.Record("s2")}\nquest: {service.Status("q1")}";
        Assert.True(look.Considerations.Any(c => c is { Verdict: StartVerdict.Start, GoesOn: true, Resumes.Session: "s2" }), seen);
        Assert.True(service.SessionCount == 2, seen);
        Assert.True((service.State("s2"), service.Status("q1")) == ("completed", "Done"), seen);
        Assert.True(service.Taken.SequenceEqual([("s2", "w1", (string?)null)]), seen);
        var resumed = Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");
        Assert.Equal("conv-s2", resumed["conversation"]!.GetValue<string>());
        var prompt = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.Equal("Not production; use staging.", prompt["blocks"]![0]!.GetValue<string>());
    }

    /// <summary>
    /// The agent's other client holds the conversation (<c>elsewhere</c>): the record goes back to `completed`, saying it
    /// cannot go on and why, its words still waiting as said and marked by their ids, and nothing else carries them on, its
    /// quest having closed. The next look does not try them again; a word said after does.
    /// </summary>
    [Fact]
    public async Task Words_a_closed_quests_session_cannot_go_on_with_wait_and_are_not_tried_again_until_more_are_said()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Protocol(service, "elsewhere");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        await service.SayAsync("s1", "Also log the port.");
        var before = service.Record("s1");

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"before: {before}\nlook: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(service.State("s1") == "completed", seen);
        Assert.EndsWith(
            "It cannot go on in this session, because its conversation is open in another client of its agent.", service.Note("s1"));
        Assert.Equal(["w1"], service.Said("s1"));
        Assert.Equal(new GoOnMark(["w1"], ContinueWhy.Elsewhere, default).Said, new GoOnMarks(_home).Read("s1")!.Said);
        Assert.Equal(ContinueWhy.Elsewhere, new GoOnMarks(_home).Read("s1")!.Why);
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");

        await service.SayAsync("s1", "Try again now.");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(2, Heard().Count(line => line["method"]!.GetValue<string>() == "session/resume"));
    }

    /// <summary>
    /// 🔴 TREEGONE1 (D51, D131; GOONTREE1 before it): a session's tree removed while its branch stands, as a repository's merge tool
    /// did to a driven session, is made again in its place on that branch before the person's words go on, and the record says so.
    /// The words go on in the same record, its harness running in the tree made again; no other tree or branch is made.
    /// </summary>
    [Fact]
    public async Task Words_to_a_session_whose_tree_was_removed_go_on_in_its_tree_made_again_on_its_branch()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Protocol(service, "resumes");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var tree = TreeOf(service, "s1");
        var folders = TreeFolders();
        File.WriteAllText(Path.Combine(tree, "work.txt"), "the session's work\n");
        GitIn(tree, "add", "-A");
        GitIn(tree, "commit", "-qm", "the session's work");
        Git("worktree", "remove", "--force", tree);
        Assert.False(Directory.Exists(tree));
        await service.SayAsync("s1", "Also log the port.");
        var branches = Branches();

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(service.Said("s1").Count == 0, seen);
        Assert.True(Branches().SequenceEqual(branches), seen);
        Assert.True(TreeFolders().SequenceEqual(folders), seen);
        // Its commit checked out again, line endings as this machine's git writes them.
        Assert.Equal("the session's work", File.ReadAllText(Path.Combine(tree, "work.txt")).TrimEnd());
        var prompt = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.True(prompt["resumed"]!.GetValue<bool>(), seen);
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(prompt["cwd"]!.GetValue<string>()), ignoreCase: true);
        var remade = Assert.Single(Events("s1"), e => e.Parts?.Any(part => part.Code == NoteCodes.TreeRemade.Code) == true);
        Assert.Contains($"`daoris/{Path.GetFileName(tree)}`", remade.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// TREEGONE1: a folder left where the tree was, which git no longer lists as one (a removal that could not delete everything),
    /// is no tree to go on in: it is moved aside, never deleted, and the tree is made again in its place on its branch.
    /// </summary>
    [Fact]
    public async Task A_folder_left_where_a_sessions_tree_was_is_moved_aside_and_the_tree_made_again()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Protocol(service, "resumes");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var tree = TreeOf(service, "s1");
        Git("worktree", "remove", "--force", tree);
        Directory.CreateDirectory(Path.Combine(tree, "node_modules"));
        File.WriteAllText(Path.Combine(tree, "node_modules", "left.txt"), "left behind\n");
        await service.SayAsync("s1", "Also log the port.");

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(service.Said("s1").Count == 0, seen);
        Assert.True(File.Exists(Path.Combine(tree, ".git")), seen);
        Assert.False(File.Exists(Path.Combine(tree, "node_modules", "left.txt")), seen);
        var aside = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Trees), ".tidied");
        Assert.Contains(Directory.EnumerateFiles(aside, "left.txt", SearchOption.AllDirectories), _ => true);
        var prompt = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(prompt["cwd"]!.GetValue<string>()), ignoreCase: true);
    }

    /// <summary>
    /// 🔴 TREEGONE1: a session whose tree and branch are both gone is held for the person, and said: no harness starts, neither in
    /// the repository's checkout nor in a fresh tree, the words stay waiting on the record, and no tree or branch is made. Each later
    /// look holds it again without saying it twice.
    /// </summary>
    [Fact]
    public async Task Words_to_a_session_whose_tree_and_branch_are_gone_are_held_and_no_harness_starts()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Protocol(service, "resumes");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var tree = TreeOf(service, "s1");
        Git("worktree", "remove", "--force", tree);
        Git("branch", "-D", $"daoris/{Path.GetFileName(tree)}");
        await service.SayAsync("s1", "Also log the port.");
        var heardBefore = Heard().Length;
        var folders = TreeFolders();

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(Heard().Length == heardBefore, seen);
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(service.Said("s1").SequenceEqual(["w1"]), seen);
        Assert.True(service.MovesAfterWords("s1").Count == 0, seen);
        Assert.Empty(Branches());
        Assert.True(TreeFolders().SequenceEqual(folders), seen);
        Assert.False(Directory.Exists(tree));
        Assert.Contains(look.Events, line => line.Contains("held", StringComparison.Ordinal) && line.Contains("are both gone", StringComparison.Ordinal));
        Assert.Single(Events("s1"), e => e.Parts?.Any(part => part.Code == NoteCodes.TreeGone.Code) == true);
    }

    /// <summary>The tree the record says its session ran in.</summary>
    private static string TreeOf(SaidStandIn service, string session) =>
        JsonNode.Parse(service.Record(session))!["tree"]!.GetValue<string>();

    private string[] Branches()
    {
        var info = new ProcessStartInfo("git", ["branch", "--list", "daoris/*", "--format=%(refname:short)"])
        {
            WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true,
        };
        using var process = Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Order()];
    }

    /// <summary>The worktrees git lists besides the repository's own: a fresh tree shows here from its open.</summary>
    private string[] TreeFolders()
    {
        var info = new ProcessStartInfo("git", ["worktree", "list", "--porcelain"])
        {
            WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true,
        };
        using var process = Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(line => line.StartsWith("worktree ", StringComparison.Ordinal)).Order()];
    }

    /// <summary>
    /// 🔴 A native session never concludes while words are held (D137 §2.1): the person writes while it works, its process
    /// exits, and the driver resumes its own conversation (<c>claude -p &lt;words&gt; --resume &lt;kept id&gt;</c>) under the same
    /// record, which concludes once that run ends with nothing held. The words show at once with their reach, and again under
    /// the same id where the resumed run took them.
    /// </summary>
    [Fact]
    public async Task Words_said_while_a_native_session_works_go_on_in_its_own_conversation_before_it_concludes()
    {
        var profile = DaorisHome.Entry(_home, HomeEntry.AgentHomes, "claude-code", "work");
        Directory.CreateDirectory(profile);
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Harnesses), """{"defaults":{"claude-code":"work"}}""");
        var taken = Path.Combine(_home, "taken.flag");
        var go = Path.Combine(_home, "go.flag");
        await using var service = SaidStandIn.Start(_repository);
        var processes = new SessionProcesses();
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "claude-code",
            TestBound = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", NativeHarness(), Log, taken, go] },
        };
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: processes,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        var tick = driver.RunOnceAsync();
        await Poll.Until(() => File.Exists(taken) && processes.InboxOf("s1") is not null, () => "the first run never took its quest");
        Assert.True(processes.InboxOf("s1")!.Hold(new ChatMessage("Also log the port.", [])));
        File.WriteAllText(go, "");
        await tick.WaitAsync(Poll.Patience);

        var runs = Heard();
        Assert.Equal(2, runs.Length);
        Assert.Null(runs[0]["resume"]);
        Assert.Equal(("native-1", "Also log the port."), (runs[1]["resume"]!.GetValue<string>(), runs[1]["prompt"]!.GetValue<string>()));
        Assert.Equal(1, service.SessionCount);
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));

        var events = Events("s1");
        Assert.Contains(events, e => e is { Kind: SessionEventKind.User, Origin: "person", Id: "said-1", Reaches: "turn-end" });
        Assert.Contains(events, e => e is { Kind: SessionEventKind.User, Origin: "person", Id: "said-1", Reaches: null, Text: "Also log the port." });
        Assert.Null(processes.InboxOf("s1"));
    }

    /// <summary>
    /// TURNFAIL3, the failed door end to end (D137's TURNFAIL1 note): a first run's turn fails (<c>door.failed</c>) while the
    /// person's word, said mid-turn, is held. The record ends `failed` with the word waiting on <c>said</c>, kept through the say
    /// door, and the next look resumes the same conversation carrying it, which takes it off the record.
    /// </summary>
    [Fact]
    public async Task A_word_said_mid_turn_to_a_door_that_fails_waits_on_the_failed_record_and_the_next_tick_carries_it()
    {
        await using var service = SaidStandIn.Start(_repository);
        var processes = new SessionProcesses();
        var driver = Protocol(service, "fails", processes);

        var tick = driver.RunOnceAsync();
        await Poll.Until(
            () => service.Status("q1") == "Taken" && processes.InboxOf("s1") is not null,
            () => "the first run never took its quest");
        Assert.True(processes.InboxOf("s1")!.Hold(new ChatMessage("Also log the port.", [])));
        File.WriteAllText(Path.Combine(_home, "go.flag"), "");
        await tick.WaitAsync(Poll.Patience);

        var first = $"record: {service.Record("s1")}";
        Assert.True(service.State("s1") == "failed", first);
        Assert.True(service.Said("s1").Count == 1, first);
        Assert.Contains("ECONNRESET", service.Note("s1"));
        Assert.Contains(Events("s1"), e => e.Parts?.Any(part => part.Code == NoteCodes.EndedWordsWait.Code) == true);

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(service.Said("s1").Count == 0, seen);
        // The resumed turn ends with its quest still taken, so the record parks asking the person.
        Assert.True(service.State("s1") == "awaiting-person", seen);
        var prompt = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.True(prompt["resumed"]!.GetValue<bool>(), seen);
        Assert.Equal(["Also log the port."], prompt["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));
    }

    /// <summary>
    /// 🔴 SAIDBACK1e (D137 §2.4): a word the person takes back between the look and the ledger's move to working never goes. The
    /// look reads two words, and the take-back lands just before the move, as the ledger orders the two: the resumed run is handed
    /// the one the move left, and only that one is taken off the record. While the move followed the spawn, the prompt was built
    /// from the words read before it, and the word taken back went.
    /// </summary>
    [Fact]
    public async Task A_word_taken_back_between_the_look_and_the_move_never_goes()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Protocol(service, "resumes");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal("w1", await service.SayAsync("s1", "Also log the port."));
        Assert.Equal("w2", await service.SayAsync("s1", "And use 9090."));
        service.TakeBackAtMove("s1", "w1");

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        var prompt = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.True(prompt["resumed"]!.GetValue<bool>(), seen);
        Assert.Equal(["And use 9090."], prompt["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));
        Assert.Equal([("s1", "w2", (string?)null)], service.Taken);
        Assert.True(service.Said("s1").Count == 0, seen);
        Assert.True(service.MovesAfterWords("s1").SequenceEqual(["working", "completed"]), seen);
    }

    /// <summary>
    /// 🔴 SESSCTX1c (D189's amendment, the session-context design §2): a resumed run whose process could not start is a start failure,
    /// never a move. The record the move to working took up ends <c>failed</c> with the spawn's words, as a first start's failed spawn
    /// does, so the strikes count it; its words wait on it to go again, said beside them; no new session opens and nothing is handed
    /// on. Failing again at the next look, its words failed twice running and wait for the person, marked, as a failed turn's do.
    /// </summary>
    [Fact]
    public async Task A_resumed_run_that_could_not_start_fails_as_a_start_and_its_words_wait_on_it()
    {
        await using var service = SaidStandIn.Start(_repository);
        await Protocol(service, "resumes").RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal("w1", await service.SayAsync("s1", "Also log the port."));
        var notAProgram = Path.Combine(_home, "not-a-program.txt");
        File.WriteAllText(notAProgram, "not a program");

        var look = await Protocol(service, "resumes", command: [notAProgram]).RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(service.MovesAfterWords("s1").SequenceEqual(["working", "failed"]), seen);
        Assert.EndsWith("Your words wait to go again at the next look.", service.Note("s1"));
        Assert.True(service.Said("s1").SequenceEqual(["w1"]), seen);
        Assert.Empty(service.Taken);
        Assert.Contains(Events("s1"), e => e.Words is ["w1"] && e.Parts?.Any(part => part.Code == NoteCodes.EndedWordsWait.Code) == true);
        Assert.DoesNotContain(Events("s1"), e => e.Why is not null);
        Assert.Null(new GoOnMarks(_home).Read("s1"));
        Assert.Contains(look.Events, line => line.StartsWith("failed  session s1", StringComparison.Ordinal));

        await Protocol(service, "resumes", command: [notAProgram]).RunOnceAsync().WaitAsync(Poll.Patience);

        seen = $"after: {service.Record("s1")}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.EndsWith("Your words failed twice and wait for you: they go again with the next words you say.", service.Note("s1"));
        Assert.Equal(GoOnMarks.FailedTwice, new GoOnMarks(_home).Read("s1")!.Why);
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/new");
        Assert.DoesNotContain(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");
    }

    /// <summary>
    /// 🔴 SESSCTX1c (D189's amendment): a driven record whose resume another client of its agent refuses (<c>elsewhere</c>) waits for
    /// the person, even with its quest taken, since a second writer in its tree could redo its work: no new session opens, its words
    /// stay waiting and marked, its note says why, and no later look carries its quest on meanwhile. A word said since tries it again.
    /// </summary>
    [Fact]
    public async Task A_taken_quests_record_whose_conversation_another_client_holds_waits_for_the_person()
    {
        await using var service = SaidStandIn.Start(_repository);
        await FailedFirstRunAsync(service);
        Assert.Equal("w1", await service.SayAsync("s1", "Also log the port."));

        var look = await Protocol(service, "elsewhere").RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True((service.State("s1"), service.Status("q1")) == ("failed", "Taken"), seen);
        Assert.EndsWith("It cannot go on in this session, because its conversation is open in another client of its agent.", service.Note("s1"));
        Assert.Equal(["w1"], service.Said("s1"));
        Assert.Equal(ContinueWhy.Elsewhere, new GoOnMarks(_home).Read("s1")!.Why);
        Assert.Contains(Events("s1"), e => e.Words is ["w1"] && e.Why == ContinueWhy.Elsewhere);

        var next = await Protocol(service, "elsewhere").RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(service.SessionCount == 1, $"look: {string.Join(" | ", next.Events)}\nafter: {service.Record("s1")}");
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");

        await service.SayAsync("s1", "Try again now.");
        await Protocol(service, "elsewhere").RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(2, Heard().Count(line => line["method"]!.GetValue<string>() == "session/resume"));
    }

    /// <summary>
    /// 🔴 SESSCTX1c (D189's amendment, the session-context design §2): the native door's harness that ends on a failed exit before it
    /// names its conversation proves nothing of its loss, so the same resume is tried once more; where that one opens it, the words
    /// go on in the same record and its own conversation. Where it ends the same way again, the reason is <c>exited</c>, never
    /// <c>gone</c>, and a closed quest's session waits with its words marked by it.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task A_native_resume_that_ends_before_naming_its_conversation_is_tried_once_more(int exits)
    {
        var profile = DaorisHome.Entry(_home, HomeEntry.AgentHomes, "claude-code", "work");
        Directory.CreateDirectory(profile);
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Harnesses), """{"defaults":{"claude-code":"work"}}""");
        var go = Path.Combine(_home, "go.flag");
        File.WriteAllText(go, "");
        await using var service = SaidStandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "claude-code",
            TestBound = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", NativeHarness(), Log, Path.Combine(_home, "taken.flag"), go] },
        };
        Daoris.Driver.Driver Native() => new(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        await Native().RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));
        Assert.Equal("w1", await service.SayAsync("s1", "Also log the port."));
        File.WriteAllText(Path.Combine(_home, "exits.count"), exits.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var look = await Native().RunOnceAsync().WaitAsync(Poll.Patience);

        var runs = Heard();
        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nruns: {string.Join(" | ", runs.Select(run => run.ToJsonString()))}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(runs.Count(run => run["resume"]?.GetValue<string>() == "native-1") == 2, seen);
        Assert.True(runs.Count(run => run["exited"] is not null) == exits, seen);
        if (exits == 1)
        {
            Assert.True(service.State("s1") == "completed" && service.Said("s1").Count == 0, seen);
            Assert.Equal([("s1", "w1", (string?)null)], service.Taken);
            Assert.StartsWith("Also log the port.", runs[^1]["prompt"]!.GetValue<string>(), StringComparison.Ordinal);
        }
        else
        {
            Assert.True(service.State("s1") == "completed", seen);
            Assert.EndsWith("It cannot go on in this session, because its agent ended before it reopened its conversation.", service.Note("s1"));
            Assert.Equal(["w1"], service.Said("s1"));
            Assert.Equal(ContinueWhy.Exited, new GoOnMarks(_home).Read("s1")!.Why);
            Assert.DoesNotContain(Events("s1"), e => e.Why == ContinueWhy.Gone);
        }
    }

    /// <summary>
    /// A first run whose turn fails with its quest still taken: the record ends <c>failed</c>, and words said to it then wait for a
    /// resume, which a door that refuses it turns into a fallback to a new session (the quest being open to it).
    /// </summary>
    private async Task FailedFirstRunAsync(SaidStandIn service)
    {
        File.WriteAllText(Path.Combine(_home, "go.flag"), "");
        await Protocol(service, "fails").RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(service.State("s1") == "failed", service.Record("s1"));
    }

    /// <summary>
    /// 🔴 SAIDBACK1f (D137 §2.4): a fallback hands its new session the words the record holds now. The resume of the failed
    /// record is refused, so the words go to a new session; one of the two is taken back after the look and after the resume put
    /// the record back, and only the other is handed on and taken off the record by the new session. While the look's words were
    /// handed, the word taken back went too.
    /// </summary>
    [Fact]
    public async Task A_word_taken_back_before_a_fallbacks_open_is_never_handed_to_the_new_session()
    {
        await using var service = SaidStandIn.Start(_repository);
        await FailedFirstRunAsync(service);
        Assert.Equal("w1", await service.SayAsync("s1", "Also log the port."));
        Assert.Equal("w2", await service.SayAsync("s1", "And use 9090."));
        service.TakeBackAtMoveBack("s1", "w1");

        // A refusal on the wire carries a driven record on (SESSCTX1c); another client holding it would hold it instead.
        var look = await Protocol(service, "refuses").RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\ntaken: {string.Join(" | ", service.Taken)}";
        Assert.True(service.SessionCount == 2, seen);
        Assert.True(service.Taken.SequenceEqual([("s1", "w2", (string?)"s2")]), seen);
        Assert.True(service.Said("s1").Count == 0, seen);
    }

    /// <summary>
    /// SAIDBACK1f: every word taken back before the fallback's open, so nothing is sent: no new session opens, no word is taken,
    /// and the fresh tree the look made for the start it will not make is let go.
    /// </summary>
    [Fact]
    public async Task Every_word_taken_back_before_a_fallbacks_open_sends_nothing_and_leaves_no_fresh_tree()
    {
        await using var service = SaidStandIn.Start(_repository);
        await FailedFirstRunAsync(service);
        Assert.Equal("w1", await service.SayAsync("s1", "Also log the port."));
        service.TakeBackAtMoveBack("s1", "w1");
        var branches = Branches();
        var folders = TreeFolders();

        var look = await Protocol(service, "refuses").RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nbranches: {string.Join(",", Branches())}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.Empty(service.Taken);
        Assert.True(Branches().SequenceEqual(branches), seen);
        Assert.True(TreeFolders().SequenceEqual(folders), seen);
        Assert.Contains(look.Events, line => line.Contains("taken back", StringComparison.Ordinal));
    }

    /// <summary>
    /// The protocol door's stand-in: it names its conversation <c>conv-&lt;session&gt;</c>, and on its first prompt takes and
    /// closes the quest. The mode is how it resumes: <c>resumes</c> honours <c>session/resume</c>, and a resumed prompt is
    /// logged with its blocks; <c>elsewhere</c> refuses it as <c>codex-acp</c> does a thread another client holds; <c>refuses</c>
    /// refuses it otherwise, on the wire, proving no loss.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync, existsSync } from 'node:fs';
            const [log, mode] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            // TREEGONE1: each line names the folder the harness runs in, so a test sees where a resume went on.
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session, cwd: process.cwd() }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            let conversation = null;
            let resumed = false;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + session;
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                heard({ method: frame.method, conversation: frame.params?.sessionId });
                if (mode === 'elsewhere') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32600,
                    message: 'This Codex session is in use by another Codex client.', data: { reason: 'thread_active_writer' } } });
                } else if (mode === 'refuses') {
                  // SESSCTX1c: a refusal on the wire that proves no loss and names no other client.
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: the thread could not be loaded' } });
                } else if (mode === 'gone') {
                  // SESSCTX1c part B: the agent no longer has the conversation, which proves it lost.
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32002, message: 'Resource not found: ' + frame.params?.sessionId } });
                } else {
                  conversation = frame.params.sessionId;
                  resumed = true;
                  send({ jsonrpc: '2.0', id: frame.id, result: {} });
                }
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks, resumed });
                if (resumed && mode === 'parks') {
                  // ANSWER2b: the answered park's resumed turn is held until the test writes `go.flag`, then closes the quest.
                  const go = log.replace(/agent\.log$/, 'go.flag');
                  for (let waited = 0; !existsSync(go) && waited < 30000; waited += 50) await new Promise((r) => setTimeout(r, 50));
                  say('Listening on the port you named.');
                  await respond({ action: 'done', reason: 'listening on the port the person named' });
                } else if (resumed) {
                  say('Logged the port, as you said.');
                } else if (mode === 'parks') {
                  // ANSWER2b: it takes its quest and asks, so it parks on the person.
                  await respond({ action: 'take' });
                  say('Which port should it listen on?');
                } else if (mode === 'asks') {
                  // SAYHELD1: it takes its quest and asks another repository, so it ends waiting on the answer (D79).
                  await respond({ action: 'take' });
                  await respond({ action: 'wait', on: 'q2' });
                  say('Asked backend for the contract.');
                } else {
                  await respond({ action: 'take' });
                  if (mode !== 'fails') await respond({ action: 'done', reason: 'served the report' });
                  if (mode === 'fails') {
                    // TURNFAIL3: the door fails the first turn, the quest still taken (a closed quest's run ends completed whatever
                    // the door said), once the test has said its word mid-turn.
                    const go = log.replace(/agent\.log$/, 'go.flag');
                    for (let waited = 0; !existsSync(go) && waited < 30000; waited += 50) await new Promise((r) => setTimeout(r, 50));
                    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'API Error: Connection dropped (ECONNRESET)' } });
                    return;
                  }
                  say('Served.');
                }
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>
    /// A stand-in for Claude Code's native door: it answers its version and its login question; run with a prompt it speaks
    /// <c>stream-json</c>, naming its conversation <c>native-1</c> on its <c>init</c> line. A first run takes the quest, says so
    /// in a file, waits for the test's word to go on, and closes it; a resumed run logs what it was resumed with.
    /// </summary>
    private string NativeHarness()
    {
        var script = Path.Combine(_home, "claude.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
            const [log, taken, go, ...argv] = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn": true}'); process.exit(0); }
            // A run with no prompt is the usage read a start's walk asks its set's accounts first (USAGEPROBE1; POOL1a1's one set):
            // this stub keeps no plan and says nothing, so the account reads as unknown (D57), and it is no run.
            if (!argv.includes('-p')) process.exit(0);
            const after = (flag) => { const at = argv.indexOf(flag); return at < 0 ? null : argv[at + 1]; };
            const prompt = after('-p');
            const resume = after('--resume');
            const url = process.env.DAORIS_SERVICE_URL;
            const line = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            // SESSCTX1c: while `exits.count` beside the log says so, a resume ends on a failed exit before it names its conversation.
            const exits = log.replace(/agent\.log$/, 'exits.count');
            const left = resume && existsSync(exits) ? Number(readFileSync(exits, 'utf8')) : 0;
            if (left > 0) {
              writeFileSync(exits, String(left - 1));
              appendFileSync(log, JSON.stringify({ resume, prompt, exited: true }) + '\n');
              process.exitCode = 1;
            } else {
              appendFileSync(log, JSON.stringify({ resume, prompt: resume ? prompt : null }) + '\n');
              line({ type: 'system', subtype: 'init', session_id: resume ?? 'native-1', cwd: process.cwd(), tools: [], model: 'stub' });
              if (!resume) {
                await respond({ action: 'take' });
                writeFileSync(taken, '');
                for (let waited = 0; !existsSync(go) && waited < 30000; waited += 50) await new Promise((r) => setTimeout(r, 50));
                await respond({ action: 'done', reason: 'served the report' });
              }
              line({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: resume ? 'Logged the port.' : 'Served.' }] } });
              line({ type: 'result', subtype: 'success', is_error: false, result: 'ok' });
            }
            """);
        return script;
    }

    private void Git(params string[] arguments) => GitIn(_repository, arguments);

    private static void GitIn(string folder, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = folder, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    /// <summary>
    /// The service as MSG1a has it, as far as these ticks reach: sessions with their adapter, account, tree, base and take; the
    /// ledger's moves, with the one move out of an ended state to working where words wait; words kept on a parked or ended
    /// record, each with its id, joined as <c>answer</c>; words taken off by their ids; a move into <c>awaiting-person</c>
    /// clearing them.
    /// </summary>
    internal sealed class SaidStandIn : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly List<JsonObject> _sessions = [];
        private readonly Dictionary<string, List<string>> _moves = [];
        private readonly List<JsonObject> _quests;
        private readonly Dictionary<string, string> _takenBackAtMove = [];
        private readonly Dictionary<string, string> _takenBackAtMoveBack = [];
        private int _words;

        public string Url { get; }

        /// <summary>Each of Daoris's own words kept (COWORK1b; LIMITRESUME1b): the session, and its key.</summary>
        public List<(string Session, string? Key)> DaorisSaid { get; } = [];

        /// <summary>Each take of words: the session, the ids joined, and the session that took them where it was another.</summary>
        public List<(string Session, string Ids, string? By)> Taken { get; } = [];

        private SaidStandIn(HttpListener listener, string url, string root)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _root = root;
            _quests =
            [
                new JsonObject
                {
                    ["id"] = "q1", ["from"] = "ask #a1", ["to"] = "engine", ["title"] = "Serve the report",
                    ["body"] = "The report needs a port.", ["status"] = "Open",
                },
            ];
            _serving = ServeAsync();
        }

        public static SaidStandIn Start(string root)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new SaidStandIn(listener, url, root);
        }

        public int SessionCount { get { lock (_sessions) return _sessions.Count; } }

        /// <summary>
        /// SAYHELD1: the question <c>q1</c>'s session asks (D79), <c>q2</c> to <c>backend</c>, which this machine does not hold: open
        /// until <see cref="Close"/>. Served only once called, so the tests that never ask read as they did.
        /// </summary>
        public void Question()
        {
            lock (_sessions)
            {
                _quests.Add(new JsonObject
                {
                    ["id"] = "q2", ["from"] = "engine", ["to"] = "backend", ["title"] = "What does the notes endpoint take?",
                    ["body"] = "We need the contract.", ["status"] = "Open",
                });
            }
        }

        /// <summary>A quest closed by whoever holds it, with its note: the question answered.</summary>
        public void Close(string quest, string status, string note)
        {
            lock (_sessions)
            {
                var closed = _quests.Single(q => q["id"]!.GetValue<string>() == quest);
                closed["status"] = status;
                closed["note"] = note;
            }
        }

        public string State(string id) => Field(id, "state")!;

        /// <summary>The record as the wire answers it, for a failure message.</summary>
        public string Record(string id)
        {
            lock (_sessions) return Wire(Find(id)).ToJsonString();
        }

        public string? Note(string id) => Field(id, "note");

        /// <summary>The permission prompts the record names the setup it runs under by (AUTONOMY1b2), or null for none kept.</summary>
        public string? Permissions(string id)
        {
            lock (_sessions) return Find(id)["access"]?[AccessRules.Permissions]?.GetValue<string>();
        }

        public IReadOnlyList<string> Said(string id)
        {
            lock (_sessions) return [.. Find(id)["said"]!.AsArray().Select(word => word!["id"]!.GetValue<string>())];
        }

        public string Status(string quest)
        {
            lock (_sessions) return _quests.Single(q => q["id"]!.GetValue<string>() == quest)["status"]!.GetValue<string>();
        }

        /// <summary>The states a record moved to since the person first wrote to it.</summary>
        public IReadOnlyList<string> MovesAfterWords(string id)
        {
            lock (_sessions) return [.. _moves.GetValueOrDefault(id) ?? []];
        }

        /// <summary>
        /// Take <paramref name="word"/> back off the record the moment before its next move to working, as the ledger orders a
        /// take-back that lands between the driver's look and its move: the record still parked or ended, so the take-back door
        /// takes it.
        /// </summary>
        public void TakeBackAtMove(string id, string word)
        {
            lock (_sessions) _takenBackAtMove[id] = word;
        }

        /// <summary>
        /// Take <paramref name="word"/> back off the record the moment before its next move out of working: the resume was
        /// tried and put the record back, so the words a fallback hands on were read before this and are now fewer (SAIDBACK1f).
        /// </summary>
        public void TakeBackAtMoveBack(string id, string word)
        {
            lock (_sessions) _takenBackAtMoveBack[id] = word;
        }

        /// <summary>What the person says to a session, through the say door as the page's box will (MSG1d): the word's id.</summary>
        public Task<string> SayAsync(string id, string text) => SayAsync(id, text, null, null);

        /// <summary>Daoris's own word, as its producer says it through the say door: marked as Daoris's under its fact's key (COWORK1b).</summary>
        public async Task<string> SayAsync(string id, string text, string? from, string? key)
        {
            using var http = new HttpClient();
            var said = new JsonObject { ["text"] = text };
            if (from is not null) said["from"] = from;
            if (key is not null) said["key"] = key;
            using var response = await http.PostAsync(
                $"{Url}/api/sessions/{id}/say", new StringContent(said.ToJsonString(), Encoding.UTF8, "application/json"));
            var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.True(response.IsSuccessStatusCode, body.ToJsonString());
            return body["said"]!["id"]!.GetValue<string>();
        }

        private string? Field(string id, string name)
        {
            lock (_sessions) return Find(id)[name]?.GetValue<string>();
        }

        private JsonObject Find(string id) => _sessions.Single(s => s["id"]!.GetValue<string>() == id);

        private static readonly HashSet<string> Live = ["queued", "starting", "working", "awaiting-person"];

        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            ["queued"] = ["starting", "stood-down", "failed", "stopped"],
            ["starting"] = ["working", "stood-down", "failed", "stopped"],
            ["working"] = ["awaiting-person", "completed", "declined", "stood-down", "failed", "stopped"],
            ["awaiting-person"] = ["working", "completed", "declined", "stopped"],
        };

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                // 🔴 One request never ends the serving (AGENTERRORS1, the tick tests' determinism): an answer that throws is a 500 kept in
                // Faults, and a client gone before its answer is written costs that answer alone. Escaping the loop, either left every
                // later request of the case unanswered, a hang its patience said as anything but this.
                (int Status, string Body) answer;
                try
                {
                    answer = Answer(context.Request);
                }
                catch (Exception error)
                {
                    lock (Faults) Faults.Add($"{context.Request.HttpMethod} {context.Request.Url?.AbsolutePath}: {error.GetType().Name}: {error.Message}");
                    answer = (500, System.Text.Json.JsonSerializer.Serialize(new { error = error.Message }));
                }

                try
                {
                    var bytes = Encoding.UTF8.GetBytes(answer.Body);
                    context.Response.StatusCode = answer.Status;
                    context.Response.ContentType = "application/json";
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                }
                catch (Exception error) when (error is HttpListenerException or IOException or ObjectDisposedException or InvalidOperationException)
                {
                    lock (Faults) Faults.Add($"{context.Request.HttpMethod} {context.Request.Url?.AbsolutePath}: its answer was not written: {error.Message}");
                }
            }
        }

        /// <summary>What the stand-in could not answer, or not write, in the order it met them: said by a failing case, empty when none.</summary>
        public List<string> Faults { get; } = [];

        /// <summary>
        /// A record as the wire answers it to this machine: its words waiting, and the person's joined as `answer` (a helper's answer is
        /// never the person's, as the service's <c>Answer</c> reads it).
        /// </summary>
        private static JsonObject Wire(JsonObject session)
        {
            var copy = (JsonObject)session.DeepClone();
            var persons = copy["said"]!.AsArray().Where(word => word!["helper"] is null && word["from"] is null).ToList();
            copy["answer"] = persons.Count == 0 ? null : string.Join("\n\n", persons.Select(word => word!["text"]!.GetValue<string>()));
            return copy;
        }

        /// <summary>
        /// HELPERWAIT1: a helper's record that serves <paramref name="serves"/>, as the host opened it for a pass (the pass itself is the
        /// test's stand-in), so its answer can be handed to the session it serves.
        /// </summary>
        public void Helper(string id, string serves)
        {
            lock (_sessions)
            {
                _sessions.Add(new JsonObject
                {
                    ["id"] = id, ["repository"] = "engine", ["state"] = "completed", ["kind"] = "chat", ["serves"] = serves,
                    ["created"] = DateTimeOffset.UtcNow.ToString("O"), ["said"] = new JsonArray(),
                });
            }
        }

        /// <summary>The states a record moved to, every one, in order (HELPERWAIT1).</summary>
        public IReadOnlyList<string> States(string id)
        {
            lock (_sessions) return [.. _states.GetValueOrDefault(id) ?? []];
        }

        private readonly Dictionary<string, List<string>> _states = [];

        /// <summary>Each helper's answer handed, by the helper's session, with the words as the driver composed them (HELPERWAIT1).</summary>
        public List<(string Helper, string Text)> HelperHands { get; } = [];

        private (int, string) Answer(HttpListenerRequest request)
        {
            var path = request.Url!.AbsolutePath;
            var all = request.QueryString["includeClosed"] == "true";
            JsonObject Body()
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
            }

            static string IdOf(string path, string suffix) => path["/api/sessions/".Length..^suffix.Length];

            lock (_sessions)
            {
                switch (request.HttpMethod, path)
                {
                    case ("GET", "/api/quests"):
                        return (200, new JsonArray([.. _quests
                            .Where(q => all || q["status"]!.GetValue<string>() is "Open" or "Taken")
                            .Select(q => q.DeepClone())]).ToJsonString());

                    case ("GET", "/api/registry"):
                        return (200, new JsonArray(new JsonObject
                        {
                            ["repository"] = "engine", ["adopted"] = true, ["registered"] = true,
                            ["root"] = _root, ["workspace"] = "default",
                        }).ToJsonString());

                    case ("GET", "/api/sessions"):
                        return (200, new JsonArray([.. _sessions
                            .Where(s => all || Live.Contains(s["state"]!.GetValue<string>()))
                            .Select(Wire)]).ToJsonString());

                    case ("POST", "/api/sessions"):
                    {
                        var body = Body();
                        var session = new JsonObject
                        {
                            ["id"] = $"s{_sessions.Count + 1}", ["quest"] = body["quest"]!.GetValue<string>(),
                            ["repository"] = "engine", ["state"] = "queued", ["kind"] = "drive",
                            ["adapter"] = body["adapter"]?.GetValue<string>(),
                            ["harnessVersion"] = body["harnessVersion"]?.GetValue<string>(),
                            ["tree"] = body["tree"]?.GetValue<string>(),
                            ["baseCommit"] = body["baseCommit"]?.GetValue<string>(),
                            ["profile"] = body["profile"]?.GetValue<string>(),
                            ["created"] = DateTimeOffset.UtcNow.AddMinutes(_sessions.Count).ToString("O"),
                            ["said"] = new JsonArray(),
                            // AUTONOMY1a: the setup it started under, as the service keeps it (ACCESSSWAP1b reads it back).
                            ["access"] = body["access"]?.DeepClone(),
                        };
                        _sessions.Add(session);
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "opened" }.ToJsonString());
                    }

                    // AUTONOMY1b2: the setup a resumed record now runs under, kept as the service does.
                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/access", StringComparison.Ordinal):
                    {
                        var session = Find(IdOf(path, "/access"));
                        session["access"] = Body()["access"]?.DeepClone();
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "kept" }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                    {
                        var id = IdOf(path, "/state");
                        var body = Body();
                        var session = Find(id);
                        var from = session["state"]!.GetValue<string>();
                        var to = body["state"]!.GetValue<string>();
                        var note = body["note"]?.GetValue<string>();
                        if (to == "working" && _takenBackAtMove.Remove(id, out var takenBack))
                        {
                            session["said"] = new JsonArray([.. session["said"]!.AsArray()
                                .Where(word => word!["id"]!.GetValue<string>() != takenBack).Select(word => word!.DeepClone())]);
                        }

                        if (from == "working" && to != "working" && _takenBackAtMoveBack.Remove(id, out var takenBackLater))
                        {
                            session["said"] = new JsonArray([.. session["said"]!.AsArray()
                                .Where(word => word!["id"]!.GetValue<string>() != takenBackLater).Select(word => word!.DeepClone())]);
                        }

                        if (!Live.Contains(from))
                        {
                            // MSG1a's one move out of an ended state: to working, with words waiting.
                            if (to != "working" || session["said"]!.AsArray().Count == 0)
                            {
                                return (409, $$"""{"error":"Session `{{id}}` is {{from}} — a finished session does not move."}""");
                            }

                            session["note"] = string.Join("\n\n", new[] { session["note"]?.GetValue<string>(), "Went on with your words at 2026-10-03 09:00 UTC.", note }
                                .Where(part => !string.IsNullOrWhiteSpace(part)));
                        }
                        else if (!Allowed.TryGetValue(from, out var moves) || !moves.Contains(to))
                        {
                            return (409, $$"""{"error":"Session `{{id}}` cannot move {{from}} → {{to}}."}""");
                        }
                        else if (note is not null)
                        {
                            session["note"] = note;
                            // Its lines by code, as the service keeps them beside the note (LANG1a): what a second failed start reads.
                            session["noteParts"] = body["noteParts"]?.DeepClone();
                        }

                        session["state"] = to;
                        session["interrupted"] = to == "stopped" && body["interrupted"]?.GetValue<bool>() == true;
                        // LIMITRESUME1b: the ledger's limit flag, on the failed end that carries it, as the service keeps it.
                        session["limit"] = to == "failed" && body["limit"]?.GetValue<bool>() == true ? true : null;
                        // When it moved, as the service keeps it (HELPERWAIT1: a park made before its helper's ask was asked is not one made on it).
                        session["updated"] = DateTimeOffset.UtcNow.ToString("O");
                        if (to == "awaiting-person") session["said"] = new JsonArray();
                        if (_moves.TryGetValue(id, out var since)) since.Add(to);
                        if (!_states.TryGetValue(id, out var states)) _states[id] = states = [];
                        states.Add(to);
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "moved" }.ToJsonString());
                    }

                    // HELPERWAIT1: a helper's answer, handed to the session its record serves as a word marked `helper`, once, and refused as
                    // the desk refuses it where the asker is parked, running or stood down.
                    case ("POST", _) when path.StartsWith("/api/helpers/", StringComparison.Ordinal) && path.EndsWith("/hand", StringComparison.Ordinal):
                    {
                        var id = path["/api/helpers/".Length..^"/hand".Length];
                        var served = Find(id)["serves"]!.GetValue<string>();
                        var asker = Find(served);
                        var state = asker["state"]!.GetValue<string>();
                        // A park the person answered takes it beside their answer (HELPERWAIT1), as the desk lets it; any other live record waits.
                        var answeredPark = state == "awaiting-person" && Wire(asker)["answer"] is not null;
                        if (Live.Contains(state) && !answeredPark) return (409, $$"""{"error":"Session `{{served}}` is {{state}}. Nothing was kept."}""");
                        if (asker["said"]!.AsArray().Any(word => word!["helper"]?.GetValue<string>() == id)) return (409, """{"error":"it goes once"}""");

                        var text = Body()["text"]!.GetValue<string>();
                        var word = new JsonObject
                        {
                            ["id"] = $"w{++_words}", ["text"] = text, ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["files"] = new JsonArray(),
                            ["reopens"] = true, ["helper"] = id,
                        };
                        asker["said"]!.AsArray().Add(word);
                        HelperHands.Add((id, text));
                        return (200, new JsonObject { ["message"] = "Handed.", ["session"] = Wire(asker), ["said"] = word.DeepClone() }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/say", StringComparison.Ordinal):
                    {
                        var id = IdOf(path, "/say");
                        var session = Find(id);
                        var state = session["state"]!.GetValue<string>();
                        if (Live.Contains(state) && state != "awaiting-person")
                        {
                            return (409, $$"""{"error":"Session `{{id}}` is {{state}}.","refusal":"running"}""");
                        }

                        // COWORK1b: Daoris's own words under its producer's key, kept once by that key (LIMITRESUME1b's limit word).
                        var said = Body();
                        var from = said["from"]?.GetValue<string>();
                        var key = said["key"]?.GetValue<string>();
                        if (key is not null && session["said"]!.AsArray().FirstOrDefault(each => each!["key"]?.GetValue<string>() == key) is { } again)
                        {
                            return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "again", ["said"] = again.DeepClone() }.ToJsonString());
                        }

                        var word = new JsonObject
                        {
                            ["id"] = $"w{++_words}", ["text"] = said["text"]!.GetValue<string>(),
                            ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["files"] = new JsonArray(), ["reopens"] = state != "awaiting-person",
                            ["from"] = from, ["key"] = key,
                        };
                        session["said"]!.AsArray().Add(word);
                        if (from is not null) DaorisSaid.Add((id, key));
                        _moves.TryAdd(id, []);
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "kept", ["said"] = word.DeepClone() }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/taken", StringComparison.Ordinal):
                    {
                        var id = IdOf(path, "/taken");
                        var body = Body();
                        var ids = body["said"]!.AsArray().Select(word => word!.GetValue<string>()).ToList();
                        var session = Find(id);
                        var left = session["said"]!.AsArray().Where(word => !ids.Contains(word!["id"]!.GetValue<string>())).Select(word => word!.DeepClone());
                        session["said"] = new JsonArray([.. left]);
                        Taken.Add((id, string.Join(',', ids), body["by"]?.GetValue<string>()));
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "taken" }.ToJsonString());
                    }

                    case ("POST", "/api/quests/q1/respond"):
                    {
                        var body = Body();
                        var quest = _quests.Single(q => q["id"]!.GetValue<string>() == "q1");
                        switch (body["action"]!.GetValue<string>())
                        {
                            case "take" when quest["status"]!.GetValue<string>() == "Open":
                                quest["status"] = "Taken";
                                foreach (var session in _sessions.Where(s => Live.Contains(s["state"]!.GetValue<string>())))
                                {
                                    session["took"] = true;
                                }

                                break;
                            case "done" when quest["status"]!.GetValue<string>() == "Taken":
                                quest["status"] = "Done";
                                quest["note"] = body["reason"]?.GetValue<string>();
                                break;
                            // SAYHELD1: the taker waits on a question (D79), named while the quest is taken, as the service keeps it.
                            case "wait" when quest["status"]!.GetValue<string>() == "Taken":
                                quest["awaits"] = body["on"]!.GetValue<string>();
                                break;
                            default:
                                return (409, """{"error":"refused"}""");
                        }

                        return (200, new JsonObject { ["message"] = "moved" }.ToJsonString());
                    }

                    default:
                        return (404, $$"""{"error":"the stand-in has no {{request.HttpMethod}} {{path}}"}""");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _serving; } catch (ObjectDisposedException) { }
        }
    }
}
