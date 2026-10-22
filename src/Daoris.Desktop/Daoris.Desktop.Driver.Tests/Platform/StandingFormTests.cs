using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TERMTRUST1a part (ii) (D201 point 2, the terminal-trust design §2.1 and §8 case 6): each press the standing confirmation names is
/// held to what its verb sends through the running Daoris, byte for byte, so a verb that grows a field fails here before its press
/// stops being covered. The modules' <c>StandingFormTests</c> holds the same payloads covered by the judge: a twin, each table naming the
/// other. And the terminal says what the pipe told it: that its press ran under the grant, or why the grant did not cover it.
/// </summary>
public sealed class StandingFormTests
{
    /// <summary>The forms as the verbs send them; the modules' <c>StandingFormTests.Sent</c> is the same table.</summary>
    public static TheoryData<string, string, string> Sent => new()
    {
        { "sessions stop s1a2b3c4", "STOP_SESSION", """{"id":"s1a2b3c4","said":true}""" },
        { "sessions finish s1a2b3c4", "RESOLVE_SESSION", """{"id":"s1a2b3c4","state":"completed","said":true}""" },
        { "driver hold engine", "SET_HOLD", """{"repository":"engine","held":true}""" },
        { "driver resume engine", "SET_HOLD", """{"repository":"engine","held":false}""" },
    };

    private sealed class NoService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("a press through the running Daoris reads no service here");
    }

    private static async Task<int> RunAsync(string words, InstanceTestHome running)
    {
        var args = words.Split(' ');
        if (args[0] == "sessions")
        {
            var ask = SessionsCommand.Read(args[1..], out var problem);
            Assert.True(problem is null, problem);
            var world = new SessionsWorld(
                    new ServiceClient("http://stand-in", null, new HttpClient(new NoService())), running.Home, DriverConfig.Empty, SessionWire.Pipe, null)
                { Instance = running.World("sessions") };
            return await SessionsCommand.RunAsync(ask!, world, new StringWriter());
        }

        var instance = running.World("daoris");
        return await DriverChoicesCommand.RunAsync(args[1..], new DriverChoicesWorld
        {
            Instance = new InstanceWorld
            {
                Home = running.Home,
                Environment = _ => null,
                BaseDirectory = instance.BaseDirectory,
                Status = instance.Status,
                Out = running.Out,
                Error = running.Error,
                Verb = "daoris",
                Answer = instance.Answer,
                Wait = instance.Wait,
            },
            ConfigPath = DaorisHome.Entry(running.Home, HomeEntry.DriverConfig),
        }, CancellationToken.None);
    }

    [Theory]
    [MemberData(nameof(Sent))]
    public async Task Each_verb_sends_exactly_the_form_its_press_is_covered_in_and_says_it_ran_under_the_grant(string words, string route, string payload)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        standIn.Standing = true;
        standIn.Data = new { said = new TreesSaid(["done."], [], 0).ToWire() };

        var exit = await RunAsync(words, running);

        Assert.Equal(0, exit);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal(route, press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
        Assert.Contains(InstanceDoor.RanStanding, running.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("waiting for you", running.Error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>§5 step 5: a press the grant names that still waits says why, and a code this build does not know says the plain wait.</summary>
    [Theory]
    [InlineData("in-agent-job", "does not cover a press from inside an agent Daoris runs")]
    [InlineData("untold", "could not tell this terminal is outside every agent it runs")]
    [InlineData("a-later-code", null)]
    public async Task A_press_that_waits_anyway_says_why(string code, string? why)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        standIn.Confirming = true;
        standIn.StandingWhy = code;
        standIn.Data = new { said = new TreesSaid(["done."], [], 0).ToWire() };

        await RunAsync("sessions stop s1a2b3c4", running);

        var said = running.Error.ToString();
        Assert.Contains(
            why is null
                ? "sessions: waiting for you to confirm this in Daoris's window…"
                : "sessions: waiting for you to confirm this in Daoris's window (",
            said, StringComparison.Ordinal);
        if (why is not null) Assert.Contains(why, said, StringComparison.Ordinal);
    }
}
