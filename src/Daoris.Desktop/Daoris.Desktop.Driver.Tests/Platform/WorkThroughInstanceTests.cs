using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RPC1e: <c>daoris-driver ask --pause|--resume|--abandon</c> and <c>quest pause|resume|abandon</c> through the running Daoris (the
/// terminal RPC design §7.2 and §7.3). A pause, a resume and an abandon's <c>--yes</c> are the person's presses, sent to the instance,
/// which shows each in its window; the abandon's list is a read. The terminal prints the lines the route answers (<c>said</c>, the
/// alone path's own words), and this process asks the service nothing.
/// </summary>
public sealed class WorkThroughInstanceTests
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

    private static WorkWorld World(EmptyService service, string home, InstanceWorld? instance) =>
        new(new ServiceClient("http://stand-in", null, new HttpClient(service)), home, DaorisHome.Entry(home, HomeEntry.DriverConfig),
            new SessionProcesses(DaorisHome.Entry(home, HomeEntry.SessionPids))) { Instance = instance };

    /// <summary>Each verb, the ask it reads, the route it sends and the payload it sends it with, beside <c>said: true</c>.</summary>
    private static (WorkAsk Ask, string Route, string Payload) Verb(string name) => name switch
    {
        "ask-pause" => (new(WorkScope.Ask, "pause", "a1"), "WORK_PAUSE", """{"ask":"a1","said":true}"""),
        "quest-pause" => (new(WorkScope.Quest, "pause", "q1"), "WORK_PAUSE", """{"quest":"q1","said":true}"""),
        "ask-resume" => (new(WorkScope.Ask, "resume", "a1"), "WORK_RESUME", """{"ask":"a1","said":true}"""),
        "quest-resume" => (new(WorkScope.Quest, "resume", "q1"), "WORK_RESUME", """{"quest":"q1","said":true}"""),
        "ask-abandon" => (new(WorkScope.Ask, "abandon", "a1") { Reason = "not now", Yes = true }, "WORK_ABANDON",
            """{"ask":"a1","reason":"not now","said":true}"""),
        "quest-abandon" => (new(WorkScope.Quest, "abandon", "q1") { Reason = "not now", Yes = true }, "WORK_ABANDON",
            """{"quest":"q1","reason":"not now","said":true}"""),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [InlineData("ask-pause")]
    [InlineData("quest-pause")]
    [InlineData("ask-resume")]
    [InlineData("quest-resume")]
    [InlineData("ask-abandon")]
    [InlineData("quest-abandon")]
    public async Task A_work_press_through_the_instance_waits_for_the_window_and_prints_the_lines_it_answers(string name)
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

        var exit = await WorkCommand.RunAsync(ask, World(service, home.Home, home.World()), output);

        Assert.Equal(0, exit);
        Assert.Equal(string.Concat(lines.Select(line => line + Environment.NewLine)), output.ToString());
        Assert.Matches(InstanceStandIn.WaitingAlone("daoris-driver"), home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal(route, press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
    }

    /// <summary>The exit code the route answers is the verb's: a pause that could not reach a session is exit 2, as alone.</summary>
    [Fact]
    public async Task The_exit_code_the_route_answers_is_the_verb_s()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new { said = new TreesSaid(["daoris-driver: paused ask #a1."], [], 2).ToWire() };

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Ask, "pause", "a1"), World(new EmptyService(), home.Home, home.World()), new StringWriter());

        Assert.Equal(2, exit);
    }

    /// <summary>An abandon's list is a read: sent to the open <c>WORK_PLAN</c>, never waiting on the window.</summary>
    [Fact]
    public async Task An_abandon_s_list_is_read_through_the_instance_and_waits_on_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new { said = new TreesSaid(["daoris-driver: abandoning #q1 would take 1 quest."], [], 0).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Quest, "abandon", "q1"), World(service, home.Home, home.World()), output);

        Assert.Equal(0, exit);
        Assert.Equal("daoris-driver: abandoning #q1 would take 1 quest." + Environment.NewLine, output.ToString());
        Assert.Equal("", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("WORK_PLAN", press.GetProperty("type").GetString());
        Assert.Equal("""{"quest":"q1","said":true}""", press.GetProperty("payload").GetRawText());
    }

    [Theory]
    [InlineData("ask-pause", "refused")]
    [InlineData("quest-resume", "expired")]
    [InlineData("quest-abandon", "refused")]
    public async Task A_work_press_the_person_refuses_or_leaves_unanswered_is_exit_1_and_does_nothing_here(string name, string code)
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

        var exit = await WorkCommand.RunAsync(ask, World(service, home.Home, home.World()), output);

        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
        Assert.Contains($"({code})", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        Assert.Single(standIn.Presses);
    }

    /// <summary>An abandon's <c>--yes</c> without its reason is said before anything is sent, in the alone path's words.</summary>
    [Fact]
    public async Task An_abandon_without_its_reason_is_told_so_before_anything_is_sent()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(
            new WorkAsk(WorkScope.Quest, "abandon", "q1") { Yes = true }, World(new EmptyService(), home.Home, home.World()), output);

        Assert.Equal(1, exit);
        Assert.Equal($"daoris-driver: {WorkAbandoning.NeedsReason}{Environment.NewLine}", output.ToString());
        Assert.Empty(standIn.Presses);
    }

    /// <summary>With no instance running, a resume acts alone, as it did, and says what it said.</summary>
    [Fact]
    public async Task With_no_instance_a_resume_acts_alone_and_says_what_it_said()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Quest, "resume", "q1"), World(service, home.Home, home.World()), output);

        Assert.Equal(1, exit);
        Assert.Equal($"daoris-driver: {WorkPausing.Unknown(WorkScope.Quest, "q1")}{Environment.NewLine}", output.ToString());
        Assert.True(service.Requests > 0);
    }
}
