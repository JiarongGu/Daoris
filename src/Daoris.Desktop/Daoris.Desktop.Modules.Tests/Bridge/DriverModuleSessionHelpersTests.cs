using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// *Ask another agent…* over the bridge (MIXAGENT2b, D163 point 2; the mixed-agents design §3, §5): the person's own ask of a
/// helper for a session, the file `daoris-driver sessions helper` writes, judged as that verb judges; the session's asks as its
/// page lists them, each with where the driver's look left it; and the person's yes to an agent's ask that waits on it under the
/// `helpers` rule's `ask`, the `approved` state the look reads.
/// </summary>
public sealed class DriverModuleSessionHelpersTests : DriverModuleBridge
{
    private static string Record(string id, string extra = "") =>
        $$"""{"id":"{{id}}","repository":"engine","adapter":"claude-code","state":"working","kind":"chat","created":"2026-10-16T09:00:00Z","updated":"2026-10-16T09:05:00Z"{{extra}}}""";

    /// <summary>The loop up over a ledger on this machine holding a conversation, a helper, a reviewer and an intake.</summary>
    private async Task<DriverLoop> UpAsync(LoopbackHost ledger)
    {
        var records = $"[{Record("c1")},{Record("h1", ""","serves":"c1" """)},{Record("r1", ""","opinion":"o1" """)},{Record("i1", ""","ask":"0fda18" """)}]";
        ledger.Serve("/api/sessions?includeClosed=true", System.Text.Encoding.UTF8.GetBytes(records));
        ledger.Serve("/api/sessions", System.Text.Encoding.UTF8.GetBytes(records));
        ledger.Serve("/api/quests?includeClosed=true", System.Text.Encoding.UTF8.GetBytes("[]"));
        ledger.Serve("/api/quests", System.Text.Encoding.UTF8.GetBytes("[]"));
        ledger.Serve("/api/registry", System.Text.Encoding.UTF8.GetBytes("[]"));
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient(ledger.Address, null));
        return loop;
    }

    [Fact]
    public async Task The_persons_ask_is_the_file_the_terminal_writes_for_the_drivers_next_look()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SESSION_HELPER_ASK", new { id = "c1", question = "Why?" }));

        using var ledger = new LoopbackHost();
        var loop = await UpAsync(ledger);
        var module = new DriverModule(Bus, loop);

        var asked = await AnswerAsync(module, "SESSION_HELPER_ASK", new { id = "c1", question = "  Which test covers the loader?  ", agent = "codex-acp" });
        var id = asked.GetProperty("asked").GetString();
        Assert.Equal(JsonValueKind.Null, asked.GetProperty("why").ValueKind);

        var ask = Assert.Single(new HelperFiles(loop.Home).Asks());
        Assert.Equal((id, "c1", "Which test covers the loader?", "codex-acp", HelperAsk.ByPerson), (ask.Id, ask.Asker, ask.Question, ask.Agent, ask.AskedBy));
        // No path of the machine's on the wire.
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), asked.GetRawText(), StringComparison.OrdinalIgnoreCase);

        // An agent left to the driver is none named.
        await AnswerAsync(module, "SESSION_HELPER_ASK", new { id = "c1", question = "And the cap?" });
        Assert.Null(new HelperFiles(loop.Home).Asks().Single(each => each.Question == "And the cap?").Agent);
    }

    /// <summary>Each refusal is a code and the verb's sentence, answered as data, and nothing is written.</summary>
    [Theory]
    [InlineData("c1", "   ", null, "blank")]
    [InlineData("c1", "Why?", "codex acp", "agent")]
    [InlineData("gone1", "Why?", null, "not-held")]
    [InlineData("h1", "Why?", null, "helper")]
    [InlineData("r1", "Why?", null, "reviewer")]
    [InlineData("i1", "Why?", null, "intake")]
    public async Task What_cannot_ask_is_refused_by_code_and_writes_nothing(string id, string question, string? agent, string why)
    {
        using var ledger = new LoopbackHost();
        var loop = await UpAsync(ledger);

        var refused = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_HELPER_ASK", new { id, question, agent });

        Assert.Equal(JsonValueKind.Null, refused.GetProperty("asked").ValueKind);
        Assert.Equal(why, refused.GetProperty("why").GetString());
        Assert.EndsWith("Nothing was asked.", refused.GetProperty("message").GetString());
        Assert.Empty(new HelperFiles(loop.Home).Asks());
    }

    [Fact]
    public async Task A_question_past_the_bound_is_refused()
    {
        using var ledger = new LoopbackHost();
        var loop = await UpAsync(ledger);

        var refused = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_HELPER_ASK",
            new { id = "c1", question = new string('a', SessionsHelperCommand.MaxQuestion + 1) });

        Assert.Equal("too-long", refused.GetProperty("why").GetString());
        Assert.Empty(new HelperFiles(loop.Home).Asks());
    }

    /// <summary>
    /// The session's asks, oldest first, each as the driver's look left it: none yet, waiting (for the person's yes where it names
    /// no code, else for what the code names), started with its helper and agent. Another session's asks are not its own.
    /// </summary>
    [Fact]
    public async Task A_sessions_asks_are_listed_with_where_the_look_left_each()
    {
        var files = new HelperFiles(Home);
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Helpers));
        var first = files.WriteAsk("c1", "Read the loader.", null, DateTimeOffset.Parse("2026-10-16T09:00:00Z"));
        var second = AgentAsk(files, "a2", "c1", "Is the cap right?", "2026-10-16T09:01:00Z");
        var third = AgentAsk(files, "a3", "c1", "Which test?", "2026-10-16T09:02:00Z");
        AgentAsk(files, "a4", "other", "Not this one's.", "2026-10-16T09:03:00Z");
        files.WriteState(new HelperState(second, HelperStates.Waiting, null, null, DateTimeOffset.UtcNow));
        files.WriteState(new HelperState(third, HelperStates.Started, null, "h9", DateTimeOffset.UtcNow) { Agent = "codex-acp", Words = "kept" });

        var helpers = (await AnswerAsync(Module(), "SESSION_HELPERS", new { id = "c1" })).GetProperty("helpers").EnumerateArray().ToList();

        Assert.Equal([first, second, third], helpers.Select(row => row.GetProperty("id").GetString()));
        Assert.Equal(["person", "agent", "agent"], helpers.Select(row => row.GetProperty("askedBy").GetString()));
        Assert.Equal([null, "waiting", "started"], helpers.Select(row => row.GetProperty("state").GetString()));
        Assert.Equal("Is the cap right?", helpers[1].GetProperty("question").GetString());
        Assert.Equal(JsonValueKind.Null, helpers[1].GetProperty("code").ValueKind);
        Assert.Equal(("h9", "codex-acp"), (helpers[2].GetProperty("helper").GetString(), helpers[2].GetProperty("startedOn").GetString()));
        // What the state keeps for the driver alone is not the page's.
        Assert.False(helpers[2].TryGetProperty("words", out _));
    }

    /// <summary>
    /// The person's yes: an agent's ask waiting on it is written `approved` and the look nudged; a yes again is the same yes. A
    /// person's own ask needs none, one past its start is past it, one gone is gone, and one waiting on a slot or an agent waits on
    /// no yes: each answered by its code, and its state left as it was.
    /// </summary>
    [Fact]
    public async Task The_persons_yes_lets_an_agents_ask_go_and_refuses_by_code_what_waits_on_none()
    {
        var files = new HelperFiles(Home);
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Helpers));
        var waiting = AgentAsk(files, "a1", "c1", "Is the cap right?", "2026-10-16T09:00:00Z");
        files.WriteState(new HelperState(waiting, HelperStates.Waiting, null, null, DateTimeOffset.UtcNow));
        var unread = AgentAsk(files, "a2", "c1", "Not looked at yet.", "2026-10-16T09:01:00Z");
        var own = files.WriteAsk("c1", "My own.", null, DateTimeOffset.UtcNow);
        var started = AgentAsk(files, "a3", "c1", "Started.", "2026-10-16T09:02:00Z");
        files.WriteState(new HelperState(started, HelperStates.Started, null, "h9", DateTimeOffset.UtcNow));
        var capped = AgentAsk(files, "a4", "c1", "Waits on a slot.", "2026-10-16T09:03:00Z");
        files.WriteState(new HelperState(capped, HelperStates.Waiting, HelperVerdict.Cap, null, DateTimeOffset.UtcNow));
        var module = Module();

        var yes = await AnswerAsync(module, "SESSION_HELPER_APPROVE", new { session = "c1", ask = waiting });
        Assert.True(yes.GetProperty("approved").GetBoolean());
        Assert.Equal(HelperStates.Approved, files.ReadState(waiting)!.State);
        Assert.True((await AnswerAsync(module, "SESSION_HELPER_APPROVE", new { session = "c1", ask = waiting })).GetProperty("approved").GetBoolean());
        Assert.True((await AnswerAsync(module, "SESSION_HELPER_APPROVE", new { session = "c1", ask = unread })).GetProperty("approved").GetBoolean());
        Assert.Equal(HelperStates.Approved, files.ReadState(unread)!.State);

        foreach (var (ask, session, why) in new[] { (own, "c1", "person"), (started, "c1", "past"), (capped, "c1", "waits"), ("zz", "c1", "gone"), (waiting, "c2", "gone") })
        {
            var refused = await AnswerAsync(module, "SESSION_HELPER_APPROVE", new { session, ask });
            Assert.False(refused.GetProperty("approved").GetBoolean());
            Assert.Equal(why, refused.GetProperty("why").GetString());
        }

        Assert.Null(files.ReadState(own));
        Assert.Equal(HelperStates.Started, files.ReadState(started)!.State);
        Assert.Equal((HelperStates.Waiting, HelperVerdict.Cap), (files.ReadState(capped)!.State, files.ReadState(capped)!.Code));
    }

    /// <summary>
    /// HELPERNO1 (D163 point 3): the person's no. An agent's ask the look holds waiting on their word is refused `declined` at once,
    /// with the coded note the look writes on the asking session's record for every refusal, so the look starts nothing for it; a no
    /// again is the same no and notes nothing more. A person's own ask needs none, one with their yes waits on the look, one the look
    /// has not read yet waits on no word of theirs yet, one past its start is past it, one waiting on a slot or an agent waits on
    /// none, and one gone is gone: each answered by its code, its state left as it was.
    /// </summary>
    [Fact]
    public async Task The_persons_no_refuses_an_agents_ask_with_its_note_and_refuses_by_code_what_waits_on_none()
    {
        var files = new HelperFiles(Home);
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Helpers));
        var waiting = AgentAsk(files, "a1", "c1", "Is the cap right?", "2026-10-16T09:00:00Z");
        files.WriteState(new HelperState(waiting, HelperStates.Waiting, null, null, DateTimeOffset.UtcNow));
        var unread = AgentAsk(files, "a2", "c1", "Not looked at yet.", "2026-10-16T09:01:00Z");
        var own = files.WriteAsk("c1", "My own.", null, DateTimeOffset.UtcNow);
        var approved = AgentAsk(files, "a3", "c1", "Has a yes.", "2026-10-16T09:02:00Z");
        files.WriteState(new HelperState(approved, HelperStates.Approved, null, null, DateTimeOffset.UtcNow));
        var started = AgentAsk(files, "a4", "c1", "Started.", "2026-10-16T09:03:00Z");
        files.WriteState(new HelperState(started, HelperStates.Started, null, "h9", DateTimeOffset.UtcNow));
        var capped = AgentAsk(files, "a5", "c1", "Waits on a slot.", "2026-10-16T09:04:00Z");
        files.WriteState(new HelperState(capped, HelperStates.Waiting, HelperVerdict.Cap, null, DateTimeOffset.UtcNow));
        var loop = Loop();
        var module = new DriverModule(Bus, loop);

        var no = await AnswerAsync(module, "SESSION_HELPER_DECLINE", new { session = "c1", ask = waiting });
        Assert.True(no.GetProperty("declined").GetBoolean());
        Assert.Equal(JsonValueKind.Null, no.GetProperty("why").ValueKind);
        Assert.Equal((HelperStates.Refused, HelperWhy.Declined), (files.ReadState(waiting)!.State, files.ReadState(waiting)!.Code));
        var note = Assert.Single(loop.Events.Page("c1").Events, e => e.Kind == SessionEventKind.Note);
        var part = Assert.Single(note.Parts!);
        Assert.Equal(("helper.refused", (object?)HelperWhy.Declined), (part.Code, part.Value("why")));

        Assert.True((await AnswerAsync(module, "SESSION_HELPER_DECLINE", new { session = "c1", ask = waiting })).GetProperty("declined").GetBoolean());
        Assert.Single(loop.Events.Page("c1").Events, e => e.Kind == SessionEventKind.Note);

        foreach (var (ask, session, why) in new[]
                 {
                     (unread, "c1", "unread"), (own, "c1", "person"), (approved, "c1", "approved"), (started, "c1", "past"),
                     (capped, "c1", "waits"), ("zz", "c1", "gone"), (waiting, "c2", "gone"),
                 })
        {
            var refused = await AnswerAsync(module, "SESSION_HELPER_DECLINE", new { session, ask });
            Assert.False(refused.GetProperty("declined").GetBoolean());
            Assert.Equal(why, refused.GetProperty("why").GetString());
        }

        Assert.Null(files.ReadState(unread));
        Assert.Null(files.ReadState(own));
        Assert.Equal(HelperStates.Approved, files.ReadState(approved)!.State);
        Assert.Equal(HelperStates.Started, files.ReadState(started)!.State);
        Assert.Equal((HelperStates.Waiting, HelperVerdict.Cap), (files.ReadState(capped)!.State, files.ReadState(capped)!.Code));
        Assert.Single(loop.Events.Page("c1").Events, e => e.Kind == SessionEventKind.Note);
    }

    /// <summary>
    /// MIXAGENT2c (D168, D163 point 3): what of the agents' asks waits on the person's word across this machine's sessions, for
    /// *What needs you*: each the look holds `waiting` with no code, oldest first, with its session, question, agent and when. One
    /// it has not read yet, one waiting on a slot, one with a yes, one past its start and a person's own wait on no word of theirs.
    /// </summary>
    [Fact]
    public async Task The_asks_waiting_on_the_persons_word_are_listed_across_sessions_oldest_first()
    {
        var files = new HelperFiles(Home);
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Helpers));
        foreach (var (id, asker, at, state, code) in new[]
                 {
                     ("a1", "c1", "2026-10-16T09:01:00Z", HelperStates.Waiting, (string?)null),
                     ("a2", "c2", "2026-10-16T09:00:00Z", HelperStates.Waiting, null),
                     ("a3", "c1", "2026-10-16T09:02:00Z", null, null),
                     ("a4", "c1", "2026-10-16T09:03:00Z", HelperStates.Waiting, HelperVerdict.Cap),
                     ("a5", "c1", "2026-10-16T09:04:00Z", HelperStates.Approved, null),
                     ("a6", "c1", "2026-10-16T09:05:00Z", HelperStates.Refused, HelperWhy.Declined),
                 })
        {
            AgentAsk(files, id, asker, $"Question {id}?", at);
            if (state is not null) files.WriteState(new HelperState(id, state, code, null, DateTimeOffset.UtcNow));
        }

        var own = files.WriteAsk("c1", "My own.", null, DateTimeOffset.UtcNow);
        files.WriteState(new HelperState(own, HelperStates.Waiting, null, null, DateTimeOffset.UtcNow));

        var waits = (await AnswerAsync(Module(), "HELPER_WAITS", new { })).GetProperty("waits").EnumerateArray().ToList();

        Assert.Equal(["a2", "a1"], waits.Select(row => row.GetProperty("id").GetString()));
        Assert.Equal(["c2", "c1"], waits.Select(row => row.GetProperty("session").GetString()));
        Assert.Equal("Question a1?", waits[1].GetProperty("question").GetString());
        Assert.Equal(JsonValueKind.Null, waits[1].GetProperty("agent").ValueKind);
        Assert.Equal(DateTimeOffset.Parse("2026-10-16T09:01:00Z"), DateTimeOffset.Parse(waits[1].GetProperty("at").GetString()!));
        // Nothing of the machine's path, nor of what the state keeps for the driver.
        Assert.False(waits[1].TryGetProperty("words", out _));
    }

    /// <summary>An agent's ask, as a connector's <c>helper_ask</c> writes it: the shape the driver's reader reads.</summary>
    private string AgentAsk(HelperFiles files, string id, string asker, string question, string at)
    {
        File.WriteAllText(files.PathOf(id, "ask"),
            $$"""{"id":"{{id}}","asker":"{{asker}}","question":"{{question}}","agent":null,"askedBy":"agent","at":"{{at}}"}""");
        return id;
    }
}
