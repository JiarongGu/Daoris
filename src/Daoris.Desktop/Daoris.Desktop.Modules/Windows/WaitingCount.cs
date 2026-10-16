namespace Daoris.Desktop;

/// <summary>
/// The count the first main window's page last told (NOTIFY1b), and which windows show it (MAINWINDOWS2b): the first main
/// window always, and each other main window while the first is put away and its button is gone, so the taskbar shows the
/// count on one button at least. Kept here, in plain <c>net10.0</c>, so that is tested; the windows only draw what they are
/// told.
/// </summary>
/// <remarks>
/// Only the first main window's page tells it (<c>useTaskbarCount</c>), and a put-away window's page keeps running, so the
/// count stays current while it is away. Called from any thread: one lock holds the pair.
/// </remarks>
public sealed class WaitingCount(MainWindows mains)
{
    private readonly object _gate = new();
    private int _count;
    private string? _description;

    /// <summary>The page told how much waits: a count below none is none, and words with none are none.</summary>
    public void Told(int count, string? description)
    {
        lock (_gate)
        {
            _count = Math.Max(0, count);
            _description = _count > 0 ? description : null;
        }
    }

    /// <summary>What the window named <paramref name="window"/> shows of the count now: nothing where it does not wear it.</summary>
    public (int Count, string? Description) For(string window)
    {
        if (!mains.WearsCount(window)) return (0, null);
        lock (_gate) return (_count, _description);
    }
}
