using System.ComponentModel;
using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris.exe</c>, the install's command (<c>Daoris.Desktop.Command</c>): starts <c>app/daoris-driver/daoris-driver.exe</c> with its arguments
/// whole and ends with its exit code. A batch file's <c>%*</c> ends at the first line break; a program's command line carries one inside a
/// quoted argument, and <see cref="ProcessStartInfo.ArgumentList"/> quotes each argument the way the program reads it back.
/// </summary>
/// <remarks>
/// The program's standard streams, environment and working folder are the command's own, since nothing is redirected: its words in, its
/// words out, and a verb that reads a terminal reads this one. The base library alone: the launcher compiles this in, as the git doors compile theirs.
/// </remarks>
public static class CommandLauncher
{
    /// <summary>The command's file in the install's <c>app/bin/</c>: a twin of <c>tools/desktop-publish.mjs</c>'s <c>CLI_LAUNCHERS</c>.</summary>
    public const string Program = "daoris.exe";

    /// <summary>The exit of a command that found no program to start: its one stderr line says where it looked.</summary>
    public const int NoProgram = 2;

    /// <summary>The program's path from the command's own folder: a twin of <c>DRIVER_HOME</c> and <c>DRIVER_EXE</c> beside <c>CLI_BIN</c>.</summary>
    private static readonly string[] Target = ["..", "daoris-driver", "daoris-driver.exe"];

    /// <summary>The desktop's program, as the install lays it out beside the folder the command runs from.</summary>
    public static string TargetOf(string folder) => Path.GetFullPath(Path.Combine([folder, .. Target]));

    /// <summary>The start of <paramref name="target"/> with <paramref name="args"/> as given, each one argument whatever it holds.</summary>
    public static ProcessStartInfo Start(string target, IReadOnlyList<string> args)
    {
        var info = new ProcessStartInfo(target) { UseShellExecute = false };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    /// <summary>Run the desktop's program laid out beside <paramref name="folder"/> with <paramref name="args"/>; its exit code, or <see cref="NoProgram"/>.</summary>
    public static int Run(IReadOnlyList<string> args, string folder, TextWriter error) => Run(TargetOf(folder), args, error);

    /// <summary>Run <paramref name="target"/> with <paramref name="args"/> and wait for it; its exit code, or <see cref="NoProgram"/> where it cannot be started.</summary>
    public static int Run(string target, IReadOnlyList<string> args, TextWriter error)
    {
        if (!File.Exists(target))
        {
            error.WriteLine($"daoris: this install's program is not at `{target}`; run the installed Daoris's update or its publish again.");
            return NoProgram;
        }

        try
        {
            using var child = Process.Start(Start(target, args));
            if (child is null)
            {
                error.WriteLine($"daoris: `{target}` did not start.");
                return NoProgram;
            }

            child.WaitForExit();
            return child.ExitCode;
        }
        catch (Win32Exception refused)
        {
            error.WriteLine($"daoris: `{target}` did not start: {refused.Message}");
            return NoProgram;
        }
    }
}
