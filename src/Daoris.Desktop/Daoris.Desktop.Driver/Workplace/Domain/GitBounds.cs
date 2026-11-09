namespace Daoris.Driver;

/// <summary>How long one git call may take, by its row in <see cref="GitCommands"/>: a backstop set well past what a large repository takes.</summary>
public static class GitBounds
{
    /// <summary>A question or a ref's move: a large repository's status or log, on a loaded machine, with room to spare.</summary>
    public static readonly TimeSpan Question = TimeSpan.FromMinutes(2);

    /// <summary>Work on a tree, a commit's hooks, a clone or the network: twice <see cref="SyncBounds.Replay"/>'s bound.</summary>
    public static readonly TimeSpan Work = TimeSpan.FromMinutes(10);

    /// <summary>The bound a call with <paramref name="arguments"/> runs under: the caller's own where it passed one.</summary>
    public static TimeSpan Of(IReadOnlyList<string> arguments, TimeSpan? own = null) => own ?? (GitCommands.IsQuestion(arguments) ? Question : Work);
}
