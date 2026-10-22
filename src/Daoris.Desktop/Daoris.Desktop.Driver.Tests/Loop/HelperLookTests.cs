using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT2a-ii (D163 point 2; the mixed agents design §3 and §5): the look's helper session, in process. The asks on file are
/// read, judged and started beside the look; the state names the helper BEFORE its turn; what the helper said (its tool's answer,
/// else its last words, else nothing) settles the ask; and the answer reaches the asker as a word marked as the helper's, through
/// the local host's door, refused as the opinion's delivery is refused. The pass itself is a stand-in here (the real one is the
/// <c>Process</c> half's <c>HelperProcessTests</c>), so nothing starts a process: the fast half (MOD8).
/// </summary>
public sealed class HelperLookTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-helper-look-" + Guid.NewGuid().ToString("N")[..8]);

    private const string Helper = "h1000001";

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-12T10:00:00Z");

    public HelperLookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HelperFiles Files => new(_home);

    /// <summary>An ask on file as <c>helper_ask</c> writes it.</summary>
    private void Ask(
        string id, string question = "Why does it drop the last day?", string by = "agent", string? agent = null, string asker = "s1", bool urgent = false)
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));
        var ask = new JsonObject
        {
            ["id"] = id, ["asker"] = asker, ["question"] = question, ["agent"] = agent, ["askedBy"] = by, ["at"] = At.AddMinutes(int.Parse(id[^1..])).ToString("O"),
        };
        if (urgent) ask["urgent"] = true;
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Helpers, $"{id}.ask.json"), ask.ToJsonString());
    }

    private static HelperRule On => new(HelperRules.On, []);

    /// <summary>What the stand-in pass saw, and does: it names its helper at once, then says what the test has it say.</summary>
    private sealed class Pass(HelperFiles files)
    {
        public List<HelperPassAsk> Asked { get; } = [];
        public List<HelperState?> SeenAtTurn { get; } = [];
        public string? Answer { get; init; }
        public string? LastWords { get; init; }
        public string Ended { get; init; } = "completed";

        public Task<HelperPassRun> RunAsync(HelperPassAsk ask, ReviewerChoice choice, CancellationToken ct)
        {
            Asked.Add(ask);
            ask.Opened?.Invoke(Helper);
            // Its turn: the state it was started under is what `helper_answer` finds its ask by.
            SeenAtTurn.Add(files.ReadState(ask.Ask.Id));
            if (Answer is not null)
            {
                File.WriteAllText(files.PathOf(ask.Ask.Id, "answer"),
                    new JsonObject { ["id"] = ask.Ask.Id, ["helper"] = Helper, ["answer"] = Answer, ["at"] = At.ToString("O") }.ToJsonString());
            }

            return Task.FromResult(new HelperPassRun(Helper, $"{Ended}  helper {Helper}") { State = Ended, LastWords = LastWords });
        }
    }

    /// <summary>The asker's work in progress as the stand-in capture hands it (COAGENT1a): one changed file and one deletion.</summary>
    private static readonly HelperCaptured Captured = new(
        "c0ffee123456", OpinionStandIn.Tip, At, [new CapturedFile("src/report.ts", new string('a', 64), 12)], ["old.md"], []);

    private Daoris.Driver.Driver Driver(
        OpinionStandIn host, Pass? pass, HelperRule? rule = null, int cap = 3, ReviewerChoice? choice = null,
        Func<string, CancellationToken, Task<HelperCaptureRead>>? capture = null) =>
        new(host.Client(), DriverConfig.Empty with
        {
            Adapter = "claude-code-acp", Cap = cap,
            Helpers = new Dictionary<string, HelperRule> { ["reports"] = rule ?? On },
        }, AdapterSet.Built(), _home)
        {
            HelperPasses = pass is null ? (_, _, _) => throw new InvalidOperationException("no helper starts here") : pass.RunAsync,
            HelperTrees = (_, _) => Task.FromResult<(string?, string?)>((OpinionStandIn.Tip, "D:/checkouts/reports")),
            HelperCaptures = capture ?? ((_, _) => Task.FromResult(new HelperCaptureRead(Captured))),
            HelperChoices = (_, _, _, _) => Task.FromResult(choice ?? new ReviewerChoice("codex-acp", "another-maker", null, "codex-acp reads it.")),
        };

    /// <summary>The host with the asker's record and the record the pass's helper opened, a chat that serves it.</summary>
    private OpinionStandIn Host(string state = "completed", string kind = "drive")
    {
        var host = new OpinionStandIn().Session("s1", state, tree: "trees/default/reports/s-1", kind: kind)
            .Session(Helper, "completed", adapter: "codex-acp", kind: "chat");
        host.Set(Helper, "serves", "s1");
        return host;
    }

    private async Task Until(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!done())
        {
            if (DateTime.UtcNow > until)
            {
                var held = Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Helpers))
                    ? string.Join(" | ", Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Helpers)).Select(file => $"{Path.GetFileName(file)}: {File.ReadAllText(file).ReplaceLineEndings(" ")}"))
                    : "no helpers folder";
                throw new TimeoutException($"the helper's pass did not settle. {held}");
            }

            await Task.Delay(20);
        }
    }

    private async Task<List<string>> Look(Daoris.Driver.Driver driver, int taken = 0, IReadOnlyList<SessionView>? active = null)
    {
        var events = new List<string>();
        await driver.HelpersAsync(new Snapshot([], [], active ?? []), taken, events, CancellationToken.None);
        return events;
    }

    /// <summary>
    /// The asker's newest turn has ended (COAGENT1a): a running asker is between turns, so its work in progress may be captured and
    /// its helper started; what happens to the answer after is what the test holds.
    /// </summary>
    private void TurnEnded(string asker = "s1") =>
        new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).Append(asker, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });

    // ——— The rule, and the judge's refusals.

    /// <summary>Under the default rule, `ask`, the ask waits for the person's yes and nothing starts, look after look.</summary>
    [Fact]
    public async Task Under_the_rule_ask_the_ask_waits_for_the_persons_yes_and_starts_nothing()
    {
        Ask("a1");
        var driver = Driver(Host(), pass: null, rule: new HelperRule(HelperRules.Ask, []));

        await Look(driver);
        await Look(driver);

        var state = Files.ReadState("a1")!;
        Assert.Equal((HelperStates.Waiting, null, null), (state.State, state.Code, state.Helper));
    }

    /// <summary>
    /// MIXAGENT2c (D168): an agent's ask that comes to wait on the person's word is said once in the look's report, so the loop's
    /// tick reaches the windows and the page asks what waits on the person again; the same wait at a later look says nothing.
    /// </summary>
    [Fact]
    public async Task An_ask_that_comes_to_wait_on_the_person_is_said_once()
    {
        Ask("a1");
        var driver = Driver(Host(), pass: null, rule: new HelperRule(HelperRules.Ask, []));

        var first = await Look(driver);
        var second = await Look(driver);

        Assert.Equal("helper  ask `a1` from session `s1` waits for the person's yes or no in What needs you", Assert.Single(first));
        Assert.Empty(second);
    }

    /// <summary>A person's yes (`approved`, MIXAGENT2b's press) lets it go under `ask`: a helper starts and answers.</summary>
    [Fact]
    public async Task An_approved_ask_goes_under_the_rule_ask()
    {
        Ask("a1");
        Files.WriteState(new HelperState("a1", HelperStates.Approved, null, null, At));
        var host = Host();
        var pass = new Pass(Files) { Answer = "It is exclusive at line 42." };
        var driver = Driver(host, pass, rule: new HelperRule(HelperRules.Ask, []));

        await Look(driver);
        await Until(() => host.HelperHands.Count == 1);

        Assert.Single(pass.Asked);
    }

    /// <summary>
    /// The person's no (HELPERNO1, the bridge's `SESSION_HELPER_DECLINE`): the ask is refused `declined` with its note already on
    /// the asker's record, so a look starts nothing, under any rule, and writes no second note and no line.
    /// </summary>
    [Theory]
    [InlineData(HelperRules.Ask)]
    [InlineData(HelperRules.On)]
    public async Task A_declined_ask_starts_nothing_and_is_not_said_again(string mode)
    {
        Ask("a1");
        var declined = new HelperState("a1", HelperStates.Refused, HelperWhy.Declined, null, At);
        Files.WriteState(declined);
        var driver = Driver(Host(), pass: null, rule: new HelperRule(mode, []));

        var events = await Look(driver);

        Assert.Empty(events);
        Assert.Equal(declined, Files.ReadState("a1"));
        Assert.DoesNotContain(new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events, e => e.Kind == SessionEventKind.Note);
    }

    [Fact]
    public async Task With_the_rule_off_an_agents_ask_is_refused_and_the_asker_reads_why_by_a_coded_note()
    {
        Ask("a1");
        var host = Host();
        var driver = Driver(host, pass: null, rule: new HelperRule(HelperRules.Off, []));

        var events = await Look(driver);

        var state = Files.ReadState("a1")!;
        Assert.Equal((HelperStates.Refused, HelperWhy.Off), (state.State, state.Code));
        Assert.Contains("refused (off)", Assert.Single(events));
        var note = Assert.Single(new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal(["helper.refused"], NoteAssert.Codes(note.Parts!));
        Assert.Equal("off", note.Parts![0].Value("why"));
        Assert.DoesNotContain("trees", note.Text);
    }

    [Theory]
    [InlineData("gone", HelperWhy.AskerGone)]
    [InlineData("helper", HelperWhy.AskerUnfit)]
    [InlineData("reviewer", HelperWhy.AskerUnfit)]
    [InlineData("intake", HelperWhy.AskerUnfit)]
    public async Task An_asker_that_is_gone_or_cannot_ask_is_refused_again_by_the_driver(string row, string code)
    {
        Ask("a1");
        var host = row == "gone" ? new OpinionStandIn() : Host();
        switch (row)
        {
            case "helper": host.Set("s1", "serves", "s0"); break;
            case "reviewer": host.Set("s1", "opinion", "o1"); break;
            case "intake": host.Set("s1", "ask", "a9"); break;
        }

        await Look(Driver(host, pass: null));

        Assert.Equal((HelperStates.Refused, code), (Files.ReadState("a1")!.State, Files.ReadState("a1")!.Code));
    }

    /// <summary>
    /// NOTIFY2h2 (D196's NOTIFY2h note): a refusal because the asker has left tells nobody, so it is news, a coded notice in <i>Done and
    /// failed</i>; one the asker is told of on its own session (it cannot ask) is the log's alone. The line itself is as it was.
    /// </summary>
    [Theory]
    [InlineData("gone", true)]
    [InlineData("helper", false)]
    public async Task A_refusal_whose_asker_has_left_is_news_and_one_it_is_told_of_is_not(string row, bool news)
    {
        Ask("a1");
        var host = row == "gone" ? new OpinionStandIn() : Host();
        if (row == "helper") host.Set("s1", "serves", "s0");

        var events = await Look(Driver(host, pass: null));

        Assert.StartsWith("helper  ask `a1` refused (", Assert.Single(events));
        var notices = Notices.Read(_home, DateTimeOffset.UtcNow)!;
        if (news) Assert.Equal(("look-helper-asker-gone-a1", LookSaid.HelperAskerGone), (Assert.Single(notices).Id, Assert.Single(notices).Code));
        else Assert.Empty(notices);
    }

    /// <summary>At most two helpers at once per asker: a third is refused `too-many`, and a finished one frees a place.</summary>
    [Fact]
    public async Task At_most_two_helpers_at_once_per_asker()
    {
        Ask("a1"); Ask("a2"); Ask("a3");
        Files.WriteState(new HelperState("a1", HelperStates.Started, null, "h-1", At));
        Files.WriteState(new HelperState("a2", HelperStates.Started, null, "h-2", At));
        var host = Host();
        host.Session("h-1", "working").Session("h-2", "working");

        await Look(Driver(host, pass: null));

        var third = Files.ReadState("a3")!;
        Assert.Equal((HelperStates.Refused, HelperWhy.TooMany), (third.State, third.Code));
    }

    /// <summary>A full cap only waits: no refusal, no start, and a later look with a slot free starts it.</summary>
    [Fact]
    public async Task A_full_cap_waits_without_a_refusal_and_a_free_slot_starts_it()
    {
        Ask("a1");
        var host = Host();
        var pass = new Pass(Files) { Answer = "Line 42." };
        var driver = Driver(host, pass, cap: 1);

        await Look(driver, active: [new SessionView("w1", "q9", "reports", "working")]);

        Assert.Equal((HelperStates.Waiting, HelperVerdict.Cap), (Files.ReadState("a1")!.State, Files.ReadState("a1")!.Code));
        Assert.Empty(pass.Asked);

        await Look(driver);
        await Until(() => host.HelperHands.Count == 1);
        Assert.Single(pass.Asked);
    }

    /// <summary>
    /// CAPCOUNT1 (D130's note): the cap counts what the driver starts. A conversation the person holds (Ask Daoris's, here) takes no
    /// slot from a helper on a cap of one; an intake the driver started, or another helper, still does.
    /// </summary>
    [Fact]
    public async Task A_conversation_the_person_holds_takes_no_slot_from_a_helper_but_what_the_driver_started_does()
    {
        Ask("a1");
        var host = Host();
        var pass = new Pass(Files) { Answer = "Line 42." };
        var driver = Driver(host, pass, cap: 1);

        await Look(driver, active: [new SessionView("i1", "ask #a0", "working") { Kind = "chat", Ask = "a0" }]);
        Assert.Equal((HelperStates.Waiting, HelperVerdict.Cap), (Files.ReadState("a1")!.State, Files.ReadState("a1")!.Code));

        await Look(driver, active: [new SessionView("w2", "reports", "working") { Kind = "chat", Serves = "s5" }]);
        Assert.Equal((HelperStates.Waiting, HelperVerdict.Cap), (Files.ReadState("a1")!.State, Files.ReadState("a1")!.Code));
        Assert.Empty(pass.Asked);

        await Look(driver, active: [new SessionView("h1", HelpRoom.Repository, "working") { Kind = "chat" }]);
        await Until(() => host.HelperHands.Count == 1);
        Assert.Single(pass.Asked);
    }

    /// <summary>
    /// HELPERYES1 (D163 point 3): the person's yes outlives a full cap. The look that finds no slot leaves the ask `approved`
    /// (not `waiting/cap`, which the next look's judge would put back to waiting for a yes), and a look with a slot starts it
    /// without a second yes.
    /// </summary>
    [Fact]
    public async Task An_approved_ask_keeps_its_yes_through_a_full_cap_and_starts_once_a_slot_frees()
    {
        Ask("a1");
        var approved = new HelperState("a1", HelperStates.Approved, null, null, At);
        Files.WriteState(approved);
        var host = Host();
        var pass = new Pass(Files) { Answer = "Line 42." };
        var driver = Driver(host, pass, rule: new HelperRule(HelperRules.Ask, []), cap: 1);
        var full = new[] { new SessionView("w1", "q9", "reports", "working") };

        var first = await Look(driver, active: full);
        var second = await Look(driver, active: full);

        Assert.Equal(approved, Files.ReadState("a1"));
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Empty(pass.Asked);

        await Look(driver);
        await Until(() => host.HelperHands.Count == 1);
        Assert.Single(pass.Asked);
    }

    /// <summary>No agent ready: the ask waits and the state says why (the walk's code); nothing starts and nothing is refused.</summary>
    [Fact]
    public async Task With_no_agent_ready_the_ask_waits_and_the_state_says_why()
    {
        Ask("a1");
        var none = new ReviewerChoice(null, null, ReviewerUnavailable.Cooling, "No second opinion: every listed reviewer of another maker is cooling.");

        await Look(Driver(Host(), pass: null, choice: none));
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Waiting });

        var state = Files.ReadState("a1")!;
        Assert.Equal((HelperStates.Waiting, ReviewerUnavailable.Cooling), (state.State, state.Code));
    }

    // ——— The pass, the answer's three outcomes, and the delivery.

    /// <summary>
    /// 🔴 The whole pass on stand-ins: the ask is started naming its helper BEFORE the helper's turn; the helper's tool wrote its
    /// answer; the state is `answered`; and the answer reaches the asker as a word marked as the helper's, prefaced as its claim,
    /// with a coded note on the asker. The agent chosen is the one the walk named, and the pass is handed the asker's last commit.
    /// </summary>
    [Fact]
    public async Task A_helpers_whole_pass_answers_the_asker_with_a_word_marked_as_the_helpers()
    {
        Ask("a1", question: "Why does it drop the last day?");
        var host = Host();
        var pass = new Pass(Files) { Answer = "The window is exclusive at src/report.ts:42." };

        var events = await Look(Driver(host, pass));
        await Until(() => host.HelperHands.Count == 1);

        var seen = Assert.Single(pass.SeenAtTurn);
        Assert.Equal((HelperStates.Started, Helper, "codex-acp"), (seen!.State, seen.Helper, seen.Agent));
        var ask = Assert.Single(pass.Asked);
        Assert.Equal(("reports", OpinionStandIn.Tip, "D:/checkouts/reports"), (ask.Repository, ask.Tip, ask.Root));
        var state = Files.ReadState("a1")!;
        Assert.Equal((HelperStates.Answered, false), (state.State, state.LastWords));
        Assert.NotNull(state.Handed);

        var (to, text) = Assert.Single(host.HelperHands);
        Assert.Equal(Helper, to);
        Assert.StartsWith("Another agent, `codex-acp` (helper session `h1000001`), answered the question you asked it", text);
        Assert.Contains("> Why does it drop the last day?", text);
        Assert.Contains("> The window is exclusive at src/report.ts:42.", text);
        var word = Assert.Single(host.Said("s1"));
        Assert.Equal((Helper, null), (word!["helper"]!.GetValue<string>(), word["by"]?.GetValue<string>()));

        var notes = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events.Where(e => e.Kind == SessionEventKind.Note).ToList();
        Assert.Equal(["helper.asked", "helper.answered"], notes.SelectMany(note => NoteAssert.Codes(note.Parts!)));
        Assert.All(notes, note => Assert.DoesNotContain("D:/", note.Text));
        var helping = Assert.Single(new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(Helper, 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal(["helper.helping"], NoteAssert.Codes(helping.Parts!));
        Assert.Empty(events);
    }

    /// <summary>The helper never used its tool: its last words, bounded, stand as the answer, marked with a `lastWords` mark.</summary>
    [Fact]
    public async Task A_helper_that_used_no_tool_has_its_last_words_stand_as_the_answer_with_a_mark()
    {
        Ask("a1");
        var host = Host();
        var pass = new Pass(Files) { LastWords = "Probably the exclusive window." };

        await Look(Driver(host, pass));
        await Until(() => host.HelperHands.Count == 1);

        var state = Files.ReadState("a1")!;
        Assert.Equal((HelperStates.Answered, true), (state.State, state.LastWords));
        var (_, text) = Assert.Single(host.HelperHands);
        Assert.Contains("did not use its answer tool", text);
        Assert.Contains("> Probably the exclusive window.", text);
        var notes = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events.Where(e => e.Kind == SessionEventKind.Note).ToList();
        Assert.Equal(["helper.asked", "helper.answered-last-words"], notes.SelectMany(note => NoteAssert.Codes(note.Parts!)));
    }

    /// <summary>No answer and no last words (or a turn that failed): refused `no-answer`, with the agent, and nothing handed.</summary>
    [Theory]
    [InlineData("completed", null)]
    [InlineData("failed", "It said something before it failed.")]
    public async Task A_helper_that_said_nothing_is_refused_no_answer(string ended, string? words)
    {
        Ask("a1");
        var host = Host();
        var pass = new Pass(Files) { Ended = ended, LastWords = words };

        await Look(Driver(host, pass));
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Refused });

        Assert.Equal(HelperWhy.NoAnswer, Files.ReadState("a1")!.Code);
        Assert.Empty(host.HelperHands);
        var note = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events.Last(e => e.Kind == SessionEventKind.Note);
        Assert.Equal(["helper.refused-by"], NoteAssert.Codes(note.Parts!));
        Assert.Equal(("codex-acp", "no-answer"), (note.Parts![0].Value("agent"), note.Parts[0].Value("why")));
    }

    /// <summary>A helper that could not be opened (the host refused its record) refuses the ask `not-started`, with the agent.</summary>
    [Fact]
    public async Task A_pass_that_opened_no_helper_refuses_the_ask_not_started()
    {
        Ask("a1");
        var host = Host();
        var driver = new Daoris.Driver.Driver(host.Client(), DriverConfig.Empty with { Cap = 3, Helpers = new Dictionary<string, HelperRule> { ["reports"] = On } }, AdapterSet.Built(), _home)
        {
            HelperPasses = (_, _, _) => Task.FromResult(new HelperPassRun(null, "refused  helper") { Code = HelperWhy.NotStarted }),
            HelperTrees = (_, _) => Task.FromResult<(string?, string?)>((OpinionStandIn.Tip, "D:/checkouts/reports")),
            HelperCaptures = (_, _) => Task.FromResult(new HelperCaptureRead(Captured)),
            HelperChoices = (_, _, _, _) => Task.FromResult(new ReviewerChoice("codex-acp", "another-maker", null, "x")),
        };

        await Look(driver);
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Refused });

        Assert.Equal(HelperWhy.NotStarted, Files.ReadState("a1")!.Code);
    }

    /// <summary>
    /// Never into a step in flight: while the asker runs the answer waits, answered but not handed; the look after its turn ends
    /// hands it, once, and a look after that hands nothing more.
    /// </summary>
    [Fact]
    public async Task A_running_asker_takes_the_answer_at_its_turn_s_end_once()
    {
        Ask("a1");
        var host = Host(state: "working");
        var pass = new Pass(Files) { Answer = "Line 42." };
        var driver = Driver(host, pass);
        TurnEnded();

        await Look(driver);
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Answered });
        await Look(driver);
        Assert.Empty(host.HelperHands);
        Assert.Null(Files.ReadState("a1")!.Handed);

        host.Move("s1", "completed");
        var events = await Look(driver);
        Assert.Single(host.HelperHands);
        Assert.Contains(events, line => line.Contains("answered by"));
        Assert.NotNull(Files.ReadState("a1")!.Handed);

        await Look(driver);
        Assert.Single(host.HelperHands);
    }

    /// <summary>
    /// 🔴 COWORK1d (the message-urgency design §5): an urgent ask's answer reaches a running asker this loop runs at its next step,
    /// where its door takes words then: kept on its record for its run with its level, held in its inbox under the record's id and sent
    /// at once, recorded as Daoris's words around the helper's claim, never the person's; and handed once.
    /// </summary>
    [Fact]
    public async Task An_urgent_answer_reaches_a_running_asker_at_its_next_step_once()
    {
        Ask("a1", urgent: true);
        var host = Host(state: "working");
        var driver = Driver(host, new Pass(Files) { Answer = "Line 42." });
        var sent = new List<ChatMessage>();
        var inbox = driver.Context.Processes.OpenInbox("s1");
        inbox.Attach(interrupt: null, deliver: message =>
        {
            lock (sent) sent.Add(message);
            return Task.FromResult("end_turn");
        });
        inbox.Flow();
        TurnEnded();

        await Look(driver);
        await Until(() => Files.ReadState("a1") is { Handed: not null });

        var answer = Assert.Single(sent);
        Assert.Contains("Line 42.", answer.Text);
        Assert.Equal("target", ChatMessage.Words(answer).Origin);
        var word = host.Said("s1")[0]!;
        Assert.Equal((word["id"]!.GetValue<string>(), "urgent"), (answer.Id, word["level"]?.GetValue<string>()));
        Assert.Empty(inbox.State.Queued);
        await Look(driver);
        Assert.Single(host.HelperHands);
    }

    /// <summary>
    /// An urgent answer to a running asker whose door takes words only at its turn's end, or that this loop does not run, waits for its
    /// end, as an ordinary one does: never into a step in flight.
    /// </summary>
    [Fact]
    public async Task An_urgent_answer_to_a_running_asker_on_a_turn_end_door_waits_for_its_end()
    {
        Ask("a1", urgent: true);
        var host = Host(state: "working");
        var driver = Driver(host, new Pass(Files) { Answer = "Line 42." });
        driver.Context.Processes.OpenInbox("s1").Attach(interrupt: null);
        TurnEnded();

        await Look(driver);
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Answered });
        await Look(driver);

        Assert.Empty(host.HelperHands);
        Assert.Null(Files.ReadState("a1")!.Handed);
    }

    /// <summary>An asker that stood down never goes on: refused with its code, and nothing handed.</summary>
    [Theory]
    [InlineData("stood-down", HelperWhy.CannotTake)]
    public async Task An_asker_that_never_goes_on_is_refused_with_its_code(string state, string code)
    {
        Ask("a1");
        var host = Host(state: state);
        var pass = new Pass(Files) { Answer = "Line 42." };

        await Look(Driver(host, pass));
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Refused });

        Assert.Equal(code, Files.ReadState("a1")!.Code);
        Assert.Empty(host.HelperHands);
    }

    /// <summary>
    /// MIXAGENT2a-iii: a conversation asker is handed the answer, open between turns (`working`) or ended, and the driver leaves
    /// the shell's chat runner a shown event it hears (the person's say door's, reaching on `resume`) whose origin is never the
    /// person's: Daoris's words around the helper's claim. One still being started waits, as a quest's step in flight does.
    /// </summary>
    [Theory]
    [InlineData("working")]
    [InlineData("completed")]
    public async Task A_conversation_asker_is_handed_the_answer_and_the_runner_is_left_an_event_to_hear(string state)
    {
        Ask("a1");
        var host = Host(state: state, kind: "chat");
        var pass = new Pass(Files) { Answer = "Line 42." };
        TurnEnded();

        await Look(Driver(host, pass));
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Answered, Handed: not null });

        Assert.Single(host.HelperHands);
        var shown = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events
            .Single(e => e.Kind == SessionEventKind.User);
        Assert.Equal(("target", "resume", host.Said("s1")[0]!["id"]!.GetValue<string>()), (shown.Origin, shown.Reaches, shown.Id));
        Assert.Equal(host.HelperHands[0].Text, shown.Text);
        Assert.Contains("Line 42.", shown.Text);
    }

    /// <summary>A conversation still being started is no open conversation: the answer waits, answered and not handed.</summary>
    [Fact]
    public async Task A_conversation_asker_still_starting_takes_the_answer_when_it_is_open()
    {
        Ask("a1");
        var host = Host(state: "starting", kind: "chat");
        var driver = Driver(host, new Pass(Files) { Answer = "Line 42." });
        TurnEnded();

        await Look(driver);
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Answered });
        await Look(driver);
        Assert.Empty(host.HelperHands);

        host.Move("s1", "working");
        await Look(driver);
        Assert.Single(host.HelperHands);
    }

    /// <summary>
    /// A restart: a `started` ask whose helper ended, the sweep's interrupted ending included, is settled at the next look (Codex's round-2
    /// #8 on COWORKUX1): with an answer `helper_answer` saved before the cut it is delivered, whatever the helper's ending; with none it is
    /// refused no-answer, which brings a session waiting on it to the person.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_started_ask_whose_helper_ended_before_a_restart_is_settled_at_the_next_look(bool saved)
    {
        Ask("a1");
        Files.WriteState(new HelperState("a1", HelperStates.Started, null, Helper, At) { Agent = "codex-acp" });
        if (saved)
        {
            File.WriteAllText(Files.PathOf("a1", "answer"),
                new JsonObject { ["id"] = "a1", ["helper"] = Helper, ["answer"] = "Line 42.", ["at"] = At.ToString("O") }.ToJsonString());
        }

        var host = Host();
        host.Move(Helper, "stopped");

        await Look(Driver(host, pass: null));

        var state = Files.ReadState("a1")!;
        if (saved)
        {
            Assert.Equal(HelperStates.Answered, state.State);
            Assert.Single(host.HelperHands);
        }
        else
        {
            Assert.Equal((HelperStates.Refused, HelperWhy.NoAnswer), (state.State, state.Code));
            Assert.Empty(host.HelperHands);
        }
    }

    // ——— HELPERWAIT1 (D163's and D188's notes): a session waiting on its own helper is not waiting on the person.

    /// <summary>An answer on file, as <c>helper_answer</c> writes it, and the ask answered by the helper that wrote it.</summary>
    private void Answered(string id = "a1", string answer = "The window is exclusive at src/report.ts:42.")
    {
        File.WriteAllText(Files.PathOf(id, "answer"),
            new JsonObject { ["id"] = id, ["helper"] = Helper, ["answer"] = answer, ["at"] = At.ToString("O") }.ToJsonString());
        Files.WriteState(new HelperState(id, HelperStates.Answered, null, Helper, At) { Agent = "codex-acp" });
    }

    private static JsonArray Parts(params string[] codes) =>
        new([.. codes.Select(code => (JsonNode)new JsonObject { ["code"] = code, ["values"] = new JsonObject(), ["text"] = "a line." })]);

    /// <summary>
    /// The asker parked as the install parked it before the rule: the clean-exit park, its quest taken, made at <paramref name="minute"/>
    /// after the look's clock (the ask <c>a1</c> was made at its first minute).
    /// </summary>
    private OpinionStandIn ParkedHost(int minute = 5, string code = "ended.parked-asked", string? answer = null)
    {
        var host = Host(state: "awaiting-person").Quest("q1", "Taken");
        host.Set("s1", "noteParts", Parts(code));
        host.Set("s1", "note", "It stopped with its quest still taken, to ask you:\n\nI asked a helper.");
        host.Set("s1", "updated", At.AddMinutes(minute).ToString("O"));
        host.Set("s1", "answer", answer);
        return host;
    }

    private static SessionView Active(string state) => new("s1", "reports", state) { Quest = "q1" };

    /// <summary>
    /// 🔴 The install's case, as Codex's round-2 #1 and #4 on COWORKUX1 read it: a session parked before the rule on the clean exit, after
    /// its own helper ask, may have asked the person in its closing words alone, so nothing proves the park is no question of theirs. It
    /// stays parked, and its helper's answer waits, said once; once the person answers, the next look hands it beside their answer, never
    /// moving the park, so its go-on takes both in one prompt.
    /// </summary>
    [Fact]
    public async Task A_session_parked_on_its_own_helper_before_the_rule_takes_the_answer_beside_the_persons()
    {
        Ask("a1");
        Answered();
        var host = ParkedHost();
        host.Set("s1", "note", "It stopped with its quest still taken, to ask you:\n\nI asked a helper. Which port should it use?");
        var driver = Driver(host, pass: null);

        var held = await Look(driver, active: [Active("awaiting-person")]);
        var again = await Look(driver, active: [Active("awaiting-person")]);

        Assert.Contains("waits for the person's answer to session s1", Assert.Single(held));
        Assert.Empty(again);
        Assert.Empty(host.Moves);
        Assert.Empty(host.HelperHands);

        host.Set("s1", "answer", "Port 8080.");
        var events = await Look(driver, active: [Active("awaiting-person") with { Answer = "Port 8080." }]);

        Assert.Empty(host.Moves);
        var (to, text) = Assert.Single(host.HelperHands);
        Assert.Equal(Helper, to);
        Assert.StartsWith("Another agent, `codex-acp` (helper session `h1000001`), answered the question you asked it", text);
        Assert.Equal(Helper, Assert.Single(host.Said("s1"))!["helper"]!.GetValue<string>());
        Assert.NotNull(Files.ReadState("a1")!.Handed);
        Assert.Contains(events, line => line.Contains("answered by codex-acp", StringComparison.Ordinal));
    }

    /// <summary>
    /// A park for a real question is unchanged, and helper words alone never move it: its question on file (<c>question_ask</c>), a park
    /// made before the ask, a park for another reason, and an ask the person made each hold the helper's answer until the person answers.
    /// </summary>
    [Theory]
    [InlineData("question")]
    [InlineData("before")]
    [InlineData("other")]
    [InlineData("person")]
    public async Task A_park_for_a_real_question_keeps_the_helpers_answer_until_its_turn_ends(string row)
    {
        Ask("a1", by: row == "person" ? "person" : "agent");
        Answered();
        var host = row switch
        {
            "before" => ParkedHost(minute: 0),
            "other" => ParkedHost(code: "ended.parked-short"),
            _ => ParkedHost(),
        };
        if (row == "question")
        {
            Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
            File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.question.json"),
                new JsonObject { ["id"] = "k1", ["question"] = "Which port?", ["at"] = At.AddMinutes(4).ToString("O") }.ToJsonString());
        }

        await Look(Driver(host, pass: null), active: [Active("awaiting-person")]);

        Assert.Empty(host.Moves);
        Assert.Empty(host.HelperHands);
        Assert.Null(Files.ReadState("a1")!.Handed);
    }

    /// <summary>
    /// 🔴 Point 4: a helper that ends with no answer brings the session that waited on it back to the person as its turn's end would have,
    /// parked, the reason first (the refusal by its code and agent) and the park's own line after it; a session with another of its own
    /// asks still open goes on waiting.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_helper_that_ends_with_no_answer_brings_its_waiting_asker_back_to_the_person(bool another)
    {
        Ask("a1");
        var host = Host().Quest("q1", "Taken");
        host.Set("s1", "noteParts", Parts("ended.awaits-helper"));
        if (another)
        {
            // Its second helper still reads.
            Ask("a2");
            Files.WriteState(new HelperState("a2", HelperStates.Started, null, "h-2", At) { Agent = "codex-acp" });
            host.Session("h-2", "working");
        }

        var driver = Driver(host, new Pass(Files) { Ended = "completed" });

        await Look(driver);
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Refused });
        var waiting = new PriorSession("s1", "trees/default/reports/s-1", "completed")
        {
            Quest = "q1", NoteParts = [NoteCodes.EndedAwaitsHelper.Part("a line.", [])], Said = [],
        };
        await driver.HelpersAsync(
            new Snapshot([new QuestView("q1", "ask #a1", "reports", "The work", "", "Taken")], [], [])
            {
                LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = waiting },
            },
            0, [], CancellationToken.None);

        if (another)
        {
            Assert.Empty(host.Moves);
            return;
        }

        var (session, state, code) = Assert.Single(host.Moves);
        Assert.Equal(("s1", "awaiting-person", "helper.refused-by"), (session, state, code));
        var note = host.Note("s1");
        Assert.StartsWith("Helper codex-acp did not answer this question: the helper ended without an answer.\n\nIt stopped with its quest still taken", note);
    }

    /// <summary>
    /// 🔴 Codex's round-2 #5 on COWORKUX1: a failed move back to the person is tried again at the next look, though nothing moved the record,
    /// and said once: a service that did not answer as a retry, one that refused the move to the person as theirs to look at.
    /// </summary>
    [Theory]
    [InlineData("unreachable")]
    [InlineData("refused")]
    public async Task A_failed_move_back_to_the_person_is_tried_again_at_the_next_look_and_said_once(string failure)
    {
        Ask("a1");
        Files.WriteState(new HelperState("a1", HelperStates.Refused, HelperWhy.NoAnswer, Helper, At) { Agent = "codex-acp" });
        var host = Host().Quest("q1", "Taken");
        host.Set("s1", "noteParts", Parts("ended.awaits-helper"));
        host.StateFailure = failure;
        var driver = Driver(host, pass: null);
        var waiting = new PriorSession("s1", "trees/default/reports/s-1", "completed")
        {
            Quest = "q1", NoteParts = [NoteCodes.EndedAwaitsHelper.Part("a line.", [])], Said = [], Updated = At,
        };
        var snapshot = new Snapshot([new QuestView("q1", "ask #a1", "reports", "The work", "", "Taken")], [], [])
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = waiting },
        };

        var events = new List<string>();
        await driver.HelpersAsync(snapshot, 0, events, CancellationToken.None);
        await driver.HelpersAsync(snapshot, 0, events, CancellationToken.None);

        Assert.Equal(2, host.MovesAsked);
        Assert.Contains(failure == "refused" ? "so it is yours to look at" : "tried again at the next look", Assert.Single(events));
        Assert.Empty(host.Moves);

        host.StateFailure = null;
        await driver.HelpersAsync(snapshot, 0, events, CancellationToken.None);
        Assert.Equal(("s1", "awaiting-person", "helper.refused-by"), Assert.Single(host.Moves));
    }

    /// <summary>
    /// 🔴 Point 6, the cost: a hand held while its asker runs is said once, and while the look's own list shows the asker as it was when
    /// the hand was held nothing is read again for it; once the asker moves, the next look hands it.
    /// </summary>
    [Fact]
    public async Task A_held_hand_is_said_once_and_read_again_only_once_its_asker_moves()
    {
        Ask("a1");
        Answered();
        var host = Host(state: "working");
        var driver = Driver(host, pass: null);

        var first = await Look(driver, active: [Active("working")]);
        var reads = host.RecordReads;
        var second = await Look(driver, active: [Active("working")]);
        var third = await Look(driver, active: [Active("working")]);

        Assert.Contains("waits for session s1's turn to end", Assert.Single(first));
        Assert.Empty(second);
        Assert.Empty(third);
        Assert.Equal(reads, host.RecordReads);
        Assert.Empty(host.HelperHands);

        host.Move("s1", "completed");
        await Look(driver);
        Assert.Single(host.HelperHands);
    }

    /// <summary>
    /// 🔴 HELPERWAIT1 (the look stops retrying a hand it cannot make): a hand the host refuses is said once, and is not asked again while the
    /// look's records show its asker as they did when it was refused; once the asker's record moves, the next look asks again.
    /// </summary>
    [Fact]
    public async Task A_hand_the_host_refuses_is_said_once_and_asked_again_only_once_its_asker_moves()
    {
        Ask("a1");
        Answered();
        var host = Host();
        host.HelperHandRefusal = "the desk cannot take it now";
        var driver = Driver(host, pass: null);
        var record = new PriorSession("s1", "trees/default/reports/s-1", "completed") { Quest = "q1", Updated = At };
        Snapshot Records(PriorSession last) =>
            new([], [], []) { LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = last } };

        var events = new List<string>();
        foreach (var _ in Enumerable.Range(0, 3)) await driver.HelpersAsync(Records(record), 0, events, CancellationToken.None);

        Assert.Equal(1, host.HelperHandsAsked);
        Assert.Contains("the desk cannot take it now", Assert.Single(events));
        Assert.Null(Files.ReadState("a1")!.Handed);

        host.HelperHandRefusal = null;
        await driver.HelpersAsync(Records(record with { Updated = At.AddMinutes(1) }), 0, events, CancellationToken.None);
        Assert.Single(host.HelperHands);
        Assert.NotNull(Files.ReadState("a1")!.Handed);
    }

    /// <summary>
    /// 🔴 One hand per ask (Fable's read of COWORKUX1, F3): a restart that cut the look between the keep and the state's mark finds the
    /// answer already handed, its word still waiting on the asker or a turn of the asker's already opened with it, and marks it handed
    /// without handing it twice.
    /// </summary>
    [Theory]
    [InlineData("waiting")]
    [InlineData("taken")]
    public async Task An_answer_handed_before_a_restart_cut_its_mark_is_never_handed_twice(string where)
    {
        Ask("a1");
        Answered();
        var host = Host();
        if (where == "waiting")
        {
            host.Set("s1", "said", new JsonArray(new JsonObject
            {
                ["id"] = "hp-w9", ["text"] = "Another agent answered.", ["at"] = At.ToString("O"), ["files"] = new JsonArray(), ["reopens"] = true,
                ["helper"] = Helper,
            }));
        }
        else
        {
            new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).Append(
                "s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Helper = Helper, Id = "hp-w9", Text = "Another agent answered." });
        }

        await Look(Driver(host, pass: null));

        Assert.Empty(host.HelperHands);
        Assert.Equal("hp-w9", Files.ReadState("a1")!.Handed);
    }

    /// <summary>
    /// 🔴 Codex's round-2 #2 and #7 on COWORKUX1: a hand that began (its mark written before the door) and whose outcome a restart lost is
    /// never made again on the absence of evidence, since an event's write may have failed and a word may have moved: with nothing on the
    /// line it settles as uncertain for the person; a conversation's notice to its runner that the word waits is no evidence of a taking.
    /// </summary>
    [Theory]
    [InlineData("nothing")]
    [InlineData("notice")]
    public async Task An_attempt_a_restart_cut_with_no_evidence_is_settled_uncertain_and_never_handed_again(string left)
    {
        Ask("a1");
        Answered();
        Files.WriteState(Files.ReadState("a1")! with { Attempted = At });
        if (left == "notice")
        {
            new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).Append(
                "s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Helper = Helper, Id = "hp-w9", Text = "Another agent answered.", Reaches = "resume" });
        }

        var host = Host();
        await Look(Driver(host, pass: null));

        Assert.Empty(host.HelperHands);
        Assert.Equal(0, host.HelperHandsAsked);
        var state = Files.ReadState("a1")!;
        Assert.Equal((HelperStates.Refused, HelperWhy.HandUncertain, Helper), (state.State, state.Code, state.Helper));
        Assert.True(HelperWaits.Open("s1", [new HelperAsk("a1", "s1", "Why?", null, HelperAsk.ByAgent, At)], Files.ReadState).Count == 0);
    }

    /// <summary>
    /// 🔴 Codex's round-2 #2: the evidence is read along the asker's line. A turn of the commissioner took the answer, and the line was handed
    /// over before the mark was written: the ask is marked handed, and its successor is never handed it again.
    /// </summary>
    [Fact]
    public async Task An_answer_taken_before_a_relay_is_found_on_the_line_and_not_handed_to_the_successor()
    {
        Ask("a1");
        Answered();
        Files.WriteState(Files.ReadState("a1")! with { Attempted = At });
        new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).Append(
            "s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Helper = Helper, Id = "hp-w9", Text = "Another agent answered." });
        var host = Host(state: "stopped").Session("s2", "completed", tree: "trees/default/reports/s-1", kind: "drive");
        host.Set("s2", "from", "s1");

        await Look(Driver(host, pass: null));

        Assert.Equal(0, host.HelperHandsAsked);
        Assert.Equal("hp-w9", Files.ReadState("a1")!.Handed);
    }

    /// <summary>
    /// 🔴 HELPERWAIT1b (Codex's round-3 #2 on COWORKUX1): a hand whose answer was lost (the host's 500 after its keep, a success that does not
    /// read, a client that gave up) proves nothing was kept no more than that it was, so its attempt mark stays and the outcome is held as
    /// uncertain until the next look reads the line back: the word found waiting marks it handed; the word consumed with no event left
    /// settles it uncertain. Either way it is never handed twice.
    /// </summary>
    [Theory]
    [InlineData("500", false)]
    [InlineData("500", true)]
    [InlineData("unreadable", true)]
    [InlineData("timeout", true)]
    public async Task A_hand_whose_answer_was_lost_keeps_its_mark_and_is_read_back_never_handed_twice(string lost, bool consumed)
    {
        Ask("a1");
        Answered();
        var host = Host();
        host.HelperHandLost = lost;
        var driver = Driver(host, pass: null);

        var first = await Look(driver);

        Assert.Equal(1, host.HelperHandsAsked);
        Assert.Contains("uncertain", Assert.Single(first));
        var held = Files.ReadState("a1")!;
        Assert.NotNull(held.Attempted);
        Assert.Null(held.Handed);

        // The word was kept; the asker's run then took it off its record, the event of its taking never written.
        var word = host.Said("s1")[0]!["id"]!.GetValue<string>();
        if (consumed) host.Take("s1");
        host.HelperHandLost = null;
        await Look(driver);

        Assert.Equal(1, host.HelperHandsAsked);
        var settled = Files.ReadState("a1")!;
        if (consumed) Assert.Equal((HelperStates.Refused, HelperWhy.HandUncertain), (settled.State, settled.Code));
        else Assert.Equal(word, settled.Handed);
    }

    /// <summary>
    /// 🔴 HELPERWAIT1b (Codex's round-3 #4 on COWORKUX1): the line is followed sixteen links at most, and one longer is refused with its reason,
    /// never handed to the part where the bound stopped, which the line has left; the answer stays with the helper for the person. A line of
    /// exactly sixteen links reaches its newest part.
    /// </summary>
    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public async Task A_line_past_sixteen_links_is_refused_never_handed_to_a_part_it_left(int links, bool handed)
    {
        Ask("a1");
        Answered();
        var host = Host(state: "stopped");
        var previous = "s1";
        for (var link = 1; link <= links; link++)
        {
            var part = $"p{link}";
            host.Session(part, link == links ? "completed" : "stopped", tree: "trees/default/reports/s-1");
            host.Set(part, "from", previous);
            host.Set(part, "created", At.AddMinutes(link).ToString("O"));
            previous = part;
        }

        var events = await Look(Driver(host, pass: null));

        if (handed)
        {
            Assert.Equal(1, host.HelperHandsAsked);
            return;
        }

        Assert.Equal(0, host.HelperHandsAsked);
        var state = Files.ReadState("a1")!;
        Assert.Equal((HelperStates.Refused, HelperWhy.CannotTake), (state.State, state.Code));
        Assert.Contains("sixteen", Assert.Single(events));
    }

    /// <summary>
    /// Point 1's input: a turn that ends holding its quest waits on its helper where its agent has an ask of its own still open, and no
    /// question its last turn asked the person; an ask of the person's, another session's, or one refused or handed is no such ask.
    /// </summary>
    [Fact]
    public async Task A_turn_waits_on_its_helper_only_with_an_open_ask_of_its_own_and_no_question()
    {
        var driver = Driver(Host(state: "working").Quest("q1", "Taken"), pass: null);
        var quest = new QuestView("q1", "ask #a1", "reports", "The work", "", "Taken");
        var since = At;

        Assert.False(await driver.WaitsOnOwnHelperAsync("s1", quest, since, CancellationToken.None));

        Ask("a1", by: "person");
        Ask("a2", asker: "s2");
        Ask("a3");
        Files.WriteState(new HelperState("a3", HelperStates.Refused, HelperWhy.Off, null, At));
        Assert.False(await driver.WaitsOnOwnHelperAsync("s1", quest, since, CancellationToken.None));

        Ask("a4");
        Assert.True(await driver.WaitsOnOwnHelperAsync("s1", quest, since, CancellationToken.None));

        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.question.json"),
            new JsonObject { ["id"] = "k1", ["question"] = "Which port?", ["at"] = At.AddMinutes(1).ToString("O") }.ToJsonString());
        Assert.False(await driver.WaitsOnOwnHelperAsync("s1", quest, since, CancellationToken.None));
        Assert.True(await driver.WaitsOnOwnHelperAsync("s1", quest, At.AddMinutes(2), CancellationToken.None));

        // 🔴 Codex's round-2 #3: a question on file that does not read is no proof none was asked, so the turn parks for the person.
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.question.json"), "{ \"id\": \"k1\", \"question\": ");
        Assert.False(await driver.WaitsOnOwnHelperAsync("s1", quest, At.AddMinutes(2), CancellationToken.None));
    }

    // ——— The choice of agent.

    [Fact]
    public void The_named_agent_is_taken_if_the_rule_lists_none_or_names_it_else_the_rules_list_else_another_maker()
    {
        var adapters = AdapterSet.Built();
        HelperAsk Ask(string? agent) => new("a1", "s1", "Why?", agent, HelperAsk.ByAgent, At);

        Assert.Equal(["codex-acp"], HelperChoice.Candidates(Ask("codex-acp"), new HelperRule(HelperRules.On, []), adapters, "claude-code"));
        Assert.Equal(["codex-acp"], HelperChoice.Candidates(Ask("CODEX-ACP"), new HelperRule(HelperRules.On, ["codex-acp", "dsh"]), adapters, "claude-code"));
        // Named, but not one the person lets be asked: the person's list, in its order.
        Assert.Equal(["dsh", "codex-acp"], HelperChoice.Candidates(Ask("stub"), new HelperRule(HelperRules.On, ["dsh", "codex-acp"]), adapters, "claude-code"));
        Assert.Equal(["dsh", "codex-acp"], HelperChoice.Candidates(Ask(null), new HelperRule(HelperRules.On, ["dsh", "codex-acp"]), adapters, "claude-code"));

        // Nothing named, no list: every agent of this machine that is not the asker's own family.
        var others = HelperChoice.Candidates(Ask(null), new HelperRule(HelperRules.On, []), adapters, "claude-code");
        Assert.Contains("codex-acp", others);
        Assert.DoesNotContain("claude-code", others);
        Assert.DoesNotContain("claude-code-acp", others);
    }

    // ——— COAGENT1a (D199 point 5; the co-agent design §2.5): the asker's work in progress, captured at its turn boundary.

    /// <summary>
    /// An asker mid-turn is not captured: the ask waits `turn`, nothing is read of its tree, and the look after its turn's end
    /// starts the helper.
    /// </summary>
    [Fact]
    public async Task An_asker_mid_turn_waits_for_its_turns_end_and_is_captured_after_it()
    {
        Ask("a1");
        var host = Host(state: "working");
        var pass = new Pass(Files) { Answer = "Line 42." };
        var captures = 0;
        var driver = Driver(host, pass, capture: (_, _) =>
        {
            captures++;
            return Task.FromResult(new HelperCaptureRead(Captured));
        });
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Tool, Text = "helper_ask" });

        await Look(driver);

        Assert.Equal((HelperStates.Waiting, HelperVerdict.Turn), (Files.ReadState("a1")!.State, Files.ReadState("a1")!.Code));
        Assert.Equal(0, captures);
        Assert.Empty(pass.Asked);

        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        await Look(driver);
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Answered });

        Assert.Equal(1, captures);
        Assert.Single(pass.Asked);
    }

    /// <summary>
    /// The capture is handed to the pass, whose copy stands at the capture's commit, and its manifest is kept beside the ask before
    /// the helper starts; the answer the asker reads names that capture and says the work may have moved since.
    /// </summary>
    [Fact]
    public async Task The_pass_reads_the_capture_its_manifest_is_kept_and_the_answer_names_it()
    {
        Ask("a1");
        var host = Host();
        var pass = new Pass(Files) { Answer = "Line 42." };
        var moved = Captured with { Commit = "3333333333333333333333333333333333333333" };

        await Look(Driver(host, pass, capture: (_, _) => Task.FromResult(new HelperCaptureRead(moved))));
        await Until(() => host.HelperHands.Count == 1);

        var asked = Assert.Single(pass.Asked);
        Assert.Same(moved, asked.Capture);
        Assert.Equal(moved.Commit, asked.Tip);
        var kept = Files.ReadCapture("a1")!;
        Assert.Equal(("c0ffee123456", moved.Commit, "src/report.ts", "old.md"), (kept.Id, kept.Commit, Assert.Single(kept.Files).Path, Assert.Single(kept.Deleted)));
        var text = host.HelperHands[0].Text;
        Assert.Contains("capture `c0ffee123456`: commit `3333333` with 1 changed or new file and 1 deletion", text);
        Assert.Contains("Your work may have moved since.", text);
    }

    /// <summary>
    /// A work in progress that cannot be captured whole refuses the ask in its code, a coded note on the asker naming no path, and
    /// the log's line naming what refused it; no helper starts and no manifest is kept.
    /// </summary>
    [Theory]
    [InlineData(HelperWhy.CaptureMerge)]
    [InlineData(HelperWhy.CaptureLink)]
    [InlineData(HelperWhy.CaptureSubmodule)]
    [InlineData(HelperWhy.CaptureUnreadable)]
    [InlineData(HelperWhy.CaptureBound)]
    [InlineData(HelperWhy.CaptureMoved)]
    public async Task A_work_in_progress_not_captured_whole_refuses_the_ask_in_its_code(string code)
    {
        Ask("a1");
        var host = Host();
        var pass = new Pass(Files) { Answer = "Line 42." };

        await Look(Driver(host, pass, capture: (_, _) => Task.FromResult(HelperCaptureRead.Refused(code, "src/escape"))));
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Refused });

        Assert.Equal(code, Files.ReadState("a1")!.Code);
        Assert.Empty(pass.Asked);
        Assert.Null(Files.ReadCapture("a1"));
        var note = Assert.Single(new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal(code, note.Parts![0].Value("why"));
        Assert.DoesNotContain("src/escape", note.Text);
    }
}
