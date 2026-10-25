namespace Daoris.Driver;

/// <summary>The desktop store could not be read or written, in words a person can act on (FOUNDATION1 z-i, the design §13.3, §13.4).</summary>
public class DesktopStoreException(string message, Exception? inner = null) : DriverException(message)
{
    /// <summary>The error beneath, where there is one: what the file's own library said.</summary>
    public Exception? Cause { get; } = inner;
}

/// <summary>
/// The store was written by a newer Daoris: its oldest-writer stamp is past this build's, so this build reads the tables it knows and
/// writes nothing (§13.3), as the quest store refuses a take's bind (QUESTSTOREVER1).
/// </summary>
public sealed class DesktopStoreNewerException(int stamped, int own)
    : DesktopStoreException(
        $"the desktop's store was written by a newer Daoris (its oldest writer {stamped}, this build's {own}); this build reads what it knows and writes nothing there. "
        + "Update Daoris.")
{
    /// <summary>The stamp the file holds.</summary>
    public int Stamped { get; } = stamped;

    /// <summary>The stamp this build writes with.</summary>
    public int Own { get; } = own;
}

/// <summary>
/// The store's write lock stayed held past the wait (§13.4's five seconds): another Daoris process on the home is writing, and this
/// one's write was refused with nothing written.
/// </summary>
public sealed class DesktopStoreBusyException(Exception inner)
    : DesktopStoreException(
        "the desktop's store is busy: another Daoris process on this home held its write lock for more than five seconds, so nothing was written. Try again.",
        inner);
