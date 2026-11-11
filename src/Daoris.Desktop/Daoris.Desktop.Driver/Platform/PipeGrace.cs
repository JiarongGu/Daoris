namespace Daoris.Driver;

/// <summary>
/// How long a runner waits for a child's pipe once the child has exited: a descendant that inherited the pipe holds it open past the
/// exit, and the text read by then is the child's answer.
/// </summary>
internal static class PipeGrace
{
    /// <summary>The wait past a child's exit.</summary>
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(2);

    /// <summary><paramref name="read"/>'s text where its pipe ended within <see cref="Wait"/>, else empty; the read given up on is observed.</summary>
    internal static async Task<string> ReadAsync(Task<string> read)
    {
        await Task.WhenAny(read, Task.Delay(Wait)).ConfigureAwait(false);
        if (read.IsCompletedSuccessfully) return read.Result;
        _ = read.ContinueWith(static ended => _ = ended.Exception, TaskScheduler.Default);
        return "";
    }
}
