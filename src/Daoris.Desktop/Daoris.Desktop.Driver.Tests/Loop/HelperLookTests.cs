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
    private void Ask(string id, string question = "Why does it drop the last day?", string by = "agent", string? agent = null, string asker = "s1")
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Helpers,$"{id}.ask.json"),
            new JsonObject
            {
                ["id"] = id, ["asker"] = asker, ["question"] = question, ["agent"] = agent, ["askedBy"] = by, ["at"] = At.AddMinutes(int.Parse(id[^1..])).ToString("O"),
            }.ToJsonString());
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

    private Daoris.Driver.Driver Driver(OpinionStandIn host, Pass? pass, HelperRule? rule = null, int cap = 3, ReviewerChoice? choice = null) =>
        new(host.Client(), DriverConfig.Empty with
        {
            Adapter = "claude-code-acp", Cap = cap,
            Helpers = new Dictionary<string, HelperRule> { ["reports"] = rule ?? On },
        }, AdapterSet.Built(), _home)
        {
            HelperPasses = pass is null ? (_, _, _) => throw new InvalidOperationException("no helper starts here") : pass.RunAsync,
            HelperTrees = (_, _) => Task.FromResult<(string?, string?)>((OpinionStandIn.Tip, "D:/checkouts/reports")),
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

        Assert.Equal("helper  ask `a1` from session `s1` waits for the person's yes or no on its session's page", Assert.Single(first));
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

        await Look(driver);
        await Until(() => Files.ReadState("a1") is { State: HelperStates.Answered });
        await Look(driver);
        Assert.Empty(host.HelperHands);

        host.Move("s1", "working");
        await Look(driver);
        Assert.Single(host.HelperHands);
    }

    /// <summary>A restart: a `started` ask whose helper ended, with an answer on file, is settled and delivered at the next look.</summary>
    [Fact]
    public async Task A_started_ask_whose_helper_ended_before_a_restart_is_settled_at_the_next_look()
    {
        Ask("a1");
        Files.WriteState(new HelperState("a1", HelperStates.Started, null, Helper, At) { Agent = "codex-acp" });
        File.WriteAllText(Files.PathOf("a1", "answer"),
            new JsonObject { ["id"] = "a1", ["helper"] = Helper, ["answer"] = "Line 42.", ["at"] = At.ToString("O") }.ToJsonString());
        var host = Host();

        await Look(Driver(host, pass: null));

        Assert.Equal(HelperStates.Answered, Files.ReadState("a1")!.State);
        Assert.Single(host.HelperHands);
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
}
