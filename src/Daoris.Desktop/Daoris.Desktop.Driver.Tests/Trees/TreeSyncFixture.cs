using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the tree sync's classes share (WSR6): one repository's list, the chain a landed branch and its next step make, a
/// commit someone else pushed, and the reads they assert with. One class until PROC1b split it four ways, by the time each
/// takes: the git-only classes run four at once and a class is the unit they run by (the whole half's wall is its summed time
/// over four, PROCSPEED1b).
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here reaches a network</b>: `origin` is a bare repository under the scratch folder, and the
/// platform's squash merge and commits are made in it (<see cref="LandedFixture"/>).
/// </remarks>
public abstract class TreeSyncFixture : LandedFixture
{
    protected static readonly HashSet<string> Nobody = [];

    protected static (string, string?, string?)[] Repositories(string root) => [("engine", "aurora", root)];

    /// <summary>
    /// B: a session's work in two commits, landed as a branch; then S, the chain's next step, grown from B's tip with
    /// one commit of its own; then the parent's tree tidied, as a landing rule's tidy leaves it, its work on B.
    /// </summary>
    protected static async Task<(TreeOpened Parent, string B, TreeOpened Step)> ParentAndStepAsync(SessionTrees trees, string root)
    {
        var parent = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(parent.Path, "shared.txt", "one\ntwo\n", "b1");
        await CommitAsync(parent.Path, "b.txt", "b\n", "b2");
        var landed = await trees.LandAsync(parent.Path, new LandingSubject("s1", "q1", "First"));
        Assert.True(landed.Landed, landed.Message);

        var step = await trees.OpenAsync(root, "engine", "aurora", from: parent.Branch);
        Assert.Equal(parent.Branch, step.GrewFrom);
        await CommitAsync(step.Path, "s.txt", "s\n", "s1");
        var removed = await trees.RemoveAsync(parent.Path);
        Assert.True(removed.Removed, removed.Message);
        return (parent, landed.Branch!, step);
    }

    /// <summary>A commit someone else pushed to origin's line — the platform's side, made in the bare origin itself (PROCSPEED1b).</summary>
    protected static Task PlatformCommitAsync(string origin, string file, string content, string message) =>
        CommitOnOriginAsync(origin, message, (file, content));

    protected static async Task<string> RevAsync(string root, string revision) => (await GitAsync(root, "rev-parse", revision)).Trim();

    protected static async Task<string[]> SubjectsAsync(string root, string range) =>
        (await GitAsync(root, "log", "--format=%s", range)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
