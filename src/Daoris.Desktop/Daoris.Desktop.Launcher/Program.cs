using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Daoris.Desktop.Launcher;

/// <summary>
/// The one executable at an install's root (D93). It starts the application from <c>app/</c> with the
/// arguments it was given, then exits: the application finds the install above its own folder
/// (<c>InstallHome.RootOf</c>), so nothing is handed over but the arguments and the environment.
/// </summary>
/// <remarks>
/// <para>A twin of the publish script's <c>SHELL_HOME</c> and <c>SHELL_EXE</c> (<c>tools/desktop-publish.mjs</c>)
/// and of the application's assembly name: the script lays the application out where this looks, and a
/// test reads this file for the names.</para>
///
/// <para>And of the application's <c>TaskbarIdentity</c> (TASKBAR1, D108): this file's name is what an
/// install's window names as the command a pin starts, and <see cref="AppId"/> is the id the window
/// carries. <c>TaskbarIdentityTests</c> reads this file and its project for both.</para>
/// </remarks>
public static class Launcher
{
    /// <summary>The folder the application sits in, beside this launcher.</summary>
    public const string AppFolder = "app";

    /// <summary>The application: Chromium's launcher, named from the app's assembly less <c>.App</c>.</summary>
    public const string ShellExe = "Daoris.Desktop.exe";

    /// <summary>
    /// The application id Windows groups Daoris's taskbar button by (D108): the same as the window's,
    /// because a launcher and the process it starts are one application to Windows, and so a refusal
    /// below sits with the person's pinned Daoris rather than on a button of its own.
    /// </summary>
    public const string AppId = "Daoris.Desktop";

    [STAThread]
    public static int Main(string[] args)
    {
        // Before anything is shown: Windows reads a process's id when it first presents something. A
        // failure changes nothing a person can see but where a refusal's button goes.
        _ = SetCurrentProcessExplicitAppUserModelID(AppId);

        var shell = Path.Combine(AppContext.BaseDirectory, AppFolder, ShellExe);
        if (!File.Exists(shell))
        {
            return Refuse($"Daoris could not find its application at {Path.Combine(AppFolder, ShellExe)} beside "
                + "this launcher. Publish it again into this folder.");
        }

        var start = new ProcessStartInfo(shell)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(shell)!,
        };
        foreach (var argument in args) start.ArgumentList.Add(argument);

        try
        {
            Process.Start(start)?.Dispose();
            return 0;
        }
        catch (Win32Exception error)
        {
            return Refuse($"Daoris could not start {Path.Combine(AppFolder, ShellExe)}: {error.Message}");
        }
    }

    private static int Refuse(string sentence)
    {
        MessageBoxW(IntPtr.Zero, sentence, "Daoris", 0x10 /* MB_ICONERROR */);
        return 1;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
