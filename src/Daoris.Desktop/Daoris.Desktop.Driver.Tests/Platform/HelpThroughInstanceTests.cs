using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RPC1e: <c>daoris-driver help</c> through the running Daoris (the terminal RPC design §7.2 and §7.3). <c>rename</c>, <c>pin</c>,
/// <c>unpin</c> and <c>delete --yes</c> are the person's presses, sent to the instance, which shows each in its window; <c>list</c> is a
/// read. The terminal prints the lines the route answers (<c>said</c>, the alone path's own words), and this process asks the service
/// nothing. <c>resume</c> stays this terminal's own until RPC1g, and a delete's first ask reads alone.
/// </summary>
public sealed class HelpThroughInstanceTests
{
    private const string Id = "c1a2b3c4";

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

    private static SessionsWorld World(EmptyService service, string home, InstanceWorld? instance) =>
        new(new ServiceClient("http://stand-in", null, new HttpClient(service)), home, DriverConfig.Empty, SessionWire.Pipe, null) { Instance = instance };

    private static Task<int> RunAsync(HelpVerbAsk ask, EmptyService service, string home, InstanceWorld? instance, TextWriter output) =>
        HelpCommand.RunAsync(ask, World(service, home, instance), _ => true, output);

    /// <summary>Each verb, the ask it reads, the route it sends and the payload it sends it with, beside <c>said: true</c>.</summary>
    private static (HelpVerbAsk Ask, string Route, string Payload) Verb(string name) => name switch
    {
        "rename" => (new("rename") { Id = Id, Text = "Workspaces" }, "HELP_RENAME", $$"""{"id":"{{Id}}","name":"Workspaces","said":true}"""),
        "rename --clear" => (new("rename") { Id = Id, Clear = true }, "HELP_RENAME", $$"""{"id":"{{Id}}","said":true}"""),
        "pin" => (new("pin") { Id = Id }, "HELP_PIN", $$"""{"id":"{{Id}}","pinned":true,"said":true}"""),
        "unpin" => (new("unpin") { Id = Id }, "HELP_PIN", $$"""{"id":"{{Id}}","pinned":false,"said":true}"""),
        "delete --yes" => (new("delete") { Id = Id, Yes = true }, "SESSION_DELETE", $$"""{"id":"{{Id}}","help":true,"said":true}"""),
        "list" => (new("list"), "HELP_CONVERSATIONS", """{"said":true}"""),
        "list --search --offset --limit --workspace --json" => (
            new("list") { Search = "remote", Offset = 5, Limit = 10, Workspace = "aurora", Json = true }, "HELP_CONVERSATIONS",
            """{"q":"remote","offset":5,"limit":10,"workspace":"aurora","json":true,"said":true}"""),
        "list --all" => (new("list") { All = true }, "HELP_CONVERSATIONS", """{"all":true,"said":true}"""),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [InlineData("rename")]
    [InlineData("rename --clear")]
    [InlineData("pin")]
    [InlineData("unpin")]
    [InlineData("delete --yes")]
    public async Task A_help_press_through_the_instance_waits_for_the_window_and_prints_the_lines_it_answers(string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (ask, route, payload) = Verb(name);
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        var line = $"help: said for {name}.";
        standIn.Data = new { said = new TreesSaid([line], [], 0).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(ask, service, home.Home, home.World("help"), output);

        Assert.Equal(0, exit);
        Assert.Equal(line + Environment.NewLine, output.ToString());
        Assert.Equal($"help: waiting for you to confirm this in Daoris's window…{Environment.NewLine}", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal(route, press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
    }

    /// <summary>The list is a read: sent to the open <c>HELP_CONVERSATIONS</c> with the listing's words, never waiting on the window.</summary>
    [Theory]
    [InlineData("list")]
    [InlineData("list --search --offset --limit --workspace --json")]
    [InlineData("list --all")]
    public async Task The_list_is_read_through_the_instance_and_waits_on_nothing(string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (ask, route, payload) = Verb(name);
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Data = new { conversations = Array.Empty<object>(), said = new TreesSaid(["help: Ask Daoris has no conversations on this machine yet."], [], 0).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(ask, service, home.Home, home.World("help"), output);

        Assert.Equal(0, exit);
        Assert.Equal("help: Ask Daoris has no conversations on this machine yet." + Environment.NewLine, output.ToString());
        Assert.Equal("", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal(route, press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
    }

    [Theory]
    [InlineData("rename", "refused")]
    [InlineData("pin", "expired")]
    [InlineData("delete --yes", "refused")]
    public async Task A_help_press_the_person_refuses_or_leaves_unanswered_is_exit_1_and_does_nothing_here(string name, string code)
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

        var exit = await RunAsync(ask, service, home.Home, home.World("help"), output);

        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
        Assert.Contains($"({code})", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        Assert.Single(standIn.Presses);
    }

    /// <summary>
    /// <c>resume</c> stays this terminal's own beside a running Daoris (RPC1g designs it first), and a delete's first ask reads the
    /// conversation here: neither sends anything, and each says what it says alone.
    /// </summary>
    [Theory]
    [InlineData("resume")]
    [InlineData("delete")]
    public async Task Resume_and_a_delete_s_first_ask_stay_alone_beside_a_running_Daoris(string verb)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(new HelpVerbAsk(verb) { Id = Id, Text = "and then?" }, service, home.Home, home.World("help"), output);

        Assert.Equal(1, exit);
        Assert.Equal($"help: no session here is {Id}.{Environment.NewLine}", output.ToString());
        Assert.Empty(standIn.Presses);
        Assert.True(service.Requests > 0);
    }

    /// <summary>With no instance running, the list is this process's own, in the same words, as it was.</summary>
    [Fact]
    public async Task With_no_instance_the_list_is_this_process_s_own()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await RunAsync(new HelpVerbAsk("list"), service, home.Home, home.World("help"), output);

        Assert.Equal(0, exit);
        Assert.Equal("help: Ask Daoris has no conversations on this machine yet." + Environment.NewLine, output.ToString());
        Assert.True(service.Requests > 0);
    }
}
