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
    private Daoris.Driver.Driver Protocol(SaidStandIn service, string mode, SessionProcesses? processes = null, IReadOnlyList<string>? command = null)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = command ?? ["node", Agent(), Log, mode] },
        };
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: processes ?? new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
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
    /// 🔴 GOONTREE1 (D137 §2.2, D51): words waiting on a closed quest's last session whose tree is gone cannot go on, and the
    /// look leaves no fresh tree or branch for the start it never made. The record says its tree is gone and keeps its words.
    /// </summary>
    [Fact]
    public async Task Words_that_cannot_go_on_because_the_tree_is_gone_leave_no_fresh_tree_behind()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Protocol(service, "resumes");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var tree = JsonNode.Parse(service.Record("s1"))!["tree"]?.GetValue<string>();
        if (tree is { Length: > 0 } && Directory.Exists(tree))
        {
            Git("worktree", "remove", "--force", tree);
            if (Directory.Exists(tree)) Directory.Delete(tree, recursive: true);
        }

        Git("worktree", "prune");
        await service.SayAsync("s1", "Also log the port.");
        var branches = Branches();
        var folders = TreeFolders();

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nbranches: {string.Join(",", Branches())}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(Branches().SequenceEqual(branches), seen);
        Assert.True(TreeFolders().SequenceEqual(folders), seen);
        Assert.Equal(["w1"], service.Said("s1"));
        Assert.Equal(ContinueWhy.Tree, new GoOnMarks(_home).Read("s1")!.Why);
    }

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
            TimeoutMinutes = 1,
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
    /// SAIDBACK1e: a record moved to working before its run's spawn goes back as it ended where the spawn fails, its words waiting
    /// on it as said, never `failed` with them. Its quest closed, nothing carries them on, so they wait marked, as words a resume
    /// the agent refused leave there, until more are said.
    /// </summary>
    [Fact]
    public async Task Words_whose_run_could_not_be_spawned_wait_on_the_record_as_it_ended()
    {
        await using var service = SaidStandIn.Start(_repository);
        await Protocol(service, "resumes").RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal("w1", await service.SayAsync("s1", "Also log the port."));
        var ended = (service.State("s1"), service.Note("s1"));
        var notAProgram = Path.Combine(_home, "not-a-program.txt");
        File.WriteAllText(notAProgram, "not a program");

        var look = await Protocol(service, "resumes", command: [notAProgram]).RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}";
        Assert.True(service.MovesAfterWords("s1").SequenceEqual(["working", "completed"]), seen);
        Assert.True((service.State("s1"), service.Note("s1")) == ended, seen);
        Assert.True(service.Said("s1").SequenceEqual(["w1"]), seen);
        Assert.Empty(service.Taken);
        Assert.Contains(Events("s1"), e => e.Words is ["w1"] && e.Why == ContinueWhy.Refused);
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/new");
        Assert.DoesNotContain(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");
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

        var look = await Protocol(service, "elsewhere").RunOnceAsync().WaitAsync(Poll.Patience);

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

        var look = await Protocol(service, "elsewhere").RunOnceAsync().WaitAsync(Poll.Patience);

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
    /// logged with its blocks; <c>elsewhere</c> refuses it as <c>codex-acp</c> does a thread another client holds.
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
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session }) + '\n');
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
            import { appendFileSync, existsSync, writeFileSync } from 'node:fs';
            const [log, taken, go, ...argv] = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn": true}'); process.exit(0); }
            const after = (flag) => { const at = argv.indexOf(flag); return at < 0 ? null : argv[at + 1]; };
            const prompt = after('-p');
            const resume = after('--resume');
            const url = process.env.DAORIS_SERVICE_URL;
            const line = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
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
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
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

        public string State(string id) => Field(id, "state")!;

        /// <summary>The record as the wire answers it, for a failure message.</summary>
        public string Record(string id)
        {
            lock (_sessions) return Wire(Find(id)).ToJsonString();
        }

        public string? Note(string id) => Field(id, "note");

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
        public async Task<string> SayAsync(string id, string text)
        {
            using var http = new HttpClient();
            using var response = await http.PostAsync(
                $"{Url}/api/sessions/{id}/say", new StringContent(new JsonObject { ["text"] = text }.ToJsonString(), Encoding.UTF8, "application/json"));
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

                var (status, body) = Answer(context.Request);
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        /// <summary>A record as the wire answers it to this machine: its words waiting, and them joined as `answer`.</summary>
        private static JsonObject Wire(JsonObject session)
        {
            var copy = (JsonObject)session.DeepClone();
            var said = copy["said"]!.AsArray();
            copy["answer"] = said.Count == 0 ? null : string.Join("\n\n", said.Select(word => word!["text"]!.GetValue<string>()));
            return copy;
        }

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
                        };
                        _sessions.Add(session);
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "opened" }.ToJsonString());
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
                        }

                        session["state"] = to;
                        if (to == "awaiting-person") session["said"] = new JsonArray();
                        if (_moves.TryGetValue(id, out var since)) since.Add(to);
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "moved" }.ToJsonString());
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

                        var word = new JsonObject
                        {
                            ["id"] = $"w{++_words}", ["text"] = Body()["text"]!.GetValue<string>(),
                            ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["files"] = new JsonArray(), ["reopens"] = state != "awaiting-person",
                        };
                        session["said"]!.AsArray().Add(word);
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
