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

    /// <summary>SQUASHSEEN1: a squash that carried it with other work says so, since its tree is not the squash's.</summary>
    [Fact]
    public void A_squash_that_carried_it_with_other_work_says_merging_it_adds_nothing() =>
        Assert.Equal(
            "its work is on `main` by content (a squash carried it: merging it there adds nothing)",
            new ContentHold("main", Squash: false) { Carried = true }.Said);

    /// <summary>REVIEWLANDED1: a landing that replayed its commits onto the line says so, since none of its own commits is there.</summary>
    [Fact]
    public void A_landings_replay_says_each_commit_is_there() =>
        Assert.Equal(
            "its work is on `main` by content (a landing replayed each of its commits there)",
            new ContentHold("main", Squash: false) { Replayed = true, OnLine = true }.Said);

    /// <summary>
    /// REVIEWLANDED1: a file that reads otherwise on the line is held only where the line's newest change to it holds one of the branch's
    /// patches (git marks it <c>=</c>); a change after it, or none at all, is the line as it stands, which does not hold it (D148).
    /// </summary>
    [Theory]
    [InlineData("\u001e=aaa\nf.txt\n\u001e+bbb\nf.txt\ng.txt\n", "f.txt", true)]
    [InlineData("\u001e+ccc\nf.txt\n\u001e=aaa\nf.txt\n", "f.txt", false)]
    [InlineData("\u001e=aaa\nf.txt\n", "g.txt", false)]
    [InlineData("\u001e=aaa\nf.txt\n\u001e+bbb\ng.txt\n", "f.txt|g.txt", false)]
    [InlineData("\u001e=aaa\nf.txt\n\u001e=bbb\ng.txt\n", "f.txt|g.txt", true)]
    public void A_replayed_file_is_held_only_where_the_lines_newest_change_is_the_replay(string log, string differs, bool held) =>
        Assert.Equal(held, ReplayHolds.LastChangedByPatch(log, differs.Split('|')));

    /// <summary>
    /// REVIEWLANDED1: a tree whose commits are on the line by content has landed, so it leaves the review where its discard would go; one
    /// held on a person's branch, one that stays for what no commit holds, and one with uncommitted paths are still the person's.
    /// </summary>
    [Fact]
    public void A_tree_landed_on_the_line_holds_nothing_to_review()
    {
        var line = new ContentHold("main", Squash: false) { Replayed = true, OnLine = true };
        const string keeps = "refs/daoris/discarded/daoris/s-1a2b3c4d";

        Assert.False(new TreeWork(2, 0) { Held = new HeldWork(line, keeps, null) }.Holds);
        Assert.True(new TreeWork(2, 1) { Held = new HeldWork(line, keeps, null) }.Holds);
        Assert.True(new TreeWork(2, 0) { Held = new HeldWork(line, null, "it holds 1 ignored path(s)") }.Holds);
        Assert.True(new TreeWork(2, 0) { Held = new HeldWork(new ContentHold("feature/x", Squash: false), keeps, null) }.Holds);
        Assert.True(new TreeWork(2, 0).Holds);
    }

    /// <summary>
    /// SQUASHSEEN1: the backlog read by rows. Two sessions moved out neighbouring rows and the squash moved both; a row the branch moved
    /// that the line still has, or one it added that the line lacks, is not held; the line's own other rows are its own.
    /// </summary>
    [Theory]
    [InlineData("- A\n- B\n- C\n", "- C\n", "- B\n- C\n", true)]
    [InlineData("- A\n- B\n- C\n", "- C\n- D\n", "- B\n- C\n- E\n\n", false)]
    [InlineData("- A\n- B\n- C\n", "- C\n- D\n- E\n", "- B\n- C\n- E\n", true)]
    [InlineData("- A\n- B\n- C\n", "- A\n- C\n", "- B\n- C\n", false)]
    [InlineData("- A\n- B\n", "- A\n- B\n\n", "- A\n\n- B\n", true)]
    [InlineData("- A\n", "- A\n- A\n", "- A\n", true)]
    public void The_backlog_holds_a_branch_where_every_row_it_moved_reads_so_on_the_line(string @base, string line, string branch, bool held) =>
        Assert.Equal(held, SquashCarries.RowsHeld(@base, line, branch));

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
