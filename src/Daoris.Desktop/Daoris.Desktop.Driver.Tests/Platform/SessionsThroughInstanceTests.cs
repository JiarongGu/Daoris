using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver sessions</c>' list through the running Daoris (RPCOPEN1, RPC1e, the terminal RPC design §7.2 and §7.3): its own
/// <c>SESSION_GROUPS</c> look, answered as lines the verb prints, and this process's own look where none answers, both from
/// <see cref="SessionsCommand.ListLines"/>.
/// </summary>
public sealed class SessionsThroughInstanceTests
{
    /// <summary>A service holding no record and no quest: what asking it reads is nothing, and each ask is counted.</summary>
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

    private static SessionsWorld World(EmptyService service, string home, InstanceWorld? instance) =>
        new(new ServiceClient("http://stand-in", null, new HttpClient(service)), home, DriverConfig.Empty, SessionWire.Pipe, null) { Instance = instance };

    [Fact]
    public async Task Through_the_instance_the_list_is_the_lines_it_answers_and_this_process_reads_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        var lines = new[] { "Waiting on you (1)", "  s1a2b3c4  waiting on you  engine — Fix the build · 3m" };
        standIn.Data = new { sessions = Array.Empty<object>(), entries = Array.Empty<object>(), said = new TreesSaid(lines, [], 0).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(
            new SessionsAsk("list") { Group = SessionGroup.You, Repository = "engine", Json = false },
            World(service, home.Home, home.World("sessions")), output);

        Assert.Equal(0, exit);
        Assert.Equal(string.Join(Environment.NewLine, lines) + Environment.NewLine, output.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("SESSION_GROUPS", press.GetProperty("type").GetString());
        var payload = press.GetProperty("payload");
        Assert.Equal(SessionGroup.You, payload.GetProperty("group").GetString());
        Assert.Equal("engine", payload.GetProperty("repository").GetString());
        Assert.True(payload.GetProperty("said").GetBoolean());
    }

    [Fact]
    public async Task With_no_instance_the_list_is_this_processs_own_look_in_the_same_words()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var service = new EmptyService();
        var alone = new StringWriter();
        var aloneExit = await SessionsCommand.RunAsync(new SessionsAsk("list"), World(service, home.Home, home.World("sessions")), alone);

        var without = new StringWriter();
        var withoutExit = await SessionsCommand.RunAsync(new SessionsAsk("list"), World(new EmptyService(), home.Home, null), without);

        Assert.Equal(0, aloneExit);
        Assert.Equal(aloneExit, withoutExit);
        Assert.Equal("sessions: no session on this machine." + Environment.NewLine, alone.ToString());
        Assert.Equal(alone.ToString(), without.ToString());
        Assert.True(service.Requests > 0);
    }

    [Fact]
    public async Task An_answer_with_no_lines_is_read_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        home.Serve().Data = new { sessions = Array.Empty<object>(), entries = Array.Empty<object>() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(new SessionsAsk("list"), World(service, home.Home, home.World("sessions")), output);

        Assert.Equal(0, exit);
        Assert.Equal("sessions: no session on this machine." + Environment.NewLine, output.ToString());
        Assert.True(service.Requests > 0);
    }
}
