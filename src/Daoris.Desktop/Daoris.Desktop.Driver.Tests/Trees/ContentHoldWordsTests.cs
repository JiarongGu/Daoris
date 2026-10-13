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

    /// <summary>SQUASHTIDY1c: where a content-held removal kept the commits it deleted, and how to have them back or let them go.</summary>
    [Fact]
    public void Where_the_commits_stay_names_the_ref_and_how_to_bring_the_branch_back() =>
        Assert.Equal(
            "Its commits stay at `refs/daoris/discarded/daoris/s-1a2b3c4d` until you delete that ref; "
            + "`git branch daoris/s-1a2b3c4d refs/daoris/discarded/daoris/s-1a2b3c4d` brings the branch back.",
            ContentHold.KeptAt("daoris/s-1a2b3c4d", "refs/daoris/discarded/daoris/s-1a2b3c4d"));

    /// <summary>
    /// SQUASHTIDY1b: where <i>Accept…</i> would be, the session's page says where its work is in Discard's own clause, as a
    /// sentence, and its discard's ask names the ref the commits stay at, in the words the discard says once it went.
    /// </summary>
    [Fact]
    public void A_held_trees_offer_says_where_its_work_is_and_its_ask_names_the_ref()
    {
        var offer = new DiscardOffer(
            "daoris/s-1a2b3c4d", "s-1a2b3c4d",
            new HeldWork(new ContentHold("main", Squash: true), "refs/daoris/discarded/daoris/s-1a2b3c4d", null));

        Assert.Equal("Its work is on `main` by content (a squash merge).", offer.Says);
        Assert.Equal(ContentHold.KeptAt("daoris/s-1a2b3c4d", "refs/daoris/discarded/daoris/s-1a2b3c4d"), offer.KeptAt);
    }

    /// <summary>SQUASHTIDY1b: a held tree whose discard would not go unforced says why it stays, and its ask has nothing to name.</summary>
    [Fact]
    public void A_held_tree_that_stays_says_why_and_names_no_ref()
    {
        var offer = new DiscardOffer(
            "daoris/s-1a2b3c4d", "s-1a2b3c4d",
            new HeldWork(new ContentHold("feature/x", Squash: false), null, "it has 2 uncommitted path(s), which a discard would destroy"));

        Assert.Equal(
            "Its work is on `feature/x` by content (each file it changed reads the same there). Its tree stays: it has 2 "
            + "uncommitted path(s), which a discard would destroy.",
            offer.Says);
        Assert.Null(offer.KeptAt);
    }

    /// <summary>
    /// SQUASHTIDY1f: a landing of a held tree, at any door, refuses in the head's own clause, and says why landing it again is
    /// refused and what to do instead. One sentence for the press's refusal and for the plan before it, so it names no tense.
    /// </summary>
    [Fact]
    public void A_held_trees_landing_says_where_its_work_is_and_offers_its_discard()
    {
        var offer = new DiscardOffer(
            "daoris/s-1a2b3c4d", "s-1a2b3c4d",
            new HeldWork(new ContentHold("main", Squash: true), "refs/daoris/discarded/daoris/s-1a2b3c4d", null));

        Assert.Equal(
            "Its work is on `main` by content (a squash merge), so it is not landed again: that would make a second copy of "
            + "it. Discard its tree instead.",
            offer.NotLanded);
    }

    /// <summary>SQUASHTIDY1f: where the discard would not go unforced, the refusal says why the tree stays, as the head does.</summary>
    [Fact]
    public void A_held_tree_that_stays_says_why_its_landing_is_refused_and_why_it_stays()
    {
        var offer = new DiscardOffer(
            "daoris/s-1a2b3c4d", "s-1a2b3c4d",
            new HeldWork(new ContentHold("feature/x", Squash: false), null, "it has 2 uncommitted path(s), which a discard would destroy"));

        Assert.Equal(
            "Its work is on `feature/x` by content (each file it changed reads the same there), so it is not landed again: that "
            + "would make a second copy of it. Its tree stays: it has 2 uncommitted path(s), which a discard would destroy.",
            offer.NotLanded);
    }
}
