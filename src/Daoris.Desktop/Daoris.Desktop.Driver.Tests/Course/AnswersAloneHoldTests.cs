using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The answers-alone bound held where a line would go on (COWORK1e, D188 point 4; the message-urgency design §2.3, §8.3, trap 11): the
/// line's events read back over its parts on this machine, and a third answer in a row with nobody between marked
/// <see cref="GoOnMarks.AnswersAlone"/> with the agent that answered, said once in the conversation, so every look leaves it waiting for
/// the person. The person's *Go on with this answer* takes the mark away and counts as their word; their *Not now* keeps the answer for
/// them, so it goes on only beside words of theirs.
/// </summary>
public sealed class AnswersAloneHoldTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-alone-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-21T09:00:00Z");

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private GoOnMarks Marks => new(_home);

    private AnswersAloneHold Hold => new(Events, Marks);

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
        var held = Assert.Single(Notes("s1", NoteCodes.AnswersHeld.Code));
        Assert.Equal(["w9"], held.Words);
        Assert.Equal("codex-acp", held.Parts![0].Value("agent"));
    }

    [Fact]
    public void A_second_answer_alone_goes_on_unmarked()
    {
        GoneOn("s1", 1);
        var asker = Asker(said: [Answer()]);

        Assert.False(Hold.Holds(asker, Records(asker, HelperRecord), At));
        Assert.Null(Marks.Read("s1"));
        Assert.Empty(Notes("s1", NoteCodes.AnswersHeld.Code));
    }

    /// <summary>The count runs across a hand-over: the part the line continues and this one, followed back by the record's <c>from</c>.</summary>
    [Fact]
    public void The_line_is_counted_across_a_hand_over()
    {
        GoneOn("s0", 1);
        GoneOn("s1", 1, started: false);
        var asker = Asker("s1", from: "s0", Answer());

        Assert.True(Hold.Holds(asker, Records(asker, Asker("s0"), HelperRecord), At));
        Assert.False(new AnswersAloneHold(Events, Marks).Holds(Asker("s2", from: null, Answer()), Records(HelperRecord), At));
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
        Assert.Single(Notes("s1", NoteCodes.AnswersHeld.Code));
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
        Assert.Equal(["w9"], Assert.Single(Notes("s1", NoteCodes.AnswersPassed.Code)).Words);
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
        Assert.Single(Notes("s1", NoteCodes.AnswersKept.Code));
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
