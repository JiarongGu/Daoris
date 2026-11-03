using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A checkout's <c>daoris status --json</c> (the context-one design §3 A, D230 point 2), asked of the doctrine tool as a child of Daoris
/// asks it and read by <see cref="DoctrineStatus.Parse"/>. Reads, and writes nothing.
/// </summary>
public static class DoctrineStatusRead
{
    /// <summary>How long the tool may take, its canon read and its sync planned, before it is called unanswering.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Run <paramref name="program"/>, then <paramref name="leading"/>, then <c>status --json</c> in <paramref name="root"/>, handed the tools'
    /// environment from <paramref name="read"/>: the status, or what was found in place of one.
    /// </summary>
    /// <param name="leading">The words before the command, as <c>node</c> takes the CLI's entry; none for a launcher.</param>
    public static async Task<DoctrineReading> ReadAsync(
        string program, IReadOnlyList<string> leading, string root, ToolsRead read, string home, CancellationToken ct)
    {
        var info = new ProcessStartInfo
        {
            FileName = program,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true, // asked from the window's host: no console may flash (Adapters.Shell)
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var word in leading) info.ArgumentList.Add(word);
        info.ArgumentList.Add("status");
        info.ArgumentList.Add("--json");
        Tools.Hand(info, read, home);

        var name = Path.GetFileName(program);
        var run = await ChildProcess.RunAsync(info, Patience, ct, closeInput: false).ConfigureAwait(false);
        if (!run.Started) return new(null, run.Error is null ? $"`{name}` did not start" : $"`{name}` would not start: {run.Error.Message}");
        if (run.TimedOut) return new(null, $"`{name} status --json` did not answer within {Patience.TotalSeconds:0}s");
        if (run.ExitCode == 0 && DoctrineStatus.Parse(run.Stdout) is { } status) return new(status, null);

        var said = First(run.Stderr) ?? First(run.Stdout);
        return new(null, said is null
            ? $"`{name} status --json` exited {run.ExitCode} and printed no status"
            : $"`{name} status --json` said: {said}");
    }

    /// <summary>The first line that says something, trimmed; null where none does.</summary>
    private static string? First(string text) =>
        text.ReplaceLineEndings("\n").Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
}
