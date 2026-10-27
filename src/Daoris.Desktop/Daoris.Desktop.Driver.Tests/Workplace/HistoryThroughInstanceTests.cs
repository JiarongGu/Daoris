using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RPC1e: <c>daoris-driver history</c>, <c>history clear --workspace</c>, <c>quest clear</c> and <c>ask --clear</c> through the running
/// Daoris (the terminal RPC design §7.2 and §7.3). A clear's <c>--yes</c> is the person's press, sent to the instance, which shows it in
/// its window; the reading and a clear's list are reads of <c>HISTORY_PLAN</c>. The terminal prints the lines the route answers
/// (<c>said</c>, the alone path's own words), and this process asks the service nothing.
/// </summary>
public sealed class HistoryThroughInstanceTests
{
    /// <summary>A service holding no record: what asking it reads is nothing, and each ask is counted.</summary>
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

    private static HistoryWorld World(EmptyService service, string home, InstanceWorld? instance) =>
        new(new ServiceClient("http://stand-in", null, new HttpClient(service)), home, DaorisHome.Entry(home, HomeEntry.DriverConfig),
            new SessionProcesses(DaorisHome.Entry(home, HomeEntry.SessionPids))) { Instance = instance };

    /// <summary>Each verb, the ask it reads, the route it sends and the payload it sends it with, beside <c>said: true</c>.</summary>
    private static (HistoryAsk Ask, string Route, string Payload) Verb(string name) => name switch
    {
        "history clear --yes" => (new(HistoryScope.Workspace, "aurora") { Clear = true, Yes = true }, "HISTORY_CLEAR",
            """{"workspace":"aurora","said":true}"""),
        "quest clear --yes" => (new(HistoryScope.Quest, "q1") { Clear = true, Yes = true }, "HISTORY_CLEAR", """{"quest":"q1","said":true}"""),
        "quest clear --failed --yes" => (new(HistoryScope.Failed, "q1") { Clear = true, Yes = true }, "HISTORY_CLEAR",
            """{"quest":"q1","failed":true,"said":true}"""),
        "ask --clear --yes" => (new(HistoryScope.Ask, "a1") { Clear = true, Yes = true }, "HISTORY_CLEAR", """{"ask":"a1","said":true}"""),
        "history" => (new(HistoryScope.Workspace, null), "HISTORY_PLAN", """{"said":true}"""),
        "history --workspace --json" => (new(HistoryScope.Workspace, "aurora") { Json = true }, "HISTORY_PLAN",
            """{"workspace":"aurora","json":true,"said":true}"""),
        "quest clear" => (new(HistoryScope.Quest, "q1") { Clear = true }, "HISTORY_PLAN", """{"quest":"q1","clear":true,"said":true}"""),
        "quest clear --failed" => (new(HistoryScope.Failed, "q1") { Clear = true }, "HISTORY_PLAN",
            """{"quest":"q1","failed":true,"clear":true,"said":true}"""),
        "history clear" => (new(HistoryScope.Workspace, "aurora") { Clear = true }, "HISTORY_PLAN",
            """{"workspace":"aurora","clear":true,"said":true}"""),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [InlineData("history clear --yes")]
    [InlineData("quest clear --yes")]
    [InlineData("quest clear --failed --yes")]
    [InlineData("ask --clear --yes")]
    public async Task A_clear_through_the_instance_waits_for_the_window_and_prints_the_lines_it_answers(string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (ask, route, payload) = Verb(name);
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        string[] lines = [$"daoris-driver: said for {name}.", "  and its second line"];
        standIn.Data = new { said = new TreesSaid(lines, [], 0).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await HistoryCommand.RunAsync(ask, World(service, home.Home, home.World()), output);

        Assert.Equal(0, exit);
        Assert.Equal(string.Concat(lines.Select(line => line + Environment.NewLine)), output.ToString());
        Assert.Matches(InstanceStandIn.WaitingAlone("daoris-driver"), home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal(route, press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
    }

    /// <summary>The reading and a clear's list are reads: sent to the open <c>HISTORY_PLAN</c>, never waiting on the window.</summary>
    [Theory]
    [InlineData("history")]
    [InlineData("history --workspace --json")]
    [InlineData("quest clear")]
    [InlineData("quest clear --failed")]
    [InlineData("history clear")]
    public async Task A_reading_or_a_list_is_read_through_the_instance_and_waits_on_nothing(string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (ask, route, payload) = Verb(name);
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new { said = new TreesSaid([$"daoris-driver: read for {name}."], [], 1).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await HistoryCommand.RunAsync(ask, World(service, home.Home, home.World()), output);

        Assert.Equal(1, exit);
        Assert.Equal($"daoris-driver: read for {name}.{Environment.NewLine}", output.ToString());
        Assert.Equal("", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal(route, press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
    }

    [Theory]
    [InlineData("history clear --yes", "refused")]
    [InlineData("ask --clear --yes", "expired")]
    public async Task A_clear_the_person_refuses_or_leaves_unanswered_is_exit_1_and_does_nothing_here(string name, string code)
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

        var exit = await HistoryCommand.RunAsync(ask, World(service, home.Home, home.World()), output);

        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
        Assert.Contains($"({code})", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        Assert.Single(standIn.Presses);
    }

    /// <summary>With no instance running, the reading is this process's own, in the same words, as it was.</summary>
    [Fact]
    public async Task With_no_instance_the_reading_is_this_process_s_own()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await HistoryCommand.RunAsync(new HistoryAsk(HistoryScope.Workspace, null), World(service, home.Home, home.World()), output);

        Assert.Equal(0, exit);
        Assert.Equal("daoris-driver: nothing finished is kept on this machine: no record names a workspace." + Environment.NewLine, output.ToString());
        Assert.True(service.Requests > 0);
    }
}
