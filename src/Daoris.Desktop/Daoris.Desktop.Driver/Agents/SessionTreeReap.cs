namespace Daoris.Driver;

/// <summary>
/// The end of a session's tree, whichever way the session ended: whatever its job still holds is ended with its tracking, the processes
/// its agent started and left running (a background command, a watcher) among them, and the count is what the machine log says.
/// </summary>
internal static class SessionTreeReap
{
    /// <summary>End every process the job still holds, and say how many that was; none where there is no job or nothing runs in it.</summary>
    internal static int End(ProcessJob? job)
    {
        var held = job?.Active ?? 0;
        if (held == 0) return 0;
        job!.Terminate();
        return held;
    }
}
