namespace Daoris.Driver;

/// <summary>
/// One change of the rule file under its keep lock: the lock taken, the rules read, the edit decided from them as they stand, and
/// the edit saved holding the same lock. Every writer of <c>permissions.json</c> goes through it, so a writer that reads
/// <c>allow: [A]</c> and saves a list built from that never writes back an allow another writer removed between its read and its save.
/// </summary>
public static class PermissionChange
{
    /// <summary>A test's other writer, run once the next change has read the rules and cleared before it runs.</summary>
    internal static readonly AsyncLocal<Action?> AfterRead = new();

    /// <param name="edit">The change, from the rules as read under the lock; it may throw to refuse it, nothing written.</param>
    /// <param name="read">Called once the rules are read, before the edit: a test's other writer, held off by the lock.</param>
    /// <param name="within">How long to wait for the lock; its default where none is given.</param>
    /// <returns>The rules as written.</returns>
    /// <exception cref="DriverException">The file does not read, or the edit is refused; nothing is written.</exception>
    /// <exception cref="IOException">Another writer held the lock for the whole of the wait; nothing is written.</exception>
    public static PermissionFile Change(string home, Func<PermissionFile, PermissionFile> edit, Action? read = null, TimeSpan? within = null)
    {
        using var held = KeepLock.Take(PermissionRules.PathOf(home), within);
        var rules = PermissionRules.Load(home);
        read?.Invoke();
        var seam = AfterRead.Value;
        AfterRead.Value = null;
        seam?.Invoke();
        var changed = edit(rules);
        PermissionRules.Save(home, changed, holding: true);
        return changed;
    }
}
