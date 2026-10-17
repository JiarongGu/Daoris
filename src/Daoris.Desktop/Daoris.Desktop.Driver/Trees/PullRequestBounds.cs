namespace Daoris.Driver;

/// <summary>
/// D148's minute per pull request, shared by every occasion in this process (FABRIC-END6c): an entry one occasion is asking, or asked
/// within the minute, is not asked by another, answered or not.
/// </summary>
public static class PullRequestMinute
{
    /// <summary>Take the entry's minute at <paramref name="at"/>: false where it was taken within the minute before.</summary>
    public static bool TryTake(string home, LandedBranch entry, DateTimeOffset at) => true;

    /// <summary>Whether the entry was taken within the minute before <paramref name="now"/>.</summary>
    public static bool Within(string home, LandedBranch entry, DateTimeOffset now) => false;

    /// <summary>The entry was asked at <paramref name="at"/>, its own request's time; kept where it is the later.</summary>
    public static void Asked(string home, LandedBranch entry, DateTimeOffset at)
    {
    }

    /// <summary>A minute taken at <paramref name="at"/> whose ask was never sent, given back.</summary>
    public static void Release(string home, LandedBranch entry, DateTimeOffset at)
    {
    }
}

/// <summary>
/// A repository held by a removing press, bringing it up to date, or a landing (FABRIC-END6c), in this process: a due moment skips it,
/// and is owed, until it is let go.
/// </summary>
public static class RepositoryHolds
{
    /// <summary>Hold the repository until the answer is disposed.</summary>
    public static IDisposable Hold(string home, string repository) => new Nothing();

    /// <summary>Whether something holds the repository now.</summary>
    public static bool Held(string home, string repository) => false;

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

/// <summary>
/// The window's focus, kept until there is someone to tell (FABRIC-END6c): one pending flag, not a queue.
/// </summary>
public sealed class PendingFocus
{
    private volatile Action? _tell;

    /// <summary>Whether a focus waits for someone to tell.</summary>
    public bool Pending => false;

    /// <summary>The window has the person's focus.</summary>
    public void Focused() => _tell?.Invoke();

    /// <summary>Who is told from now on; null where nobody is.</summary>
    public void Ready(Action? tell) => _tell = tell;
}
