namespace Daoris.Driver;

/// <summary>
/// The claims this process's own acts threw out of, by their jobs (one to each claim): their run's look takes them over though they are its own,
/// and reads the phase each stands at as a dead run's. Held in memory: a restart is a new run, whose looks take every claim of the old one.
/// </summary>
internal static class PoolAbandoned
{
    private static readonly HashSet<string> Jobs = [];

    internal static bool Holds(string job)
    {
        lock (Jobs) return Jobs.Contains(job);
    }

    internal static void Forget(string job)
    {
        lock (Jobs) Jobs.Remove(job);
    }

    /// <summary>The act, its claim marked abandoned where it throws, however it throws.</summary>
    internal static async Task<PoolDone> GuardAsync(string job, Func<Task<PoolDone>> act)
    {
        try
        {
            return await act().ConfigureAwait(false);
        }
        catch
        {
            lock (Jobs) Jobs.Add(job);
            throw;
        }
    }
}
