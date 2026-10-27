using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDSYNC3 (D87's 2026-10-15 amendment point 1): <i>Bring up to date</i> judges a branch's commits as a landing does, so it counts
/// only what the line lacks. WSR6's plan said <i>replays 3 commit(s)</i> of a branch whose first was squash-merged. Pure: the one log
/// the look reads, what it leaves out, where the walk by content is worth asking, and the stretches a replay skips between.
/// </summary>
public sealed class LineHeldBeyondTests
{
    private static BeyondCommit Held(string sha, params string[] files) => new(sha, true, files);

    private static BeyondCommit Own(string sha, params string[] files) => new(sha, false, files);

    /// <summary>One log reads every commit beyond the cut, oldest first: whether the line has its patch (<c>=</c>), and its files.</summary>
    [Fact]
    public void The_log_reads_each_commit_with_whether_the_line_holds_it_and_its_files()
    {
        // As git writes it with `--cherry-mark --right-only` (read off a scratch repository): `=` the line has the patch, `>` it lacks it.
        const string log = "\u001e=aaa\n\nCHANGELOG.md\n\u001e>bbb\n\nmore.txt\nsrc/notes.cs\n\u001e>ccc\n\n";

        var read = LineHolds.ReadBeyond(log);

        Assert.Equal(["aaa", "bbb", "ccc"], read.Select(each => each.Sha));
        Assert.Equal([true, false, false], read.Select(each => each.Held));
        Assert.Equal(["CHANGELOG.md"], read[0].Files);
        Assert.Equal(["more.txt", "src/notes.cs"], read[1].Files);
        Assert.Empty(read[2].Files);
        Assert.Empty(LineHolds.ReadBeyond(""));
    }

    /// <summary>
    /// The install's case: the first commit squash-merged under a new id, two more on top. The cut moves past it, so the replay is a
    /// plain one from there, and two are counted, not three.
    /// </summary>
    [Fact]
    public void Commits_the_line_holds_at_the_start_move_the_cut_past_them()
    {
        var held = LineHolds.Beyond("leaves", [Held("aaa"), Own("bbb"), Own("ccc")]);

        Assert.Equal("aaa", held.Cut);
        Assert.Equal(["bbb", "ccc"], held.Commits);
        Assert.Empty(held.Skips);
        Assert.Equal((2, 1), (held.Kept, held.LeftOut));
    }

    /// <summary>A branch whose every commit the line holds holds nothing of its own: it moves onto the line, nothing replayed.</summary>
    [Fact]
    public void A_branch_the_line_holds_entirely_holds_nothing_of_its_own()
    {
        var held = LineHolds.Beyond("leaves", [Held("aaa"), Held("bbb")]);

        Assert.Equal("bbb", held.Cut);
        Assert.Empty(held.Commits);
        Assert.Equal((0, 2), (held.Kept, held.LeftOut));
    }

    /// <summary>A commit the line holds between two it lacks (a cherry-pick of the middle one) is skipped by the replay, not counted.</summary>
    [Fact]
    public void A_commit_the_line_holds_between_kept_ones_is_skipped()
    {
        var held = LineHolds.Beyond("leaves", [Own("aaa"), Held("bbb"), Own("ccc")]);

        Assert.Equal("leaves", held.Cut);
        Assert.Equal(["aaa", "bbb", "ccc"], held.Commits);
        Assert.Equal(["bbb"], held.Skips);
        Assert.Equal((2, 1), (held.Kept, held.LeftOut));
    }

    /// <summary>Where the line holds none, the cut is where it was and every commit is its own, as WSR6 counted it.</summary>
    [Fact]
    public void Nothing_held_leaves_the_cut_and_counts_every_commit()
    {
        var held = LineHolds.Beyond("leaves", [Own("aaa"), Own("bbb")]);

        Assert.Equal(("leaves", 2, 0), (held.Cut, held.Kept, held.LeftOut));
        Assert.Empty(held.Skips);
        var none = LineHolds.Beyond("leaves", []);
        Assert.Equal(("leaves", 0, 0), (none.Cut, none.Kept, none.LeftOut));
    }

    /// <summary>
    /// The walk by content proves commit after commit, so the look asks it only where it can find something: the line changed every
    /// file the first commit it does not hold by patch touched. A squash of several does; a line that only moved a changelog beside
    /// the work does not.
    /// </summary>
    [Fact]
    public void The_walk_by_content_is_asked_only_where_the_line_changed_every_file_the_first_kept_commit_touched()
    {
        string[] squashed = ["work.txt", "shared.txt", "later.txt"];
        string[] changelog = ["CHANGELOG.md"];

        Assert.True(LineHolds.MayHoldByContent([Own("aaa", "work.txt"), Own("bbb", "shared.txt"), Own("ccc", "notes.txt")], squashed));
        Assert.True(LineHolds.MayHoldByContent([Held("aaa", "CHANGELOG.md"), Own("bbb"), Own("ccc", "work.txt")], squashed));
        Assert.False(LineHolds.MayHoldByContent([Own("aaa", "CHANGELOG.md", "src/notes.cs")], changelog));
        Assert.False(LineHolds.MayHoldByContent([Held("aaa", "CHANGELOG.md")], changelog));
        Assert.False(LineHolds.MayHoldByContent([Own("aaa")], squashed));
        Assert.False(LineHolds.MayHoldByContent([], squashed));
    }

    /// <summary>
    /// A replay that skips commits replays the runs between them, each from the commit before its first (LANDSYNC1's stretches): the
    /// same shape a landing's replay and <i>Bring up to date</i> take.
    /// </summary>
    [Fact]
    public void A_replay_skipping_commits_replays_each_run_between_them()
    {
        string[] commits = ["aaa", "bbb", "ccc", "ddd", "eee"];

        Assert.Equal([("aaa^", "aaa"), ("ccc^", "ddd")], BranchReplays.Stretches(commits, new HashSet<string> { "bbb", "eee" }));
        Assert.Equal([("aaa^", "eee")], BranchReplays.Stretches(commits, new HashSet<string>()));
        Assert.Empty(BranchReplays.Stretches(["aaa"], new HashSet<string> { "aaa" }));
    }
}
