using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The answers-alone bound held where a line would go on: the
/// line's count kept in its admission, and a third answer in a row with nobody between marked
/// <see cref="GoOnMarks.AnswersAlone"/> with the agent that answered, said once in the conversation, so every look leaves it waiting for
/// the person. The person's *Go on with this answer* takes the mark away and counts as their word; their *Not now* keeps the answer for
/// them, so it goes on only beside words of theirs.
/// </summary>
public sealed class AnswersAloneHoldTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "answers-alone-hold", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-21T09:00:00Z");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private GoOnMarks Marks => new(_home);

    private ContinuationAdmissions Admissions => new(_home, () => StoreSettled.Yes);

    private AnswersAloneHold Hold => new(Events, Marks, Admissions);

    /// <summary>The session's record of a start, then each go-on by a helper's answer, as the go-on's opening keeps them.</summary>
    private void GoneOn(string session, int times, bool started = true)
    {
        var events = Events;
        if (started)
        {
            events.Append(session, new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "The quest's instruction." });
            events.Append(session, new SessionEvent { Kind = SessionEventKind.Message, Text = "Asked a helper." });
            events.Append(session, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        }

        for (var each = 0; each < times; each++)
        {
            events.Append(session, new SessionEvent { Kind = SessionEventKind.Note, Text = "— a helper's answer is the next turn." });
            events.Append(session, new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Helper = "h1", Id = $"{session}-a{each}", Text = "Answered." });
            events.Append(session, new SessionEvent { Kind = SessionEventKind.Message, Text = "Read it, and asked again." });
            events.Append(session, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        }
    }

    private static SaidWordView Answer(string id = "w9") => new(id, "Another agent answered.", At, [], true) { Helper = "h1" };

    private static SaidWordView Said(string id = "w8") => new(id, "Go on.", At, [], true);

    private static PriorSession Asker(string id = "s1", string? from = null, params SaidWordView[] said) =>
        new(id, "trees/default/reports/s-1", "completed") { Said = said, From = from, Adapter = "claude-code-acp" };

    private static readonly PriorSession HelperRecord = new("h1", null, "completed") { Adapter = "codex-acp", Serves = "s1", Kind = "chat" };

    private static Dictionary<string, PriorSession> Records(params PriorSession[] records) =>
        records.ToDictionary(record => record.Session, StringComparer.Ordinal);

    private IReadOnlyList<SessionEvent> Notes(string session, string code) =>
        [.. Events.After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == code) == true)];

    [Fact]
    public void A_third_answer_alone_is_marked_for_the_person_by_the_agent_that_answered_and_said_once()
    {
        GoneOn("s1", 2);
        var asker = Asker(said: [Answer("w9")]);

        Assert.True(Hold.Holds(asker, Records(asker, HelperRecord), At));
        Assert.True(Hold.Holds(asker, Records(asker, HelperRecord), At.AddMinutes(1)));

        var mark = Marks.Read("s1")!;
        Assert.Equal((GoOnMarks.AnswersAlone, "codex-acp"), (mark.Why, mark.Agent));
        Assert.Equal(["w9"], mark.Said);
        Assert.True(GoOnMarks.Judged(asker, mark));
        var held = Assert.Single(Notes("s1", HeldNoteCodes.AnswersHeld.Code));
        Assert.Equal(["w9"], held.Words);
        Assert.Equal("codex-acp", held.Parts![0].Value("agent"));
    }

    /// <summary>
    /// The number a line is held at is handed on before the hold marks or says anything, so the course's answer a go-on gives with it is heard
    /// before the hold's note: a reader of the note has the answer too. A line already waiting hands it on again.
    /// </summary>
    [Fact]
    public void The_held_number_is_handed_on_before_the_hold_marks_or_says_anything()
    {
        GoneOn("s1", 2);
        var asker = Asker(said: [Answer("w9")]);
        var records = Records(asker, HelperRecord);
        var heard = new List<(int? At, bool Marked, int Said)>();
        void Held(int? at) => heard.Add((at, Marks.Read("s1") is not null, Notes("s1", HeldNoteCodes.AnswersHeld.Code).Count));

        Assert.True(Hold.Holds(asker, records, At, heldAt: Held));
        Assert.True(Hold.Holds(asker, records, At.AddMinutes(1), heldAt: Held));

        Assert.Equal([(Hold.At("s1", records), false, 0), (Hold.At("s1", records), true, 1)], heard);
    }

    [Fact]
    public void CONTEXTDRIVE1s_a_hold_is_one_act_at_the_count_its_admission_read_and_a_new_answer_while_it_waits_adds_none()
    {
        GoneOn("s1", 2);
        var acts = new SupervisorRuleActs(_home, Events);
        var rule = new SupervisorRule("aa", SupervisorRules.AnswersAlone, "in-a-row", 1, SupervisorRules.Hold, null, SupervisorRules.Machine, null);
        var hold = new AnswersAloneHold(Events, Marks, Admissions, new SupervisorRuleBook([new SupervisorRuleSet(SupervisorRules.Machine, 1, [rule])]), acts);
        var asker = Asker(said: [Answer("w9")]);

        Assert.True(hold.Holds(asker, Records(asker, HelperRecord), At));
        var waiting = Asker(said: [Answer("w9"), Answer("w10")]);
        Assert.True(hold.Holds(waiting, Records(waiting, HelperRecord), At.AddMinutes(1)));

        var act = Assert.Single(acts.Of("s1"));
        Assert.Equal((2, 1), (act.N, act.Rule.At));
        Assert.Single(Notes("s1", HeldNoteCodes.AnswersHeld.Code));
        Assert.Equal(["w10", "w9"], Marks.Read("s1")!.Said.Order(StringComparer.Ordinal));
    }

    /// <summary>The hold's words give the rule's number, never a fixed "third": the note carries it and the go-on's refusal says it.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void The_hold_names_the_number_its_rule_holds_the_line_at_and_never_a_third(int bound)
    {
        GoneOn("s1", bound);
        var rule = new SupervisorRule("aa", SupervisorRules.AnswersAlone, "in-a-row", bound, SupervisorRules.Hold, null, SupervisorRules.Machine, null);
        var hold = new AnswersAloneHold(Events, Marks, Admissions, new SupervisorRuleBook([new SupervisorRuleSet(SupervisorRules.Machine, 1, [rule])]));
        var asker = Asker(said: [Answer("w9")]);
        int? heard = null;

        Assert.True(hold.Holds(asker, Records(asker, HelperRecord), At, heldAt: at => heard = at));

        Assert.Equal(bound, heard);
        var held = Assert.Single(Notes("s1", HeldNoteCodes.AnswersHeld.Code));
        Assert.Equal(bound.ToString(), held.Parts![0].Value("n")?.ToString());
        Assert.DoesNotContain("third", held.Text);
        Assert.Contains(bound.ToString(), held.Text);
        var refusal = AnswersAloneNotes.Refusal("s1", heard);
        Assert.DoesNotContain("third", refusal);
        Assert.Contains(bound.ToString(), refusal);
    }

    [Fact]
    public void A_second_answer_alone_goes_on_unmarked()
    {
        GoneOn("s1", 1);
        var asker = Asker(said: [Answer()]);

        Assert.False(Hold.Holds(asker, Records(asker, HelperRecord), At));
        Assert.Null(Marks.Read("s1"));
        Assert.Empty(Notes("s1", HeldNoteCodes.AnswersHeld.Code));
    }

    /// <summary>The count runs across a hand-over: the part the line continues and this one, followed back by the record's <c>from</c>.</summary>
    [Fact]
    public void The_line_is_counted_across_a_hand_over()
    {
        GoneOn("s0", 1);
        GoneOn("s1", 1, started: false);
        var asker = Asker("s1", from: "s0", Answer());

        Assert.True(Hold.Holds(asker, Records(asker, Asker("s0"), HelperRecord), At));
        Assert.False(new AnswersAloneHold(Events, Marks, Admissions).Holds(Asker("s2", from: null, Answer()), Records(HelperRecord), At));
    }

    // ——— The count kept in the line's admission.

    /// <summary>Each go-on's prompt as its session takes it: the answer, the agent acting on it, the turn's end.</summary>
    private void Took(string session, SessionEvent word)
    {
        var events = Events;
        events.Append(session, word);
        events.Append(session, new SessionEvent { Kind = SessionEventKind.Message, Text = "Read it, and went on." });
        events.Append(session, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
    }

    private static SessionEvent Taken(SaidWordView word) => new()
    {
        Kind = SessionEventKind.User, Origin = "target", Id = word.Id, Text = word.Text, Helper = word.Helper, Opinion = word.By,
        From = word.Supervisors ? SupervisorHanded.From : null,
    };

    private static readonly SaidWordView Findings = new("w2", "A second opinion's findings.", At, [], true) { By = "o1" };

    private static readonly SaidWordView Supervised = new("w3", SupervisorHanded.Text("The monthly one"), At, [], false) { From = SupervisorHanded.From, Decision = "d1" };

    private AloneCount CountOf(string session, IReadOnlyDictionary<string, PriorSession> records) => Admissions.Count(Hold.History(session, records))!;

    /// <summary>A helper's answer, an opinion's findings and the supervisor's word are one count, each go-on admitted then taken.</summary>
    [Fact]
    public void A_mixed_helper_opinion_and_supervisor_sequence_holds_the_third()
    {
        GoneOn("s1", 0);
        var records = Records(Asker(), HelperRecord);

        Assert.False(Hold.Holds(Asker(said: [Answer("w1")]), records, At));
        Took("s1", Taken(Answer("w1")));
        Assert.False(Hold.Holds(Asker(said: [Findings]), records, At));
        Took("s1", Taken(Findings));

        Assert.True(Hold.Holds(Asker(said: [Supervised]), records, At));
        var count = CountOf("s1", records);
        Assert.Equal((2, LineCounts.Exact, "admission"), (count.N, count.Known, count.From));
        Assert.All(Admissions.Of("s1")!.Admitted, row => Assert.NotNull(row.Taken));
        Assert.Equal(GoOnMarks.AnswersAlone, Marks.Read("s1")!.Why);
    }

    /// <summary>Two answers judged before either went on, as two executors queue them: the first is admitted, the second held for the person.</summary>
    [Fact]
    public void Two_answers_queued_together_are_admitted_once()
    {
        GoneOn("s1", 1);
        var records = Records(Asker(), HelperRecord);

        Assert.False(Hold.Holds(Asker(said: [Answer("a1")]), records, At, [Answer("a1")]));
        Assert.True(Hold.Holds(Asker(said: [Answer("a2")]), records, At, [Answer("a2")]));
        // The first judged again is the same go-on, and still admitted.
        Assert.False(Hold.Holds(Asker(said: [Answer("a1")]), records, At, [Answer("a1")]));
        Assert.Equal(["a2"], Marks.Read("s1")!.Said);
    }

    /// <summary>
    /// A hand-over's brief and a restart keep the count, and the log the count is seeded from going (by retention, or a file lost)
    /// resets nothing, where the events alone would count it again from nothing.
    /// </summary>
    [Fact]
    public void A_hand_over_and_a_restart_keep_the_count_and_a_lost_log_resets_nothing()
    {
        GoneOn("s0", 1);
        var records = Records(Asker("s0"), HelperRecord);
        Assert.False(Hold.Holds(Asker("s0", said: [Answer("w1")]), records, At));
        Took("s0", Taken(Answer("w1")));

        File.Delete(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions), "s0.events.jsonl"));
        Events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Continues = "s0", Text = "The brief of the line." });
        var handed = Asker("s1", from: "s0", Answer("w2"));
        var restarted = new AnswersAloneHold(Events, new GoOnMarks(_home), new ContinuationAdmissions(_home, () => StoreSettled.Yes));

        Assert.True(restarted.Holds(handed, Records(handed, Asker("s0"), HelperRecord), At));
        Assert.Equal(2, CountOf("s1", Records(handed, Asker("s0"), HelperRecord)).N);
        Assert.True(AnswersAlone.Count(Events.After("s1", 0).Events) < SupervisorRuleBook.AnswersAloneBound);
    }

    /// <summary>A wrapper Daoris composed, a brief, a retry's instruction and a hand-over reset nothing; the person's word and Daoris's fact word do.</summary>
    [Fact]
    public void The_persons_and_Daoriss_fact_words_reset_and_no_wrapper_brief_retry_or_hand_over_does()
    {
        GoneOn("s1", 2);
        var records = Records(Asker(), HelperRecord);
        Assert.True(Hold.Holds(Asker(said: [Answer("w9")]), records, At));
        Marks.Clear("s1");

        Took("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "— the answer, in Daoris's words around it." });
        Took("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Continues = "s0", Text = "The brief of the line." });
        Took("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "Try that again." });
        Assert.True(Hold.Holds(Asker(said: [Answer("w9")]), records, At));
        Marks.Clear("s1");

        Took("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Carry on." });
        Assert.False(Hold.Holds(Asker(said: [Answer("w9")]), records, At));
        Took("s1", Taken(Answer("w9")));
        Assert.False(Hold.Holds(Asker(said: [Answer("w10")]), records, At));
        Took("s1", Taken(Answer("w10")));
        Assert.True(Hold.Holds(Asker(said: [Answer("w11")]), records, At));
        Marks.Clear("s1");

        Took("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", From = DaorisWords.From, Text = "Daoris's go-on." });
        Assert.False(Hold.Holds(Asker(said: [Answer("w11")]), records, At));
    }

    /// <summary>
    /// A quest's resume opens a run of its quest with no <c>from</c>: it is on its quest's line (<see cref="LineKeys.Of"/>), so the count the
    /// first run left carries to it, where the record's own <c>from</c> walk would start again from nothing.
    /// </summary>
    [Fact]
    public void D79s_resume_is_on_its_quests_line_and_keeps_the_count()
    {
        GoneOn("s1", 2);
        GoneOn("s2", 0);
        var first = Asker("s1") with { Kind = "driven", Quest = "q1", Created = At };
        var resumed = Asker("s2", said: [Answer()]) with { Kind = "driven", Quest = "q1", Created = At.AddMinutes(5) };

        Assert.True(Hold.Holds(resumed, Records(first, resumed, HelperRecord), At));
        Assert.Equal("q1", CountOf("s2", Records(first, resumed, HelperRecord)).Line);
    }

    /// <summary>A waited quest's result counts (the comms design §11): the resume that takes it is reserved on its line once, held by nothing.</summary>
    [Fact]
    public void A_waited_quests_result_counts_once()
    {
        GoneOn("s1", 1);
        var asker = Asker() with { Kind = "driven", Quest = "q1" };
        var records = Records(asker, HelperRecord);

        Assert.Equal(2, Hold.Waited(asker, "q9", [], records)!.N);
        Assert.Equal(2, Hold.Waited(asker, "q9", [], records)!.N);

        Assert.True(Hold.Holds(asker with { Said = [Answer()] }, records, At));
    }

    /// <summary>A conversation never waits on a quest, so no quest's result counts on its line or wakes it.</summary>
    [Fact]
    public void A_conversation_is_never_counted_a_quests_result()
    {
        GoneOn("c1", 1);
        var chat = Asker("c1") with { Kind = "chat" };

        Assert.Null(Hold.Waited(chat, "q9", [], Records(chat)));
        Assert.Null(Admissions.Of("c1"));
    }

    /// <summary>The supervisor's route reads the same count: at the bound once two go-ons were admitted, whatever the events say.</summary>
    [Fact]
    public void The_supervisors_bound_reads_the_admission()
    {
        GoneOn("s1", 1);
        var records = Records(Asker(), HelperRecord);
        Assert.False(Hold.AtBound("s1", records));

        Assert.False(Hold.Holds(Asker(said: [Answer("w1")]), records, At));

        Assert.True(Hold.AtBound("s1", records));
        Assert.True(AnswersAlone.Count(Events.After("s1", 0).Events) < SupervisorRuleBook.AnswersAloneBound);
    }

    /// <summary>The steward's choice 2: a line with no events here counts nothing, and a teammate's record is never this machine's.</summary>
    [Fact]
    public void A_line_with_no_events_here_and_a_teammates_record_never_hold()
    {
        var asker = Asker(said: [Answer()]);
        Assert.False(Hold.Holds(asker, Records(asker), At));

        GoneOn("s1", 2);
        Assert.False(Hold.Holds(Asker("peer/s1", said: [Answer()]), Records(asker), At));
        Assert.Null(Marks.Read("s1"));
    }

    [Fact]
    public void Words_with_the_persons_among_them_never_hold()
    {
        GoneOn("s1", 2);
        var asker = Asker(said: [Answer(), Said()]);

        Assert.False(Hold.Holds(asker, Records(asker, HelperRecord), At));
        Assert.Null(Marks.Read("s1"));
    }

    /// <summary>
    /// The look's entry point: every record whose words are answers alone is judged, its line read from the service's records, and one
    /// already held is left as it is.
    /// </summary>
    [Fact]
    public async Task The_look_marks_the_records_whose_answers_would_go_on_a_third_time()
    {
        GoneOn("s1", 2);
        GoneOn("s2", 2);
        var host = new OpinionStandIn().Session("s1", "completed", tree: "trees/default/reports/s-1").Session("s2", "completed")
            .Session("h1", "completed", adapter: "codex-acp", kind: "chat");
        var said = new[] { Answer("w9") };
        IReadOnlyList<PriorSession> looked = [Asker("s1", said: said), Asker("s2", said: [Said("w7")]), Asker("s3")];

        Assert.Equal(1, await Hold.LookAsync(host.Client(), looked, At, CancellationToken.None));
        Assert.Equal(GoOnMarks.AnswersAlone, Marks.Read("s1")!.Why);
        Assert.Null(Marks.Read("s2"));
        Assert.Equal(0, await Hold.LookAsync(host.Client(), looked, At, CancellationToken.None));
        Assert.Single(Notes("s1", HeldNoteCodes.AnswersHeld.Code));
    }

    /// <summary>
    /// The person's *Go on with this answer* (§8.3; the steward's choice 1): the mark goes and the press is kept as their word first, so the
    /// answer goes on and the next two answers go on alone again.
    /// </summary>
    [Fact]
    public void Go_on_with_this_answer_takes_the_mark_away_and_counts_as_the_persons_word()
    {
        GoneOn("s1", 2);
        var asker = Asker(said: [Answer("w9")]);
        Assert.True(Hold.Holds(asker, Records(asker, HelperRecord), At));

        var answer = AnswersAloneWord.GoOn(Marks, Events, "s1");

        Assert.Equal((true, (string?)null), (answer.Stands, answer.Why));
        Assert.Null(Marks.Read("s1"));
        Assert.Equal(["w9"], Assert.Single(Notes("s1", HeldNoteCodes.AnswersPassed.Code)).Words);
        Assert.False(Hold.Holds(asker, Records(asker, HelperRecord), At));
    }

    /// <summary>
    /// The person's *Not now* (§8.3): the answer is theirs, kept on the record and marked so no look takes it on by itself, until a word of
    /// theirs is said beside it.
    /// </summary>
    [Fact]
    public void Not_now_keeps_the_answer_for_the_person_until_their_own_word()
    {
        GoneOn("s1", 2);
        var asker = Asker(said: [Answer("w9")]);
        Assert.True(Hold.Holds(asker, Records(asker, HelperRecord), At));

        var answer = AnswersAloneWord.NotNow(Marks, Events, "s1", At);

        Assert.True(answer.Stands);
        var mark = Marks.Read("s1")!;
        Assert.Equal((GoOnMarks.NotNow, "codex-acp"), (mark.Why, mark.Agent));
        Assert.True(GoOnMarks.Judged(asker, mark));
        Assert.False(GoOnMarks.Judged(Asker(said: [Answer("w9"), Said()]), mark));
        Assert.Single(Notes("s1", HeldNoteCodes.AnswersKept.Code));
        // Kept, it waits on the person no more: neither act answers it again.
        Assert.Equal((false, AnswersAloneWord.Unheld), (AnswersAloneWord.GoOn(Marks, Events, "s1").Stands, AnswersAloneWord.NotNow(Marks, Events, "s1", At).Why));
    }

    [Fact]
    public void An_answer_the_bound_does_not_hold_is_answered_with_its_code_and_nothing_written()
    {
        Marks.Mark("s1", ["w9"], ContinueWhy.Of(ContinueWhy.Tree), At);

        var answer = AnswersAloneWord.GoOn(Marks, Events, "s2");

        Assert.Equal((false, AnswersAloneWord.Unheld), (answer.Stands, answer.Why));
        Assert.False(AnswersAloneWord.NotNow(Marks, Events, "s1", At).Stands);
        Assert.Equal(ContinueWhy.Tree, Marks.Read("s1")!.Why);
        Assert.Empty(Events.After("s1", 0).Events);
    }
}
