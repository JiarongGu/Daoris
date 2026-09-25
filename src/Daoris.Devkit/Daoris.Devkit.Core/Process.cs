using System.Diagnostics;
using System.Text;

namespace Daoris.Devkit;

/// <param name="ExitCode">The process's exit code.</param>
/// <param name="Output">Everything on stdout.</param>
/// <param name="Error">Everything on stderr.</param>
public readonly record struct ProcessOutput(int ExitCode, string Output, string Error);

/// <summary>Running other programs — the devkit's actual job.</summary>
public static class Process
{
    /// <summary>Run a program and capture what it said.</summary>
    /// <remarks>
    /// Reads both streams concurrently rather than draining one then the other. A child that fills the
    /// pipe it is not being read from blocks forever, and the program most likely to do that is a build
    /// producing thousands of warning lines on stderr — which is exactly what a gate runs.
    /// </remarks>
    public static ProcessOutput Run(string file, IReadOnlyList<string> arguments, string workingDirectory)
    {
        (file, arguments) = Resolve(file, arguments);
        var start = new ProcessStartInfo
        {
            FileName = file,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(start)
            ?? throw new DevkitException($"could not start '{file}'");

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new ProcessOutput(process.ExitCode, output.Result, error.Result);
    }

    /// <summary>
    /// A command as a person types it, made startable (REV3): on Windows a name with no extension is
    /// looked up by `PATHEXT` — as `cmd` would — and a `.cmd` or `.bat` is run through `cmd.exe`.
    /// </summary>
    /// <remarks>
    /// Started without a shell, .NET looks for `name.exe` only, and npm installs a command as
    /// `name.cmd`: the doctrine gate's default `daoris` could not start on any Windows machine that had
    /// installed it. The driver's harness actions learned the same thing (FIX-LOG, `WindowsShim`).
    /// Through `cmd.exe` an argument is re-read by `cmd`, so one carrying its metacharacters is refused
    /// rather than passed.
    /// </remarks>
    private static (string File, IReadOnlyList<string> Arguments) Resolve(string file, IReadOnlyList<string> arguments)
    {
        if (!OperatingSystem.IsWindows() || Path.HasExtension(file)) return (file, arguments);

        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var places = file.Contains(Path.DirectorySeparatorChar) || file.Contains(Path.AltDirectorySeparatorChar)
            ? [file]
            : (Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Combine(directory, file));

        foreach (var place in places)
        {
            foreach (var extension in extensions)
            {
                var candidate = place + extension;
                if (!File.Exists(candidate)) continue;
                if (!extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    return (candidate, arguments);
                }

                if (arguments.FirstOrDefault(a => a.IndexOfAny(['"', '%', '&', '|', '<', '>', '^', '\r', '\n']) >= 0) is { } unsafeArgument)
                {
                    throw new DevkitException(
                        $"'{candidate}' is a command script, and cmd would re-read the argument '{unsafeArgument}' — "
                        + "name the program itself under 'doctrine.command' instead.");
                }

                return ("cmd.exe", ["/d", "/c", candidate, .. arguments]);
            }
        }

        return (file, arguments);
    }

    /// <summary>
    /// Run a declared command line, streaming its output straight through.
    /// </summary>
    /// <remarks>
    /// Streamed rather than captured: a declared gate is usually a build or a test run, and watching it
    /// produce output is most of how anyone knows it is still alive. Capturing would also mean holding
    /// a large build log in memory to print it unchanged at the end.
    ///
    /// Through the platform shell, because what is declared is a command LINE — with pipes, quoting and
    /// argument forms that belong to the shell the author was writing for.
    /// </remarks>
    public static int RunShell(string commandLine, string workingDirectory)
    {
        var (file, arguments) = OperatingSystem.IsWindows()
            ? ("cmd.exe", new[] { "/d", "/c", commandLine })
            : ("/bin/sh", ["-c", commandLine]);

        var start = new ProcessStartInfo { FileName = file, WorkingDirectory = workingDirectory, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(start)
            ?? throw new DevkitException($"could not start '{file}'");
        process.WaitForExit();
        return process.ExitCode;
    }
}
