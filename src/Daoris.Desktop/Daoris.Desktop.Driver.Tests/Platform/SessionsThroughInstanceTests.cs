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

    /// <summary>
    /// RPC1c: <c>sessions archive</c> and <c>unarchive</c> are the person's presses, sent to the running Daoris, which shows them in its
    /// window; the terminal says it waits, then prints the lines the confirmed press answers, which are the alone path's words
    /// (<see cref="SessionArchive.ArchiveSaid"/>), and this process reads and writes nothing.
    /// </summary>
    [Theory]
    [InlineData("archive", true, "sessions: archived s1a2b3c4.")]
    [InlineData("unarchive", false, "sessions: unarchived s1a2b3c4; it is back in the group its state puts it in.")]
    public async Task Archive_and_unarchive_through_the_instance_wait_for_the_window_and_print_the_lines_it_answers(
        string verb, bool archived, string line)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        standIn.Data = new { archived = Array.Empty<object>(), said = new TreesSaid([line], [], 0).ToWire() };
        var service = new EmptyService();
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(new SessionsAsk(verb) { Ids = ["s1a2b3c4"] }, World(service, home.Home, home.World("sessions")), output);

        Assert.Equal(0, exit);
        Assert.Equal(line + Environment.NewLine, output.ToString());
        Assert.Equal($"sessions: waiting for you to confirm this in Daoris's window…{Environment.NewLine}", home.Error.ToString());
        Assert.Equal(0, service.Requests);
        Assert.False(File.Exists(new SessionArchive(home.Home).FilePath));
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("SESSION_ARCHIVE", press.GetProperty("type").GetString());
        Assert.Equal($$"""{"ids":["s1a2b3c4"],"archived":{{(archived ? "true" : "false")}},"said":true}""", press.GetProperty("payload").GetRawText());
    }

    /// <summary>RPC1c: refused in the window, the archive is the verb's exit 1 with the instance's sentence, and nothing is archived here.</summary>
    [Fact]
    public async Task An_archive_the_person_refuses_in_the_window_is_exit_1_and_archives_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Refuse;
        standIn.Code = "refused";
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(
            new SessionsAsk("archive") { Ids = ["s1a2b3c4"] }, World(new EmptyService(), home.Home, home.World("sessions")), output);

        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
        Assert.Contains("sessions: refused refused. (refused)", home.Error.ToString());
        Assert.False(File.Exists(new SessionArchive(home.Home).FilePath));
    }

    /// <summary>With no instance running, the archive acts alone and says the same words as the instance's answer carries.</summary>
    [Fact]
    public async Task With_no_instance_an_archive_acts_alone_in_the_words_it_would_answer()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(
            new SessionsAsk("archive") { Ids = ["s1a2b3c4"] }, World(new EmptyService(), home.Home, home.World("sessions")), output);

        var said = SessionArchive.ArchiveSaid([new ArchiveOutcome("s1a2b3c4", ArchiveVerdict.Unknown)]);
        Assert.Equal(said.Code, exit);
        Assert.Equal(string.Concat(said.Out.Select(line => line + Environment.NewLine)), output.ToString());
        Assert.Equal("sessions: kept s1a2b3c4: no session here is s1a2b3c4." + Environment.NewLine, output.ToString());
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
