namespace Daoris.Driver;

/// <summary>
/// The plugins, read fresh each look like the config (D64): a harness declared since the last look is spawnable now, a server
/// declared since is handed now, a hook process disabled since is stopped now. The catalogue refuses a name this build carries
/// before anything of that plugin is taken.
/// </summary>
[LookPass("plugins", LookPhase.Local)]
internal sealed class PluginReload(IPluginWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct) => wiring.Stages.TimeAsync("plugins", async () =>
    {
        var catalog = PluginCatalog.Load(wiring.Home, wiring.Built.Names);
        wiring.Adopt(catalog);
        if (wiring.Hooks is { } hooks)
        {
            round.Events.AddRange(await hooks.ReconcileAsync(catalog, ct, wiring.Lines).ConfigureAwait(false));
        }
    });
}
