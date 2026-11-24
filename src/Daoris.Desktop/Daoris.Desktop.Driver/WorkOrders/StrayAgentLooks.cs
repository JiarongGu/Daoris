namespace Daoris.Driver;

/// <summary>
/// The look's reaper of agent processes left running after their session ended (<see cref="StrayAgents"/>): each is said in the machine
/// log with its id, agent, pin and age, and ended after its grace, or at once where its pin has moved on. Only processes Daoris recorded
/// as it started them are ever read.
/// </summary>
[LookPass("stray-agents", LookPhase.After, "browser-sign-ins")]
internal sealed class StrayAgentsLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        IReadOnlySet<string> active;
        try
        {
            active = (await wiring.Service.ActiveSessionsAsync(ct).ConfigureAwait(false)).Select(session => session.Id).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // A service that did not answer says nothing of which sessions have ended.
            return;
        }

        var settings = wiring.Harnesses.Settings;
        var reaper = new StrayAgents(
            DaorisHome.Entry(wiring.Home, HomeEntry.SessionPids), DaorisHome.Entry(wiring.Home, HomeEntry.Toolchain),
            agent => PinsOf(settings, agent), () => wiring.Clock.GetUtcNow(), wiring.Log);
        round.Events.AddRange(reaper.Look(active, wiring.Processes.RunsHere));
    }

    /// <summary>The versions pinned for an agent, the machine's and each workspace's.</summary>
    internal static IReadOnlySet<string> PinsOf(HarnessSettings settings, string agent)
    {
        var pins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (settings.Versions.TryGetValue(agent, out var version)) pins.Add(version);
        foreach (var circle in settings.WorkspaceVersions.Values)
        {
            if (circle.TryGetValue(agent, out var pinned)) pins.Add(pinned);
        }

        return pins;
    }
}
