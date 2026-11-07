namespace Daoris.Driver;

/// <summary>
/// The one reader of <see cref="OwnedProcesses.Census"/> (D249): as this process stops, a <c>processes.running</c> line says which purposes
/// still hold a process and the ids they hold, so a process that outlived its work is found in the log and the job's kill at the end is seen
/// to have had something to end. Written only where something runs; it reads and ends nothing.
/// </summary>
public static class RunningProcessesLine
{
    /// <summary>The log event.</summary>
    public const string Event = "processes.running";

    /// <summary>Write the line for what <paramref name="owned"/> holds now, or nothing where it holds nothing.</summary>
    public static void Write(MachineLog log, OwnedProcesses owned)
    {
        var running = owned.Census();
        if (running.Count == 0) return;
        log.Info(Event, [.. running.Select(each => (each.Purpose.ToString().ToLowerInvariant(), (object?)string.Join(',', each.Ids)))]);
    }
}
