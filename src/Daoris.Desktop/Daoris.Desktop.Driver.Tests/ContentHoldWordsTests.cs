using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SQUASHTIDY1: how a session branch's work was found held by content, in the one clause every door says it with: the review's
/// Discard and <c>trees remove</c> after "removed …", and the clean-up's row in the terminal. The page shows the driver's
/// sentence as it is, so no catalogue words it again.
/// </summary>
public sealed class ContentHoldWordsTests
{
    [Fact]
    public void A_squash_names_the_line_and_says_a_squash_merge_left_it() =>
        Assert.Equal("its work is on `main` by content (a squash merge)", new ContentHold("main", Squash: true).Said);

    [Fact]
    public void Files_held_name_the_branch_that_holds_them() =>
        Assert.Equal(
            "its work is on `feature/x` by content (each file it changed reads the same there)",
            new ContentHold("feature/x", Squash: false).Said);

    [Fact]
    public void A_line_on_origin_is_named_as_git_names_it() =>
        Assert.Equal("its work is on `origin/main` by content (a squash merge)", new ContentHold("origin/main", Squash: true).Said);
}
