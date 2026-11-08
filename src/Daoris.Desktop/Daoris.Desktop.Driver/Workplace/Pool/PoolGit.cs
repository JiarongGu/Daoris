namespace Daoris.Driver;

/// <summary>
/// The git of one claim: every process started suspended in the claim's named job and joined to it, so a takeover opens the job by its name,
/// ends what a dead run left running and waits it empty before it reads anything. Closing the job ends whatever still runs.
/// </summary>
internal sealed class PoolGit : IDisposable
{
    /// <summary>How long one git may run before the claim's processes are ended.</summary>
    internal static readonly TimeSpan Bound = TimeSpan.FromMinutes(10);

    private readonly string _home;
    private readonly ProcessJob _job;

    private PoolGit(string home, ProcessJob job)
    {
        _home = home;
        _job = job;
    }

    /// <summary>The job's name for a new claim: unique, so no other process holds it.</summary>
    internal static string NewJobName() => $"daoris-kept-{Guid.NewGuid():N}";

    /// <summary>The claim's git; null where there is no job to hold it in (off Windows, or a name already held).</summary>
    internal static PoolGit? Create(string home, string jobName) =>
        ProcessJob.CreateNamed(jobName) is { Job: { } job } ? new PoolGit(home, job) : null;

    /// <summary>
    /// End the job a dead run's claim named and wait until it holds no process. False where a job of that name is there and would not
    /// open or empty, which is never "ended"; true where it ended or none answers to the name.
    /// </summary>
    internal static async Task<bool> EndAsync(string jobName)
    {
        try
        {
            using var job = ProcessJob.OpenToEnd(jobName);
            if (job is null) return true;
            job.Terminate();
            return await job.WaitEmptyAsync(Bound).ConfigureAwait(false);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>git with <paramref name="arguments"/> in <paramref name="folder"/>; the exit is -1 where it did not answer within its bound and its processes are ended.</summary>
    internal async Task<(int Code, string Stdout, string Stderr)> GitAsync(string folder, IReadOnlyList<string> arguments, CancellationToken ct = default)
    {
        var (info, refusal) = WorkingTree.GitStart(folder, arguments, _home);
        if (info is null) return (-1, "", $"git could not be started ({refusal})");
        // Placed as every git is; a claim's git runs to its bound once placed, whoever stops waiting.
        var ask = GitStartAsks.For(info, arguments, GitRunner.Held, Bound, shares: false);
        return await GitScheduler.Shared.RunAsync(ask, _ => PlacedAsync(info), ct).ConfigureAwait(false);
    }

    private async Task<(int Code, string Stdout, string Stderr)> PlacedAsync(System.Diagnostics.ProcessStartInfo info)
    {
        HeldChild child;
        try
        {
            child = HeldChild.Start(_job, info, input: false);
        }
        catch (DriverException error)
        {
            return (-1, "", error.Message);
        }

        using (child)
        {
            child.Resume();
            await child.WriteAsync(null).ConfigureAwait(false);
            var (exit, stdout, stderr) = await child.ExitAsync(Bound).ConfigureAwait(false);
            if (exit is { } code) return (code, stdout, stderr);
            _job.Terminate();
            await _job.WaitEmptyAsync(Bound).ConfigureAwait(false);
            return (-1, "", "git did not answer within its bound, and its processes were ended");
        }
    }

    public void Dispose() => _job.Dispose();
}
