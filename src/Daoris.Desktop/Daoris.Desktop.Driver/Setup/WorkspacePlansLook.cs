namespace Daoris.Driver;

/// <summary>
/// The workspace plans, worked before the look reads the quests, so a set-up a plan publishes now is planned,
/// and started, in this same look. Nothing is read unless a plan works and is not paused.
/// </summary>
[LookPass("setup", LookPhase.Local, "together")]
internal sealed class WorkspacePlansLook(ISetupWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct) => wiring.Stages.TimeAsync("setup", () => WorkAsync(round.Events, ct));

    /// <summary>
    /// Every workspace plan under the home worked once: the next set-up published while fewer than its
    /// <c>atOnce</c> are open, a refusal skipped, the pilot's pause taken, each said in <paramref name="events"/>. A plan the
    /// service could not answer for is said, and tried again at the next look: a plan is never a dead look.
    /// </summary>
    private async Task WorkAsync(List<string> events, CancellationToken ct)
    {
        try
        {
            var world = wiring.SetupPlans?.Invoke() ?? new WorkspaceSetupWorld(wiring.Service, wiring.Home);
            events.AddRange(await WorkspaceSetup.TickAsync(
                world, wiring.Config, wiring.Unadopted(), wiring.Home, DateOnly.FromDateTime(DateTime.Now), DateTimeOffset.UtcNow, ct, wiring.Lines).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                          or IOException or UnauthorizedAccessException)
        {
            events.Add(wiring.Lines.Retry(
                "setup", $"setup  the workspace plans could not be worked this look, and are tried again at the next: {error.Message}"));
        }
    }
}
