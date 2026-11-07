using System.Text;

namespace Daoris.Driver;

/// <summary>What a rule's command did: its exit, and its output as it wrote it.</summary>
internal sealed record CommandRan(int Code, string Output)
{
    /// <summary>An install's failure in its own words, for a note: its first line naming an error, else its last line.</summary>
    internal string Tail
    {
        get
        {
            var lines = Output.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
            if (lines.Count == 0) return $"it exited {Code}";
            return lines.FirstOrDefault(line => line.Contains("error", StringComparison.OrdinalIgnoreCase)) ?? lines[^1];
        }
    }
}

/// <summary>
/// A dependency rule's commands, run as an integration gate's command is (<see cref="IInstallRunner"/>) in exactly the environment
/// <see cref="InstallEnvironment"/> composes, bounded, and ended with everything it started at the bound or a cancel. Its output goes to a
/// log beside the tree's files record.
/// </summary>
internal static class DependencyCommands
{
    /// <summary>How long an install may run before it is ended, as a gate's bound.</summary>
    internal static readonly TimeSpan InstallBound = TimeSpan.FromMinutes(30);

    /// <summary>How long a <c>toolchain</c> read may run: it prints what a key reads.</summary>
    internal static readonly TimeSpan ReadBound = TimeSpan.FromMinutes(2);

    /// <summary>The test seam: run with each install's folder as it starts, where a test acts during an install. Production sets none.</summary>
    internal static readonly AsyncLocal<Action<string>?> Installing = new();

    /// <summary>What an install ended by a terminal's visit exits with.</summary>
    internal const int Visited = -2;

    /// <param name="visited">Cancelled by a terminal's visit to the tree, which ends the command with all it started.</param>
    internal static async Task<CommandRan> RunAsync(
        string home, string tree, string command, string directory, IReadOnlyDictionary<string, string> environment, TimeSpan bound, CancellationToken ct,
        CancellationToken visited = default)
    {
        var log = DaorisHome.Entry(home, HomeEntry.TreeFiles, $"{TreeFilesRecord.IdOf(tree)}.{Guid.NewGuid().ToString("N")[..8]}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        int code;
        // A job of its own, so its end ends whatever the command started; disposed, it ends what is left.
        using var job = ProcessJob.Create();
        IRunningInstall started;
        try
        {
            started = InstallRunner.Current.Start(job, command, directory, environment, log);
        }
        catch (DriverException refused)
        {
            return new CommandRan(-1, refused.Message);
        }

        using (var step = started)
        using (var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct, visited))
        {
            bounded.CancelAfter(bound);
            try
            {
                code = await step.ExitAsync(bounded.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                job?.Terminate();
                step.Stop();
                if (ct.IsCancellationRequested) throw;
                code = visited.IsCancellationRequested ? Visited : -1;
            }
        }

        string output;
        try
        {
            output = File.Exists(log) ? await File.ReadAllTextAsync(log, Encoding.UTF8, CancellationToken.None).ConfigureAwait(false) : "";
            File.Delete(log);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            output = "";
        }

        return new CommandRan(code, code == -1 ? $"it ran past its bound of {bound.TotalMinutes:0} minutes and was ended" : output);
    }
}
