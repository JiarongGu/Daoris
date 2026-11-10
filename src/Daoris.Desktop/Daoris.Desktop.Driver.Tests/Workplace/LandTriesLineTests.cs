using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Two automatic landings that need nothing of the person: a line that already holds everything on the branch, and a merge that waits in its
/// line's integration and is tried again at the next look. Each has a code of its own, is never worded as waiting for a review, and the
/// first closes its entry while the second is read again at every look.
/// </summary>
public sealed class LandTriesLineTests
{
    private const string Tip = "6f1c0de";

    private static LandEntry Tried(string code) =>
        new("s1", "q1", "web-app", "work", "tree", DateTimeOffset.UnixEpoch)
        {
            Tries = [new AutoTry(DateTimeOffset.UnixEpoch, code) { Tip = Tip, Status = "clean" }],
        };

    [Fact]
    public void A_line_that_holds_everything_closes_its_entry_and_a_queued_merge_is_read_again_at_every_look()
    {
        Assert.True(LandingCode.Closes(LandingCode.LineHolds));
        Assert.False(LandTries.ShouldTry(Tried(LandingCode.LineHolds), Tip, "clean"));
        Assert.False(LandingCode.Closes(LandingCode.Queued));
        Assert.True(LandTries.ShouldTry(Tried(LandingCode.Queued), Tip, "clean"));
        Assert.Equal(LandingCode.LineHolds, LandTries.CodeOf(new TreeLanding(false, "nothing to merge") { Refusal = LandingCode.LineHolds }));
    }

    [Theory]
    [InlineData(LandingCode.LineHolds, "landing.line-holds")]
    [InlineData(LandingCode.Queued, "landing.queued")]
    public void Neither_is_worded_as_waiting_for_your_review(string code, string note)
    {
        var said = LandTryNotes.Of(code, new TreeLanding(false, "as git says it.") { Refusal = code });

        Assert.True(LandTryNotes.Says(code));
        Assert.Equal([note, null], NoteAssert.Codes(said.Parts));
        Assert.DoesNotContain("waits for your review", said.Text);
        Assert.Contains(code == LandingCode.Queued ? ChatNoteCodes.LandingQueued : ChatNoteCodes.LandingLineHolds, NoteCodes.All);
    }
}
