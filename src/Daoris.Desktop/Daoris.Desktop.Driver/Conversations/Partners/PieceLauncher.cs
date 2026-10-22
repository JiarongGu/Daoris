using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The partner's launcher (COAGENT1b; D199's note on how a partner starts in its strict job): a small program of Daoris's own,
/// <c>daoris-piece-start.exe</c> in the install's <c>app/bin/</c> beside the git doors (<c>Daoris.Desktop.PieceStart</c>), started for a
/// partner alone. It joins the piece's named job (<see cref="PieceJobs.NameOf"/>), which the driver made empty and holds, and only then
/// starts the harness as its child with the stdio it was handed, so no instruction of the harness runs outside the job, and the doors
/// read the launcher as they read any session's process. Where the job cannot be joined it refuses, <see cref="Refused"/>, and starts
/// nothing: never the best effort an ordinary session's job is.
/// </summary>
/// <remarks>
/// <para>Its words: <c>--job &lt;name&gt; -- &lt;harness&gt; [its arguments…]</c>, as <see cref="Arguments"/> writes them. The name must be a
/// piece's job; the harness's environment and working folder are the launcher's own, which the driver set as it sets a harness's.</para>
/// <para>The base library alone: the launcher compiles this in with <see cref="ProcessJob"/>, as the git doors compile theirs.</para>
/// </remarks>
public static class PieceLauncher
{
    /// <summary>The program's file in the install's <c>app/bin/</c>: a twin of <c>tools/desktop-publish.mjs</c>'s <c>PIECE_START</c>.</summary>
    public const string Program = "daoris-piece-start.exe";

    /// <summary>The exit of a start the launcher refused, the harness never started: its one stderr line says why.</summary>
    public const int Refused = 125;

    /// <summary>What every piece's job is named from, and the only jobs the launcher joins.</summary>
    public const string JobPrefix = "Local\\daoris-piece-";

    private const string JobOption = "--job";

    /// <summary>The launcher's words for a start of <paramref name="harness"/> in <paramref name="job"/>.</summary>
    public static IReadOnlyList<string> Arguments(string job, string harness, IEnumerable<string> arguments) =>
        [JobOption, job, "--", harness, .. arguments];

    /// <summary>Its words read: the job, the harness and the harness's own arguments; or why they cannot be taken.</summary>
    public static ((string Job, string Harness, IReadOnlyList<string> Arguments)? Call, string? Problem) Read(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 4 || arguments[0] != JobOption || arguments[2] != "--")
        {
            return (null, "daoris-piece-start: takes --job <a piece's job> -- <the harness> [its arguments], as the driver passes them.");
        }

        var job = arguments[1];
        if (!job.StartsWith(JobPrefix, StringComparison.Ordinal) || job.Length == JobPrefix.Length)
        {
            return (null, $"daoris-piece-start: joins a piece's job alone, and {job} is not one.");
        }

        if (string.IsNullOrWhiteSpace(arguments[3])) return (null, "daoris-piece-start: was given no harness to start.");
        return ((job, arguments[3], arguments.Skip(4).ToList()), null);
    }

    /// <summary>
    /// The launcher's run: its words read, the job joined through <paramref name="join"/>, then the harness started through
    /// <paramref name="start"/> and its exit answered. A refusal starts nothing.
    /// </summary>
    public static int Run(IReadOnlyList<string> arguments, TextWriter error, Func<string, bool> join, Func<ProcessStartInfo, TextWriter, int> start)
    {
        var (call, problem) = Read(arguments);
        if (call is not { } taken) return Refuse(error, problem!);
        if (!join(taken.Job))
        {
            return Refuse(error, $"daoris-piece-start: could not join the piece's job {taken.Job}, so the harness was not started.");
        }

        // Not the tools' environment (TOOLS5): the launcher's own, which the driver built for the harness and handed the launcher whole.
        var info = new ProcessStartInfo(taken.Harness)
        {
            UseShellExecute = false,
            // inherits its console: the launcher is the partner's process to the doors, and the harness speaks its protocol over the stdio
            // the driver handed the launcher. A spawn with no stream redirected and no window is given a console of its own, which cuts
            // that stdio; the launcher's own console was made with no window, so the harness's has none either.
            CreateNoWindow = false,
        };
        foreach (var word in taken.Arguments) info.ArgumentList.Add(word);
        return start(info, error);
    }

    /// <summary>The harness started and waited for: its exit is the launcher's.</summary>
    public static int Start(ProcessStartInfo info, TextWriter error)
    {
        try
        {
            using var process = Process.Start(info)!;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception failure)
        {
            return Refuse(error, $"daoris-piece-start: could not start {Path.GetFileName(info.FileName)} ({failure.Message}).");
        }
    }

    private static int Refuse(TextWriter error, string line)
    {
        error.WriteLine(line);
        error.Flush();
        return Refused;
    }
}
