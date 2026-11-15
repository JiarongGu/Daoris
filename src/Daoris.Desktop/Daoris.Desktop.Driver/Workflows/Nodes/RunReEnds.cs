using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// A run at its graph's end that a session still waits on is ended again at the next look, through the run's one end
/// (<see cref="RunSessionEnds.FinishAsync"/>): an end the service refused once, and so paid, is never owed again, and the run's end fires
/// only on its graph's transition to it. One with nothing waiting does nothing and says so once (<see cref="EmptyRunEnds.Nothing"/>).
/// </summary>
public static class RunReEnds
{
    /// <summary>Ends the run again where its record holds an end and a session of this machine still awaits a step of it.</summary>
    public static async Task AgainAsync(
        RunEndPorts ports, string run, RunEnd? ended, IEnumerable<QuestView> quests, bool passed, List<string> said, CancellationToken ct)
    {
        if (ended is null || ports.Finish is null || ports.Sessions is null) return;
        var records = await ports.Sessions(ct).ConfigureAwait(false);
        if (records is null) return;
        if (RunWaiters.Of(run, records).Count == 0)
        {
            RunSessionEnds.Quiet(ports, run, EmptyRunEnds.Nothing);
            return;
        }

        await RunSessionEnds.FinishAsync(ports, run, ended, quests, passed, said, ct).ConfigureAwait(false);
    }
}
