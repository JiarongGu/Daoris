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
}
