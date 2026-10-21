using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RPC1e: <c>daoris-driver ask channel &lt;id&gt; "…"</c> through the running Daoris (the terminal RPC design §7.2 and §7.3): a post is the
/// person's words, sent to the instance's <c>CHANNEL_POST</c>, which shows it in its window; the terminal prints the lines the route
/// answers (<c>said</c>, the alone path's own words), and this process asks the service nothing. A read stays the service's.
/// </summary>
public sealed class ChannelThroughInstanceTests
{
    /// <summary>A service holding no ask: each ask is counted, and none is found.</summary>
    private sealed class EmptyService : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"error":"not-found","code":"not-found"}""", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private static Task<int> RunAsync(ChannelAsk ask, EmptyService service, InstanceWorld? instance, TextWriter output) =>
        ChannelCommand.RunAsync(ask, new ServiceClient("http://stand-in", null, new HttpClient(service)), output, instance);

    [Theory]
    [InlineData(null, null, false, """{"ask":"a1","text":"The v3 one.","said":true}""")]
    [InlineData("db", "m1", false, """{"ask":"a1","text":"The v3 one.","to":"db","re":"m1","said":true}""")]
    [InlineData(null, null, true, """{"ask":"a1","text":"The v3 one.","ordinary":true,"said":true}""")]
    public async Task A_post_through_the_instance_waits_for_the_window_and_prints_the_lines_it_answers(string? to, string? re, bool ordinary, string payload)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        const string Line = "Posted to ask #a1's channel, for everyone, urgent: each session it reaches reads it at its next step.";
        standIn.Data = new { posted = true, said = new TreesSaid([Line], [], 0).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(new ChannelAsk("a1", to, re, "The v3 one.", ordinary), service, home.World("ask"), output);

        Assert.Equal(0, exit);
        Assert.Equal(Line + Environment.NewLine, output.ToString());
        Assert.Equal($"ask: waiting for you to confirm this in Daoris's window…{Environment.NewLine}", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("CHANNEL_POST", press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
    }

    [Theory]
    [InlineData("refused")]
    [InlineData("expired")]
    public async Task A_post_the_person_refuses_or_leaves_unanswered_is_exit_1_and_posts_nothing_here(string code)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Refuse;
        standIn.Code = code;
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(new ChannelAsk("a1", null, null, "hello"), service, home.World("ask"), output);

        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
        Assert.Contains($"({code})", home.Error.ToString());
        Assert.Equal(0, service.Requests);
    }

    /// <summary>
    /// A read stays the service's beside a running Daoris: it moves the person's own mark, the service's door and their press, which the
    /// service writes once whoever asks; nothing is sent to the instance.
    /// </summary>
    [Fact]
    public async Task A_read_stays_the_service_s_beside_a_running_Daoris()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(new ChannelAsk("a1", null, null, null), service, home.World("ask"), output);

        Assert.Equal(1, exit);
        Assert.Equal("No ask #a1 here." + Environment.NewLine, output.ToString());
        Assert.Empty(standIn.Presses);
        Assert.True(service.Requests > 0);
    }
}
