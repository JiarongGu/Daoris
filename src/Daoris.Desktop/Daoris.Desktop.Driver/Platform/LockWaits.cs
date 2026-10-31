namespace Daoris.Driver;

/// <summary>
/// The waits on a lock a flow makes while it holds a checkout alone (GITLOCK1's order, the checkout's <c>TreeLock</c>): each lock says so
/// before it waits, and a test sees them. Only a process start's place, a record's lock and the desktop store's write lock may be waited for
/// there, since none's holder waits.
/// </summary>
public static class LockWaits
{
    /// <summary>The waits a flow holding a checkout alone may make.</summary>
    public const string Starts = "a process start's place", Records = "a record's lock";

    /// <summary>The desktop store's write lock, as the act journal waits on it under the checkout's (GITAPP2a): one short transaction holds it.</summary>
    public const string Store = "the desktop store's write lock";

    private static readonly AsyncLocal<object?> s_holding = new();

    /// <summary>
    /// The test seam: told what each wait on a lock is that a flow makes while it holds a checkout alone. Null in production; an async local,
    /// so classes running in parallel never see each other's.
    /// </summary>
    internal static readonly AsyncLocal<Action<string>?> Seen = new();

    /// <summary>The flow's innermost hold alone, which the checkout's lock sets as it takes one and puts back as it lets go.</summary>
    internal static object? Holding
    {
        get => s_holding.Value;
        set => s_holding.Value = value;
    }

    /// <summary>Said by every wait on a lock before it waits: told to <see cref="Seen"/> where this flow holds a checkout alone.</summary>
    internal static void Waits(string what)
    {
        if (s_holding.Value is not null) Seen.Value?.Invoke(what);
    }
}
