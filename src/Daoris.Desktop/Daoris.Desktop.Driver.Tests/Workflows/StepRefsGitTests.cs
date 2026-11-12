using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git: a step's commit kept on its attempt's ref (<see cref="StepRefs"/>) is found by a later reader after a restart and after
/// <c>git gc --prune=now</c>, each attempt's on its own, and the run's refs go at its settled end alone.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class StepRefsGitTests : IDisposable
{
    private const string Run = "q7";

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "step-refs", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public StepRefsGitTests()
    {
        _root = Path.Combine(_home, "engine");
        GitSeed.PlainAsync(_root, "engine").GetAwaiter().GetResult();
    }

    public void Dispose() => GitSeed.Remove(_home);

    /// <summary>A commit no branch holds: what a replay makes in its own tree, which git's collection would take but for its ref.</summary>
    private async Task<string> LooseCommitAsync(string message)
    {
        var tree = (await GitAsync(_root, "write-tree")).Trim();
        return (await GitAsync(_root, "commit-tree", tree, "-p", "HEAD", "-m", message)).Trim();
    }

    private async Task<bool> HeldAsync(string commit) => (await RunAsync(_root, "cat-file", "-e", commit + "^{commit}")).ExitCode == 0;

    private static NodeRecord Record(bool ended, string attempt) => new NodeRecord(RunUnits.IdOf(Run))
    {
        Ended = ended ? new RunEnd(Run, DateTimeOffset.UnixEpoch) : null,
        Attempts =
        [
            new NodeAttempt("rebase", 2, "daoris", new WorkPerformCall(RunUnits.IdOf(Run), "rebase", 1, "i-1", "k-1", "tip-1", [], DateTimeOffset.UnixEpoch), attempt),
        ],
    };

    [Fact]
    public async Task A_step_ref_an_attempt_reads_survives_a_restart_and_gc_and_goes_at_the_runs_settled_end()
    {
        var first = await LooseCommitAsync("rebased once");
        var second = await LooseCommitAsync("rebased again");
        Assert.True((await StepRefs.KeepAsync(_root, Run, "rebase", 1, first, default)).Done);
        Assert.True((await StepRefs.KeepAsync(_root, Run, "rebase", 2, second, default)).Done);
        Assert.Equal("refs/daoris/steps/q7/rebase/2", StepRefs.Of(Run, "rebase", 2));

        await GitAsync(_root, "reflog", "expire", "--expire=now", "--all");
        await GitAsync(_root, "gc", "--prune=now", "--quiet");
        Assert.True(await HeldAsync(first));
        Assert.True(await HeldAsync(second));
        Assert.Equal(
            [(StepRefs.Of(Run, "rebase", 1), first), (StepRefs.Of(Run, "rebase", 2), second)],
            await StepRefs.OfRunAsync(_root, Run, default));

        Assert.False(StepRefs.Releases(Record(ended: false, AttemptStates.Settled)));
        Assert.False(StepRefs.Releases(Record(ended: true, AttemptStates.Sent)));
        Assert.True(StepRefs.Releases(Record(ended: true, AttemptStates.Settled)));
        Assert.True((await StepRefs.ReleaseAsync(_root, Run, default)).Done);
        Assert.Empty(await StepRefs.OfRunAsync(_root, Run, default));
        Assert.True((await StepRefs.ReleaseAsync(_root, Run, default)).Done);
    }
}
