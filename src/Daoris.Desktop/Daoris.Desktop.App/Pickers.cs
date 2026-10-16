namespace Daoris.Desktop;

/// <summary>
/// The system's folder and file pickers, owned by a window (MAINWINDOWS2c, D171 point 2): the one whose page asked, which
/// <see cref="Owner"/> finds, rather than the first main window, which is hidden while put away.
/// </summary>
/// <remarks>
/// <para>Marshalled onto the owner's thread, because an IPC request arrives on whatever thread the bridge dispatched it on and a
/// dialog shown from anywhere else either throws or opens with no owner, a modal floating free of its window. Each window
/// runs its own pump, so the owner's own thread is the one that shows it.</para>
/// </remarks>
internal static class Pickers
{
    /// <summary>
    /// The window to own a picker now: the one whose page asked, which the page names as it names itself to its frame
    /// (<paramref name="window"/>, null for the first main window), as <see cref="AskingWindow.Owner"/> judges it, else the first
    /// main window or an open one. The kit's own record of the page that sent a request (<c>PageSender</c>) is internal to it.
    /// </summary>
    public static Form Owner(MainForm first, SecondaryWindowHost windows, string? window)
    {
        Form? asking = window is null || window == MainWindows.First ? null : windows.FormNamed(window);
        return AskingWindow.Owner(asking, first, windows.MainForms(), Usable);
    }

    private static bool Usable(Form window) => !window.IsDisposed && window.IsHandleCreated && window.Visible;

    /// <summary>The native folder dialog, for adding a repository (D48 §7). Null is the person cancelling.</summary>
    public static string? Folder(Form owner)
    {
        if (owner.InvokeRequired) return (string?)owner.Invoke(() => Folder(owner));

        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a repository to add to this machine's registry",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.SelectedPath : null;
    }

    /// <summary>The system's file picker, for a tool named as a file (TOOLS7): the program chosen, or null when cancelled.</summary>
    public static string? File(Form owner, string? title)
    {
        if (owner.InvokeRequired) return (string?)owner.Invoke(() => File(owner, title));

        using var dialog = new OpenFileDialog
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Choose a program" : title,
            Filter = "Programs (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
    }
}
