namespace Daoris.Driver;

/// <summary>
/// How a flow's memo (<see cref="GitReadOnce"/>, <see cref="GitFacts"/>, <see cref="RefSnapshot"/>) asks git: under the flow's own
/// token, never its first asker's, so a stage that joined a read a speculative stage began is answered though that stage left; each
/// asker is answered under its own token.
/// </summary>
internal static class GitMemoAsks
{
    /// <summary>The memo's one ask of git, under <paramref name="flow"/>.</summary>
    internal static Func<Task<T>> Under<T>(Func<CancellationToken, Task<T>> ask, CancellationToken flow) => () => ask(flow);

    /// <summary>The held answer, as <paramref name="asker"/> waits for it: its leaving ends its own wait, never the read.</summary>
    internal static Task<T>? Joined<T>(Task<T>? held, CancellationToken asker) =>
        held is not null && asker.CanBeCanceled && !held.IsCompleted ? held.WaitAsync(asker) : held;
}
