namespace Daoris.Driver;

/// <summary>
/// The one job every process a candidate's gates start runs in (INTEGRATE1f part f2; the integration design §5.6, CISCOPE1e2's job,
/// QUEUEDRAIN1's lesson): named by the candidate (<see cref="IntegrationJobs.NameOf"/>), made empty, each process started suspended and
/// joined before it runs (<see cref="HeavyStep"/>). A drop, a stop or the run's end closes it, its descendants ending with it, and
/// <b>before</b> any place in the machine's admission is let go, so a place is never free while a process of its run still lives
/// (<see cref="EndLeftAsync"/>, called while the place is still held, and <see cref="DisposeAsync"/>).
/// </summary>
/// <remarks>
/// Off Windows there is no job: <see cref="Job"/> is null, a step is ended from its root, and what it started is best effort, as a heavy
/// run's is. A job of the candidate's name that an earlier run left (a step run again from its start, after a crash) is ended and waited
/// for before this one is made, so two attempts never run at once.
/// </remarks>
internal sealed class CandidateJob : IAsyncDisposable
{
    private CandidateJob(string name, ProcessJob? job) => (Name, Job) = (name, job);

    public string Name { get; }

    /// <summary>The job processes join; null off Windows.</summary>
    public ProcessJob? Job { get; }

    /// <summary>The candidate's job, made new: whatever a run before left in a job of this name is ended first.</summary>
    /// <exception cref="DriverException">The system would not make the job, or a job of the name could not be ended: nothing may run, since what it started could not be ended.</exception>
    public static async Task<CandidateJob> OpenAsync(string name)
    {
        if (!OperatingSystem.IsWindows()) return new CandidateJob(name, null);
        try
        {
            await ProcessJob.EndNamedAsync(name).ConfigureAwait(false);
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            throw new DriverException($"the job {name}, which an earlier attempt left, could not be opened to end what it holds ({error.Message}); nothing ran.");
        }

        var job = ProcessJob.Create(name)
                  ?? throw new DriverException($"the system would not make the job {name} to hold the candidate's processes, so they could not be ended together; nothing ran.");
        return new CandidateJob(name, job);
    }

    /// <summary>
    /// End every process the job holds and wait until it holds none, the job left open for the next call. A count that does not read is
    /// ended all the same: "cannot tell" is never "nothing left". Called while the call's place in the admission is still held.
    /// </summary>
    public async Task EndLeftAsync()
    {
        if (Job is null) return;
        Job.Terminate();
        await Job.WaitEmptyAsync().ConfigureAwait(false);
    }

    /// <summary>Ends what is left, then closes the job.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Job is null) return;
        try
        {
            await EndLeftAsync().ConfigureAwait(false);
        }
        finally
        {
            Job.Dispose();
        }
    }
}
