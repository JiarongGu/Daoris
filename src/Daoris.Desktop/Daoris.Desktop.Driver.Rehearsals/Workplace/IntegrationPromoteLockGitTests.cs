using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git, the promotion and the merge tool's one lock on the line: a landing that comes while a run holds the checkout moves nothing
/// and says whose run it is; it takes the same lock for its move, in the tool's shape, and lets it go after; a lock a killed run left is taken back.
/// </summary>
public sealed partial class IntegrationPromoteGitTests
{
    private static string MergeToolLock(string root) => MergeRunLock.PathOf(root);

    private static long StartOfThisProcess() => new DateTimeOffset(TreeLock.SelfStarted(), TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static void HeldByTheTool(string root, int pid, long started, string verb = "merge") =>
        File.WriteAllText(MergeToolLock(root), $$"""{"pid":{{pid}},"started":{{started}},"at":"2026-11-21T10:00:00Z","verb":"{{verb}}"}""");

    /// <summary>
    /// 🔴 A landing that comes while the merge tool's run holds the checkout (an open merge) moves nothing, says whose run holds it, and leaves
    /// the checkout's files and the line as they stood; tried again once the run has ended, it promotes, holds the same lock for the move (said
    /// as a landing) and lets it go after. The incident: a landing fast-forwarded the line under a merge open in the checkout.
    /// </summary>
    [Fact]
    public async Task A_landing_during_the_merge_tools_run_waits_until_the_run_ends_and_holds_the_same_lock_for_its_move()
    {
        var candidate = await FormedAsync(
            [Member("feature/a", ("a.txt", "a\n"))], detached: false, beforeFormed: root => CommitAsync(root, ".gitignore", "local/\n", "local is ignored"));
        Green();
        Directory.CreateDirectory(Path.Combine(candidate.Root, "local"));
        HeldByTheTool(candidate.Root, Environment.ProcessId, StartOfThisProcess());
        var promotes = Promotes(candidate);
        var seen = "";
        promotes.Seam = point =>
        {
            if (point == "before-move") seen = File.ReadAllText(MergeToolLock(candidate.Root));
            return Task.CompletedTask;
        };

        var held = await promotes.RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Held, held.Kind);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        Assert.False(File.Exists(Path.Combine(candidate.Root, "a.txt")));
        Assert.Equal(IntegrationStepOutcomes.Held, Last(candidate, IntegrationStepNames.Promote).Outcome);
        var said = Last(candidate, IntegrationStepNames.Promote).Said!;
        Assert.Contains("merge tool", said, StringComparison.Ordinal);
        Assert.Contains($"pid {Environment.ProcessId}", said, StringComparison.Ordinal);
        Assert.Contains("nothing was moved", said, StringComparison.Ordinal);
        Assert.Equal("", seen);
        Assert.Contains("\"verb\":\"merge\"", File.ReadAllText(MergeToolLock(candidate.Root)), StringComparison.Ordinal);

        File.Delete(MergeToolLock(candidate.Root));
        var run = await promotes.RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.Contains($"\"pid\":{Environment.ProcessId}", seen, StringComparison.Ordinal);
        Assert.Contains("\"verb\":\"landing\"", seen, StringComparison.Ordinal);
        Assert.False(File.Exists(MergeToolLock(candidate.Root)), "the landing let the line's lock go when its move ended");
    }

    /// <summary>A lock a killed run left (its pid now another process, begun elsewhen) is taken back by its process check: the landing moves the line and lets the lock go.</summary>
    [Fact]
    public async Task A_lock_a_killed_run_left_is_taken_back_by_its_process_check()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))]);
        Green();
        Directory.CreateDirectory(Path.Combine(candidate.Root, "local"));
        HeldByTheTool(candidate.Root, Environment.ProcessId, StartOfThisProcess() - (60 * 60 * 1000));

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.False(File.Exists(MergeToolLock(candidate.Root)));
    }
}
