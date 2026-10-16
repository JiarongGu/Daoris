namespace Daoris.Desktop;

/// <summary>
/// Which window owns a picker (MAINWINDOWS2c, D171 point 2): the one whose page asked. The first main window owned them all,
/// and is hidden while put away, so a dialog asked from another main window opened behind it or beside nobody.
/// </summary>
/// <remarks>
/// The judgement here, in plain <c>net10.0</c> where it is tested, over any window type; the page names its own window in the
/// request (<c>window</c>, as it names itself to its frame over <c>DAORIS.WINDOWS</c>), the application finds that window and
/// shows the dialog on its thread. The kit knows the page that sent a request (<c>PageSender</c>) but keeps that internal.
/// </remarks>
public static class AskingWindow
{
    /// <summary>
    /// The owner of a dialog: <paramref name="asking"/> where it is still <paramref name="usable"/> (open and shown), else the
    /// <paramref name="first"/> main window, else the first of <paramref name="others"/> that is. Where none is, the first, which
    /// the dialog's own placing then copes with. <paramref name="asking"/> is null for a press from no page (the tray, a launch).
    /// </summary>
    public static T Owner<T>(T? asking, T first, IEnumerable<T> others, Func<T, bool> usable) where T : class
    {
        if (asking is not null && usable(asking)) return asking;
        if (usable(first)) return first;
        return others.FirstOrDefault(usable) ?? first;
    }
}
