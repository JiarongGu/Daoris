using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landing refused because the repository's own checkout holds work of its own waits on that checkout, not on the session's tree and not
/// on a review: it is tried again at the pass after the checkout's status differs from the one it refused at, and at no pass where nothing moved.
/// </summary>
public sealed class LandTriesRootTests
{
    private const string Tip = "6f1c0de";

    private static readonly string Dirty = LandTries.Fingerprint(" M docs/a.md\n?? scratch.txt\n");

    private static LandEntry Refused(string? status) =>
        new("s1", "q1", "web-app", "work", "tree", DateTimeOffset.UnixEpoch)
        {
            Tries = [new AutoTry(DateTimeOffset.UnixEpoch, LandingCode.RootDirty) { Tip = Tip, Status = status }],
        };

    [Fact]
    public void A_root_that_has_not_moved_is_not_tried_again()
    {
        Assert.False(LandTries.ShouldTry(Refused(Dirty), Tip, "clean", root: Dirty));
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("sha256:0123456789abcdef")]
    public void A_root_whose_status_differs_is_tried_again(string now)
    {
        Assert.True(LandTries.ShouldTry(Refused(Dirty), Tip, "clean", root: now));
    }

    [Fact]
    public void A_root_that_cannot_be_read_is_tried_again_and_so_is_one_refused_with_no_status_kept()
    {
        Assert.True(LandTries.ShouldTry(Refused(Dirty), Tip, "clean", root: null));
        Assert.True(LandTries.ShouldTry(Refused(null), Tip, "clean", root: Dirty));
    }

    /// <summary>The session's tree moving, with the root as it stood, tries nothing.</summary>
    [Fact]
    public void A_moved_session_tree_does_not_try_a_landing_that_waits_on_the_root()
    {
        Assert.False(LandTries.ShouldTry(Refused(Dirty), "another-tip", "sha256:aaaaaaaaaaaaaaaa", root: Dirty));
    }

    [Fact]
    public void Its_code_waits_on_the_root_and_never_closes_an_entry_or_waits_on_the_tree()
    {
        Assert.True(LandingCode.WaitsOnRoot(LandingCode.RootDirty));
        Assert.False(LandingCode.WaitsOnRoot(LandingCode.Refused));
        Assert.False(LandingCode.Closes(LandingCode.RootDirty));
        Assert.False(LandingCode.OnChange(LandingCode.RootDirty));
        Assert.True(LandTryNotes.Says(LandingCode.RootDirty));
        Assert.Equal(LandingCode.RootDirty, LandTries.CodeOf(new TreeLanding(false, "not clean") { Refusal = LandingCode.RootDirty }));
    }

    /// <summary>The note says the session waits for the checkout to be clean, under a code of its own, and asks no review of the person.</summary>
    [Fact]
    public void Its_note_says_it_waits_for_the_checkout_to_be_clean_and_not_for_a_review()
    {
        var note = LandTryNotes.Of(LandingCode.RootDirty, new TreeLanding(false, "the repository's own checkout is not clean.") { Refusal = LandingCode.RootDirty });

        Assert.Equal(["landing.root-dirty", null], NoteAssert.Codes(note.Parts));
        Assert.Contains("checkout is clean", note.Text);
        Assert.DoesNotContain("waits for your review", note.Text);
        Assert.Contains(ChatNoteCodes.LandingRootDirty, NoteCodes.All);
    }
}
