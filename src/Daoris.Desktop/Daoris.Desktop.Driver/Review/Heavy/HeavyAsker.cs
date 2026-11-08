using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The process a heavy run is for, as its slot names its holder and its waiter: the run lives while it does. A terminal's run is
/// its own; one the running Daoris runs for a terminal is still the terminal's, since the run ends with that terminal's connection.
/// </summary>
/// <param name="Pid">Its process.</param>
/// <param name="Started">Its start in UTC ticks, so a process id used again is not taken for it.</param>
public sealed record HeavyAsker(int Pid, long Started)
{
    /// <summary>This process.</summary>
    public static HeavyAsker ThisProcess()
    {
        using var self = Process.GetCurrentProcess();
        return new(self.Id, self.StartTime.ToUniversalTime().Ticks);
    }
}
