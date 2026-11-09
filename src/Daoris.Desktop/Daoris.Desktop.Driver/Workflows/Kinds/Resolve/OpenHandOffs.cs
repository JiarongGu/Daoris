namespace Daoris.Driver;

/// <summary>
/// The hand-offs a driver's start finds open, each put back before the loop's first pass (§2.4 point 6): its resolver stopped and awaited,
/// its edits kept on the edits ref, the tree on its branch at its tip, the row closed. Once per start: a hand-off a later pass opened is the
/// live door's, never restored from under its resolver; a restoration that failed is tried again at the next pass.
/// </summary>
internal sealed class OpenHandOffs(string home, IResolvers resolvers, Action<string>? log = null)
{
    private readonly object _gate = new();
    private Task? _restored;

    /// <summary>The hand-offs' table; the home's own where none is handed.</summary>
    public TreeHandOffs? HandOffs { get; init; }

    /// <summary>Completes once the hand-offs this start found open are put back; at once after the first time.</summary>
    public Task BeforePassAsync(CancellationToken ct)
    {
        lock (_gate) return _restored ??= RestoreAsync(ct);
    }

    private async Task RestoreAsync(CancellationToken ct)
    {
        // Never finished inside the lock, so a failure's reset is not overwritten by the task it resets.
        await Task.Yield();
        try
        {
            await ResolveDoor.RestoreOpenAsync(home, HandOffs ?? new TreeHandOffs(home), resolvers, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException or DesktopStoreException)
        {
            log?.Invoke($"resolve  a hand-off open since the driver's last run was not put back: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at the next pass.");
            lock (_gate) _restored = null;
        }
    }
}
