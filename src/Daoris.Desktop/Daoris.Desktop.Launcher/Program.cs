using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Daoris.Driver;

namespace Daoris.Desktop.Launcher;

/// <summary>
/// The one executable at an install's root (D93). It starts the application from <c>app/</c> with the
/// arguments it was given, then exits: the application finds the install above its own folder
/// (<c>InstallHome.RootOf</c>), so nothing is handed over but the arguments and the environment.
/// </summary>
/// <remarks>
/// <para><b>And the one program that can replace the application</b> (UPDATE1, D139 §5): while nothing runs from
/// <c>app/</c>, a build staged in <c>update/staged/</c> is checked and swapped in, journalled in <c>update/swap.json</c>,
/// and rolled back when it will not come up. The application asks for it with <c>--update --after &lt;pid&gt;</c> as it
/// closes; a start finding a build staged does the same first. The swap is <c>StagedBuild.cs</c>, compiled in.</para>
///
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

    /// <summary>
    /// The application's word that it has closed for an update (UPDATE1, D139 §5): <c>--update --after &lt;pid&gt;</c>, first,
    /// then whatever the application is to be started with.
    /// </summary>
    public const string UpdateArgument = "--update";

    public const string AfterArgument = "--after";

    /// <summary>How long an update waits for the application that asked for it to end, and then for everything from <c>app/</c>.</summary>
    private static readonly TimeSpan ClosingWithin = TimeSpan.FromMinutes(3);

    [STAThread]
    public static int Main(string[] args)
    {
        // Before anything is shown: Windows reads a process's id when it first presents something. A
        // failure changes nothing a person can see but where a refusal's button goes.
        _ = SetCurrentProcessExplicitAppUserModelID(AppId);

        var root = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        var shell = Path.Combine(root, AppFolder, ShellExe);
        var (updating, after, rest) = Read(args);

        // The swap (D139 §5) happens only while nothing runs from `app/`: the application cannot be replaced while it runs,
        // and a start beside a running one is the person opening Daoris again, which the running one answers.
        try
        {
            if (updating) WaitForClose(after, Path.Combine(root, AppFolder));
            var running = StagedBuild.RunningFrom(Path.Combine(root, AppFolder)).Count > 0;
            var swap = new InstallSwap(root, start: arguments => Start(shell, arguments), alive: Alive);
            swap.Recover(running);
            if (updating && running)
            {
                // Asked for, and still held once the wait is over: refused, so the build before it, started below, does not
                // close for the same build again.
                swap.Held("something still ran from app/ after the application closed, so nothing was replaced.");
            }
            else if (!running && StagedBuild.IsStaged(root) && (updating || Wanted(root)))
            {
                // Installed: the new application is running, started by the swap. Anything else: the one in `app/` is the
                // build before it, and is started below as on any start.
                if (!swap.Run(rest).StartOld) return 0;
            }
        }
        catch (Exception)
        {
            // Whatever the update met, the launcher's last act is starting Daoris: one that died here would leave no window
            // after the application had closed for the update. A swap journals each move before the next, so what it did
            // not put back, the next start does. An update asked for still consumes what is staged, so the application is
            // not closed for the same build again.
            if (updating)
            {
                try
                {
                    new InstallSwap(root, start: _ => null, alive: _ => false).Held("the launcher met an error before it could swap.");
                }
                catch (Exception)
                {
                    // Nothing more to try: the application starts below, on the build in app/.
                }
            }
        }

        if (!File.Exists(shell))
        {
            return Refuse($"Daoris could not find its application at {Path.Combine(AppFolder, ShellExe)} beside "
                + "this launcher. Publish it again into this folder.");
        }

        return Start(shell, rest) is null
            ? Refuse($"Daoris could not start {Path.Combine(AppFolder, ShellExe)}.")
            : 0;
    }

    /// <summary>The update's words, if the application said them first, and the arguments the application is to be started with.</summary>
    internal static (bool Updating, int? After, string[] Handed) Read(string[] args)
    {
        if (args is not [UpdateArgument, .. var tail]) return (false, null, args);
        if (tail is [AfterArgument, var pid, .. var rest] && int.TryParse(pid, out var after)) return (true, after, rest);
        return (true, null, tail);
    }

    /// <summary>
    /// Wait for the application that asked for the update, then for everything else running from <c>app/</c> — its engine's
    /// processes, its browser, the host it started — each bounded, so a process that will not end costs the swap, not the start.
    /// </summary>
    private static void WaitForClose(int? after, string app)
    {
        if (after is { } pid)
        {
            try
            {
                using var asked = Process.GetProcessById(pid);
                asked.WaitForExit(ClosingWithin);
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }

        var until = DateTime.UtcNow + ClosingWithin;
        while (StagedBuild.RunningFrom(app).Count > 0 && DateTime.UtcNow < until) Thread.Sleep(250);
    }

    /// <summary>
    /// Whether a start should install what is staged (D139 §5): unless the person said *Not now* of it, in the request under
    /// the home the application runs on — the install's own <c>data/</c>, or a home named for this start alone, as the
    /// application decides it (D63, D105).
    /// </summary>
    private static bool Wanted(string root)
    {
        var named = Environment.GetEnvironmentVariable("DAORIS_HOME");
        var account = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("DAORIS_HOME", EnvironmentVariableTarget.User)
            : null;
        var home = string.IsNullOrWhiteSpace(named) || string.Equals(named.Trim(), account?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(root, "data")
            : named.Trim();
        var staged = StagedBuild.Read(root, out _)?.Id;
        return UpdateRequests.ModeFor(UpdateRequests.Read(home), staged) != UpdateMode.NotNow;
    }

    /// <summary>Start the application with these arguments; its process id, or null when it would not start.</summary>
    private static int? Start(string shell, IReadOnlyList<string> arguments)
    {
        if (!File.Exists(shell)) return null;

        // Not the tools' environment (TOOLS5): the application is Daoris's own program, and every child it starts is
        // handed the tools' environment there, read at each start; its own environment is never rewritten (§2.4).
        // Through the shell (UPDATE1): a start that does not go through it hands the application every inheritable handle
        // this launcher holds, which it inherited from the application that asked for the update, so the new one found the
        // engine's DevTools port held by the builds before it. The shell's start inherits none and still hands the
        // environment.
        var start = new ProcessStartInfo(shell)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(shell)!,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        try
        {
            using var started = Process.Start(start);
            return started?.Id;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static bool Alive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return false;
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
