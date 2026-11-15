using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Which record a start goes on in its own conversation after Daoris cut it (<see cref="CarryOnResume"/>), and that every other carry-on, a
/// move to another account or another agent among them, still opens a new part linked by <c>from</c>.
/// </summary>
public sealed class CarryOnResumeTests
{
    private static readonly DateTimeOffset Ended = new(2026, 11, 15, 10, 1, 0, TimeSpan.Zero);

    private static readonly QuestView Taken = new("q1", "Asker", "Game", "Ask q1", "Why.", "Taken");

    private static PriorSession Cut(bool interrupted = true, string state = "stopped") =>
        new("s1", "D:/fam/Game", state, "the driver was stopped while this ran.", "Game", Interrupted: interrupted)
        {
            Adapter = "acp-stub", Profile = "account-1", Updated = Ended,
        };

    [Fact]
    public void A_cut_of_Daoris_s_is_keyed_by_an_update_s_build_or_the_moment_it_ended_and_nothing_else_is_a_cut()
    {
        var update = Cut() with { NoteParts = [EndNoteCodes.UpdateClosed.Part("Daoris stopped for an update.", ("build", "b7"))] };

        Assert.Equal("2026-11-15T10-01-00.0000000Z", CarryOnResume.Close(Cut()));
        Assert.Equal("b7", CarryOnResume.Close(update));
        Assert.Null(CarryOnResume.Close(Cut(interrupted: false)));
        Assert.Null(CarryOnResume.Close(Cut(state: "failed")));
    }

    [Fact]
    public void Only_an_open_or_taken_quest_s_cut_waiting_on_nothing_and_not_handed_over_goes_on_in_its_own_record()
    {
        Assert.Equal("s1", CarryOnResume.Line(Taken, Cut(), handedFrom: false)?.Session);
        Assert.Null(CarryOnResume.Line(Taken, Cut(), handedFrom: true));
        Assert.Null(CarryOnResume.Line(Taken with { Status = "Done" }, Cut(), handedFrom: false));
        Assert.Null(CarryOnResume.Line(Taken with { Awaits = "q2" }, Cut(), handedFrom: false));
        Assert.Null(CarryOnResume.Line(Taken, Cut(interrupted: false), handedFrom: false));
    }

    [Fact]
    public void A_cut_whose_own_account_is_not_ready_moves_to_another_and_opens_a_record_linked_by_from()
    {
        var elsewhere = new LineAnswer.Elsewhere(new HarnessSelection(Refusal: null, Profile: "account-2"), NextHold.Cooling);

        var course = DrivenCourse.LimitOwn(Cut(), elsewhere, standing: null);
        var from = DrivenCourse.From(null, Cut(), "account-2", walked: null, retried: false, pressed: false, agent: null, decided: null);

        Assert.Equal((CourseFromWhy.CarryOn, CourseFromBy.Daoris), (Assert.IsType<LineCourse.Move>(course).Why, ((LineCourse.Move)course).By));
        Assert.Equal(new ContinuedFrom("s1", HandOverWhy.CarryOn, HandOverBy.Daoris), from);
    }

    [Fact]
    public void A_cut_on_another_agent_never_resumes_its_conversation_and_opens_a_record_linked_by_from()
    {
        var asIfWords = Cut() with { Said = [new SaidWordView(null, ShellClose.Words, Ended, [], true) { From = DaorisWords.From }] };

        var judged = Continuations.Judge(
            _ => true, asIfWords, "codex-acp", doorResumes: true, "account-1", new HarnessConversation("acp-stub", "conv-s1"));
        var from = DrivenCourse.From(null, Cut(), "account-1", walked: null, retried: false, pressed: false, agent: "codex-acp", decided: null);

        Assert.Equal(ContinueWhy.DoorChanged("acp-stub", "codex-acp").Code, judged?.Code);
        Assert.Equal(new ContinuedFrom("s1", HandOverWhy.CarryOn, HandOverBy.Daoris), from);
    }
}
