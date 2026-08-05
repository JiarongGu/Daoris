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
