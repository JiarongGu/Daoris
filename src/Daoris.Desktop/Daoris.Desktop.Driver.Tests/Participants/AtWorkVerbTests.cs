using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1a rule 20: <c>daoris session at-work</c>. With no Daoris window running the verb composes the same answer by
/// the same function, issuing no token, so an open child reads <i>not known whether it still runs</i> and never <i>working</i>.
/// </summary>
public sealed class AtWorkVerbTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-atwork-verb-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly StandInLedger _ledger = new();

    public AtWorkVerbTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        _ledger.Publish("q1", "engine");
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static SessionsAsk Read(params string[] words)
    {
        var ask = SessionsCommand.Read(words, out var problem);
        Assert.Null(problem);
        return ask!;
    }

    private SessionsWorld World() => new(_ledger.Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null);

    private void OpenChild(string adapter = "stub")
    {
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "s-1");
        Directory.CreateDirectory(tree);
        _ledger.Driven("s1", "q1", tree, adapter: adapter);
        ChildLineage.Start(_home, "s1", "a000000000000001", "4242 638000000000000000", DateTimeOffset.UnixEpoch);
        ChildLineage.Append(_home, "s1", new JsonObject { ["by"] = "stream", ["event"] = "started", ["agent"] = "a1", ["proc"] = "a000000000000001" }.ToJsonString());
    }

    [Fact]
    public void The_verb_reads_one_session_and_json_or_is_a_usage_problem()
    {
        var plain = Read("at-work", "s1a2b3c4");
        Assert.Equal(("at-work", "s1a2b3c4", false), (plain.Verb, plain.Ids[0], plain.Json));
        Assert.True(Read("at-work", "s1", "--json").Json);

        foreach (var words in new[] { new[] { "at-work" }, ["at-work", "s1", "--x"], ["at-work", "s1", "s2"] })
        {
            Assert.Null(SessionsCommand.Read(words, out var problem));
            Assert.Contains("at-work", problem, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task With_no_window_an_open_child_is_not_known_whether_it_still_runs_and_never_working()
    {
        OpenChild();
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1"), World(), output));

        var text = output.ToString();
        Assert.Contains("a1 · not known whether it still runs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("working with", text, StringComparison.Ordinal);
        Assert.DoesNotContain("a1 · working", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_prints_the_answer_whole_with_the_child_s_state_uncertain()
    {
        OpenChild();
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1", "--json"), World(), output));

        var answer = JsonDocument.Parse(output.ToString()).RootElement.GetProperty("answer");
        Assert.Equal("s1", answer.GetProperty("attended").GetString());
        Assert.Equal("uncertain", answer.GetProperty("subagents").GetProperty("children")[0].GetProperty("state").GetString());
    }

    [Fact]
    public async Task With_no_window_a_codex_child_is_marked_seen_by_calls_as_the_window_marks_it()
    {
        OpenChild("codex-acp");
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1", "--json"), World(), output));

        var subagents = JsonDocument.Parse(output.ToString()).RootElement.GetProperty("answer").GetProperty("subagents");
        Assert.True(subagents.GetProperty("seenByCalls").GetBoolean());
    }

    [Fact]
    public async Task With_no_window_the_entry_s_other_parts_are_read_beside_the_attended_one()
    {
        OpenChild();
        _ledger.Driven("s2", "q1", DaorisHome.Entry(_home, HomeEntry.Trees, "s-1"));
        _ledger.InEntry("s1", "quest:q1/engine");
        _ledger.InEntry("s2", "quest:q1/engine");
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s2", "--json"), World(), output));

        var parts = JsonDocument.Parse(output.ToString()).RootElement.GetProperty("answer").GetProperty("parts");
        Assert.Equal(["s2", "s1"], parts.EnumerateArray().Select(part => part.GetProperty("key").GetString()).ToArray());
    }

    private void AskHelper(string id, string asker, string question)
    {
        var files = new HelperFiles(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(files.PathOf(id, "ask"))!);
        File.WriteAllText(files.PathOf(id, "ask"), new JsonObject
        {
            ["id"] = id, ["asker"] = asker, ["question"] = question, ["agent"] = "codex-acp", ["askedBy"] = "agent",
            ["at"] = Moments.Write(new DateTimeOffset(2026, 11, 4, 16, 29, 0, TimeSpan.Zero)),
        }.ToJsonString());
    }

    /// <summary>
    /// COMMS1i (the comms design §7): the verb is the window's twin. A lead whose agent asked a helper and waits on a quest says what it
    /// waits on and its co-workers, in text and in JSON, from the same read the window's <i>At work</i> and head draw.
    /// </summary>
    [Fact]
    public async Task COMMS1i_the_verb_says_what_the_line_waits_on_and_its_co_workers_in_text_and_json()
    {
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "s-1");
        Directory.CreateDirectory(tree);
        _ledger.Publish("q2", "core");
        _ledger.Driven("s1", "q1", tree, state: "completed");
        _ledger.Awaiting("q1", "q2");
        AskHelper("h7", "s1", "Is the lock held across the import?");
        var text = new StringWriter();
        var json = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1"), World(), text));
        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1", "--json"), World(), json));

        var said = text.ToString();
        Assert.Contains("waiting on: #q2 of core · asked · delivery kept; helper h7 · codex-acp · asked 16:29 · asked · delivery kept", said, StringComparison.Ordinal);
        Assert.Contains("co-workers:\n  helper h7 · codex-acp", said.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("not read:", said, StringComparison.Ordinal);
        var exchanges = JsonDocument.Parse(json.ToString()).RootElement.GetProperty("answer").GetProperty("exchanges");
        Assert.Equal(["helper:h7", "quest:q2"], exchanges.EnumerateArray().Select(each => each.GetProperty("id").GetString()).Order().ToArray());
        Assert.All(exchanges.EnumerateArray(), each => Assert.True(each.GetProperty("holds").GetBoolean()));
    }

    /// <summary>
    /// COMMS1i (wave 2's rest): a reviewer reading the line's work is one of its co-workers, the line waiting on it while it reads, read
    /// from the reviewer's record that serves the line.
    /// </summary>
    [Fact]
    public async Task COMMS1i_the_verb_lists_the_line_s_reviewer_among_its_co_workers()
    {
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "s-1");
        Directory.CreateDirectory(tree);
        _ledger.Driven("s1", "q1", tree, state: "completed");
        _ledger.Working("r1", "engine", tree, serves: "s1", opinion: "o1");
        var text = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1"), World(), text));

        var said = text.ToString().ReplaceLineEndings("\n");
        Assert.Contains("waiting on: reviewer o1 · stub · asked · delivery kept · by r1", said, StringComparison.Ordinal);
        Assert.Contains("co-workers:\n  reviewer o1 · stub", said, StringComparison.Ordinal);
    }

    /// <summary>COMMS1i: the composition takes the exchanges from its port, a quest's worded with its lanes, and says a source not read.</summary>
    [Fact]
    public void COMMS1i_the_composition_carries_the_line_s_exchanges_and_its_unread_sources()
    {
        var routing = new AtWorkRouting(
            [], _ => null, id => id == "q2" ? new AtWorkQuest("q2", "Open") { To = "core", Lanes = "docs" } : null, _ => 0, new SliceHealth(true))
        {
            Exchanging = _ => new AtWorkExchanges([new AtWorkExchange("quest:q2", "s1", "work", "core", "kept", "asked", true)], ["helper"]),
        };

        var answer = AtWork.Compose(
            "s1", null, id => new AtWorkRecord(id, "completed"), _ => LineageReading.None, new SliceHealth(true), new SliceHealth(true), routing).Answer!;

        Assert.Equal(["docs"], Assert.Single(answer.Exchanges!).Lanes);
        Assert.Equal(["helper"], answer.Unread);
        Assert.Null(AtWork.Compose("s1", null, id => new AtWorkRecord(id, "completed"), _ => LineageReading.None, new SliceHealth(true), new SliceHealth(true),
            routing with { Exchanging = _ => AtWorkExchanges.None }).Answer!.Exchanges);
    }

    [Fact]
    public async Task With_no_window_a_lead_that_waits_on_a_question_says_so_and_its_lane_part_says_how_it_joined()
    {
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "s-1");
        Directory.CreateDirectory(tree);
        _ledger.Publish("q2", "core");
        _ledger.Driven("s1", "q1", tree, state: "completed");
        _ledger.Awaiting("q1", "q2");
        _ledger.InEntry("s1", "quest:q1/engine");
        var text = new StringWriter();
        var json = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1"), World(), text));
        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1", "--json"), World(), json));

        Assert.Contains("s1 (attended) · completed · waits on #q2, asked of core", text.ToString(), StringComparison.Ordinal);
        var part = JsonDocument.Parse(json.ToString()).RootElement.GetProperty("answer").GetProperty("parts")[0];
        Assert.Equal("q2", part.GetProperty("waits").GetProperty("quest").GetString());
        Assert.Equal("core", part.GetProperty("waits").GetProperty("of").GetString());
        Assert.Equal(1, part.GetProperty("number").GetInt32());
        Assert.Equal("opened", part.GetProperty("joined").GetProperty("began").GetString());
    }

    [Fact]
    public async Task With_no_window_a_chat_that_took_a_quest_says_it_took_it_and_what_it_waits_on()
    {
        _ledger.Publish("q2", "core");
        _ledger.Working("c1", "engine", Path.Combine(_home, "engine"));
        _ledger.Took("c1", "q1");
        _ledger.Awaiting("q1", "q2");
        var text = new StringWriter();
        var json = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "c1"), World(), text));
        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "c1", "--json"), World(), json));

        Assert.Contains("c1 (attended) · working · waits on #q2, asked of core · took #q1", text.ToString(), StringComparison.Ordinal);
        var part = JsonDocument.Parse(json.ToString()).RootElement.GetProperty("answer").GetProperty("parts")[0];
        Assert.True(part.GetProperty("joined").GetProperty("took").GetBoolean());
        Assert.Equal("q1", part.GetProperty("joined").GetProperty("quest").GetString());
        Assert.Equal("q2", part.GetProperty("waits").GetProperty("quest").GetString());
    }

    [Fact]
    public async Task With_no_window_a_session_that_published_and_waits_on_none_says_so()
    {
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "s-1");
        Directory.CreateDirectory(tree);
        _ledger.Driven("s1", "q1", tree, state: "completed");
        _ledger.Publish("q3", "engine");
        _ledger.PublishedBy("q3", "s1");
        _ledger.InEntry("s1", "quest:q1/engine");
        var text = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1"), World(), text));

        Assert.Contains("published 1, waits on none", text.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_window_the_quests_a_chat_published_say_who_took_each_in_text_and_json_alike()
    {
        _ledger.Publish("q3", "core");
        _ledger.PublishedBy("q3", "c1");
        _ledger.Publish("q4", "engine", title: "The second");
        _ledger.PublishedBy("q4", "c1");
        _ledger.Publish("q5", "engine", parent: "q4");
        _ledger.Working("c1", "engine", Path.Combine(_home, "engine"));
        _ledger.Working("c2", "engine", Path.Combine(_home, "engine"));
        _ledger.Took("c2", "q4");
        var text = new StringWriter();
        var json = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "c1"), World(), text));
        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "c1", "--json"), World(), json));

        var lines = text.ToString();
        Assert.Contains("#q3 · The work of #q3 · of core · not taken yet", lines, StringComparison.Ordinal);
        Assert.Contains("#q4 · The second · taken by conversation c2", lines, StringComparison.Ordinal);
        Assert.Contains("#q5 · The work of #q5 · the next step of #q4 · not taken yet", lines, StringComparison.Ordinal);
        var quests = JsonDocument.Parse(json.ToString()).RootElement.GetProperty("answer").GetProperty("quests");
        Assert.Equal(["q3", "q4", "q5"], quests.EnumerateArray().Select(quest => quest.GetProperty("id").GetString()).Order().ToArray());
        var taken = quests.EnumerateArray().Single(quest => quest.GetProperty("id").GetString() == "q4");
        Assert.Equal("c2", taken.GetProperty("taker").GetProperty("conversation").GetString());
        var step = quests.EnumerateArray().Single(quest => quest.GetProperty("id").GetString() == "q5");
        Assert.Equal("q4", step.GetProperty("step").GetString());
        Assert.True(step.GetProperty("taker").GetProperty("untaken").GetBoolean());
    }

    [Fact]
    public async Task With_no_window_the_quest_a_session_serves_says_what_asked_it()
    {
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, "s-1");
        Directory.CreateDirectory(tree);
        _ledger.PublishedBy("q1", "other");
        _ledger.Driven("s1", "q1", tree);
        _ledger.InEntry("s1", "quest:q1/engine");
        var text = new StringWriter();
        var json = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1"), World(), text));
        Assert.Equal(0, await SessionsCommand.RunAsync(Read("at-work", "s1", "--json"), World(), json));

        Assert.Contains("asked by: #q1 · published by other", text.ToString(), StringComparison.Ordinal);
        var asker = JsonDocument.Parse(json.ToString()).RootElement.GetProperty("answer").GetProperty("askedBy");
        Assert.Equal(("q1", "other"), (asker.GetProperty("quest").GetString(), asker.GetProperty("by").GetProperty("session").GetString()));
    }

    private static int Asked(InstanceStandIn standIn) =>
        standIn.Presses.Count(press => press.GetProperty("type").GetString() == "SESSION_AT_WORK");

    [Fact]
    public async Task A_window_that_stays_pending_is_asked_once_a_second_and_at_the_bound_is_said_not_read()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new { pending = true, waits = "slices" };
        var clock = new MovedClock();
        var output = new StringWriter();
        var run = SessionsCommand.RunAsync(Read("at-work", "s1"), World() with { Instance = home.World("sessions"), Clock = clock }, output);

        var seconds = 0;
        while (!run.IsCompleted)
        {
            var patience = Stopwatch.StartNew();
            while (!clock.Waiting && !run.IsCompleted && patience.Elapsed < TimeSpan.FromSeconds(20)) await Task.Delay(2);
            if (run.IsCompleted) break;
            Assert.Equal(seconds + 1, Asked(standIn));
            clock.Advance(TimeSpan.FromSeconds(1));
            seconds++;
        }

        Assert.Equal(2, await run);
        Assert.Equal(30, seconds);
        Assert.Equal(31, Asked(standIn));
        Assert.Equal("", output.ToString());
        Assert.Contains("not read yet: this window has not judged its sessions", home.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_window_that_answers_after_pending_has_its_answer_printed()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        var asks = 0;
        standIn.Respond = (type, _) => type != "SESSION_AT_WORK" ? null : Interlocked.Increment(ref asks) == 1
            ? new { pending = true, waits = "slices" }
            : new
            {
                answer = new
                {
                    of = new { session = "s1" }, attended = "s1",
                    parts = new[] { new { key = "s1", state = "working", children = new { count = 0, working = 0 } } },
                    subagents = new { read = "read", working = 0, children = Array.Empty<object>() },
                },
            };
        var clock = new MovedClock();
        var output = new StringWriter();
        var run = SessionsCommand.RunAsync(Read("at-work", "s1"), World() with { Instance = home.World("sessions"), Clock = clock }, output);

        await clock.Timed.WaitAsync(Poll.Patience);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(0, await run.WaitAsync(Poll.Patience));
        Assert.Contains("s1 (attended) · working", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("subagents: 0 working", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, Asked(standIn));
    }

    [Fact]
    public async Task A_session_the_records_do_not_hold_is_said_so_and_exits_two()
    {
        var output = new StringWriter();
        var error = Console.Error;
        var said = new StringWriter();
        Console.SetError(said);
        try
        {
            Assert.Equal(2, await SessionsCommand.RunAsync(Read("at-work", "s9"), World(), output));
        }
        finally
        {
            Console.SetError(error);
        }

        Assert.Equal("", output.ToString());
        Assert.Contains("no session s9 is held here", said.ToString(), StringComparison.Ordinal);
    }
}
