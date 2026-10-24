using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1e (D155 point 7; the second-agent design §6.3–§6.7): a second opinion delivered. A first pass's findings go once to
/// the working session as its next turn, through the local host's door, marked as another agent's claims and never the
/// person's; at its turn's end, never into a step in flight; to the person where it cannot take them. Its answers are read as
/// its turn ends, a fix's commit checked from git and a finding left unanswered read as unresolved. A pass that gave no
/// opinion is <i>Try again</i> with why, and one that raised nothing says what it read: never <i>no issues</i>.
/// </summary>
/// <remarks>
/// The host is <see cref="OpinionStandIn"/>, in process, and git is <see cref="StandInGit"/>: nothing here starts a process, so
/// the fast half (MOD8). The resumed turn itself rides D137's door, whose rows are <c>AcpResumeTests</c>,
/// <c>NativeResumeTests</c> and the <c>Process</c> half's <c>OpinionResumeTickTests</c>.
/// </remarks>
public sealed class OpinionDeliveryTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-deliver-" + Guid.NewGuid().ToString("N")[..8]);

    public OpinionDeliveryTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly OpinionPassAsk Asked =
        new(OpinionRules.Landing, "s1", "reports", "D:/checkouts/reports", OpinionStandIn.Base, OpinionStandIn.Tip,
            new OpinionRule(["codex-acp"], [OpinionRules.Landing]));

    private Daoris.Driver.Driver Driver(OpinionStandIn host, StandInGit? git = null) =>
        new(host.Client(), DriverConfig.Empty with { Adapter = "claude-code-acp" }, AdapterSet.Built(), _home)
        {
            OpinionGit = git is null ? WorkingTree.ReadGitAsync : git.Read,
            Passes = (_, _, _) => throw new InvalidOperationException("no recheck is asked here"),
        };

    private Task<OpinionDelivery> Deliver(OpinionStandIn host, string opinion = "op1", StandInGit? git = null) =>
        Driver(host, git).DeliverAsync(opinion, Asked);

    // ——— Handing (design §6.3).

    /// <summary>
    /// 🔴 A first pass's findings go to its working session once, as a word on its record whose <c>by</c> names the opinion,
    /// when that record has ended: it goes on with them at the driver's next look, its own conversation resumed (D137). The
    /// look after sees them with the session, and hands nothing again.
    /// </summary>
    [Fact]
    public async Task A_first_pass_s_findings_go_once_to_its_ended_working_session()
    {
        var host = new OpinionStandIn().Session("s1", "completed").Opinion("op1", "s1", weights: ["must", "should"]);

        var first = await Deliver(host);
        var again = await Deliver(host);

        Assert.Equal((OpinionDeliveryStates.Handed, "s1"), (first.State, first.Session));
        Assert.Contains("as another agent's claims", first.Line);
        Assert.Equal(["op1"], host.Hands);
        var word = Assert.Single(host.Said("s1"));
        Assert.Equal("op1", word!["by"]!.GetValue<string>());
        Assert.Equal(OpinionDeliveryStates.WithSession, again.State);
        Assert.Contains("at the driver's next look", again.Line);
    }

    /// <summary>
    /// A running turn takes another agent's claims at its end, never at its next step (D136, design §6.3), and a park after the
    /// person's answer: nothing is handed while it runs or waits, and the look after its turn ends hands them.
    /// </summary>
    [Theory]
    [InlineData("working")]
    [InlineData("starting")]
    [InlineData("awaiting-person")]
    public async Task A_running_or_parked_working_session_takes_them_at_its_turn_s_end(string state)
    {
        var host = new OpinionStandIn().Session("s1", state).Opinion("op1", "s1", weights: ["must"]);

        var waiting = await Deliver(host);
        Assert.Equal(OpinionDeliveryStates.Waiting, waiting.State);
        Assert.Contains("never into a step in flight", waiting.Line);
        Assert.Empty(host.Hands);

        host.Move("s1", "completed");
        Assert.Equal(OpinionDeliveryStates.Handed, (await Deliver(host)).State);
        Assert.Equal(["op1"], host.Hands);
    }

    /// <summary>
    /// 🔴 Where the working session cannot take them (design §6.7), the findings go to the person instead: unanswered, each
    /// <c>must</c> disputed, the reason kept beside the opinion so a later look says the same without asking again. Nothing
    /// is handed, and nothing carries them to a new session as the person's words.
    /// </summary>
    [Theory]
    [InlineData("stood-down", ContinueWhy.StoodDown)]
    [InlineData("intake", ContinueWhy.Intake)]
    [InlineData("chat", OpinionToPerson.Conversation)]
    [InlineData("gone", OpinionToPerson.NotFound)]
    public async Task A_working_session_that_cannot_take_them_sends_them_to_the_person(string row, string code)
    {
        var host = new OpinionStandIn().Opinion("op1", "s1", weights: ["must", "should", "must"]);
        switch (row)
        {
            case "stood-down": host.Session("s1", "stood-down"); break;
            case "intake": host.Session("s1", "completed").Set("s1", "ask", "a1"); break;
            case "chat": host.Session("s1", "completed", kind: "chat"); break;
        }

        var delivered = await Deliver(host);

        Assert.Equal((OpinionDeliveryStates.ToPerson, code), (delivered.State, delivered.Why));
        Assert.Contains("go to the person, unanswered", delivered.Line);
        Assert.Equal([1, 3], delivered.Disputes!.First);
        Assert.Empty(host.Hands);
        Assert.Equal(code, new OpinionDeliveries(_home).Read("op1").Person);

        // Kept: the next look says the same, and still hands nothing, whatever the record does since.
        if (row != "gone") host.Move("s1", "completed");
        Assert.Equal((OpinionDeliveryStates.ToPerson, code), ((await Deliver(host)).State, (await Deliver(host)).Why));
        Assert.Empty(host.Hands);
    }

    /// <summary>
    /// Words the working session could not go on with are marked by their ids (MSG1b): a look that finds the handed word marked
    /// says the findings went to the person, with the mark's reason, and keeps it.
    /// </summary>
    [Fact]
    public async Task Findings_their_session_could_not_go_on_with_go_to_the_person()
    {
        var host = new OpinionStandIn().Session("s1", "completed").Opinion("op1", "s1", weights: ["must"]);
        await Deliver(host);
        var word = host.Handed("op1")!["word"]!.GetValue<string>();
        new GoOnMarks(_home).Mark("s1", [word], ContinueWhy.Of(ContinueWhy.Elsewhere), DateTimeOffset.UtcNow);

        var delivered = await Deliver(host);

        Assert.Equal((OpinionDeliveryStates.ToPerson, ContinueWhy.Elsewhere), (delivered.State, delivered.Why));
        Assert.Contains("its conversation is open in another client of its agent", delivered.Line);
        Assert.Equal(ContinueWhy.Elsewhere, new OpinionDeliveries(_home).Read("op1").Person);
    }

    /// <summary>
    /// COWORK1e (the message-urgency design §8.3): findings the answers-alone bound holds wait for the person's choice, still handed to
    /// their session; once the person says *Not now* they go to the person, unanswered, as findings nobody takes do.
    /// </summary>
    [Fact]
    public async Task Findings_held_by_the_answers_alone_bound_wait_until_the_person_keeps_them()
    {
        var host = new OpinionStandIn().Session("s1", "completed").Opinion("op1", "s1", weights: ["must"]);
        await Deliver(host);
        var word = host.Handed("op1")!["word"]!.GetValue<string>();
        new GoOnMarks(_home).Mark("s1", [word], GoOnMarks.AnswersAlone, DateTimeOffset.UtcNow, agent: "codex-acp");

        Assert.NotEqual(OpinionDeliveryStates.ToPerson, (await Deliver(host)).State);
        Assert.Null(new OpinionDeliveries(_home).Read("op1").Person);

        new GoOnMarks(_home).Mark("s1", [word], GoOnMarks.NotNow, DateTimeOffset.UtcNow, agent: "codex-acp");
        var kept = await Deliver(host);

        Assert.Equal((OpinionDeliveryStates.ToPerson, GoOnMarks.NotNow), (kept.State, kept.Why));
        Assert.Contains("you kept them rather than let a third answer in a row go on with nobody between", kept.Line);
    }

    // ——— Failure and nothing raised (design §6.1, §8.4, §10).

    /// <summary>
    /// 🔴 A pass that ended without an opinion, or ran out of time, is <i>Try again</i> for the person with why. It is never
    /// <i>no issues</i>: no agent's reading stands for the work.
    /// </summary>
    [Theory]
    [InlineData("ended", "its session ended without saying one")]
    [InlineData("out-of-time", "its 20 minutes ran out before it said one")]
    public async Task A_pass_that_gave_no_opinion_is_try_again_with_why_and_never_no_issues(string why, string said)
    {
        var host = new OpinionStandIn().Session("s1", "completed").Opinion("op1", "s1", state: "failed", why: why);

        var delivered = await Deliver(host);

        Assert.Equal((OpinionDeliveryStates.TryAgain, why), (delivered.State, delivered.Why));
        Assert.Contains(said, delivered.Line);
        Assert.Contains("try again", delivered.Line);
        Assert.DoesNotContain("no issues", delivered.Line);
        Assert.Empty(host.Hands);
    }

    /// <summary>A pass still being read waits, and hands nothing.</summary>
    [Fact]
    public async Task A_pass_still_being_read_waits()
    {
        var host = new OpinionStandIn().Session("s1", "completed").Opinion("op1", "s1", state: "reading");

        Assert.Equal(OpinionDeliveryStates.Reading, (await Deliver(host)).State);
        Assert.Empty(host.Hands);
    }

    /// <summary>A pass that raised nothing says so with what it read, never as <i>no issues</i>, and hands nothing.</summary>
    [Fact]
    public async Task A_pass_that_raised_nothing_says_what_it_read()
    {
        var host = new OpinionStandIn().Session("s1", "completed").Opinion("op1", "s1", weights: []);

        var delivered = await Deliver(host);

        Assert.Equal(OpinionDeliveryStates.RaisedNothing, delivered.State);
        Assert.Equal("raised-nothing  Codex (OpenAI) raised nothing in what it read: src/report.ts and the two commits", delivered.Line);
        Assert.Empty(host.Hands);
    }

    /// <summary>A recheck's findings go to the person, never back to the working session by themselves (design §6.5).</summary>
    [Fact]
    public async Task A_recheck_s_findings_go_to_the_person_and_are_never_handed()
    {
        var host = new OpinionStandIn().Session("s1", "completed")
            .Opinion("op2", "s1", weights: ["must"], pass: "recheck", rechecks: "op1");
        host.Rechecked("op2", 1, "withdrawn");

        var delivered = await Deliver(host, "op2");

        Assert.Equal((OpinionDeliveryStates.ToPerson, OpinionToPerson.Recheck), (delivered.State, delivered.Why));
        Assert.Contains("1 finding of its own, and 1 first-pass finding withdrawn", delivered.Line);
        Assert.Empty(host.Hands);
        Assert.Empty(host.Said("s1"));
    }

    /// <summary>The service holds no such opinion: said, and nothing done.</summary>
    [Fact]
    public async Task An_opinion_the_host_does_not_hold_is_unknown()
    {
        Assert.Equal(OpinionDeliveryStates.Unknown, (await Deliver(new OpinionStandIn(), "nothing1")).State);
    }

    // ——— The words, read and resumed (D137 as D155 amends it).

    /// <summary>
    /// The record's words are read with <c>by</c> (XAGENT1c): another agent's claims, never the person's. Absent, they are the
    /// person's, as every word kept before was.
    /// </summary>
    [Fact]
    public void A_word_is_read_with_the_opinion_it_is_from()
    {
        using var document = JsonDocument.Parse("""
            [{"id":"s1","state":"completed","said":[
              {"id":"w1","text":"Also log the port.","at":"2026-10-09T09:00:00Z","files":[],"reopens":true},
              {"id":"w2","text":"Another agent, Codex by OpenAI, read your work…","at":"2026-10-09T09:01:00Z","files":[],"reopens":true,"by":"op1"}]}]
            """);

        var record = ServiceClient.ReadRecord(document.RootElement.GetRawText(), "s1")!;

        Assert.Equal([null, "op1"], record.Waiting.Select(word => word.By));
        Assert.Equal([true, false], record.Waiting.Select(word => word.Persons));
        Assert.Equal(["op1"], record.Findings);
        Assert.True(record.WordsWaiting);
    }

    /// <summary>
    /// 🔴 The resumed run's record shows another agent's findings as the turn Daoris composed around that agent's claims, naming
    /// the opinion, never as the person's (design §6.3); the person's words beside them stay theirs. Its first line says whose.
    /// </summary>
    [Fact]
    public void The_resumed_opening_shows_the_findings_as_another_agent_s_never_the_person_s()
    {
        var resume = new ResumeAsk(
            "conv-s1",
            [
                new SaidWordView("w1", "Also log the port.", DateTimeOffset.UnixEpoch, [], Reopens: true),
                new SaidWordView("w2", "Another agent, Codex by OpenAI, read your work…", DateTimeOffset.UnixEpoch, [], Reopens: true) { By = "op1" },
            ],
            Continuations.Opening("acp-stub", null, null, answer: false, findings: true, persons: true));

        var opening = resume.Opening();

        Assert.Equal("— your words and another agent's findings are the next turn of its own conversation, resumed on `acp-stub`.", opening[0].Text);
        Assert.Equal(("person", (string?)null, "w1"), (opening[1].Origin, opening[1].Opinion, opening[1].Id));
        Assert.Equal(("target", (string?)"op1", "w2"), (opening[2].Origin, opening[2].Opinion, opening[2].Id));
        Assert.Equal(["op1"], resume.Opinions);
        // Both doors carry the words unchanged: one block each on the protocol door, joined by a blank line on the native one.
        Assert.Equal(["Also log the port.", "Another agent, Codex by OpenAI, read your work…"], resume.Blocks);
        Assert.Equal("Also log the port.\n\nAnother agent, Codex by OpenAI, read your work…", resume.Prompt);
        Assert.Equal(
            "— another agent's findings are the next turn of its own conversation, resumed on `claude-code`.",
            Continuations.Opening("claude-code", null, null, answer: false, findings: true, persons: false));
    }

    /// <summary>
    /// An ended record going on with another agent's findings alone says so on its note, never "your words", and a session
    /// handed them is given the one tool that answers them beside the person's rules.
    /// </summary>
    [Fact]
    public void A_record_going_on_with_findings_says_so_and_is_given_the_tool_that_answers_them()
    {
        Assert.Equal(
            "It goes on with another agent's findings in its own conversation, in the tree it worked in, to check and answer each.",
            Continuations.GoingOnWithFindingsNoted.Note);
        Assert.DoesNotContain("your words", Continuations.GoingOnWithFindingsNoted.Note);
        Assert.Equal(["mcp__daoris-knowledge__opinion_answer"], DriverContinuations.AnswersFindings);
    }

    // ——— The answers, read as the turn ends (design §6.4).

    private (StandInGit Git, string Tree, string Tip, string Fix, string Later, string Off) History()
    {
        var tree = Path.Combine(_home, "tree");
        Directory.CreateDirectory(tree);
        var git = new StandInGit(tree);
        git.Commit("base");
        var tip = git.Commit("the work read", ("src/report.ts", "v1"));
        var off = git.Commit("elsewhere", ("src/other.ts", "x"));
        git.Reset(tip);
        var fix = git.Commit("the fix", ("src/report.ts", "v2"));
        var later = git.Commit("a note", ("docs/notes.md", "n"));
        return (git, tree, tip, fix, later, off);
    }

    /// <summary>
    /// 🔴 A fix counts only where git reads its commit as one the turn made: after the work the other agent read, on the
    /// session's branch at or before its tip. Anything else said fixed reads as unresolved, with why. A finding with no answer
    /// is unresolved, said as not answered; a rejection and an unresolved stand as said; a <c>must</c> not fixed is disputed.
    /// </summary>
    [Fact]
    public async Task Answers_are_read_as_the_turn_ends_each_fix_checked_from_git()
    {
        var (git, tree, tip, fix, later, off) = History();
        var host = new OpinionStandIn().Opinion("op1", "s1", weights: ["must", "must", "must", "should", "must", "note", "must"], tip: tip);
        host.Answer("op1", 1, "fixed", commit: fix[..9]);
        host.Answer("op1", 2, "fixed", commit: tip);
        host.Answer("op1", 3, "fixed", commit: off);
        host.Answer("op1", 4, "rejected", evidence: "the twin test holds the table");
        host.Answer("op1", 5, "unresolved", why: "cannot reproduce here");
        host.Answer("op1", 7, "fixed", commit: "abcdef0123");
        var view = (await host.Client().Opinions.ReadAsync("op1"))!;

        var reading = await OpinionAnswers.ReadAsync(view, "s1", tree, git.Read, DateTimeOffset.UnixEpoch, CancellationToken.None);

        Assert.Equal(later, reading.Tip);
        Assert.Equal(
            [
                (1, "fixed", (string?)fix, (string?)null, false),
                (2, "unresolved", null, OpinionAnswerWhy.FixNotAfter, true),
                (3, "unresolved", null, OpinionAnswerWhy.FixOffBranch, true),
                (4, "rejected", null, null, false),
                (5, "unresolved", null, null, true),
                (6, "unresolved", null, OpinionAnswerWhy.NotAnswered, false),
                (7, "unresolved", null, OpinionAnswerWhy.FixUnknown, true),
            ],
            reading.Findings.Select(row => (row.Finding, row.Counts, row.Fix, row.Why, row.Disputed)));
        Assert.True(reading.AnyFixed);
        Assert.Equal(
            "— the answers to Codex (OpenAI)'s 7 findings, as its turn ended: 1 fixed, each commit read from git as one this turn made; "
            + "3 said fixed whose commit git did not read as one this turn made, read as unresolved; 1 rejected with its evidence; "
            + "1 unresolved; 1 not answered, read as unresolved. 4 `must` findings not fixed wait for the person.",
            OpinionAnswers.Line(view, reading));
    }

    /// <summary>A tree that is gone checks no fix: said fixed, it reads as unresolved, because git could not read it.</summary>
    [Fact]
    public async Task A_fix_in_a_tree_that_is_gone_is_not_counted()
    {
        var host = new OpinionStandIn().Opinion("op1", "s1", weights: ["must"]);
        host.Answer("op1", 1, "fixed", commit: "abcdef0123");
        var view = (await host.Client().Opinions.ReadAsync("op1"))!;

        var reading = await OpinionAnswers.ReadAsync(
            view, "s1", Path.Combine(_home, "gone"), new StandInGit(_home).Read, DateTimeOffset.UnixEpoch, CancellationToken.None);

        Assert.Null(reading.Tip);
        Assert.Equal(("unresolved", OpinionAnswerWhy.FixUnread), (reading.Findings[0].Counts, reading.Findings[0].Why));
    }

    /// <summary>
    /// The look after the session's turn ended reads its answers where its end did not (a run that failed before it could),
    /// keeps them beside the opinion, and says them in the session's conversation, naming the opinion.
    /// </summary>
    [Fact]
    public async Task A_turn_that_ended_unread_has_its_answers_read_at_the_next_look_and_kept()
    {
        var (git, tree, tip, fix, _, _) = History();
        var host = new OpinionStandIn().Session("s1", "completed", tree: tree).Opinion("op1", "s1", weights: ["must", "should"], tip: tip);
        var driver = Driver(host, git);
        await driver.DeliverAsync("op1", Asked);
        host.Take("s1");
        host.Answer("op1", 2, "rejected", evidence: "the twin test holds it");

        var delivered = await Driver(host, git).DeliverAsync("op1", Asked with { Rule = Asked.Rule with { Recheck = false } });

        Assert.Equal(OpinionDeliveryStates.Answered, delivered.State);
        Assert.Equal([1], delivered.Disputes!.First);
        Assert.Contains("1 dispute for the person", delivered.Line);
        Assert.Contains("No recheck: the rule turns the recheck off.", delivered.Line);
        var kept = new OpinionDeliveries(_home).Read("op1");
        Assert.Equal(["unresolved", "rejected"], kept.Answered!.Findings.Select(row => row.Counts));
        Assert.Equal(OpinionRechecks.Off, kept.RecheckWhy);
        var note = Assert.Single(new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events);
        Assert.Equal(("op1", SessionEventKind.Note), (note.Opinion, note.Kind));
        Assert.StartsWith("— the answers to Codex (OpenAI)'s 2 findings", note.Text);
    }

    /// <summary>What the driver keeps of an opinion reads back as it was written, and a file that does not read is nothing kept.</summary>
    [Fact]
    public void What_is_kept_of_a_delivery_reads_back()
    {
        var store = new OpinionDeliveries(_home);
        var reading = new OpinionReading("s1", OpinionStandIn.Tip, DateTimeOffset.Parse("2026-10-09T09:30:00Z"),
        [
            new OpinionAnswerRead(1, "must", "fixed") { Said = "fixed", Commit = "abc1234", Fix = OpinionStandIn.Tip },
            new OpinionAnswerRead(2, "should", "unresolved") { Why = OpinionAnswerWhy.NotAnswered },
        ]);

        store.Answered("op1", reading);
        store.Rechecked("op1", "op2", null);
        store.Rechecked("op1", null, OpinionRechecks.NoFix);
        store.ToPerson("op1", ContinueWhy.Tree, DateTimeOffset.UnixEpoch);
        store.ToPerson("op1", ContinueWhy.Gone, DateTimeOffset.UnixEpoch);

        var read = store.Read("op1");
        Assert.Equal(("op2", (string?)null), (read.Recheck, read.RecheckWhy));
        Assert.Equal(ContinueWhy.Tree, read.Person);
        Assert.Equal(reading.Findings, read.Answered!.Findings);
        Assert.Equal((reading.Session, reading.Tip, reading.At), (read.Answered.Session, read.Answered.Tip, read.Answered.At));
        Assert.DoesNotContain("\r", File.ReadAllText(store.PathOf("op1")!));

        File.WriteAllText(store.PathOf("op1")!, "{ not json");
        Assert.Equal(OpinionDelivered.None, store.Read("op1"));
        Assert.Null(store.PathOf("../elsewhere"));
    }

    // ——— A chat run's send-back (CHATFLOW1e; the chat-flow design §6, §6.1, §6.2).

    /// <summary>A chat run's first pass, asked at the person's press with their turn count then.</summary>
    private static OpinionPassAsk OnRun(int turns = 0) => Asked with { Run = "chat-c1", Turns = turns };

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>A tree of the conversation's own, standing, with the work read at its tip, a fix after it, and git over it.</summary>
    private (StandInGit Git, string Tree, string Tip) ChatTree()
    {
        var tree = Path.Combine(_home, "s-c1");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "README.md"), "a tree that stands");
        var git = new StandInGit(tree);
        git.Commit("base");
        return (git, tree, git.Commit("the work read", ("src/report.ts", "v1")));
    }

    /// <summary>A conversation's turns, a door scripted in memory: each turn ends when the test says.</summary>
    private sealed class Conversation
    {
        private readonly List<TaskCompletionSource> _ends = [];

        public Conversation(Action<ChatMessage>? onTake = null) => Turns = new ChatTurns(
            ready: () => Task.FromResult(true),
            take: (message, sent) =>
            {
                var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                onTake?.Invoke(message);
                lock (Took)
                {
                    Took.Add(message);
                    _ends.Add(ended);
                }

                sent();
                return ended.Task;
            },
            interrupt: () => Task.CompletedTask, changed: _ => { });

        public ChatTurns Turns { get; }

        public List<ChatMessage> Took { get; } = [];

        public void End(int at)
        {
            lock (Took) _ends[at].TrySetResult();
        }
    }

    /// <summary>
    /// The shell's chat runner as the loop is handed it (<see cref="IChatSendBacks"/>), over real turns: a live conversation's send-back is its
    /// turns' own operation, and an ended one goes on where it can, the findings its first turn.
    /// </summary>
    private sealed class Runner(SessionEvents events, OpinionStandIn host) : IChatSendBacks
    {
        public Dictionary<string, Conversation> Held { get; } = new(StringComparer.Ordinal);

        /// <summary>Where set, an ended conversation never goes on, for this reason.</summary>
        public string? CannotGoOn { get; set; }

        public List<string> WentOn { get; } = [];

        public async Task<SentBack> SendBackAsync(ChatSendBackAsk ask, CancellationToken ct = default)
        {
            if (!Held.TryGetValue(ask.Session, out var chat)) return new(SendBack.NotHere);
            return new(await chat.Turns.SendBackAsync(
                ask.Pressed, () => ChatPresses.TurnsOf(events, ask.Line),
                async token => await ask.Hand(ask.Session, token) is { } word ? ChatRunner.Findings(word, ask.Answered) : null, ct));
        }

        public async Task<SentBack> GoOnWithFindingsAsync(ChatSendBackAsk ask, CancellationToken ct = default)
        {
            if (CannotGoOn is { } never) return new(SendBack.CannotGoOn, never);
            if (await ask.Hand(ask.Session, ct) is not { } word) return new(SendBack.Refused);
            host.Move(ask.Session, "working");
            var chat = Held[ask.Session] = new Conversation();
            WentOn.Add(ask.Session);
            chat.Turns.Take(ChatRunner.Findings(word, ask.Answered));
            return new(SendBack.Reserved);
        }

        public bool? TurnEnded(string sessionId) =>
            Held.TryGetValue(sessionId, out var chat) ? chat.Turns.State is { Taking: false, Opening: false, Queued.Count: 0 } : null;
    }

    /// <summary>A test double's agent that is never run (OpinionRecheckTests' machine): present by a file look on a command this test writes.</summary>
    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public System.Diagnostics.ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    /// <summary>The build's own agents by name, each present by a file look, so a recheck's reviewer walk asks no real binary.</summary>
    private (AdapterSet Adapters, HarnessRoster Roster) Machine()
    {
        var command = Path.Combine(_home, "agent-here");
        File.WriteAllText(command, "");
        var adapters = new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "claude-code", "claude-code-acp", "codex-acp" })
        {
            var real = AdapterSet.Built().Resolve(name).Toolchain!;
            adapters[name] = new Adapter(name, new HarnessToolchain(
                Binary: [command], VersionArguments: [], ProfileVariable: real.ProfileVariable, AccountOf: real.AccountOf, ProbeByPresence: true,
                Product: real.Product, Maker: real.Maker, RunsInOwnHome: true));
        }

        var set = new AdapterSet(adapters);
        return (set, new HarnessRoster(set, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    private Daoris.Driver.Driver ChatDriver(OpinionStandIn host, StandInGit git, IChatSendBacks? runner) =>
        new(host.Client(), DriverConfig.Empty with { Adapter = "claude-code-acp" }, AdapterSet.Built(), _home)
        {
            OpinionGit = git.Read,
            Passes = (_, _, _) => throw new InvalidOperationException("no recheck is asked here"),
            SendsBack = runner,
        };

    /// <summary>
    /// 🔴 Its send-back reaches the conversation only between turns (§6.1 (b)): while a turn that is not the person's runs (a Daoris word's),
    /// the findings wait and nothing is handed; once it ends they are handed to the conversation's record and taken as a turn of their own,
    /// an ordinary word with <c>by</c>, never steered; while that turn runs the delivery reads it by the turn state, never by the record.
    /// </summary>
    [Fact]
    public async Task A_chat_runs_send_back_reaches_its_conversation_only_between_turns()
    {
        var (git, tree, tip) = ChatTree();
        var host = new OpinionStandIn().Chat("c1", "working", tree).Opinion("op1", "c1", weights: ["must", "should"], tip: tip);
        var runner = new Runner(Events, host);
        var chat = runner.Held["c1"] = new Conversation();
        chat.Turns.Take(new ChatMessage("Daoris says the opinion was asked.", []) { Daoris = new DaorisWord("k1") });
        var driver = ChatDriver(host, git, runner);

        var waiting = await driver.DeliverAsync("op1", OnRun());
        Assert.Equal(OpinionDeliveryStates.Waiting, waiting.State);
        Assert.Empty(host.Hands);

        chat.End(0);
        await Poll.Until(() => !chat.Turns.Running, () => "Daoris's turn never ended");
        var handed = await driver.DeliverAsync("op1", OnRun());

        Assert.Equal((OpinionDeliveryStates.Handed, "c1"), (handed.State, handed.Session));
        Assert.Equal(["c1"], host.HandsTo);
        await Poll.Until(() => chat.Took.Count == 2, () => "the findings never became the conversation's next turn");
        Assert.Equal(("op1", MessageLevel.Ordinary), (chat.Took[1].Helped!.By, chat.Took[1].Level));
        Assert.Equal("ordinary", host.Said("c1")[0]!["level"]!.GetValue<string>());
        Assert.Equal(OpinionDeliveryStates.WithSession, (await driver.DeliverAsync("op1", OnRun())).State);
        Assert.Single(host.Hands, each => each == "op1");
        chat.Turns.Gone();
    }

    /// <summary>
    /// 🔴 To the person only where the person's turn count rose since the press (§6.1 (b), Codex's findings 2 and 4): their word waits, its
    /// keeping is in flight when the send-back arrives, or their turn already ended with the chat idle. Their words go first, the findings are
    /// theirs as <c>person-went-on</c>, nothing is handed, and no turn starts for them.
    /// </summary>
    [Theory]
    [InlineData("waiting")]
    [InlineData("keeping")]
    [InlineData("answered")]
    public async Task A_person_who_went_on_since_the_press_has_the_findings_and_their_words_go_first(string row)
    {
        var (git, tree, tip) = ChatTree();
        var host = new OpinionStandIn().Chat("c1", "working", tree).Opinion("op1", "c1", weights: ["must"], tip: tip);
        var runner = new Runner(Events, host);
        var chat = runner.Held["c1"] = new Conversation();
        var driver = ChatDriver(host, git, runner);
        Task<OpinionDelivery> delivering;
        switch (row)
        {
            case "waiting":
                chat.Turns.Take(new ChatMessage("Daoris's word", []) { Daoris = new DaorisWord("k1") });
                Assert.Equal(TurnReach.TurnEnd, chat.Turns.Take(new ChatMessage("rename it first", []) { Level = MessageLevel.Ordinary }));
                delivering = driver.DeliverAsync("op1", OnRun());
                break;
            case "keeping":
                // A word arriving between the reservation's check and its take: its service write held until the send-back waits on it.
                var record = new Daoris.Driver.Tests.StandInSaid();
                var keeping = new TaskCompletionSource();
                record.Answering = _ => keeping.Task;
                var kept = chat.Turns.KeepAsync(record, new ChatMessage("rename it first", []), default);
                await Poll.Until(() => record.Said.Count == 1);
                delivering = driver.DeliverAsync("op1", OnRun());
                await Task.Yield();
                keeping.SetResult();
                await kept;
                break;
            default:
                // A turn of theirs sent and finished after the press: nothing waits, and the chat is idle when the findings arrive.
                Events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "rename it first", Id = "w1" });
                delivering = driver.DeliverAsync("op1", OnRun());
                break;
        }

        var delivered = await delivering;

        Assert.Equal((OpinionDeliveryStates.ToPerson, OpinionToPerson.PersonWentOn), (delivered.State, delivered.Why));
        Assert.Contains("your words went first", delivered.Line);
        Assert.Empty(host.Hands);
        Assert.DoesNotContain(chat.Took, message => message.Helped is not null);
        Assert.Equal(OpinionToPerson.PersonWentOn, new OpinionDeliveries(_home).Read("op1").Person);
        if (row == "answered") Assert.Empty(chat.Took);
        chat.Turns.Gone();
    }

    /// <summary>
    /// Waiting only for activity that is not the person's, a live chat no runner here holds, or a headless host (§6.1 (b)): the headless host
    /// (<c>SendsBack</c> null) and a runner that holds another window's conversation leave the findings waiting, nothing handed.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_headless_host_or_a_conversation_no_runner_here_holds_leaves_them_waiting(bool headless)
    {
        var (git, tree, tip) = ChatTree();
        var host = new OpinionStandIn().Chat("c1", "working", tree).Opinion("op1", "c1", weights: ["must"], tip: tip);

        var delivered = await ChatDriver(host, git, headless ? null : new Runner(Events, host)).DeliverAsync("op1", OnRun());

        Assert.Equal(OpinionDeliveryStates.Waiting, delivered.State);
        Assert.Contains(headless ? "no conversation runs on this host" : "no runner here holds its conversation", delivered.Line);
        Assert.Empty(host.Hands);
        Assert.Null(new OpinionDeliveries(_home).Read("op1").Person);
    }

    /// <summary>
    /// 🔴 The answers are read in the answering turn's frozen reading (§6.1 (c), Codex's finding 3): a person's turn queued behind it moves the
    /// tree's <c>HEAD</c> as it starts, and the reading holds the answering turn's tip all the same. The next look asks the one recheck over that
    /// reading, from the first pass's tip to the answering turn's end, never the tree's tip at the look.
    /// </summary>
    [Fact]
    public async Task The_answers_are_read_in_the_answering_turns_frozen_reading_and_the_recheck_reads_that()
    {
        var (git, tree, tip) = ChatTree();
        var host = new OpinionStandIn().Chat("c1", "working", tree).Opinion("op1", "c1", weights: ["must", "should"], tip: tip);
        var runner = new Runner(Events, host);
        string? fix = null;
        var chat = runner.Held["c1"] = new Conversation(message =>
        {
            if (message.Text == "now rename it") git.Commit("the person's rename", ("src/report.ts", "v3"));
        });
        var asked = new List<OpinionPassAsk>();
        var (adapters, roster) = Machine();
        var driver = new Daoris.Driver.Driver(host.Client(), DriverConfig.Empty with { Adapter = "claude-code-acp" }, adapters, _home, harnesses: roster)
        {
            OpinionGit = git.Read,
            SendsBack = runner,
            Passes = (ask, choice, _) =>
            {
                asked.Add(ask);
                host.Opinion("op2", "c1", state: "reading", pass: "recheck", rechecks: "op1", tip: ask.Tip, @base: ask.Base);
                return Task.FromResult(new OpinionPassRun("opened  second opinion op2", true) { Opinion = "op2", Session = "r-op2" });
            },
        };

        Assert.Equal(OpinionDeliveryStates.Handed, (await driver.DeliverAsync("op1", OnRun())).State);
        await Poll.Until(() => chat.Took.Count == 1, () => "the findings never became the conversation's turn");
        fix = git.Commit("the fix", ("src/report.ts", "v2"));
        host.Answer("op1", 1, "fixed", commit: fix);
        host.Answer("op1", 2, "rejected", evidence: "the twin test holds it");
        chat.Turns.Take(new ChatMessage("now rename it", []));
        chat.End(0);

        await Poll.Until(() => new OpinionDeliveries(_home).Read("op1").Answered is not null, () => "the answering turn's reading was never kept");
        await Poll.Until(() => chat.Took.Count == 2, () => "the person's turn never ran after the reading was frozen");
        var reading = new OpinionDeliveries(_home).Read("op1").Answered!;
        Assert.Equal(("c1", fix, false), (reading.Session, reading.Tip, reading.Steered));
        Assert.Equal(["fixed", "rejected"], reading.Findings.Select(row => row.Counts));

        var delivered = await driver.DeliverAsync("op1", OnRun() with { Workspace = "work" });

        Assert.Equal(OpinionDeliveryStates.Answered, delivered.State);
        var recheck = Assert.Single(asked);
        Assert.Equal((tip, fix), (recheck.Base, recheck.Tip));
        Assert.Equal("op2", new OpinionDeliveries(_home).Read("op1").Recheck);

        // The run's gate reads `read-again` while the recheck reads, and never with nothing reading: the look asked it as the answers were kept.
        var client = host.Client();
        var gate = await OpinionGate.JudgeAsync(
            new OpinionGateFacts("reports", new ResolvedOpinion(Asked.Rule, OpinionSource.Repository))
            {
                Session = "c1", Run = "chat-c1", Tip = fix, Asks = [new OpinionAskKept(DateTimeOffset.UnixEpoch, OpinionRules.Landing, "c1", tip) { Opinion = "op1" }],
            },
            new OpinionReads(
                id => client.Opinions.ReadAsync(id), id => new OpinionDeliveries(_home).Read(id), _ => Task.FromResult<bool?>(true),
                _ => Task.FromResult<int?>(0)));
        Assert.Equal((OpinionGateStates.ReadAgain, "op2"), (gate.State, gate.Recheck?.Id));
        chat.End(1);
        chat.Turns.Gone();
    }

    /// <summary>
    /// 🔴 Who rechecks is independent of the answer too (§6.2, Codex's finding 9): the record that answered after a hand-over is of the first
    /// reviewer's family, so that reviewer is not presented as independent of the fixes; the recheck goes to another of the rule's reviewers,
    /// and where none is left, to none, its findings the person's.
    /// </summary>
    [Fact]
    public async Task A_recheck_is_never_read_by_the_family_of_the_successor_that_answered()
    {
        var (git, tree, tip) = ChatTree();
        var host = new OpinionStandIn().Chat("c1", "completed", tree).Chat("c2", "working", tree, from: "c1", minute: 5, adapter: "codex-acp")
            .Opinion("op1", "c1", weights: ["must"], tip: tip);
        var runner = new Runner(Events, host);
        var chat = runner.Held["c2"] = new Conversation();
        var choices = new List<ReviewerChoice>();
        var (adapters, roster) = Machine();
        var driver = new Daoris.Driver.Driver(host.Client(), DriverConfig.Empty with { Adapter = "claude-code-acp" }, adapters, _home, harnesses: roster)
        {
            OpinionGit = git.Read,
            SendsBack = runner,
            Passes = (_, choice, _) =>
            {
                choices.Add(choice);
                return Task.FromResult(OpinionPassRun.Unavailable(choice));
            },
        };

        Assert.Equal((OpinionDeliveryStates.Handed, "c2"), ((await driver.DeliverAsync("op1", OnRun())).State, host.HandsTo.Single()));
        await Poll.Until(() => chat.Took.Count == 1);
        host.Answer("op1", 1, "fixed", commit: git.Commit("the fix", ("src/report.ts", "v2")));
        chat.End(0);
        await Poll.Until(() => new OpinionDeliveries(_home).Read("op1").Answered is not null);

        var delivered = await driver.DeliverAsync("op1", OnRun() with { Workspace = "work" });

        var choice = Assert.Single(choices);
        Assert.False(choice.Chosen);
        Assert.Equal(ReviewerUnavailable.NotIndependent, choice.Code);
        Assert.Contains(choice.Working, family => family.Agent == "codex-acp");
        Assert.Equal(ReviewerUnavailable.NotIndependent, new OpinionDeliveries(_home).Read("op1").RecheckWhy);
        Assert.Equal(OpinionDeliveryStates.Answered, delivered.State);
        chat.Turns.Gone();
    }

    /// <summary>
    /// 🔴 A word of the person's steered into the answering turn marks its reading <c>person-steered</c> (§6.1 (c), the Opus check's finding 6):
    /// no recheck is asked for it, and its answers go to the person, who presses again.
    /// </summary>
    [Fact]
    public async Task A_reading_whose_turn_took_a_persons_word_asks_no_recheck()
    {
        var (git, tree, tip) = ChatTree();
        var host = new OpinionStandIn().Chat("c1", "working", tree).Opinion("op1", "c1", weights: ["must"], tip: tip);
        var steering = new Runner(Events, host);
        var chat = steering.Held["c1"] = new Conversation();
        var driver = ChatDriver(host, git, steering);
        await driver.DeliverAsync("op1", OnRun());
        await Poll.Until(() => chat.Took.Count == 1);
        host.Answer("op1", 1, "fixed", commit: git.Commit("the fix", ("src/report.ts", "v2")));
        new OpinionDeliveries(_home).Answered("op1", (await OpinionAnswers.ReadAsync(
            (await host.Client().Opinions.ReadAsync("op1"))!, "c1", tree, git.Read, DateTimeOffset.UnixEpoch, CancellationToken.None)) with { Steered = true });

        var delivered = await driver.DeliverAsync("op1", OnRun());

        Assert.Equal(OpinionDeliveryStates.Answered, delivered.State);
        Assert.Contains("your words reached the turn that answered", delivered.Line);
        Assert.Equal(OpinionRechecks.PersonSteered, new OpinionDeliveries(_home).Read("op1").RecheckWhy);
        chat.Turns.Gone();
    }

    /// <summary>
    /// 🔴 Findings handed before a hand-over (§6.1 (e)): the record they went to ended before it answered, and they are handed again, once, to
    /// the line's newest record in the same tree, which takes them as a turn of its own and answers them in its own frozen reading.
    /// </summary>
    [Fact]
    public async Task Findings_handed_before_a_hand_over_are_handed_again_to_the_newest_record_which_answers_them()
    {
        var (git, tree, tip) = ChatTree();
        var host = new OpinionStandIn().Chat("c1", "working", tree).Opinion("op1", "c1", weights: ["must"], tip: tip);
        var runner = new Runner(Events, host);
        var first = runner.Held["c1"] = new Conversation();
        var driver = ChatDriver(host, git, runner);
        Assert.Equal(OpinionDeliveryStates.Handed, (await driver.DeliverAsync("op1", OnRun())).State);

        // The person handed the conversation over before its turn on the findings ran: c1 ended, and c2 goes on in the same tree.
        first.Turns.Gone();
        runner.Held.Remove("c1");
        host.Move("c1", "completed");
        host.Chat("c2", "working", tree, from: "c1", minute: 5, adapter: "codex-acp");
        var next = runner.Held["c2"] = new Conversation();

        var again = await driver.DeliverAsync("op1", OnRun());

        Assert.Equal((OpinionDeliveryStates.Handed, "c2"), (again.State, again.Session));
        Assert.Equal(["c1", "c2"], host.HandsTo);
        Assert.Equal(("c2", "c1"), (host.Handed("op1")!["session"]!.GetValue<string>(), host.Handed("op1")!["from"]!.GetValue<string>()));
        await Poll.Until(() => next.Took.Count == 1, () => "the newest record never took the findings");
        host.Answer("op1", 1, "unresolved", why: "cannot reproduce");
        next.End(0);
        await Poll.Until(() => new OpinionDeliveries(_home).Read("op1").Answered is not null);
        Assert.Equal("c2", new OpinionDeliveries(_home).Read("op1").Answered!.Session);
        next.Turns.Gone();
    }

    /// <summary>
    /// 🔴 An ended resumable chat with no words of the person's waiting (§6.1 (f), Codex's finding 5): the findings are handed to the ended record
    /// and it goes on through its runner, answering them in its own conversation; a runner's definitive refusal sends them to the person, never
    /// to a new session.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(ContinueWhy.Unkept)]
    public async Task An_ended_resumable_chat_goes_on_with_the_findings_through_its_runner(string? never)
    {
        var (git, tree, tip) = ChatTree();
        var host = new OpinionStandIn().Chat("c1", "completed", tree).Opinion("op1", "c1", weights: ["must"], tip: tip);
        var runner = new Runner(Events, host) { CannotGoOn = never };

        var delivered = await ChatDriver(host, git, runner).DeliverAsync("op1", OnRun());

        if (never is not null)
        {
            Assert.Equal((OpinionDeliveryStates.ToPerson, never), (delivered.State, delivered.Why));
            Assert.Empty(host.Hands);
            Assert.Empty(runner.WentOn);
            return;
        }

        Assert.Equal((OpinionDeliveryStates.Handed, "c1"), (delivered.State, delivered.Session));
        Assert.Equal(["c1"], runner.WentOn);
        Assert.Equal("op1", host.Said("c1")[0]!["by"]!.GetValue<string>());
        var chat = runner.Held["c1"];
        await Poll.Until(() => chat.Took.Count == 1);
        host.Answer("op1", 1, "rejected", evidence: "the twin test holds it");
        chat.End(0);
        await Poll.Until(() => new OpinionDeliveries(_home).Read("op1").Answered is not null, () => "its own conversation's answers were never read");
        chat.Turns.Gone();
    }

    /// <summary>
    /// 🔴 (f), the Opus check's finding 7: the count compared first. A person who went on after the press and ended the chat again moved past the
    /// findings: they go to the person as <c>person-went-on</c>, and the conversation is never resumed.
    /// </summary>
    [Fact]
    public async Task A_person_who_went_on_after_the_press_and_ended_the_chat_again_has_the_findings()
    {
        var (git, tree, tip) = ChatTree();
        var host = new OpinionStandIn().Chat("c1", "completed", tree).Opinion("op1", "c1", weights: ["must"], tip: tip);
        var runner = new Runner(Events, host);
        Events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "make the change", Id = "w1" });
        Events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "one more thing", Id = "w2" });

        var delivered = await ChatDriver(host, git, runner).DeliverAsync("op1", OnRun(turns: 1));

        Assert.Equal((OpinionDeliveryStates.ToPerson, OpinionToPerson.PersonWentOn), (delivered.State, delivered.Why));
        Assert.Empty(runner.WentOn);
        Assert.Empty(host.Hands);
    }

    /// <summary>The host's opinion reads whole: its candidate, reviewer, findings, recheck's words, where they went and answers.</summary>
    [Fact]
    public async Task The_host_s_opinion_reads_whole()
    {
        var host = new OpinionStandIn().Session("s1", "completed").Opinion("op1", "s1", weights: ["must", "note"]);
        host.Answer("op1", 1, "rejected", evidence: "e");
        host.Rechecked("op1", 2, "withdrawn");
        await Deliver(host);

        var view = (await host.Client().Opinions.ReadAsync("op1"))!;

        Assert.Equal(("op1", "landing", "s1", "r-op1", OpinionStandIn.Tip, "codex-acp", "Codex (OpenAI)"),
            (view.Id, view.Occasion, view.Working, view.Session, view.Tip, view.Adapter, view.Who));
        Assert.True(view.First && view.Given);
        Assert.Equal(["must", "note"], view.Findings!.Select(finding => finding.Weight));
        Assert.Equal("withdrawn", view.Rechecked[2]);
        Assert.Equal("s1", view.HandedTo);
        Assert.Equal(("rejected", "e"), (view.AnswerTo(1)!.Said, view.AnswerTo(1)!.Evidence));
        Assert.Null(view.AnswerTo(2));
        Assert.Equal(20, view.Minutes);
        Assert.Null(OpinionViews.Read("""{"id":"x"}"""));
    }
}
