using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RPC1e: <c>daoris-driver opinion ask|stop|anyway|myself</c> through the running Daoris (the terminal RPC design §7.2 and §7.3): each
/// the person's press, sent to the instance, which shows it in its window; <c>opinion show</c> is a read of <c>OPINION_GATE</c>. The
/// terminal prints the lines the route answers (<c>said</c>, the alone path's own words), and this process asks the service nothing.
/// </summary>
public sealed class OpinionThroughInstanceTests
{
    /// <summary>A service holding nothing: each ask is counted, and none is found.</summary>
    private sealed class EmptyService : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"error":"nothing here"}""", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private static Task<int> RunAsync(OpinionCommandAsk ask, EmptyService service, string home, InstanceWorld? instance, TextWriter output)
    {
        var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        return OpinionCommand.RunAsync(
            ask, new OpinionPresses(home, client), client, new SessionProcesses(DaorisHome.Entry(home, HomeEntry.SessionPids)), output, instance);
    }

    /// <summary>Each verb, the ask it reads, the route it sends and the payload it sends it with, beside <c>said: true</c>.</summary>
    private static (OpinionCommandAsk Ask, string Route, string Payload) Verb(string name) => name switch
    {
        "ask" => (new("ask", "s1"), "ASK_OPINION", """{"id":"s1","said":true}"""),
        "ask --reviewer" => (new("ask", "s1") { Reviewer = "codex-acp", Words = "look at the dates" }, "ASK_OPINION",
            """{"id":"s1","reviewer":"codex-acp","words":"look at the dates","said":true}"""),
        "ask --same-agent --failure" => (new("ask", "s1") { SameAgent = true, Failure = true }, "ASK_OPINION",
            """{"id":"s1","sameAgent":true,"occasion":"failure","said":true}"""),
        "stop" => (new("stop", "o1"), "STOP_OPINION", """{"opinion":"o1","said":true}"""),
        "anyway" => (new("anyway", "s1") { Words = "it is fine" }, "OPINION_ANYWAY", """{"id":"s1","words":"it is fine","said":true}"""),
        "myself" => (new("myself", "s1"), "OPINION_MYSELF", """{"id":"s1","said":true}"""),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [InlineData("ask")]
    [InlineData("ask --reviewer")]
    [InlineData("ask --same-agent --failure")]
    [InlineData("stop")]
    [InlineData("anyway")]
    [InlineData("myself")]
    public async Task An_opinion_press_through_the_instance_waits_for_the_window_and_prints_the_lines_it_answers(string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (ask, route, payload) = Verb(name);
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        string[] lines = [$"opinion: said for {name}.", "opinion: and what the gate says after it."];
        standIn.Data = new { said = new TreesSaid(lines, [], 0).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(ask, service, home.Home, home.World("opinion"), output);

        Assert.Equal(0, exit);
        Assert.Equal(string.Concat(lines.Select(line => line + Environment.NewLine)), output.ToString());
        Assert.Equal($"opinion: waiting for you to confirm this in Daoris's window…{Environment.NewLine}", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal(route, press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
    }

    /// <summary><c>opinion show</c> is a read: sent to the open <c>OPINION_GATE</c>, never waiting on the window.</summary>
    [Fact]
    public async Task Show_is_read_through_the_instance_and_waits_on_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new { session = "s1", said = new TreesSaid(["opinion: no second opinion is asked of this work."], [], 0).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(new OpinionCommandAsk("show", "s1"), service, home.Home, home.World("opinion"), output);

        Assert.Equal(0, exit);
        Assert.Equal("opinion: no second opinion is asked of this work." + Environment.NewLine, output.ToString());
        Assert.Equal("", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("OPINION_GATE", press.GetProperty("type").GetString());
        Assert.Equal("""{"id":"s1","said":true}""", press.GetProperty("payload").GetRawText());
    }

    [Theory]
    [InlineData("ask", "refused")]
    [InlineData("stop", "expired")]
    [InlineData("myself", "refused")]
    public async Task An_opinion_press_the_person_refuses_or_leaves_unanswered_is_exit_1_and_does_nothing_here(string name, string code)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (ask, _, _) = Verb(name);
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Refuse;
        standIn.Code = code;
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(ask, service, home.Home, home.World("opinion"), output);

        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
        Assert.Contains($"({code})", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        Assert.Single(standIn.Presses);
    }

    /// <summary>With no instance to look for, a press acts alone as it did, asking the service itself.</summary>
    [Fact]
    public async Task With_no_instance_a_press_acts_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(new OpinionCommandAsk("stop", "o1"), service, home.Home, home.World("opinion"), output);

        Assert.Equal(1, exit);
        Assert.StartsWith("opinion: ", output.ToString(), StringComparison.Ordinal);
        Assert.True(service.Requests > 0);
    }
}
