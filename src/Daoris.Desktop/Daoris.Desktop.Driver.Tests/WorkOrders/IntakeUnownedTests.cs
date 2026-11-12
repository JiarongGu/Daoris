using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where no department owns an ask, the intake says so rather than route it to the nearest repository: its last message ends with
/// one line the driver reads (<see cref="IntakeUnowned"/>), and the intake's one park, the record that waits on the person, then
/// carries the sentence that names Ask Daoris as the one that can open a department. The intake creates nothing and publishes nothing.
/// </summary>
public sealed class IntakeUnownedTests
{
    private const string Why = "a billing system, and no repository here declares billing or accepts payments work";

    private static AskView Ask(string state = "Proposed", IReadOnlyList<string>? quests = null, string tier = "person") =>
        new("a1b2c3", "work", "build a billing system", state, tier) { Quests = quests ?? [] };

    // ——— The reader of the verdict.

    [Theory]
    [InlineData("no-department-owns-this: " + Why, Why)]
    [InlineData("I read the room and every declaration.\n\nno-department-owns-this: " + Why + "\n", Why)]
    [InlineData("It fits none.\r\nNo-Department-Owns-This:   " + Why + "   \r\n\r\n", Why)]
    [InlineData("`no-department-owns-this: " + Why + "`", Why)]
    public void The_intakes_last_line_can_say_that_no_department_owns_the_work(string lastWords, string reason)
    {
        Assert.Equal(reason, IntakeUnowned.Read(lastWords));
    }

    [Fact]
    public void A_verdict_that_is_malformed_is_no_verdict()
    {
        // Nothing said, or nothing after the marker.
        Assert.Null(IntakeUnowned.Read(null));
        Assert.Null(IntakeUnowned.Read(""));
        Assert.Null(IntakeUnowned.Read("no-department-owns-this:"));
        Assert.Null(IntakeUnowned.Read("no-department-owns-this:   \n"));
        // The marker is the whole last line, never a phrase inside prose or an earlier line.
        Assert.Null(IntakeUnowned.Read("I would say no-department-owns-this: " + Why));
        Assert.Null(IntakeUnowned.Read("no-department-owns-this: " + Why + "\nWhich repository should take it?"));
        // A close that is not the marker, and a reason too long to be one sentence.
        Assert.Null(IntakeUnowned.Read("no department owns this: " + Why));
        Assert.Null(IntakeUnowned.Read("no-department-owns-this: " + new string('x', IntakeUnowned.ReasonChars + 1)));
    }

    // ——— What the park says.

    [Fact]
    public void An_ask_no_department_owns_is_held_with_the_sentence_that_names_ask_daoris_and_nothing_is_published()
    {
        var concluded = IntakeObservation.Conclude(0, before: 0, Ask(), unowned: Why);

        Assert.Equal("awaiting-person", concluded.State);
        Assert.True(SessionStates.IsParked(concluded.State));
        Assert.Equal(
            "no department owns ask `#a1b2c3`: nothing in this workspace's declarations covers the work. It is held for Ask Daoris — "
            + "ask it to open a department or a room for it (`daoris repository create`, or `daoris place <repository> --room <path>`) — "
            + "or route it yourself with `daoris ask publish a1b2c3 --to <repository>`, or end it with `daoris ask close a1b2c3 "
            + "--reason \"…\"`. The intake said: " + Why,
            concluded.Note);
        NoteAssert.Holds(concluded.Note, concluded.Parts);
        Assert.Equal(["intake.unowned", null], NoteAssert.Codes(concluded.Parts));
        Assert.Equal("a1b2c3", concluded.Parts![0].Value("ask"));
        Assert.Equal(NoteBy.Agent, concluded.Parts![1].By);
    }

    /// <summary>The verdict is one more shape of the park, never another way of ending: only a clean exit that published nothing carries it.</summary>
    [Fact]
    public void The_verdict_changes_only_the_park_a_clean_exit_that_published_nothing_makes()
    {
        // No verdict: the ordinary park, in its own words.
        var asking = IntakeObservation.Conclude(0, before: 0, Ask());
        Assert.Equal("awaiting-person", asking.State);
        Assert.Equal(["intake.asks"], NoteAssert.Codes(asking.Parts));

        // A failed exit, a refused turn and quests the ask gained end as they end, whatever the last message said.
        Assert.Equal(["intake.exit"], NoteAssert.Codes(IntakeObservation.Conclude(4, 0, Ask(), unowned: Why).Parts));
        Assert.Equal("failed", IntakeObservation.Conclude(0, 0, Ask(), "refused", Why).State);
        var published = IntakeObservation.Conclude(0, 0, Ask("Published", ["q1"], IntakeObservation.ByIntake), unowned: Why);
        Assert.Equal("completed", published.State);
        Assert.Equal(["intake.published"], NoteAssert.Codes(published.Parts));
        Assert.Equal("stood-down", IntakeObservation.Conclude(0, 0, null, unowned: Why).State);
    }

    // ——— What the intake is told.

    [Fact]
    public void The_intake_is_told_when_no_department_owns_the_ask_and_that_it_never_creates_one()
    {
        var flat = System.Text.RegularExpressions.Regex.Replace(IntakePrompt.Compose(Ask()), @"\s+", " ");

        Assert.Contains($"`{IntakeUnowned.Marker} <the work, and why nothing here owns it, in one sentence>`", flat);
        // When: nothing declares it and no repository's own files fit it, which is not a choice between owners.
        Assert.Contains("no declaration covers the work and no repository's own files fit it", flat);
        Assert.Contains("different from not being settled between owners", flat);
        // What it does: publishes nothing, and ends on that line alone.
        Assert.Contains("Publish nothing, and end your last message with one line", flat);
        // What it leaves to Ask Daoris: the intake never creates, and never routes to the nearest repository for want of an owner.
        Assert.Contains("which can open a department or a room for it", flat);
        Assert.Contains("you never create one", flat);
        Assert.Contains("never send it to the nearest repository", flat);
        // What it says of an unsettled ask stands unchanged.
        Assert.Contains(
            "When they do not settle it — nothing points at one repository, or more than one could — do not guess, and publish nothing. "
            + "End by saying plainly what you would need to know; the person decides.",
            flat);
    }
}
