using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1a-i's door (D163, the mixed-agents design §2 and §5): a conversation goes on with another agent. The ask judges and
/// keeps the box <c>&lt;home&gt;/sessions/&lt;id&gt;.hand-over.json</c>; what cannot be handed over is refused by a code with its
/// sentence, before anything is kept; the briefing the new conversation's agent reads is a pure function of what Daoris kept.
/// </summary>
/// <remarks>In-process: the service is a stand-in reached through the real client, and the roster's accounts are handed in. The fast half.</remarks>
public sealed class HandOverTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-hand-over-" + Guid.NewGuid().ToString("N")[..8]);

    public HandOverTests()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        Directory.CreateDirectory(Path.Combine(_home, "tree"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The roster's answer: the accounts of an agent, or null for an agent it does not have.</summary>
    private static IReadOnlyList<string>? Roster(string agent) => agent switch
    {
        "codex-acp" => ["work", "home"],
        "claude-code" => ["account-1"],
        _ => null,
    };

    private JsonObject Chat(
        string id, string state = "completed", string? quest = null, string kind = "chat", string? ask = null, string? tree = null,
        string? from = null) => new()
    {
        ["id"] = id, ["repository"] = "engine", ["state"] = state, ["kind"] = kind, ["quest"] = quest, ["ask"] = ask,
        ["adapter"] = "claude-code", ["profile"] = "account-1", ["tree"] = tree ?? Path.Combine(_home, "tree"),
        ["created"] = T0.ToString("O"), ["updated"] = T0.AddMinutes(5).ToString("O"), ["said"] = new JsonArray(), ["from"] = from,
    };

    private Task<HandOverAnswer> AskAsync(Room room, string id, string agent = "codex-acp", string? account = null) =>
        HandOver.AskAsync(room.Client(), _home, Roster, id, agent, account, HandOverWhy.Limit, HandOverBy.Person, T0.AddMinutes(30));

    /// <summary>🔴 A conversation handed to another agent keeps its box: the agent, the account, why, by whom and when.</summary>
    [Fact]
    public async Task A_conversation_is_handed_over_by_keeping_its_box()
    {
        var room = new Room([Chat("c0nv0001")]);

        var answer = await AskAsync(room, "c0nv0001", account: "work");

        Assert.Equal((true, (string?)null), (answer.Kept, answer.Why));
        Assert.Equal("it goes on with codex-acp as soon as its turn is over, in a conversation of its own.", answer.Message);
        var box = new HandOverBox(_home).Read("c0nv0001");
        Assert.Equal(("codex-acp", "work", HandOverWhy.Limit, HandOverBy.Person, T0.AddMinutes(30)), (box!.Agent, box.Account, box.Why, box.AskedBy, box.At));
    }

    public static TheoryData<string, string, string?, string, string> Refusals => new()
    {
        { "nobody00", "codex-acp", null, WordsNever.NotFound, "no session here is nobody00." },
        { "laptop/s9", "codex-acp", null, WordsNever.Teammate, "laptop/s9 ran on laptop, where its conversation is; nothing said here reaches it." },
        { "1ntake00", "codex-acp", null, WordsNever.Intake, "1ntake00 is an intake; answer its ask #a1 instead: publish it or close it." },
        { "qu3st000", "codex-acp", null, HandOver.NotConversation, "qu3st000 is a quest's session: it is handed over from a terminal, `daoris-driver sessions hand-over qu3st000 --agent <adapter>`; its page's press comes later." },
        { "c0nv0001", "gemini", null, HandOver.UnknownAgent, "no agent here is called gemini." },
        { "c0nv0001", "codex-acp", "other", HandOver.NotItsAccount, "other is not an account of codex-acp." },
        { "c0nv0001", "claude-code", "account-1", HandOver.SameAgent, "c0nv0001 already runs on claude-code as account-1." },
        { "tr33g0n3", "codex-acp", null, HandOver.TreeGone, "the tree tr33g0n3 ran in is gone, so there is nothing to go on in." },
        // HANDWORDS1: a conversation that went on in another is not handed over a second time from its tree.
        { "h4nd3d00", "codex-acp", null, WordsNever.Continued, "h4nd3d00 went on in conversation m0v3d000; hand that one over instead." },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task What_cannot_be_handed_over_is_refused_by_its_code_and_nothing_is_kept(string id, string agent, string? account, string why, string message)
    {
        var room = new Room(
        [
            Chat("c0nv0001"), Chat("laptop/s9"), Chat("1ntake00", state: "awaiting-person", ask: "a1"),
            Chat("qu3st000", kind: "driven", quest: "q1"), Chat("tr33g0n3", tree: Path.Combine(_home, "gone")),
            Chat("h4nd3d00"), Chat("m0v3d000", state: "working", from: "h4nd3d00"),
        ]);

        var answer = await AskAsync(room, id, agent, account);

        Assert.Equal((false, why, message), (answer.Kept, answer.Why, answer.Message));
        Assert.Empty(Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Sessions), "*" + HandOverBox.Suffix));
    }

    [Fact]
    public async Task A_second_hand_over_waits_behind_the_first_and_Ask_Daoris_is_refused()
    {
        var room = new Room([Chat("c0nv0001"), HelpChat("h31p0001")]);

        Assert.True((await AskAsync(room, "c0nv0001")).Kept);
        var again = await AskAsync(room, "c0nv0001", "claude-code", null);
        Assert.Equal((false, HandOver.Waiting), (again.Kept, again.Why));
        Assert.Equal("c0nv0001 is already waiting to be handed over to codex-acp.", again.Message);

        var help = await AskAsync(room, "h31p0001");
        Assert.Equal((false, WordsNever.Help), (help.Kept, help.Why));
    }

    private JsonObject HelpChat(string id)
    {
        var record = Chat(id);
        record["repository"] = HelpRoom.Repository;
        return record;
    }

    // ——— The box.

    [Fact]
    public void The_box_round_trips_without_a_byte_order_mark_or_a_carriage_return_and_clears()
    {
        var box = new HandOverBox(_home);
        box.Write("c0nv0001", new HandOverRequest("codex-acp", null, HandOverWhy.Press, HandOverBy.Agent, T0));

        var bytes = File.ReadAllBytes(box.PathOf("c0nv0001"));
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal(new HandOverRequest("codex-acp", null, HandOverWhy.Press, HandOverBy.Agent, T0), box.Read("c0nv0001"));
        Assert.Equal(["c0nv0001"], box.Pending());

        box.Clear("c0nv0001");
        Assert.Null(box.Read("c0nv0001"));
        Assert.Empty(box.Pending());
    }

    [Fact]
    public void A_box_that_does_not_read_is_none()
    {
        File.WriteAllText(new HandOverBox(_home).PathOf("c0nv0001"), "{ not json");

        Assert.Null(new HandOverBox(_home).Read("c0nv0001"));
        Assert.Null(new HandOverBox(_home).Read("../escape"));
    }

    // ——— The briefing, a pure function.

    /// <summary>
    /// 🔴 What the new conversation's agent is told first, in Daoris's one English: whose conversation it continues and why, the
    /// request as the person first wrote it, its last plan and words, and what is uncommitted: nothing of the old context.
    /// </summary>
    [Fact]
    public void The_briefing_names_the_conversation_why_the_request_the_plan_the_words_and_the_uncommitted()
    {
        var briefing = HandOverBriefing.Compose(
            "claude-code", "c0nv0001", HandOverWhy.Limit, "Make the port configurable.",
            [new PlanEntry("Read the config", "completed"), new PlanEntry("Write the flag", "pending")],
            "I read the config and began the flag.", ["src/Port.cs", "README.md"]);

        Assert.Equal(
            "You continue the claude-code conversation `c0nv0001`, handed over because the account it ran on reached its limit. "
            + "None of its own context carries over to you: this is what Daoris kept of it.\n\n"
            + "The person first wrote:\n\n> Make the port configurable.\n\n"
            + "Its plan, as its record last kept it:\n\n    - [completed] Read the config\n    - [pending] Write the flag\n\n"
            + "Its last words were:\n\n> I read the config and began the flag.\n\n"
            + "Uncommitted in this tree:\n\n    - src/Port.cs\n    - README.md",
            briefing);
    }

    [Fact]
    public void The_briefing_leaves_out_what_there_is_none_of_and_bounds_what_is_long()
    {
        var plain = HandOverBriefing.Compose("codex-acp", "c0nv0001", HandOverWhy.Press, null, [], null, []);
        Assert.Equal(
            "You continue the codex-acp conversation `c0nv0001`, handed over because the person asked for it. "
            + "None of its own context carries over to you: this is what Daoris kept of it.",
            plain);

        var long_ = HandOverBriefing.Compose(
            "codex-acp", "c0nv0001", HandOverWhy.Near, new string('a', 9000), [], new string('b', 9000),
            [.. Enumerable.Range(0, 80).Select(each => $"f{each}.cs")]);
        Assert.Contains("the account it ran on was near its limit", long_);
        Assert.DoesNotContain(new string('a', 4001), long_);
        Assert.DoesNotContain(new string('b', 4001), long_);
        Assert.Contains("    - f49.cs", long_);
        Assert.DoesNotContain("f50.cs", long_);
        Assert.Contains("… and 30 more", long_);
    }

    // ——— Taking the box (D163 §3): end the old conversation, start the new in its tree, brief it, say where it went.

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private sealed class Taken
    {
        public List<string> Calls { get; } = [];

        public ChatMessage? Said { get; set; }

        public bool Reached { get; set; } = true;

        public ChatStart Started { get; set; } = new("n3w00001", "opened");

        /// <summary>The session the start was told the new conversation continues (MIXAGENT1a-iii), with why and who asked.</summary>
        public ContinuedFrom? From { get; set; }
    }

    private Task<HandedOver> TakeAsync(Room room, Taken taken, HandOverRequest ask, string id = "c0nv0001")
    {
        new HandOverBox(_home).Write(id, ask);
        return HandOverTake.RunAsync(
            room.Client(), _home, Events, output: null, id, ask, Roster,
            end: session =>
            {
                taken.Calls.Add($"end {session}");
                return Task.CompletedTask;
            },
            start: (prior, request, from, _) =>
            {
                taken.Calls.Add($"start {Path.GetFileName(prior.Tree)} {prior.Repository} {request.Agent} {request.Account}");
                taken.From = from;
                return Task.FromResult(taken.Started);
            },
            say: (session, message) =>
            {
                taken.Calls.Add($"say {session}");
                taken.Said = message;
                return Task.FromResult(taken.Reached);
            });
    }

    private static HandOverRequest Ask(string agent = "codex-acp", string? account = "work") =>
        new(agent, account, HandOverWhy.Limit, HandOverBy.Daoris, T0);

    /// <summary>
    /// 🔴 The old conversation ends first (its tree is held by one session at a time), the new starts in the same tree on the named
    /// agent and account, and is told Daoris's briefing, never the person's words; both records say where it went and where it
    /// came from, by code, and the box is gone.
    /// </summary>
    [Fact]
    public async Task Taking_the_box_ends_the_old_starts_the_new_in_its_tree_and_briefs_it()
    {
        var events = Events;
        events.Append("c0nv0001", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Make the port configurable.\nAnd log it." });
        var taken = new Taken();

        var handed = await TakeAsync(new Room([Chat("c0nv0001")]), taken, Ask());

        Assert.Equal(["end c0nv0001", "start tree engine codex-acp work", "say n3w00001"], taken.Calls);
        // The new record names the one it continues, why and who asked (MIXAGENT1a-iii): the box's, never the person's by default.
        Assert.Equal(new ContinuedFrom("c0nv0001", HandOverWhy.Limit, HandOverBy.Daoris), taken.From);
        Assert.Equal(("n3w00001", true, (string?)null), (handed.SessionId, handed.Kept, handed.Why));
        Assert.Equal("conversation `n3w00001` goes on from `c0nv0001` with codex-acp.", handed.Message);
        var message = taken.Said!;
        Assert.Equal("", message.Text);
        Assert.StartsWith("You continue the claude-code conversation `c0nv0001`, handed over because the account it ran on reached its limit.", message.Briefing);
        Assert.Contains("> Make the port configurable.\n> And log it.", message.Briefing);
        Assert.Equal(["chat.taken-over"], NoteAssert.Codes(message.TakenOver!.Parts));
        Assert.Equal(("claude-code", "c0nv0001", "limit"), (message.TakenOver.Parts[0].Value("agent"), message.TakenOver.Parts[0].Value("session"), message.TakenOver.Parts[0].Value("why")));
        var old = Assert.Single(events.Page("c0nv0001", limit: SessionEventText.MaxPage).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal(["chat.handed-over"], NoteAssert.Codes(old.Parts!));
        Assert.Equal(("codex-acp", "n3w00001"), (old.Parts![0].Value("agent"), old.Parts[0].Value("session")));
        Assert.Null(new HandOverBox(_home).Read("c0nv0001"));
    }

    /// <summary>
    /// 🔴 A move by the usage curve (SWITCH1c-ii, the switch design §4) is a take on the same agent and another account: the new
    /// conversation's first line and the old one's last are the moved pair (SWITCH1c-i), naming the accounts and how far over its
    /// curve the one it left was, in place of the hand-over's two; its record continues the old by `curve`, asked by `daoris`.
    /// </summary>
    [Fact]
    public async Task A_move_by_the_curve_is_said_by_the_moved_pair_in_place_of_the_hand_over_s_lines()
    {
        var events = Events;
        var taken = new Taken();
        var ask = new HandOverRequest("claude-code", "account-3", HandOverWhy.Curve, HandOverBy.Daoris, T0);
        new HandOverBox(_home).Write("c0nv0001", ask);

        var handed = await HandOverTake.RunAsync(
            new Room([Chat("c0nv0001")]).Client(), _home, events, output: null, "c0nv0001", ask,
            agent => agent == "claude-code" ? ["account-1", "account-3"] : null,
            end: session =>
            {
                taken.Calls.Add($"end {session}");
                return Task.CompletedTask;
            },
            start: (prior, request, from, _) =>
            {
                taken.Calls.Add($"start {prior.Repository} {request.Agent} {request.Account}");
                taken.From = from;
                return Task.FromResult(taken.Started);
            },
            say: (session, message) =>
            {
                taken.Calls.Add($"say {session}");
                taken.Said = message;
                return Task.FromResult(true);
            },
            moved: new SwitchChoice.MoveTo("account-3", "weekly", 0.62, 45.0));

        Assert.Equal(["end c0nv0001", "start engine claude-code account-3", "say n3w00001"], taken.Calls);
        Assert.Equal(new ContinuedFrom("c0nv0001", HandOverWhy.Curve, HandOverBy.Daoris), taken.From);
        Assert.True(handed.Kept, handed.Message);
        Assert.StartsWith(
            "You continue the claude-code conversation `c0nv0001`, handed over because the account it ran on was over the usage curve",
            taken.Said!.Briefing);
        var came = taken.Said.TakenOver!;
        Assert.Equal(["chat.moved-from"], NoteAssert.Codes(came.Parts));
        Assert.Equal(
            ("account-1", "weekly", 62, 45),
            (came.Parts[0].Value("from"), came.Parts[0].Value("window"), came.Parts[0].Value("used"), came.Parts[0].Value("curve")));
        var went = Assert.Single(events.Page("c0nv0001", limit: SessionEventText.MaxPage).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal(["chat.moved"], NoteAssert.Codes(went.Parts!));
        Assert.Equal(("account-3", "account-1"), (went.Parts![0].Value("to"), went.Parts[0].Value("from")));
        Assert.Null(new HandOverBox(_home).Read("c0nv0001"));
    }

    /// <summary>A box judged again as it is taken: a tree gone while it waited ends nothing and starts nothing, and the box is gone.</summary>
    [Fact]
    public async Task A_box_that_no_longer_holds_is_refused_with_nothing_ended_or_started()
    {
        var taken = new Taken();

        var handed = await TakeAsync(new Room([Chat("tr33g0n3", tree: Path.Combine(_home, "gone"))]), taken, Ask(), "tr33g0n3");

        Assert.Empty(taken.Calls);
        Assert.Equal(((string?)null, false, HandOver.TreeGone), (handed.SessionId, handed.Kept, handed.Why));
        Assert.Null(new HandOverBox(_home).Read("tr33g0n3"));
    }

    /// <summary>A start the roster refuses is said with its own sentence, the old conversation already ended, and the box does not stay to be tried at every come-up.</summary>
    [Fact]
    public async Task A_start_that_is_refused_says_why_and_leaves_no_box()
    {
        var taken = new Taken { Started = new ChatStart(null, "every account of codex-acp is cooling.") };

        var handed = await TakeAsync(new Room([Chat("c0nv0001")]), taken, Ask());

        Assert.Equal(["end c0nv0001", "start tree engine codex-acp work"], taken.Calls);
        Assert.Equal((null, false, (string?)null), (handed.SessionId, handed.Kept, handed.Why));
        Assert.Equal(
            "c0nv0001 ended, but the conversation with codex-acp did not start: every account of codex-acp is cooling. Its tree is as it was.",
            handed.Message);
        Assert.Null(new HandOverBox(_home).Read("c0nv0001"));
    }

    /// <summary>A new conversation whose briefing did not reach it is still the new conversation: said, so the person writes to it.</summary>
    [Fact]
    public async Task A_briefing_that_did_not_reach_the_new_conversation_is_said()
    {
        var taken = new Taken { Reached = false };

        var handed = await TakeAsync(new Room([Chat("c0nv0001")]), taken, Ask());

        Assert.Equal(("n3w00001", true, HandOver.Unbriefed), (handed.SessionId, handed.Kept, handed.Why));
        Assert.Equal(
            "conversation `n3w00001` opened with codex-acp, but what Daoris kept of `c0nv0001` did not reach it: tell it where you were.",
            handed.Message);
        // Said in the new conversation's record, by a code, and the old one still names the new one.
        var unsaid = Assert.Single(Events.Page("n3w00001", limit: SessionEventText.MaxPage).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal(["chat.briefing-unsaid"], NoteAssert.Codes(unsaid.Parts!));
        Assert.Contains(Events.Page("c0nv0001", limit: SessionEventText.MaxPage).Events, e => e.Parts?.Any(part => part.Code == "chat.handed-over") == true);
    }

    /// <summary>The request is the person's first message, whole; a driven session's composed target is none.</summary>
    [Fact]
    public void The_request_is_the_first_message_the_person_wrote_whole()
    {
        var events = Events;
        events.Append("c0nv0001", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "First.\nSecond line." });
        events.Append("c0nv0001", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Later." });
        events.Append("c0nv0002", new SessionEvent { Kind = SessionEventKind.User, Origin = "daoris", Text = "A composed target." });

        Assert.Equal("First.\nSecond line.", events.FirstAsked("c0nv0001"));
        Assert.Null(events.FirstAsked("c0nv0002"));
        Assert.Null(events.FirstAsked("nothing0"));
    }

    /// <summary>A conversation handed over opens with one note in the record, never a message of the person's and never the browser's line.</summary>
    [Fact]
    public void A_handed_over_conversation_records_its_note_in_place_of_a_person_s_message()
    {
        var message = new ChatMessage("", []) { Briefing = "You continue…", TakenOver = HandOverNotes.TakenOver("claude-code", "c0nv0001", HandOverWhy.Near) };

        var recorded = ChatRunner.Recorded(message).ToList();

        var only = Assert.Single(recorded);
        Assert.Equal((SessionEventKind.Note, "chat.taken-over"), (only.Kind, only.Parts![0].Code));
        Assert.Equal([SessionEventKind.User], ChatRunner.Recorded(new ChatMessage("hello", [])).Select(e => e.Kind));
    }

    // ——— The terminal's door (D50): `daoris-driver sessions hand-over <id> --agent <adapter> [--account <name>]`.

    [Fact]
    public void The_terminal_reads_an_id_an_agent_and_an_account_and_its_usage_says_the_verb()
    {
        var ask = SessionsCommand.Read(["hand-over", "s1", "--agent", "codex-acp", "--account", "work"], out var problem);
        Assert.Null(problem);
        Assert.Equal(("hand-over", "s1", "codex-acp", "work"), (ask!.Verb, ask.Ids[0], ask.Agent, ask.Account));
        Assert.Null(SessionsCommand.Read(["hand-over", "s1", "--agent", "codex-acp"], out _)!.Account);

        Assert.Null(SessionsCommand.Read(["hand-over", "s1"], out problem));
        Assert.Equal("`hand-over` takes one session's id, `--agent <adapter>` and, where one is wanted, `--account <name>`.", problem);
        Assert.Null(SessionsCommand.Read(["hand-over", "--agent", "codex-acp"], out _));
        Assert.Null(SessionsCommand.Read(["hand-over", "s1", "--agent"], out _));
        Assert.Contains("sessions hand-over <id> --agent <adapter> [--account <name>]", SessionsCommand.Usage);
        Assert.Contains("sessions hand-over <id> --agent <adapter> [--account <name>]", DriverCommand.Usage);
    }

    /// <summary>The verb opens nothing itself: the host's seam does, as start-from's, and its exit is the verb's.</summary>
    [Fact]
    public async Task The_terminal_hands_the_ask_to_the_host_s_door_and_says_so_where_there_is_none()
    {
        var room = new Room([Chat("c0nv0001")]);
        (string, string, string?)? seen = null;
        var world = new SessionsWorld(room.Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null)
        {
            HandOver = (id, agent, account, _) =>
            {
                seen = (id, agent, account);
                return Task.FromResult(0);
            },
        };
        var ask = SessionsCommand.Read(["hand-over", "c0nv0001", "--agent", "codex-acp"], out _)!;

        Assert.Equal(0, await SessionsCommand.RunAsync(ask, world, new StringWriter()));
        Assert.Equal(("c0nv0001", "codex-acp", (string?)null), seen);

        var output = new StringWriter();
        Assert.Equal(2, await SessionsCommand.RunAsync(ask, world with { HandOver = null }, output));
        Assert.StartsWith("sessions: nothing here can hand a conversation over", output.ToString());
    }

    /// <summary>
    /// 🔴 MIXAGENT1a-ii (D50): on a quest's session the verb opens no conversation in the terminal: it judges, keeps the box and says
    /// the driver's next look takes it. The host's conversation door is never asked.
    /// </summary>
    [Fact]
    public async Task The_terminal_keeps_a_quest_s_session_s_box_for_the_look_and_never_opens_a_conversation()
    {
        var room = QuestRoom();
        var asked = false;
        var world = new SessionsWorld(room.Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null)
        {
            HandOver = (_, _, _, _) =>
            {
                asked = true;
                return Task.FromResult(0);
            },
            Drives = Drives,
            LoopRuns = () => true,
        };
        var ask = SessionsCommand.Read(["hand-over", "qu3st000", "--agent", "codex-acp", "--account", "work"], out _)!;
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(ask, world, output));

        Assert.False(asked);
        Assert.Equal("sessions: it is stopped at the driver's next look, then goes on in the same tree with codex-acp.\n", output.ToString().ReplaceLineEndings("\n"));
        var box = new HandOverBox(_home).Read("qu3st000")!;
        Assert.Equal(("codex-acp", "work", HandOverWhy.Press, HandOverBy.Person), (box.Agent, box.Account, box.Why, box.AskedBy));
    }

    [Fact]
    public async Task The_terminal_says_where_no_loop_holds_the_home_and_refuses_with_the_judge_s_sentence()
    {
        var room = QuestRoom();
        var world = new SessionsWorld(room.Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null) { Drives = Drives, LoopRuns = () => false };
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(SessionsCommand.Read(["hand-over", "qu3st000", "--agent", "codex-acp"], out _)!, world, output));
        Assert.EndsWith("no driver is running on this home now: its look takes it when one does.\n", output.ToString().ReplaceLineEndings("\n"));

        var refused = new StringWriter();
        Assert.Equal(1, await SessionsCommand.RunAsync(SessionsCommand.Read(["hand-over", "qu3st000", "--agent", "gemini"], out _)!, world, refused));
        Assert.Equal("sessions: no agent here is called gemini.\n", refused.ToString().ReplaceLineEndings("\n"));
    }

    /// <summary>One line says what came of it: kept, taken at once, or refused with its sentence.</summary>
    [Theory]
    [InlineData(true, "c0nv0002", null, 0, "sessions: conversation `c0nv0002` goes on from it with codex-acp.")]
    [InlineData(true, null, null, 0, "sessions: it goes on with codex-acp as soon as its turn is over.")]
    [InlineData(false, null, "waiting", 1, "sessions: it is already waiting.")]
    public void The_terminal_says_what_came_of_it_in_one_line(bool kept, string? session, string? why, int exit, string line)
    {
        var output = new StringWriter();

        var result = SessionsCommand.HandedOver(new HandedOver(session, kept, why, line["sessions: ".Length..]), output);

        Assert.Equal(line + "\n", output.ToString().ReplaceLineEndings("\n"));
        Assert.Equal(session is null ? exit : null, result);
    }

    // ——— A quest's session (MIXAGENT1a-ii, D163 point 1): judged by the quest's state, whose take it is, the session's place
    // among the quest's runs here, and the named agent's door; kept as a box the driver's next look takes.

    /// <summary>The roster as the terminal reads it for a driven start: an agent's accounts and the wire its door rides.</summary>
    private static HandOverAgent? Drives(string agent) => agent switch
    {
        "codex-acp" => new(["work", "home"], SessionWire.Acp),
        "claude-code" => new(["account-1"], SessionWire.Pipe),
        _ => null,
    };

    private JsonObject Driven(string id, string quest = "q1", string state = "working", string repository = "engine", int minute = 0)
    {
        var record = Chat(id, state, quest, kind: "driven");
        record["repository"] = repository;
        record["created"] = T0.AddMinutes(minute).ToString("O");
        record["updated"] = T0.AddMinutes(minute + 5).ToString("O");
        return record;
    }

    private static JsonObject QuestRow(string id, string status, string to = "engine") => new()
    {
        ["id"] = id, ["from"] = "game", ["to"] = to, ["title"] = "t", ["body"] = "b", ["status"] = status,
    };

    private Room QuestRoom(params JsonObject[] records) => new(records.Length == 0 ? [Driven("qu3st000")] : records)
    {
        Quests = [QuestRow("q1", "Taken"), QuestRow("q2", "Done"), QuestRow("q3", "Taken", to: "loose")],
        Registry = [Repo("engine", adopted: true), Repo("loose", adopted: false)],
        Claims = new(StringComparer.Ordinal) { ["q1"] = "held", ["q3"] = "held" },
    };

    private static JsonObject Repo(string name, bool adopted) => new() { ["repository"] = name, ["adopted"] = adopted, ["root"] = "root-of-" + name };

    private Task<HandOverAnswer> AskQuestAsync(Room room, string id, string agent = "codex-acp", string? account = null) =>
        QuestHandOver.AskAsync(room.Client(), _home, Drives, id, agent, account, HandOverWhy.Limit, HandOverBy.Person, T0.AddMinutes(30));

    /// <summary>🔴 A quest's session handed to another agent keeps its box, as a conversation's is, and says the driver's next look takes it.</summary>
    /// <summary>
    /// 🔴 FABRIC-CLOSE1c20: a take lost to the person's own reopen is refused in the reopen's words and its own code, never as another
    /// machine's take.
    /// </summary>
    [Fact]
    public async Task A_take_lost_to_the_persons_reopen_is_refused_as_that()
    {
        var room = QuestRoom(Driven("qu3st000", minute: 20), Driven("else0000", "q4"));
        room.Quests.Add(QuestRow("q4", "Taken"));
        room.Claims["q4"] = "lost";
        room.Whys["q4"] = "source-reopened";

        var answer = await AskQuestAsync(room, "else0000");

        Assert.Equal((false, HandOver.QuestReopened), (answer.Kept, answer.Why));
        Assert.Contains("you reopened", answer.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("another machine", answer.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_quest_s_session_is_handed_over_by_keeping_its_box_for_the_drivers_look()
    {
        var answer = await AskQuestAsync(QuestRoom(), "qu3st000", account: "work");

        Assert.Equal((true, (string?)null), (answer.Kept, answer.Why));
        Assert.Equal("it is stopped at the driver's next look, then goes on in the same tree with codex-acp.", answer.Message);
        var box = new HandOverBox(_home).Read("qu3st000");
        Assert.Equal(("codex-acp", "work", HandOverWhy.Limit, HandOverBy.Person, T0.AddMinutes(30)), (box!.Agent, box.Account, box.Why, box.AskedBy, box.At));
    }

    public static TheoryData<string, string, string?, string, string> QuestRefusals => new()
    {
        { "qu3st000", "gemini", null, HandOver.UnknownAgent, "no agent here is called gemini." },
        { "qu3st000", "codex-acp", "other", HandOver.NotItsAccount, "other is not an account of codex-acp." },
        { "qu3st000", "claude-code", "account-1", HandOver.SameAgent, "qu3st000 already runs on claude-code as account-1." },
        { "d0n3q000", "codex-acp", null, HandOver.QuestClosed, "#q2 is not open or taken any more, so there is no work left to hand over." },
        { "else0000", "codex-acp", null, HandOver.QuestElsewhere, "#q4 is not taken by this machine, so its work is not this machine's to hand over." },
        { "st00d000", "codex-acp", null, HandOver.QuestElsewhere, "it stood down: #q1 is someone else's, so there is nothing of this machine's to hand over." },
        { "0ld00000", "codex-acp", null, HandOver.NotLastRun, "#q1 went on in a later session here, qu3st000: hand that one over." },
        { "p4rk0000", "codex-acp", null, HandOver.Parked, "p4rk0000 is waiting on you: answer it instead." },
        { "l00s0000", "claude-code", null, HandOver.NoDoor,
            "claude-code drives only repositories that have adopted Daoris, and `loose` has not: a session there would have no connector to take the quest with." },
    };

    [Theory]
    [MemberData(nameof(QuestRefusals))]
    public async Task What_a_quest_s_session_cannot_be_handed_over_for_is_refused_by_its_code_and_nothing_is_kept(
        string id, string agent, string? account, string why, string message)
    {
        var room = QuestRoom(
            Driven("qu3st000", minute: 20), Driven("0ld00000", minute: 0), Driven("d0n3q000", "q2"), Driven("else0000", "q4"),
            Driven("st00d000", state: "stood-down", minute: 40), Driven("p4rk0000", "q5", "awaiting-person"),
            Driven("l00s0000", "q3", repository: "loose"));
        room.Quests.Add(QuestRow("q4", "Taken"));
        room.Quests.Add(QuestRow("q5", "Taken"));
        room.Claims["q4"] = "none";
        room.Claims["q5"] = "held";

        var answer = await AskQuestAsync(room, id, agent, account);

        Assert.Equal((false, why, message), (answer.Kept, answer.Why, answer.Message));
        Assert.Empty(Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Sessions), "*" + HandOverBox.Suffix));
    }

    [Fact]
    public async Task A_second_ask_for_a_quest_s_session_waits_behind_the_first_and_an_open_quest_may_be_handed_over()
    {
        var room = QuestRoom();
        room.Quests[0] = QuestRow("q1", "Open");

        Assert.True((await AskQuestAsync(room, "qu3st000")).Kept);
        var again = await AskQuestAsync(room, "qu3st000", "claude-code");
        Assert.Equal((false, HandOver.Waiting), (again.Kept, again.Why));
        Assert.Equal("qu3st000 is already waiting to be handed over to codex-acp.", again.Message);
    }

    // ——— The quest's held override: <home>/handed/<quest>.json, written atomically, kept until the quest leaves this machine.

    [Fact]
    public void The_handed_store_round_trips_without_a_byte_order_mark_or_a_carriage_return_and_clears()
    {
        var store = new HandedQuests(_home);
        var handed = new HandedQuest("codex-acp", "work", "qu3st000", HandOverWhy.Near, HandOverBy.Person, T0);
        store.Write("q1", handed);

        var bytes = File.ReadAllBytes(store.PathOf("q1"));
        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.Handed, "q1.json"), store.PathOf("q1"));
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal(handed, store.Read("q1"));
        Assert.Equal(["q1"], store.All().Keys);

        store.Clear("q1");
        Assert.Null(store.Read("q1"));
        Assert.Empty(store.All());
    }

    [Fact]
    public void A_handed_file_that_does_not_read_or_a_quest_id_that_is_no_name_is_none()
    {
        var store = new HandedQuests(_home);
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Handed));
        File.WriteAllText(store.PathOf("q1"), "{ not json");

        Assert.Null(store.Read("q1"));
        Assert.Empty(store.All());
        Assert.Null(store.Read("../escape"));
        store.Write("../escape", new HandedQuest("codex-acp", null, "qu3st000", HandOverWhy.Press, HandOverBy.Person, T0));
        Assert.Empty(Directory.EnumerateFiles(_home, "escape*", SearchOption.AllDirectories));
        Assert.Null(store.PathOf("../escape"));
    }

    /// <summary>
    /// MIXAGENT1a-iii (D165 point 3): `curve` is the reason a record continues another by the person's switching setting, said in
    /// the briefing's and the record's English. SWITCH1c-i: the box takes it, since `chat.taken-over` now declares it, so the
    /// automatic move (SWITCH1c-ii) asks the box as a press does.
    /// </summary>
    [Fact]
    public void Curve_is_a_reason_a_record_continues_another_and_one_the_box_takes()
    {
        Assert.Equal("curve", HandOverWhy.Curve);
        Assert.Equal("the account it ran on was over the usage curve the person's switching setting draws", HandOverWhy.Briefed(HandOverWhy.Curve));
        Assert.Equal("its account was over its usage curve", HandOverWhy.Noted(HandOverWhy.Curve));
        Assert.True(HandOverWhy.Known(HandOverWhy.Curve));
    }

    /// <summary>The service's half of what the door reads: this machine's records, closed ones included, and a quest's state.</summary>
    private sealed class Room(IEnumerable<JsonObject> records) : HttpMessageHandler
    {
        private readonly List<JsonObject> _records = [.. records];

        public List<JsonObject> Quests { get; init; } = [];

        public List<JsonObject> Registry { get; init; } = [];

        public Dictionary<string, string> Claims { get; init; } = new(StringComparer.Ordinal);

        /// <summary>Why a lost claim lost, as the claim door says it beside the claim (FABRIC-CLOSE1c18).</summary>
        public Dictionary<string, string> Whys { get; init; } = new(StringComparer.Ordinal);

        public ServiceClient Client() => new("http://room.test", null, new HttpClient(this, disposeHandler: false));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/sessions") return Task.FromResult(Json(new JsonArray([.. _records.Select(each => each.DeepClone())])));
            if (request.Method == HttpMethod.Get && path == "/api/quests") return Task.FromResult(Json(new JsonArray([.. Quests.Select(each => each.DeepClone())])));
            if (request.Method == HttpMethod.Get && path == "/api/registry") return Task.FromResult(Json(new JsonArray([.. Registry.Select(each => each.DeepClone())])));
            if (request.Method == HttpMethod.Get && path.StartsWith("/api/quests/", StringComparison.Ordinal) && path.EndsWith("/claim", StringComparison.Ordinal))
            {
                var quest = path["/api/quests/".Length..^"/claim".Length];
                return Task.FromResult(Json(new JsonObject { ["claim"] = Claims.GetValueOrDefault(quest, "none"), ["why"] = Whys.GetValueOrDefault(quest) }));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":"no such door"}""") });
        }

        private static HttpResponseMessage Json(JsonNode body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json") };
    }
}
