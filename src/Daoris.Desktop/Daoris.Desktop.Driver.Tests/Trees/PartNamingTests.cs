using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREEFOLLOWNAME1 (D167's note on it): a part of an entry whose tree is on a branch of its own (TREEFOLLOW1) lands as that
/// branch, so its landing is named for its own quest and title, not the entry's first part's. The name is the part's alone:
/// the entry's branch and the session recorded stay as they are. The fast half holds the naming apart from git;
/// <c>EntryLandingTests</c> holds the doors over real git.
/// </summary>
public sealed class PartNamingTests
{
    private static readonly LandingSubject Entry = new("s1", "q1", "Fix the gap")
    {
        Name = "Gap", EntryBranch = "feature/q1-fix-the-gap", Body = "Fix it.", Links = ["https://example.test/1"],
    };

    /// <summary>The part's own quest, title, name, body and links name the landing; its session and the entry's branch are kept.</summary>
    [Fact]
    public void A_part_on_its_own_branch_is_named_for_its_own_quest_and_title()
    {
        var own = new LandingSubject("s2", "q2", "Check the gap") { Name = "Check", Body = "Check it.", Links = null };

        var named = (Entry with { Session = "s2" }).NamedFor(own);

        Assert.Equal(("s2", "q2", "Check the gap", "Check", "Check it."), (named.Session, named.Quest, named.Title, named.Name, named.Body));
        Assert.Null(named.Links);
        Assert.Equal(("feature/q1-fix-the-gap", true), (named.EntryBranch, named.Joins));
    }

    /// <summary>A part with no quest of its own (a conversation) is named for its opening words, as a session alone is.</summary>
    [Fact]
    public void A_part_with_no_quest_is_named_for_its_opening()
    {
        var own = new LandingSubject("s2", null, "Look at the gap");

        var named = Entry.NamedFor(own);

        Assert.Equal((null, "Look at the gap", null), (named.Quest, named.Title, named.Name));
    }
}
