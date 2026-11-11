namespace Daoris.Driver;

/// <summary>
/// The look's claim of the plugin commands the person did not watch or that failed (<see cref="PluginCommandNotices"/>), so each is in
/// <i>Done and failed</i> once, the news keeping a dismissed one dismissed.
/// </summary>
[LookPass("plugin-commands", LookPhase.After, "run-outcomes")]
internal sealed class PluginCommandsLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var occurrences = PluginCommandNotices.Occurrences(wiring.Home, now);
        if (occurrences.Count == 0) return Task.CompletedTask;
        var claimed = new News(wiring.Home).Claim(PluginCommandNotices.Teller, occurrences, null, now);
        if (claimed.Told.Count > 0) round.Events.Add($"{claimed.Told.Count} plugin command(s) kept in Done and failed.");
        return Task.CompletedTask;
    }
}
