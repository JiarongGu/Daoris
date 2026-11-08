using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the cut of a session's own wait answers: the stop reason of a turn that completes as the cut wins is not dropped.</summary>
public sealed class AcpWaitsCutTests
{
    [Fact]
    public async Task A_go_on_s_turn_that_completes_as_the_cut_wins_gives_its_stop_reason()
    {
        using var cut = new CancellationTokenSource();
        var turn = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = cut.Token.Register(() => turn.TrySetResult("end_turn"));

        var stop = await AcpWaits.CutAsync(turn.Task, cut, CancellationToken.None);

        Assert.Equal("end_turn", stop);
    }

    [Fact]
    public async Task A_wait_the_cut_cancels_gives_no_stop_and_another_failure_is_the_run_s()
    {
        using var cut = new CancellationTokenSource();
        var cancelled = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = cut.Token.Register(() => cancelled.TrySetCanceled(cut.Token));
        Assert.Null(await AcpWaits.CutAsync(cancelled.Task, cut, CancellationToken.None));

        using var other = new CancellationTokenSource();
        var failed = Task.FromException<string?>(new InvalidOperationException("the wait failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AcpWaits.CutAsync(failed, other, CancellationToken.None));
    }
}
