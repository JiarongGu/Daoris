using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A flow's memo asks git under the flow's own token, so a stage that joined a read is answered though its starter left.</summary>
public sealed class GitMemoAsksTests
{
    [Fact]
    public async Task A_used_read_that_joined_a_memo_survives_its_speculative_starters_cancellation()
    {
        var release = new TaskCompletionSource();
        var handed = new List<CancellationToken>();
        GitRuns.Runs.Value = async (_, _, token) =>
        {
            lock (handed) handed.Add(token);
            await release.Task.WaitAsync(token);
            return new ChildRun(true, 0, "abc\n", "", false);
        };
        try
        {
            using (GitReadOnce.Within())
            {
                using var speculative = new CancellationTokenSource();
                var root = Path.GetFullPath("_memo");
                var started = WorkingTree.GitAsync(root, ["merge-base", "one", "two"], speculative.Token);
                var used = WorkingTree.GitAsync(root, ["merge-base", "one", "two"], CancellationToken.None);
                speculative.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => started);
                release.SetResult();
                Assert.Equal((0, "abc\n", ""), await used.WaitAsync(TimeSpan.FromSeconds(10)));
                lock (handed) Assert.False(Assert.Single(handed).CanBeCanceled);
            }
        }
        finally
        {
            GitRuns.Runs.Value = null;
        }
    }
}
