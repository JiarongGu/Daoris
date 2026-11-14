using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over a repository kept in memory: a step's commit kept on its attempt's ref (<see cref="StepRefs"/>) is found by a later reader,
/// each attempt's on its own, and the run's refs go at its settled end alone. That git keeps what a ref holds through a collection is
/// git's own, which the repository contract's real half proves for refs.
/// </summary>
public sealed class StepRefsMemoryTests : IDisposable
{
    private const string Run = "q7";

    private static readonly string Root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "step-refs-memory", "engine");

    private readonly MemoryRepository _repository = new(Root);
    private readonly IDisposable _chosen;

    public StepRefsMemoryTests()
    {
        _repository.Write(Root, "README.md", "# engine\n");
        Commit("first");
        _chosen = FlowRepository.Use(_repository);
    }

    public void Dispose() => _chosen.Dispose();

    private string Commit(string message)
    {
        _repository.Write(Root, "work.txt", message);
        Assert.True(_repository.CommitAllAsync(Root, message, default).GetAwaiter().GetResult().Ok);
        return _repository.HeadAsync(Root, default).GetAwaiter().GetResult()!;
    }

    private static NodeRecord Record(bool ended, string attempt) => new NodeRecord(RunUnits.IdOf(Run))
    {
        Ended = ended ? new RunEnd(Run, DateTimeOffset.UnixEpoch) : null,
        Attempts =
        [
            new NodeAttempt("rebase", 2, "daoris", new WorkPerformCall(RunUnits.IdOf(Run), "rebase", 1, "i-1", "k-1", "tip-1", [], DateTimeOffset.UnixEpoch), attempt),
        ],
    };

    [Fact]
    public async Task A_step_ref_an_attempt_reads_is_found_by_a_later_reader_and_goes_at_the_runs_settled_end()
    {
        var first = Commit("rebased once");
        var second = Commit("rebased again");
        Assert.True((await StepRefs.KeepAsync(Root, Run, "rebase", 1, first, default)).Done);
        Assert.True((await StepRefs.KeepAsync(Root, Run, "rebase", 2, second, default)).Done);
        Assert.Equal("refs/daoris/steps/q7/rebase/2", StepRefs.Of(Run, "rebase", 2));

        Assert.Equal(
            [(StepRefs.Of(Run, "rebase", 1), first), (StepRefs.Of(Run, "rebase", 2), second)],
            await StepRefs.OfRunAsync(Root, Run, default));

        Assert.False(StepRefs.Releases(Record(ended: false, AttemptStates.Settled)));
        Assert.False(StepRefs.Releases(Record(ended: true, AttemptStates.Sent)));
        Assert.True(StepRefs.Releases(Record(ended: true, AttemptStates.Settled)));
        Assert.True((await StepRefs.ReleaseAsync(Root, Run, default)).Done);
        Assert.Empty(await StepRefs.OfRunAsync(Root, Run, default));
        Assert.True((await StepRefs.ReleaseAsync(Root, Run, default)).Done);
    }
}
