using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RPC1e: <c>daoris-driver setup &lt;repository&gt; [--plan]</c> through the running Daoris (the terminal RPC design §7.2 and §7.3): the
/// press is the person's, sent to the instance's <c>SETUP_PRESS</c>, which shows it in its window; <c>--plan</c> is a read of
/// <c>SETUP_PLAN</c>. The terminal prints the lines the route answers (<c>said</c>, the alone path's own words), and this process asks
/// the service nothing.
/// </summary>
public sealed class SetupThroughInstanceTests
{
    /// <summary>A service holding nothing, each ask counted.</summary>
    private sealed class EmptyService : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private static Task<int> RunAsync(string[] args, EmptyService service, string home, InstanceWorld? instance, TextWriter output) =>
        SetupCommand.RunAsync(
            args, output, new SetupWorld(new ServiceClient("http://stand-in", null, new HttpClient(service)), home), DriverConfig.Empty,
            SessionWire.Pipe, home, new DateOnly(2026, 10, 22), instance);

    [Fact]
    public async Task The_press_through_the_instance_waits_for_the_window_and_prints_the_lines_it_answers()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        string[] lines = ["setup: `atlas`, in workspace `work`", "Asked as `#a1b2c3d4` in `work`."];
        standIn.Data = new { published = true, said = new TreesSaid(lines, [], 0).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(["atlas"], service, home.Home, home.World("setup"), output);

        Assert.Equal(0, exit);
        Assert.Equal(string.Concat(lines.Select(line => line + Environment.NewLine)), output.ToString());
        Assert.Equal($"setup: waiting for you to confirm this in Daoris's window…{Environment.NewLine}", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("SETUP_PRESS", press.GetProperty("type").GetString());
        Assert.Equal("""{"repository":"atlas","said":true}""", press.GetProperty("payload").GetRawText());
    }

    [Fact]
    public async Task The_plan_is_read_through_the_instance_and_waits_on_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new { pressable = false, said = new TreesSaid(["setup: `atlas`", "refused:"], [], 1).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(["atlas", "--plan"], service, home.Home, home.World("setup"), output);

        Assert.Equal(1, exit);
        Assert.Equal($"setup: `atlas`{Environment.NewLine}refused:{Environment.NewLine}", output.ToString());
        Assert.Equal("", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("SETUP_PLAN", press.GetProperty("type").GetString());
        Assert.Equal("""{"repository":"atlas","said":true}""", press.GetProperty("payload").GetRawText());
    }

    [Theory]
    [InlineData("refused")]
    [InlineData("expired")]
    public async Task A_press_the_person_refuses_or_leaves_unanswered_is_exit_1_and_publishes_nothing_here(string code)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Refuse;
        standIn.Code = code;
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(["atlas"], service, home.Home, home.World("setup"), output);

        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
        Assert.Contains($"({code})", home.Error.ToString());
        Assert.Equal(0, service.Requests);
    }

    /// <summary>Words the verb does not take are its usage, said before anything is sent.</summary>
    [Fact]
    public async Task Words_it_does_not_take_are_its_usage_before_anything_is_sent()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        var output = new StringWriter();

        var exit = await RunAsync(["atlas", "engine"], new EmptyService(), home.Home, home.World("setup"), output);

        Assert.Equal(2, exit);
        Assert.StartsWith("setup: one repository at a time", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(standIn.Presses);
    }
}
