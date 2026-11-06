namespace Daoris.Driver;

/// <summary>
/// What a look settles on the machine before it reads anything: a cool-off the file would not take, written again once per look and
/// held in memory meanwhile so the account stays passed until its reset; what an update's drain held, said again if a later drain
/// holds it once this look does not drain; and the notices the file keeps, moved into the desktop store.
/// </summary>
[LookPass("kept", LookPhase.Before)]
internal sealed class LookKept(ILookStagesWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        AccountCooling.WriteUnkept(wiring.Home, wiring.Context.Harnesses.Clock());
        if (!wiring.Config.Draining) wiring.Runs.DrainLifted();

        Notices.ImportFile(wiring.Home);
        return Task.CompletedTask;
    }
}
