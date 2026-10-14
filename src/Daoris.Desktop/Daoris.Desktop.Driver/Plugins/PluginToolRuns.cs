using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A plugin's tool checks as programs on this machine (PLUGTOOL1a, D150 point 7; STRUCT3a): how each is started and its
/// answer read. <see cref="PluginToolChecks"/> asks through its <see cref="CheckRunner"/> port, whose
/// real runner this is; a fast test hands a stand-in.
/// </summary>
internal static class PluginToolRuns
{
    /// <summary>UTF-8 with no byte-order mark: a mark is not part of what a program is told.</summary>
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The <c>PATH</c> this process was started with: where the system's tools are found when none is named.</summary>
    public static string? InheritedPath() => Environment.GetEnvironmentVariable(Tools.PathVariable);

    /// <summary>A start, handed the tools' environment, in <paramref name="root"/>.</summary>
    /// <exception cref="DriverException">An argument a Windows command shim would read again.</exception>
    public static ProcessStartInfo StartInfo(string file, IEnumerable<string> arguments, string root, ToolsRead read, string home)
    {
        var info = new ProcessStartInfo
        {
            FileName = file,
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        // git walks up: a scratch folder under a checkout answers for the checkout, so git is told no repository is here,
        // as a trial's sample frames are (D101).
        info.Environment["GIT_DIR"] = Path.Combine(root, ".git");
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        Tools.Hand(info, read, home);
        info.FileName = HarnessActions.WindowsShim(
            info.FileName, info.ArgumentList, info.Environment.TryGetValue(Tools.PathVariable, out var childPath) ? childPath : null);
        return info;
    }

    /// <summary>
    /// Start a program, type it nothing, and read both streams whole, waiting <paramref name="patience"/> at most; a program
    /// still running then is ended, and answered no exit code.
    /// </summary>
    public static async Task<CheckRun> RunAsync(ProcessStartInfo info, TimeSpan patience, CancellationToken ct)
    {
        // Nothing is typed to it: a program that waits for input reads its end at once. A start failure is said in Daoris's
        // words by the caller: the system's message names the working directory.
        var run = await ChildProcess.RunAsync(info, patience, ct, closeInput: true).ConfigureAwait(false);
        return new(run.Started, run.ExitCode, run.Stdout, run.Stderr);
    }
}
