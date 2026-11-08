using System.Diagnostics;
using System.Text;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A child process timed from its start to its end, and the lines of a bench written to the console and to a file beside the bench's home.</summary>
internal sealed class BenchRuns(string log, Action<string> echo)
{
    internal void Say(string line)
    {
        echo(line);
        File.AppendAllText(log, line + "\n");
    }

    /// <summary>Runs <paramref name="file"/> with <paramref name="arguments"/> in <paramref name="folder"/>; the seconds it took and its exit code.</summary>
    internal async Task<(double Seconds, int Code)> TimedAsync(string folder, string file, params string[] arguments)
    {
        var start = new ProcessStartInfo(file) { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        var watch = Stopwatch.StartNew();
        using var child = Process.Start(start)!;
        var tail = new StringBuilder();
        void Keep(string? line)
        {
            if (line is null) return;
            lock (tail)
            {
                tail.AppendLine(line);
                if (tail.Length > 4000) tail.Remove(0, tail.Length - 2000);
            }
        }

        child.OutputDataReceived += (_, each) => Keep(each.Data);
        child.ErrorDataReceived += (_, each) => Keep(each.Data);
        child.BeginOutputReadLine();
        child.BeginErrorReadLine();
        await child.WaitForExitAsync();
        watch.Stop();
        var said = tail.ToString().Trim();
        if (child.ExitCode != 0) Say($"  ! {file} {string.Join(' ', arguments)} exited {child.ExitCode}: {said[^Math.Min(600, said.Length)..]}");
        return (watch.Elapsed.TotalSeconds, child.ExitCode);
    }
}
