using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A press that waits for the window says, beside its request id, why the pipe could not place the terminal where its
/// <c>CONFIRMING</c> carries a code, and a code this build does not know says the plain wait.
/// </summary>
public sealed class PlaceNoteWaitTests
{
    private sealed class NoService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("a press through the running Daoris reads no service here");
    }

    [Theory]
    [InlineData("untold", "could not tell this terminal is outside every agent it runs")]
    [InlineData("a-later-code", null)]
    public async Task A_press_that_waits_says_why_the_terminal_was_not_placed(string code, string? why)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        standIn.Confirming = true;
        standIn.StandingWhy = code;
        standIn.Data = new { said = new TreesSaid(["done."], [], 0).ToWire() };
        var ask = SessionsCommand.Read(["stop", "s1a2b3c4"], out var problem);
        Assert.True(problem is null, problem);
        var world = new SessionsWorld(
                new ServiceClient("http://stand-in", null, new HttpClient(new NoService())), running.Home, DriverConfig.Empty, false, null)
            { Instance = running.World("sessions") };

        await SessionsCommand.RunAsync(ask!, world, new StringWriter());

        var said = running.Error.ToString();
        if (why is null) Assert.Matches(InstanceStandIn.Waiting("sessions"), said);
        else Assert.Matches(@"sessions: waiting for you to confirm this in Daoris's window \(request [0-9a-f-]{36}; ", said);
        if (why is not null) Assert.Contains(why, said, StringComparison.Ordinal);
    }
}
