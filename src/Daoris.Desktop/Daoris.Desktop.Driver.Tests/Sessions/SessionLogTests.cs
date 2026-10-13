using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the person runs, and how long each part of it takes, in the machine log (LOG1b, D94): the service
/// client says when a session record opens and moves, the conversation record says when its events land,
/// and the watcher turns the two into lines, never with anyone's words.
/// </summary>
/// <remarks>
/// Driven through the real client over a stand-in ledger and the real conversation record, so what is
/// asserted is what the shell's loop would write; the clock is the test's.
/// </remarks>
public sealed class SessionLogTests : IDisposable
{
    private const string Ledger = "http://ledger.test";

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-sessionlog-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = new(2026, 9, 30, 7, 10, 0, TimeSpan.Zero);

    private readonly StandInLedger _ledger = new();

    public SessionLogTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed record Watching(ServiceClient Client, SessionEvents Events, MachineLog Log, SessionLog Watch) : IDisposable
    {
        public void Dispose()
        {
            Watch.Dispose();
            Log.Dispose();
            Client.Dispose();
        }
    }

    private Watching Watch()
    {
        var client = new ServiceClient(Ledger, null, new HttpClient(_ledger));
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var log = new MachineLog(_home, "desktop", () => _now);
        return new Watching(client, events, log, new SessionLog(log, client, events, () => _now));
    }

    /// <summary>Every line written so far, parsed.</summary>
    private List<JsonElement> Lines()
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder).OrderBy(path => path, StringComparer.Ordinal)
                .SelectMany(StubFile.Lines)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone()),
        ];
    }

    private string Raw() => string.Join("\n", Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).Select(StubFile.Text));

    private List<JsonElement> Named(string @event) =>
        [.. Lines().Where(line => line.GetProperty("event").GetString() == @event)];

    private static JsonElement Data(JsonElement line) => line.GetProperty("data");

    private void Said(SessionEvents events, string session, SessionEvent e, double atSeconds) =>
        events.Append(session, e with { At = _now.AddSeconds(atSeconds) });

    [Fact]
    public async Task A_session_opened_and_ended_is_two_lines_and_the_end_says_how_long_it_ran()
    {
        using var w = Watch();

        var (id, _) = await w.Client.OpenChatAsync("engine", "claude-code-acp");
        await w.Client.AdvanceAsync(id!, "starting");
        await w.Client.AdvanceAsync(id!, "working");
        _now = _now.AddSeconds(90.7);
        await w.Client.AdvanceAsync(id!, "completed", note: "the conversation ended");

        var started = Data(Assert.Single(Named("session.started")));
        Assert.Equal(id, started.GetProperty("session").GetString());
        Assert.Equal("chat", started.GetProperty("kind").GetString());
        Assert.Equal("claude-code-acp", started.GetProperty("adapter").GetString());
        Assert.Equal("engine", started.GetProperty("repository").GetString());

        // A move that is not an ending is no line: `starting` and `working` are the ledger's business.
        var ended = Data(Assert.Single(Named("session.ended")));
        Assert.Equal(id, ended.GetProperty("session").GetString());
        Assert.Equal("completed", ended.GetProperty("state").GetString());
        Assert.Equal(90, ended.GetProperty("seconds").GetInt64());
        Assert.DoesNotContain("the conversation ended", Raw());
    }

    /// <summary>Which door opened it is the kind: the ledger's record calls an intake and Ask Daoris a chat.</summary>
    [Theory]
    [InlineData("driven", "engine")]
    [InlineData("chat", "game")]
    [InlineData("intake", "daoris:intake")]
    [InlineData("help", "daoris:help")]
    public async Task Each_door_names_its_kind_and_the_repository_the_ledger_put_it_in(string kind, string repository)
    {
        using var w = Watch();

        var (id, _) = kind switch
        {
            // A driven session names its quest; the ledger answers with the quest's receiver.
            "driven" => await w.Client.OpenSessionAsync("q1", "claude-code"),
            "chat" => await w.Client.OpenChatAsync("game", "claude-code"),
            "intake" => await w.Client.OpenIntakeAsync("a1", "claude-code", "room"),
            _ => await w.Client.OpenHelpAsync("claude-code", "room"),
        };

        var started = Data(Assert.Single(Named("session.started")));
        Assert.Equal(id, started.GetProperty("session").GetString());
        Assert.Equal(kind, started.GetProperty("kind").GetString());
        Assert.Equal(repository, started.GetProperty("repository").GetString());
    }

    /// <summary>
    /// The three waits a person sits through, each timed from the conversation record's own stamps: the
    /// open to the first prompt handed over, a prompt to the first thing back, and a prompt to its turn's end.
    /// </summary>
    [Fact]
    public async Task The_open_the_first_answer_and_each_turn_are_timed_and_nobody_s_words_are_written()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenChatAsync("engine", "claude-code-acp");
        var s = id!;

        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "rename the secret module" }, 5.84);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Usage, Used = 10, Size = 100 }, 6);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Thought, Text = "the agent's private reasoning" }, 7);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Message, Text = "the agent's answer" }, 8);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" }, 20);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "and the second question" }, 30);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "t1", Title = "Read a private file", Input = "{\"path\":\"secret.txt\"}" }, 31.5);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "cancelled" }, 35);

        var opened = Data(Assert.Single(Named("session.opened")));
        Assert.Equal(s, opened.GetProperty("session").GetString());
        Assert.Equal("claude-code-acp", opened.GetProperty("adapter").GetString());
        Assert.Equal(5840, opened.GetProperty("openMs").GetInt64());

        var answered = Named("turn.answered").Select(Data).ToList();
        Assert.Equal([1160L, 1500L], answered.Select(line => line.GetProperty("firstAnswerMs").GetInt64()));
        Assert.All(answered, line => Assert.Equal(s, line.GetProperty("session").GetString()));

        var ended = Named("turn.ended").Select(Data).ToList();
        Assert.Equal(["end_turn", "cancelled"], ended.Select(line => line.GetProperty("stopReason").GetString()));
        Assert.Equal([14160L, 5000L], ended.Select(line => line.GetProperty("turnMs").GetInt64()));

        var raw = Raw();
        foreach (var words in new[] { "rename the secret module", "private reasoning", "the agent's answer", "second question", "Read a private file", "secret.txt" })
        {
            Assert.DoesNotContain(words, raw);
        }
    }

    /// <summary>
    /// A prompt the driver sends as two lines of the record (its target, then the person's answer to a
    /// parked session) is one turn, timed from the first.
    /// </summary>
    [Fact]
    public async Task A_turn_that_opens_with_two_prompts_is_timed_from_the_first()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code-acp");
        var s = id!;

        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "the composed target" }, 2);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "carry on" }, 2.5);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Message, Text = "on it" }, 4);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" }, 12);

        Assert.Equal(2000, Data(Assert.Single(Named("session.opened"))).GetProperty("openMs").GetInt64());
        Assert.Equal(2000, Data(Assert.Single(Named("turn.answered"))).GetProperty("firstAnswerMs").GetInt64());
        Assert.Equal(10000, Data(Assert.Single(Named("turn.ended"))).GetProperty("turnMs").GetInt64());
    }

    /// <summary>
    /// A session this process never saw open (one a previous run left, swept as an orphan) still ends in
    /// the log, and says it cannot say how long it ran: absent is never zero.
    /// </summary>
    [Fact]
    public async Task A_session_whose_open_was_not_seen_ends_with_no_seconds_and_opens_no_wait()
    {
        using var w = Watch();

        Said(w.Events, "orphan1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "hello" }, 1);
        Said(w.Events, "orphan1", new SessionEvent { Kind = SessionEventKind.Message, Text = "hi" }, 3);
        await w.Client.AdvanceAsync("orphan1", "stopped", note: "swept");

        Assert.Empty(Named("session.opened"));
        Assert.Equal(2000, Data(Assert.Single(Named("turn.answered"))).GetProperty("firstAnswerMs").GetInt64());
        var ended = Data(Assert.Single(Named("session.ended")));
        Assert.Equal("stopped", ended.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, ended.GetProperty("seconds").ValueKind);
    }

    /// <summary>A turn's end with no prompt seen before it says how it ended, and not how long it took.</summary>
    [Fact]
    public async Task A_turn_end_with_no_prompt_before_it_has_no_duration()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenChatAsync("engine", "claude-code");

        Said(w.Events, id!, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" }, 4);

        var ended = Data(Assert.Single(Named("turn.ended")));
        Assert.Equal("error", ended.GetProperty("stopReason").GetString());
        Assert.Equal(JsonValueKind.Null, ended.GetProperty("turnMs").ValueKind);
    }

    /// <summary>
    /// UNBLOCK5 (D122 §3.10): a call refused on the protocol door is one ask, as the record says it. The
    /// refusal arrives as an update to the call (HELP4's <c>refused</c>), carrying no kind, so the kind is the
    /// one the call was announced with. The same call refused again is still one ask, a call that merely
    /// failed is none, and neither the call's title nor its input reaches the log.
    /// </summary>
    [Fact]
    public async Task A_refused_call_is_one_ask_with_its_kind_and_never_its_words()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenChatAsync("engine", "claude-code-acp");
        var s = id!;

        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "ship it" }, 1);
        Said(w.Events, s, new SessionEvent
        {
            Kind = SessionEventKind.Tool, Id = "c9", Title = "git push origin release", ToolKind = "execute",
            Status = "pending", Input = "{\"command\":\"git push origin release\"}",
        }, 2);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Note, Id = "c9", Text = "permission refused: `git push origin release`" }, 2.1);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "c9", Status = "refused" }, 2.2);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "c9", Status = "refused", Output = "denied" }, 2.3);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "c10", Title = "cargo build", ToolKind = "execute", Status = "pending" }, 3);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "c10", Status = "failed" }, 4);

        var refused = Data(Assert.Single(Named("permission.refused")));
        Assert.Equal(s, refused.GetProperty("session").GetString());
        Assert.Equal("claude-code-acp", refused.GetProperty("adapter").GetString());
        Assert.Equal("execute", refused.GetProperty("kind").GetString());
        // The protocol door's wire names a call's kind and title, never its tool, nor what decided it.
        Assert.Equal(JsonValueKind.Null, refused.GetProperty("tool").ValueKind);
        Assert.Equal(JsonValueKind.Null, refused.GetProperty("by").ValueKind);

        var raw = Raw();
        foreach (var words in new[] { "git push", "origin release", "cargo build", "ship it", "denied" })
        {
            Assert.DoesNotContain(words, raw);
        }
    }

    /// <summary>
    /// The native door's refusal names its tool and what decided it, in the wire's own words (UNBLOCK5), and a
    /// refused call the record never announced is still an ask, of a kind nobody said.
    /// </summary>
    [Fact]
    public async Task The_native_door_s_refusal_names_its_tool_and_what_decided_it()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code");
        var s = id!;

        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "toolu_7", Status = "refused", ToolKind = "execute", ToolName = "Bash", RefusedBy = "rule" }, 1);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "toolu_8", Status = "refused", ToolName = "mcp__daoris-knowledge__quest_publish", RefusedBy = "classifier" }, 2);

        var lines = Named("permission.refused").Select(Data).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(("claude-code", "Bash", "execute", "rule"), (
            lines[0].GetProperty("adapter").GetString(), lines[0].GetProperty("tool").GetString(),
            lines[0].GetProperty("kind").GetString(), lines[0].GetProperty("by").GetString()));
        Assert.Equal("mcp__daoris-knowledge__quest_publish", lines[1].GetProperty("tool").GetString());
        Assert.Equal(JsonValueKind.Null, lines[1].GetProperty("kind").ValueKind);
        Assert.Equal("classifier", lines[1].GetProperty("by").GetString());
    }

    /// <summary>
    /// 🔴 The line never carries words (D94 §5): a tool's name or a decider that is not an identifier is
    /// written as null, whatever a wire put there. A session whose open this process never saw has no adapter.
    /// </summary>
    [Fact]
    public void A_name_that_is_not_an_identifier_is_written_as_null()
    {
        using var w = Watch();

        Said(w.Events, "orphan2", new SessionEvent
        {
            Kind = SessionEventKind.Tool, Id = "t1", Status = "refused",
            ToolName = "Bash(git push --force)", RefusedBy = "the person said no", ToolKind = "run a command",
        }, 1);

        var refused = Data(Assert.Single(Named("permission.refused")));
        Assert.Equal("orphan2", refused.GetProperty("session").GetString());
        foreach (var field in new[] { "adapter", "tool", "kind", "by" })
        {
            Assert.Equal(JsonValueKind.Null, refused.GetProperty(field).ValueKind);
        }

        var raw = Raw();
        Assert.DoesNotContain("git push", raw);
        Assert.DoesNotContain("said no", raw);
    }

    /// <summary>A parked session the person answered ends `completed`, through the answer door.</summary>
    [Fact]
    public async Task An_answered_session_ends_in_the_log()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code");
        _now = _now.AddSeconds(42);

        var (ok, _) = await w.Client.AnswerSessionAsync(id!, "yes, the second option");

        Assert.True(ok);
        var ended = Data(Assert.Single(Named("session.ended")));
        Assert.Equal("completed", ended.GetProperty("state").GetString());
        Assert.Equal(42, ended.GetProperty("seconds").GetInt64());
        Assert.DoesNotContain("second option", Raw());
    }

    /// <summary>
    /// WSSETUP11 (D124 §7.3): a session that stopped to ask the person (D83) is the owner's complaint, counted.
    /// One line per park, naming the door, the repository and the workspace the record ran in, and never what
    /// the session asked. A park is no ending: the record ends when the person answers.
    /// </summary>
    [Fact]
    public async Task A_session_that_parks_is_one_line_naming_its_kind_repository_and_workspace_and_never_its_words()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code-acp");
        await w.Client.AdvanceAsync(id!, "working");
        await w.Client.AdvanceAsync(id!, "awaiting-person", note: "It stopped to ask you: which report is the daily one?");

        var parked = Data(Assert.Single(Named("session.parked")));
        Assert.Equal(id, parked.GetProperty("session").GetString());
        Assert.Equal("driven", parked.GetProperty("kind").GetString());
        Assert.Equal("engine", parked.GetProperty("repository").GetString());
        Assert.Equal("work", parked.GetProperty("workspace").GetString());
        Assert.Empty(Named("session.ended"));
        Assert.DoesNotContain("daily one", Raw());

        await w.Client.AnswerSessionAsync(id!, "the one the plant reads");

        Assert.Equal("completed", Data(Assert.Single(Named("session.ended"))).GetProperty("state").GetString());
        Assert.Single(Named("session.parked"));
        Assert.DoesNotContain("plant reads", Raw());
    }

    /// <summary>
    /// ANSWER1b (D131 §5): an answer that keeps the record parked moves nothing. It is no ending, and no second park:
    /// the record ends when its resumed conversation does, or when the driver carries the answer on in a new session.
    /// </summary>
    [Fact]
    public async Task An_answer_that_keeps_its_park_is_neither_an_ending_nor_a_second_park()
    {
        using var w = Watch();
        _ledger.AnswerKeepsPark = true;
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code");
        await w.Client.AdvanceAsync(id!, "awaiting-person");

        var (ok, _) = await w.Client.AnswerSessionAsync(id!, "the one the plant reads");

        Assert.True(ok);
        Assert.Single(Named("session.parked"));
        Assert.Empty(Named("session.ended"));
    }

    /// <summary>
    /// <c>session.answered</c> (ANSWER1a, D131 §2): written as the driver gives it, whether the answer resumed its own
    /// conversation and why not by a code, and never the answer.
    /// </summary>
    [Fact]
    public void An_answer_taken_up_is_one_line_saying_whether_it_resumed_and_why_not()
    {
        using var w = Watch();

        w.Client.AccountSaid(Continuations.Answered("s1", "claude-code-acp", ContinueWhy.Of(ContinueWhy.Gone)));
        w.Client.AccountSaid(Continuations.Answered("s2", "claude-code-acp", why: null));

        var lines = Named("session.answered").Select(Data).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(("s1", false, "gone"),
            (lines[0].GetProperty("session").GetString(), lines[0].GetProperty("resumed").GetBoolean(), lines[0].GetProperty("why").GetString()));
        Assert.Equal((true, JsonValueKind.Null), (lines[1].GetProperty("resumed").GetBoolean(), lines[1].GetProperty("why").ValueKind));
    }

    /// <summary>
    /// <c>session.reopened</c> (D137 §3.3): written as the driver gives it, from which state, whether its own conversation
    /// resumed, why not by a code, and the door the words were said at (MSG1d), null where none was kept. Never the words.
    /// </summary>
    [Fact]
    public void A_reopen_taken_up_is_one_line_naming_the_door_its_words_were_said_at()
    {
        using var w = Watch();

        w.Client.AccountSaid(Continuations.Reopened("s1", "claude-code-acp", "completed", why: null, door: "screen"));
        w.Client.AccountSaid(Continuations.Reopened("s2", "claude-code-acp", "failed", ContinueWhy.Of(ContinueWhy.Tree), door: null));

        var lines = Named("session.reopened").Select(Data).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(("s1", "completed", true, "screen"), (
            lines[0].GetProperty("session").GetString(), lines[0].GetProperty("from").GetString(),
            lines[0].GetProperty("resumed").GetBoolean(), lines[0].GetProperty("door").GetString()));
        Assert.Equal(("tree", JsonValueKind.Null), (lines[1].GetProperty("why").GetString(), lines[1].GetProperty("door").ValueKind));
    }

    /// <summary>
    /// MSG1d (D137 §3.1): the person's words shown the moment they are said carry <c>reaches</c>, and are no prompt. The
    /// open's wait and each turn are timed from the prompt that took them: a word said while a session opened, or to one that
    /// had ended, times nothing from when it was said, and nobody's words are written.
    /// </summary>
    [Fact]
    public async Task Words_shown_waiting_are_no_prompt_and_time_nothing()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code-acp");
        var s = id!;

        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Id = "said-1", Text = "the level file moved", Reaches = "next-step" }, 1);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "the composed target" }, 3);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Message, Text = "on it" }, 4);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" }, 10);
        // Written to after it ended, then gone on with: the turn is the resumed run's, from the words where it took them.
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Id = "w1", Text = "also the changelog", Reaches = "resume", Door = "screen" }, 100);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Id = "w1", Text = "also the changelog" }, 160);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Message, Text = "added" }, 161);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" }, 170);

        Assert.Equal(3000, Data(Assert.Single(Named("session.opened"))).GetProperty("openMs").GetInt64());
        Assert.Equal([1000L, 1000L], Named("turn.answered").Select(Data).Select(line => line.GetProperty("firstAnswerMs").GetInt64()));
        Assert.Equal([7000L, 10000L], Named("turn.ended").Select(Data).Select(line => line.GetProperty("turnMs").GetInt64()));
        Assert.DoesNotContain("changelog", Raw());
    }

    /// <summary>
    /// A park is the state the attention watch calls one (<see cref="SessionStates.IsParked"/>), and no other:
    /// <c>AttentionTests</c> holds the same rows, so what the log counts is what the person was told about.
    /// </summary>
    [Theory]
    [InlineData("awaiting-person", true)]
    [InlineData("working", false)]
    [InlineData("starting", false)]
    [InlineData("completed", false)]
    [InlineData("failed", false)]
    public async Task A_park_line_is_written_for_the_state_the_attention_watch_calls_a_park(string state, bool parked)
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code");

        await w.Client.AdvanceAsync(id!, state);

        Assert.Equal(parked ? 1 : 0, Named("session.parked").Count);
    }

    /// <summary>
    /// An intake parks too, asking the person about its ask (D65), and says it was an intake. A park whose open
    /// this process never saw names its session and nothing it cannot know: absent is null, never a guess.
    /// </summary>
    [Fact]
    public async Task An_intake_that_parks_says_so_and_a_park_whose_open_was_not_seen_names_only_its_session()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenIntakeAsync("a1", "claude-code", "room");

        await w.Client.AdvanceAsync(id!, "awaiting-person");
        await w.Client.AdvanceAsync("orphan3", "awaiting-person");

        var lines = Named("session.parked").Select(Data).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(
            ("intake", "daoris:intake", "work"),
            (lines[0].GetProperty("kind").GetString(), lines[0].GetProperty("repository").GetString(), lines[0].GetProperty("workspace").GetString()));
        Assert.Equal("orphan3", lines[1].GetProperty("session").GetString());
        foreach (var field in new[] { "kind", "repository", "workspace" })
        {
            Assert.Equal(JsonValueKind.Null, lines[1].GetProperty(field).ValueKind);
        }
    }

    /// <summary>
    /// A session's start names the workspace its record runs in, so parks per week can be held against the
    /// sessions started there, and a set-up's session says it is one (WSSETUP11), so the report can find each
    /// set-up's cost. Every other session's start carries no such field.
    /// </summary>
    [Fact]
    public async Task A_sessions_start_names_its_workspace_and_a_set_ups_says_it_is_one()
    {
        using var w = Watch();

        await w.Client.OpenSessionAsync("q1", "claude-code");
        await w.Client.OpenSessionAsync("q2", "claude-code", setup: true);

        var started = Named("session.started").Select(Data).ToList();
        Assert.Equal(2, started.Count);
        Assert.All(started, line => Assert.Equal("work", line.GetProperty("workspace").GetString()));
        Assert.False(started[0].TryGetProperty("setup", out _));
        Assert.True(started[1].GetProperty("setup").GetBoolean());
    }

    /// <summary>
    /// WSSETUP5 (D124 §3.4): each registration followed from a line is one line, its repository and a word from the
    /// outcomes' list, never the sentence its row says; and none once the watch has let go.
    /// </summary>
    [Fact]
    public void A_registration_followed_is_one_line_with_its_repository_and_outcome_and_never_its_words()
    {
        using var w = Watch();

        w.Client.Followed(new RegistrationFollowed("game", RegistryOutcome.NotSetUp, "words a person reads", "main", "abc1234"));
        w.Watch.Dispose();
        w.Client.Followed(new RegistrationFollowed("engine", RegistryOutcome.Registered, "more words", Sent: true));

        var line = Assert.Single(Named("registry.followed"));
        Assert.Equal("info", line.GetProperty("level").GetString());
        Assert.Equal("""{"repository":"game","outcome":"not-set-up"}""", Data(line).GetRawText());
        Assert.DoesNotContain("words a person reads", Raw(), StringComparison.Ordinal);
        Assert.DoesNotContain("main", Data(line).GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// LAND2b (design §8): each try to land a done session is one <c>landing.auto</c> line, codes and counts and a plugin's id,
    /// never a sentence; and none once the watch has let go.
    /// </summary>
    [Fact]
    public void A_landing_at_done_is_one_line_of_codes_and_counts()
    {
        using var w = Watch();

        w.Client.LandingSaid(LandingLine.Auto("s1", "engine", "aurora", AutoLandingCode.PluginFailed, 2, null, "acme.lands", false));
        w.Watch.Dispose();
        w.Client.LandingSaid(LandingLine.Auto("s2", "engine", "aurora", AutoLandingCode.Landed, 1, null, null, null));

        var line = Assert.Single(Named("landing.auto"));
        Assert.Equal("info", line.GetProperty("level").GetString());
        Assert.Equal(
            """{"session":"s1","repository":"engine","workspace":"aurora","code":"plugin-failed","commits":2,"uncommitted":null,"plugin":"acme.lands","pushed":false}""",
            Data(line).GetRawText());
    }

    /// <summary>
    /// EVID1b (D144 §5): each read of a done's evidence is one <c>evidence.checked</c> line, counts and codes, never a path, the
    /// commit or the service's words; and none once the watch has let go.
    /// </summary>
    [Fact]
    public void An_evidence_read_is_one_line_of_counts_and_codes()
    {
        using var w = Watch();
        var verdict = new EvidenceVerdict(new string('a', 40), EvidenceCodes.SessionEnd,
        [
            new EvidenceRead(1, "docs/secret-plan.md", null, EvidenceCodes.Found) { Object = new string('b', 40), Changed = true },
            new EvidenceRead(2, "docs/other.md", null, EvidenceCodes.Missing),
        ]);
        var outcome = new EvidenceOutcome(verdict, new EvidencePosted(EvidencePosted.Kept, "Read the evidence of quest `#q1`."), "evidence read at …");

        w.Client.EvidenceSaid(EvidenceLine.Checked("q1", new EvidenceAt("C:/trees/q1", EvidenceCodes.SessionEnd) { Session = "s1" }, 2, outcome));
        w.Watch.Dispose();
        w.Client.EvidenceSaid(EvidenceLine.Checked("q2", new EvidenceAt("C:/trees/q2", EvidenceCodes.Terminal), 2, outcome));

        var line = Assert.Single(Named("evidence.checked"));
        Assert.Equal("info", line.GetProperty("level").GetString());
        Assert.Equal(
            """{"quest":"q1","session":"s1","how":"session-end","items":2,"found":1,"missing":1,"uncommitted":0,"case":0,"noQueue":0,"outcome":"kept"}""",
            Data(line).GetRawText());
        Assert.DoesNotContain("secret-plan", Raw(), StringComparison.Ordinal);
        Assert.DoesNotContain("trees", Raw(), StringComparison.Ordinal);
        Assert.DoesNotContain("aaaaaaaa", Raw(), StringComparison.Ordinal);
    }

    /// <summary>
    /// WSSETUP6 (D124 §4.1): a workspace plan's lines, each with its fields only (names, words and counts, never a sentence
    /// or a path); and none once the watch has let go. A terminal's press writes the same lines through the static writer.
    /// </summary>
    [Fact]
    public void A_workspace_plans_lines_carry_their_words_and_never_a_sentence()
    {
        using var w = Watch();

        w.Client.SetupSaid(SetupLine.Planned("work", 29, 1, 2));
        w.Client.SetupSaid(SetupLine.Published("work", "atlas", "q1"));
        w.Client.SetupSaid(SetupLine.Skipped("work", "billing", SetupRefusals.NotDriven));
        w.Client.SetupSaid(SetupLine.Paused("work", SetupPausedBy.Pilot));
        w.Client.SetupSaid(SetupLine.Resumed("work"));
        w.Watch.Dispose();
        w.Client.SetupSaid(SetupLine.Stopped("work"));

        Assert.Equal("""{"workspace":"work","repositories":29,"atOnce":1,"pilot":2}""", Data(Assert.Single(Named("setup.planned"))).GetRawText());
        Assert.Equal("""{"workspace":"work","repository":"atlas","quest":"q1"}""", Data(Assert.Single(Named("setup.published"))).GetRawText());
        Assert.Equal("""{"workspace":"work","repository":"billing","refusal":"not-driven"}""", Data(Assert.Single(Named("setup.skipped"))).GetRawText());
        Assert.Equal("""{"workspace":"work","by":"pilot"}""", Data(Assert.Single(Named("setup.paused"))).GetRawText());
        Assert.Equal("""{"workspace":"work"}""", Data(Assert.Single(Named("setup.resumed"))).GetRawText());
        Assert.Empty(Named("setup.stopped"));
        Assert.Equal("info", Assert.Single(Named("setup.published")).GetProperty("level").GetString());
    }

    /// <summary>
    /// WSSETUP11 (D124 §7.3): what a set-up costs is measured per turn, as METER1 splits it: the tokens read
    /// anew, from the cache and written, a turn's tool calls, and the context at its high-water against the
    /// window. Each count is the wire's, null where it said none, and a call's later updates are the same call.
    /// </summary>
    [Fact]
    public async Task A_turns_end_says_what_it_consumed_how_many_tools_it_called_and_how_full_its_context_got()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code-acp");
        var s = id!;

        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "the composed target" }, 1);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Usage, Used = 10_000, Size = 1_000_000 }, 2);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "t1", ToolKind = "read", Status = "pending", Title = "Read secret.txt" }, 3);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "t1", Status = "completed", Output = "the file's words" }, 4);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Usage, Used = 48_000, Size = 1_000_000 }, 5);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "t2", ToolKind = "execute", Status = "pending" }, 6);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, ToolKind = "other" }, 7);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Usage, Used = 30_000, Size = 1_000_000 }, 8);
        Said(w.Events, s, new SessionEvent
        {
            Kind = SessionEventKind.Turn, StopReason = "end_turn",
            Tokens = new TurnTokens(Input: 12, Output: 80, CacheRead: 51_100, CacheWrite: 16_700),
        }, 9);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "and then?" }, 10);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Message, Text = "done" }, 11);
        Said(w.Events, s, new SessionEvent
        {
            Kind = SessionEventKind.Turn, StopReason = "end_turn", Tokens = new TurnTokens(Input: 5, Output: 7, CacheRead: null, CacheWrite: null),
        }, 12);

        var ended = Named("turn.ended").Select(Data).ToList();
        Assert.Equal(2, ended.Count);
        long? Count(JsonElement line, string field) =>
            line.GetProperty(field).ValueKind == JsonValueKind.Null ? null : line.GetProperty(field).GetInt64();

        // Two calls with ids, one of them updated, and one with none, which cannot be told from the next.
        Assert.Equal(
            [12L, 51_100L, 16_700L, 80L, 3L, 48_000L, 1_000_000L],
            new[] { "input", "cacheRead", "cacheWrite", "output", "calls", "used", "size" }.Select(field => Count(ended[0], field)));
        // A turn that called nothing called none; a count the wire did not give, and a context it did not report, are null.
        Assert.Equal(
            [5L, null, null, 7L, 0L, null, null],
            new[] { "input", "cacheRead", "cacheWrite", "output", "calls", "used", "size" }.Select(field => Count(ended[1], field)));

        var raw = Raw();
        foreach (var words in new[] { "secret.txt", "the file's words", "and then?", "composed target" })
        {
            Assert.DoesNotContain(words, raw);
        }
    }

    /// <summary>What the ledger refused did not happen, so nothing is written about it.</summary>
    [Fact]
    public async Task A_refused_open_or_move_writes_nothing()
    {
        using var w = Watch();
        _ledger.Refusing = true;

        var (id, message) = await w.Client.OpenChatAsync("engine", "claude-code");
        await Assert.ThrowsAsync<DriverException>(() => w.Client.AdvanceAsync("s9", "completed"));
        var (answered, _) = await w.Client.AnswerSessionAsync("s9", "words");

        Assert.Null(id);
        Assert.Equal("the repository is busy", message);
        Assert.False(answered);
        Assert.Empty(Lines());
    }

    /// <summary>A watcher that fails costs its own line, never the call the ledger already answered.</summary>
    [Fact]
    public async Task A_failing_watcher_never_fails_the_ledger_call()
    {
        using var client = new ServiceClient(Ledger, null, new HttpClient(_ledger));
        client.Opened += _ => throw new InvalidOperationException("a broken watcher");
        client.Moved += _ => throw new InvalidOperationException("a broken watcher");

        var (id, _) = await client.OpenChatAsync("engine", "claude-code");
        var moved = await client.AdvanceAsync(id!, "completed");

        Assert.NotNull(id);
        Assert.Equal("moved", moved);
    }

    /// <summary>A watcher let go of writes nothing more.</summary>
    [Fact]
    public async Task A_disposed_watcher_writes_nothing_more()
    {
        var w = Watch();
        await w.Client.OpenChatAsync("engine", "claude-code");
        w.Watch.Dispose();

        await w.Client.OpenChatAsync("game", "claude-code");
        w.Events.Append("s2", new SessionEvent { Kind = SessionEventKind.User, Text = "later" });

        Assert.Single(Lines());
        w.Dispose();
    }

    /// <summary>
    /// The ledger's doors, standing in: an open answers a record in the workspace `work`, a move answers the
    /// state it moved to, and a refusal answers 409 with the ledger's sentence.
    /// </summary>
    private sealed class StandInLedger : HttpMessageHandler
    {
        private int _next;

        public bool Refusing { get; set; }

        /// <summary>The ledger as ANSWER1b has it: an answer keeps the record parked (D131 §5).</summary>
        public bool AnswerKeepsPark { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            using var body = JsonDocument.Parse(request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(ct));
            string? Field(string name) => body.RootElement.TryGetProperty(name, out var value) ? value.GetString() : null;

            if (Refusing) return Answer(HttpStatusCode.Conflict, new { error = "the repository is busy" });

            var moved = System.Text.RegularExpressions.Regex.Match(path, "^/api/sessions/([^/]+)/(state|answer)$");
            if (moved.Success)
            {
                var state = moved.Groups[2].Value == "answer" ? (AnswerKeepsPark ? "awaiting-person" : "completed") : Field("state");
                return Answer(HttpStatusCode.OK, new { session = new { id = moved.Groups[1].Value, state }, message = "moved" });
            }

            var id = $"s{Interlocked.Increment(ref _next)}";
            var repository = path switch
            {
                "/api/sessions" => "engine",
                "/api/sessions/chat" => Field("repository"),
                "/api/sessions/intake" => "daoris:intake",
                "/api/sessions/help" => "daoris:help",
                _ => null,
            };
            return repository is null
                ? Answer(HttpStatusCode.NotFound, new { error = "no such door" })
                : Answer(HttpStatusCode.OK, new
                {
                    session = new { id, repository, adapter = Field("adapter"), state = "queued", workspace = "work" },
                    message = "queued",
                });
        }

        private static HttpResponseMessage Answer(HttpStatusCode status, object payload) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
    }
}
