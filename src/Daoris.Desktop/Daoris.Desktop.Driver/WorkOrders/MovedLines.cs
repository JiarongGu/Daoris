namespace Daoris.Driver;

/// <summary>
/// The lines Daoris moved since the look last followed each, registered from the line before the look reads the
/// registry, so a set-up that just landed is planned by what it now declares. Nothing is read with git, or asked of the service,
/// unless a line moved.
/// </summary>
[LookPass("lines", LookPhase.Local, "watch")]
internal sealed class MovedLines(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct) => wiring.Stages.TimeAsync("lines", () => FollowAsync(round.Events, ct));

    /// <summary>
    /// Each repository whose line Daoris moved since the look last followed it, registered from its line, with what
    /// changed or must be fixed added to <paramref name="events"/>. A pass the service did not answer is said, and the
    /// lines stay due for the next look: following is never a dead look.
    /// </summary>
    private async Task FollowAsync(List<string> events, CancellationToken ct)
    {
        var due = RegistryFollowing.Due(wiring.Home);
        if (due.Count == 0) return;
        try
        {
            var report = await RegistrationFollow.FollowAsync(new RegistrationWorld(wiring.Service, wiring.Home, wiring.Config), due, ct).ConfigureAwait(false);
            events.AddRange(report.Followed.Select(followed => RegistrationFollow.EventLine(followed, wiring.Lines)).OfType<string>());
            if (report.Refresh is { } refresh) events.Add(wiring.Lines.Retry("registry", $"registry  the index was not read again after registering: {refresh}"));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            events.Add(wiring.Lines.Retry(
                "registry", $"registry  the lines Daoris moved could not be followed this look, and are tried again at the next: {error.Message}"));
        }
    }
}
