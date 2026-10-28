using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The answers-alone bound (COWORK1e, D188 point 4; the message-urgency design §2.3, trap 11): a line of work goes on by solicited
/// answers alone (a helper's answer, a second opinion's findings) at most twice in a row, counted from the line's events. Only a word of
/// the person's or Daoris's on the line resets it, the person's *Go on with this answer* among them; a hand-over never does; an answer
/// taken inside a running turn is no go-on and counts nothing; a line with no events counts nothing.
/// </summary>
public sealed class AnswersAloneTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-21T09:00:00Z");

    // ——— The events a line keeps, as the doors write them.

    private static SessionEvent Started() => new() { Kind = SessionEventKind.User, Origin = "target", Text = "The quest's instruction." };

    private static SessionEvent Person(string text = "Go on.") => new() { Kind = SessionEventKind.User, Origin = "person", Text = text };

    private static SessionEvent Daoris() => new() { Kind = SessionEventKind.User, Origin = "target", From = DaorisWords.From, Text = "Daoris's go-on." };

    private static SessionEvent Helper(string id = "w1") =>
        new() { Kind = SessionEventKind.User, Origin = "target", Helper = "h1", Id = id, Text = "Another agent answered." };

    private static SessionEvent Findings() =>
        new() { Kind = SessionEventKind.User, Origin = "target", Opinion = "o1", Text = "A second opinion's findings." };

    private static SessionEvent Brief() =>
        new() { Kind = SessionEventKind.User, Origin = "target", Continues = "s0", Text = "The brief of the line." };

    private static SessionEvent Opening() => new() { Kind = SessionEventKind.Note, Text = "— a helper's answer is the next turn." };

    private static SessionEvent Acts() => new() { Kind = SessionEventKind.Message, Text = "I read it and went on." };

    private static SessionEvent Ended() => new() { Kind = SessionEventKind.Turn, StopReason = "end_turn" };

    private static SessionEvent Passed() => AnswersAloneNotes.Passed(["w9"]);

    /// <summary>One turn: what it was prompted with, then the agent acting on it, then its end.</summary>
    private static IEnumerable<SessionEvent> Turn(params SessionEvent[] prompt) => [.. prompt, Acts(), Ended()];

    private static SaidWordView Answer(string id = "w9") => new(id, "Another agent answered.", At, [], true) { Helper = "h1" };

    private static SaidWordView Finding(string id = "w8") => new(id, "Findings.", At, [], true) { By = "o1" };

    private static SaidWordView Said(string id = "w7") => new(id, "My words.", At, [], true);

    [Fact]
    public void Each_turn_prompted_by_answers_alone_counts_one_in_a_row()
    {
        IEnumerable<SessionEvent> line = [.. Turn(Started()), .. Turn(Opening(), Helper()), .. Turn(Opening(), Findings())];

        Assert.Equal(2, AnswersAlone.Count(line));
    }

    [Fact]
    public void The_third_answer_in_a_row_with_nobody_between_holds_and_the_second_does_not()
    {
        IEnumerable<SessionEvent> once = [.. Turn(Started()), .. Turn(Opening(), Helper())];
        IEnumerable<SessionEvent> twice = [.. once, .. Turn(Opening(), Helper("w2"))];

        Assert.False(AnswersAlone.Holds([Answer()], once));
        Assert.True(AnswersAlone.Holds([Answer()], twice));
        Assert.True(AnswersAlone.Holds([Finding(), Answer()], twice));
    }

    /// <summary>
    /// COAGENT1b (the co-agent design §4.2): a piece's answer counts as a helper's, by its mark; and a piece's notes on the lead (refused,
    /// no work, out of time, uncertain, answered) reset nothing, whether beside an answer in its prompt or between two answers.
    /// </summary>
    [Fact]
    public void A_pieces_answer_counts_and_its_notes_reset_nothing()
    {
        static SessionEvent Noted(Noted noted) => new() { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts };
        var piece = new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Helper = "pt1", Id = "w5", Text = "A partner's answer." };
        IEnumerable<SessionEvent> line =
        [
            .. Turn(Started()),
            .. Turn(Noted(PartnerNotes.Refused(PartnerWhy.NoWork)), piece),
            Noted(PartnerNotes.Ended(PartnerEnds.OutOfTime, "codex-acp")!),
            Noted(PartnerNotes.Uncertain(PartnerWhy.JobUnknown)),
            .. Turn(Noted(PartnerNotes.Answered("codex-acp", "pt2")), Helper("w6")),
        ];

        Assert.Equal(2, AnswersAlone.Count(line));
        Assert.True(AnswersAlone.Holds([new SaidWordView("w7", "A partner's answer.", At, [], true) { Helper = "pt3", Key = "partner:p3" }], line));
    }

    /// <summary>Only a word of the person's or Daoris's resets it, whether it prompted a turn or was taken inside one.</summary>
    [Fact]
    public void A_word_of_the_persons_or_of_Daoriss_on_the_line_resets_the_count()
    {
        IEnumerable<SessionEvent> twice = [.. Turn(Opening(), Helper()), .. Turn(Opening(), Helper("w2"))];

        Assert.Equal(0, AnswersAlone.Count([.. twice, .. Turn(Person())]));
        Assert.Equal(0, AnswersAlone.Count([.. twice, .. Turn(Daoris())]));
        // An answer beside the person's words in one prompt is no answer alone: the next answer is the first again.
        Assert.Equal(1, AnswersAlone.Count([.. twice, .. Turn(Opening(), Person(), Helper("w3")), .. Turn(Opening(), Helper("w4"))]));
        // The person's word taken inside a turn an answer prompted: the next answer is the first again.
        Assert.Equal(1, AnswersAlone.Count([.. twice, Helper("w3"), Acts(), Person("Wait."), Acts(), Ended(), .. Turn(Opening(), Helper("w5"))]));
    }

    /// <summary>The person's *Go on with this answer* (the steward's choice 1): their word, so the next two answers go on alone again.</summary>
    [Fact]
    public void The_persons_press_to_go_on_with_the_answer_counts_as_their_word()
    {
        IEnumerable<SessionEvent> twice = [.. Turn(Opening(), Helper()), .. Turn(Opening(), Helper("w2"))];
        IEnumerable<SessionEvent> passed = [.. twice, .. Turn(Passed(), Opening(), Helper("w3"))];

        Assert.Equal(0, AnswersAlone.Count(passed));
        Assert.False(AnswersAlone.Holds([Answer()], [.. passed, .. Turn(Opening(), Helper("w4"))]));
        Assert.True(AnswersAlone.Holds([Answer()], [.. passed, .. Turn(Opening(), Helper("w4")), .. Turn(Opening(), Helper("w5"))]));
        // Pressed while nothing has gone on yet: the answer it lets go is judged against the press.
        Assert.False(AnswersAlone.Holds([Answer()], [.. twice, Passed()]));
    }

    /// <summary>An urgent answer taken at a step of a running turn is no go-on (§2.3): no run went on past its own turn.</summary>
    [Fact]
    public void An_answer_taken_inside_a_running_turn_counts_nothing()
    {
        IEnumerable<SessionEvent> line = [Started(), Acts(), Helper(), Acts(), Helper("w2"), Acts(), Ended(), .. Turn(Opening(), Helper("w3"))];

        Assert.Equal(1, AnswersAlone.Count(line));
    }

    /// <summary>A hand-over's brief neither counts nor resets: the count runs across the line's parts, as across retries.</summary>
    [Fact]
    public void A_hand_over_neither_counts_nor_resets()
    {
        IEnumerable<SessionEvent> line = [.. Turn(Started()), .. Turn(Opening(), Helper()), .. Turn(Brief()), .. Turn(Opening(), Helper("w2"))];

        Assert.Equal(2, AnswersAlone.Count(line));
        Assert.True(AnswersAlone.Holds([Answer()], line));
    }

    /// <summary>Two answers handed in one prompt are one go-on; a word shown while it waits and a go-on no agent answered are none.</summary>
    [Fact]
    public void Only_a_turn_that_happened_counts_and_it_counts_once()
    {
        Assert.Equal(1, AnswersAlone.Count(Turn(Opening(), Helper(), Helper("w2"))));
        Assert.Equal(0, AnswersAlone.Count([Helper() with { Reaches = "resume" }]));
        // A resume the agent would not take: its opening was written, nothing acted, and the try again is one go-on.
        Assert.Equal(1, AnswersAlone.Count([Opening(), Helper(), new SessionEvent { Kind = SessionEventKind.Note, Text = "— it cannot go on." }, .. Turn(Opening(), Helper())]));
    }

    /// <summary>The steward's choice 2: a line whose events are missing counts nothing, and never holds.</summary>
    [Fact]
    public void A_line_with_no_events_counts_nothing_and_never_holds()
    {
        Assert.Equal(0, AnswersAlone.Count([]));
        Assert.False(AnswersAlone.Holds([Answer()], []));
    }

    /// <summary>
    /// Every door that keeps a helper's answer on its record marks it as that helper's, so the count tells it from Daoris's composed
    /// prompt: a go-on's opening (both go-on paths), a conversation's turn (the turn pump) and a word taken at a step.
    /// </summary>
    [Fact]
    public void Every_door_keeps_a_helpers_answer_marked_as_the_helpers()
    {
        var opening = new ResumeAsk("c1", [Answer("w1"), Finding("w2"), Said("w3")], "— resumed.").Opening();
        Assert.Equal([null, "h1", null, null], opening.Select(e => e.Helper));
        Assert.Equal(1, AnswersAlone.Count([.. opening.Take(2), Acts(), Ended()]));

        var turn = new ChatMessage("Another agent answered.", []) { Helped = Answer("w4"), Id = "w4" };
        Assert.Equal(["h1"], ChatRunner.Recorded(turn).Select(e => e.Helper));
        Assert.Equal("h1", ChatMessage.Words(turn).Helper);
        Assert.Null(ChatMessage.Words(new ChatMessage("Mine.", [])).Helper);
    }

    /// <summary>Words that are not all solicited answers never hold: the person's, Daoris's and a claim the person passed on.</summary>
    [Fact]
    public void Only_answers_alone_are_held()
    {
        IEnumerable<SessionEvent> twice = [.. Turn(Opening(), Helper()), .. Turn(Opening(), Helper("w2"))];

        Assert.False(AnswersAlone.Holds([Answer(), Said()], twice));
        Assert.False(AnswersAlone.Holds([Answer(), Said() with { From = DaorisWords.From }], twice));
        Assert.False(AnswersAlone.Holds([Answer(), Said() with { From = "agent" }], twice));
        Assert.False(AnswersAlone.Holds([], twice));
        Assert.True(AnswersAlone.Solicited(Answer()) && AnswersAlone.Solicited(Finding()) && !AnswersAlone.Solicited(Said()));
    }

    // ——— SUPERVISOR1a-i (D208; the supervisor design §4.3): one count, D188's, with the supervisor in both its predicates.

    private static SessionEvent Supervisor(string id = "w4") =>
        new() { Kind = SessionEventKind.User, Origin = "target", From = SupervisorHanded.From, Id = id, Text = "The supervisor chose." };

    private static SaidWordView Supervised(string id = "w3") =>
        new(id, SupervisorHanded.Text("The monthly one"), At, [], false) { From = SupervisorHanded.From, Decision = "d1" };

    /// <summary>Three supervisor-only go-ons in a row: the third is held, as a helper's would be; and after the person's word, none is.</summary>
    [Fact]
    public void Supervisor_only_go_ons_count_and_the_third_in_a_row_is_held()
    {
        IEnumerable<SessionEvent> twice = [.. Turn(Started()), .. Turn(Supervisor()), .. Turn(Supervisor("w5"))];

        Assert.Equal(2, AnswersAlone.Count(twice));
        Assert.True(AnswersAlone.Solicited(Supervised()));
        Assert.True(AnswersAlone.Holds([Supervised()], twice));
        Assert.False(AnswersAlone.Holds([Supervised()], [.. twice, .. Turn(Person())]));
    }

    /// <summary>
    /// Mixed prompts and alternating kinds are one count: a supervisor's answer beside a helper's is one go-on, and a supervisor's, then a
    /// helper's, then an opinion's in a row hold the next as any two would. Daoris's word resets it; the supervisor's never does.
    /// </summary>
    [Fact]
    public void Mixed_and_alternating_answers_are_one_count_and_the_supervisor_never_resets_it()
    {
        IEnumerable<SessionEvent> mixed = [.. Turn(Started()), .. Turn(Supervisor(), Helper("w6")), .. Turn(Findings())];
        IEnumerable<SessionEvent> alternating = [.. Turn(Started()), .. Turn(Helper()), .. Turn(Supervisor()), .. Turn(Findings())];

        Assert.Equal(2, AnswersAlone.Count(mixed));
        Assert.Equal(3, AnswersAlone.Count(alternating));
        Assert.True(AnswersAlone.Holds([Supervised(), Answer()], mixed));
        Assert.Equal(0, AnswersAlone.Count([.. alternating, .. Turn(Daoris())]));
    }

    /// <summary>
    /// Read from the line's events, so the count crosses a restart; a supervisor's event an agent forged into the log only raises the
    /// count (it never resets it), which holds the line sooner: the conservative side.
    /// </summary>
    [Fact]
    public void A_forged_supervisor_event_only_raises_the_count()
    {
        IEnumerable<SessionEvent> once = [.. Turn(Started()), .. Turn(Helper())];
        var forged = Supervisor("forged") with { Origin = "person" };

        Assert.Equal(1, AnswersAlone.Count(once));
        Assert.Equal(2, AnswersAlone.Count([.. once, .. Turn(forged)]));
        Assert.Equal(2, AnswersAlone.Count([.. once, .. Turn(Helper("w2"), forged)]));
    }

    // ——— SUPERVISOR1b (D208; the supervisor design §4.3): the route's `supervisor-bound` reads the same one count.

    /// <summary>
    /// A line is at the bound where its count by the event predicate is D188's two, whichever answers took it there: the supervisor's,
    /// a helper's, an opinion's, mixed; the person's or Daoris's word puts it back. At the bound a waiting supervisor's word, by the
    /// waiting-word predicate, is one the hold would keep, so the route refuses to keep a decision only to be held behind it.
    /// </summary>
    [Fact]
    public void The_supervisors_route_is_at_the_bound_by_both_predicates()
    {
        IEnumerable<SessionEvent> once = [.. Turn(Started()), .. Turn(Supervisor())];
        IEnumerable<SessionEvent> twice = [.. once, .. Turn(Helper("w6"))];

        Assert.False(AnswersAlone.AtBound(once));
        Assert.True(AnswersAlone.AtBound(twice));
        Assert.True(AnswersAlone.AtBound([.. Turn(Started()), .. Turn(Supervisor()), .. Turn(Supervisor("w5"))]));
        Assert.True(AnswersAlone.AtBound([.. Turn(Started()), .. Turn(Findings()), .. Turn(Supervisor(), Helper("w6"))]));
        Assert.False(AnswersAlone.AtBound([.. twice, .. Turn(Person())]));
        Assert.False(AnswersAlone.AtBound([.. twice, .. Turn(Daoris())]));
        Assert.True(AnswersAlone.Holds([Supervised()], twice));
        Assert.False(AnswersAlone.Holds([Supervised()], once));
    }

    /// <summary>
    /// The hold answers the route from this machine's own events, read across the line's parts as the look reads them: at the bound
    /// after two go-ons alone, short of it after one, and never for a record that is not this machine's. Each test its own scratch home.
    /// </summary>
    [Fact]
    public void The_hold_reads_the_bound_from_the_lines_events_here()
    {
        var home = Path.Combine(AppContext.BaseDirectory, "alone-bound", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
            var hold = new AnswersAloneHold(events, new GoOnMarks(home));
            foreach (var e in Turn(Started())) events.Append("s0", e);
            foreach (var e in Turn(Supervisor())) events.Append("s0", e);
            var records = new Dictionary<string, PriorSession>(StringComparer.Ordinal)
            {
                ["s1"] = new("s1", null, "awaiting-person") { From = "s0" },
            };

            Assert.False(hold.AtBound("s0", records));
            foreach (var e in Turn(Helper("w6"))) events.Append("s1", e);
            Assert.True(hold.AtBound("s1", records));
            Assert.False(hold.AtBound("s1", new Dictionary<string, PriorSession>(StringComparer.Ordinal)));
            Assert.False(hold.AtBound("peer/s1", records));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }
}
