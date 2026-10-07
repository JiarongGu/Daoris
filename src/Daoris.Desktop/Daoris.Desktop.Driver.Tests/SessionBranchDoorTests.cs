using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAND3's terminal door, without git: a failed or superseded attempt's branch is offered for removal beside the clean-up's
/// row, `trees remove` tells a path from a session or its branch, and the record finds a branch by either name. The git
/// half is <c>LandingTidyTests</c>.
/// </summary>
public sealed class SessionBranchDoorTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-branch-door-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (DirectoryNotFoundException) { /* nothing was written */ }
    }

    /// <summary>Only a kept branch holding commits no branch of the person's holds is offered, and the offer is the door's words.</summary>
    [Theory]
    [InlineData(SweepKind.Unlanded, 2, true)]
    [InlineData(SweepKind.Unlanded, 0, false)]
    [InlineData(SweepKind.Landed, 2, false)]
    [InlineData(SweepKind.Empty, 0, false)]
    [InlineData(SweepKind.Dirty, 0, false)]
    [InlineData(SweepKind.InUse, 0, false)]
    public void A_failed_or_superseded_attempts_branch_is_offered_for_removal(string kind, int commits, bool offered)
    {
        var item = new SweepItem("engine", "aurora", "daoris/s-1a2b3c4d", null, kind, commits, null, null);

        var offer = SessionTrees.RemovalOffered(item);

        if (!offered) Assert.Null(offer);
        else Assert.Contains("`daoris-driver trees remove daoris/s-1a2b3c4d --repository engine --force`", offer);
    }

    /// <summary>
    /// LAND4: beside a kept branch whose tree is still here and holds commits no branch of the person's holds, the session that
    /// tree is, the newest of this machine's naming it, and the door that accepts its work, whatever its ending. Never for a
    /// branch whose tree is gone (its discard is the door), one with no commits, one no record of this machine's names, or
    /// one a session still holds.
    /// </summary>
    [Theory]
    [InlineData(SweepKind.Unlanded, 2, true, "failed", true)]
    [InlineData(SweepKind.Unlanded, 2, true, "completed", true)]
    [InlineData(SweepKind.Unlanded, 2, true, "stopped", true)]
    [InlineData(SweepKind.Unlanded, 2, false, "failed", false)]
    [InlineData(SweepKind.Unlanded, 0, true, "failed", false)]
    [InlineData(SweepKind.Landed, 2, true, "completed", false)]
    [InlineData(SweepKind.Dirty, 0, true, "completed", false)]
    [InlineData(SweepKind.InUse, 0, true, "awaiting-person", false)]
    [InlineData(SweepKind.Unlanded, 2, true, null, false)]
    public void A_kept_branch_whose_tree_holds_commits_offers_its_sessions_landing(
        string kind, int commits, bool tree, string? state, bool offered)
    {
        const string here = "X:/daoris/trees/aurora/engine/s-4e6837ed";
        var item = new SweepItem("engine", "aurora", "daoris/s-4e6837ed", tree ? here : null, kind, commits, null, null);
        SessionRecord[] records = state is null
            ? []
            : [
                new SessionRecord("0lder000", "engine", "failed") { Tree = here.Replace('/', '\\'), Created = DateTimeOffset.UnixEpoch },
                new SessionRecord("4e6837ed", "engine", state) { Tree = here.ToUpperInvariant(), Created = DateTimeOffset.UnixEpoch.AddHours(1) },
                new SessionRecord("laptop/s9", "engine", "failed") { Tree = here, Created = DateTimeOffset.UnixEpoch.AddHours(2) },
            ];

        var session = SessionTrees.SessionOfTree(records, item.Tree);
        var offer = SessionTrees.LandingOffered(item, session);

        Assert.Equal(tree && state is not null ? "4e6837ed" : null, session?.Id);
        if (!offered) Assert.Null(offer);
        else Assert.Equal($"if its work is wanted: `daoris-driver trees land 4e6837ed` accepts it (session 4e6837ed, {state}, its tree here)", offer);
    }

    [Theory]
    [InlineData("daoris/s-1a2b3c4d", false)]
    [InlineData("s-1a2b3c4d", false)]
    [InlineData("fd12f1bc", false)]
    [InlineData("trees/aurora/engine/s-1a2b3c4d", true)]
    [InlineData("C:\\daoris\\trees\\aurora\\engine\\s-1a2b3c4d", true)]
    [InlineData("/srv/daoris/trees/aurora/engine/s-1a2b3c4d", true)]
    public void A_removal_tells_a_path_from_a_session_or_its_branch(string named, bool path) =>
        Assert.Equal(path, SessionTrees.NamesAPath(named));

    /// <summary>The record finds a branch by its whole name or its tree's, narrowed to a repository where one is said.</summary>
    [Fact]
    public void The_record_finds_a_session_branch_by_either_name()
    {
        var trees = new SessionTrees(_home);
        trees.Grown.Record(new GrownBranch("engine", "aurora", "daoris/s-1a2b3c4d", "main", "abc", null, DateTimeOffset.UtcNow));
        trees.Grown.Record(new GrownBranch("game", "aurora", "daoris/s-1a2b3c4d", "main", "def", null, DateTimeOffset.UtcNow));

        Assert.Equal(2, trees.FindBranches("s-1a2b3c4d", null).Count);
        Assert.Equal("game", Assert.Single(trees.FindBranches("daoris/s-1a2b3c4d", "GAME")).Repository);
        Assert.Empty(trees.FindBranches("s-ffffffff", null));
    }

    /// <summary>A session's tree, gone or not, names its branch by the layout Daoris chose; a path outside the home names none.</summary>
    [Fact]
    public void A_sessions_tree_names_its_branch_by_the_layout()
    {
        var trees = new SessionTrees(_home);

        Assert.Equal(("aurora", "engine", "daoris/s-1a2b3c4d"),
            trees.BranchOfTree(Path.Combine(trees.TreesRoot, "aurora", "engine", "s-1a2b3c4d")));
        Assert.Null(trees.BranchOfTree(Path.Combine(Path.GetTempPath(), "elsewhere", "s-1a2b3c4d")));
    }
}
