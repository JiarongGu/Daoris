using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The running Daoris answers on a pipe (RPC1a, D171 point 3, the terminal RPC design §2.2, §4, §6, §8): a terminal's request is
/// the kit's envelope on a line, run by the dispatcher every window's page uses, so a declared press over the pipe is under way
/// and publishes its change as a window's press does; a person's route and a window's are refused before anything runs; a lost
/// connection cancels nothing and <c>RESULT</c> says how the press ended; an id sent twice runs once; and a closing instance
/// answers every new hello <c>stopping</c>.
/// </summary>
/// <remarks>
/// Each test serves its own pipe in this process, named by its own scratch home's id, so nothing here meets a running Daoris.
/// The pipe is Windows': elsewhere a test returns at once, as the terminal module's do.
/// </remarks>
public sealed class InstancePipeTests : Bridge
{
    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>What the test's press declares, as a route's mark does (D169 point 2).</summary>
    private static StateChange? Declared(IpcRequest request) =>
        request.Module == "TEST.PRESS" && request.Type is "MOVE" or "PROGRESS"
            ? new StateChange(StateScopes.Sessions | StateScopes.Landing, "id")
            : null;

    private static readonly PipeCensus Census = PipeCensus.Daoris
        .With("TEST.PRESS", "MOVE", PipeClass.Open)
        .With("TEST.PRESS", "PROGRESS", PipeClass.Open);

    /// <summary>The dispatcher as the shell composes it: the refusal log, then the publisher over the loop's list, then the modules.</summary>
    private (IMessageDispatcher Dispatcher, PressesUnderWay UnderWay, ServiceProvider Services) Compose(MachineLog log, params IIpcModule[] modules)
    {
        var underWay = new PressesUnderWay();
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        // The kit's request tracker, as the application's builder registers it, telling a request that outlives a short grace.
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(Bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        foreach (var module in modules) services.AddSingleton(module);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher
            .Use(RefusalLog.Middleware(log))
            .Use(StatePublisher.Middleware(Bus, Declared, underWay)));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMessageDispatcher>(), underWay, provider);
    }

    private InstancePipe Pipe(IMessageDispatcher dispatcher, MachineLog log, PipeLimits? limits = null) =>
        new(dispatcher, Bus, Home, log, Census, version: "9.9.9-test", limits: limits);

    private string HomeId => HomeIdentity.Read(Home)!;

    [Fact]
    public async Task A_declared_press_over_the_pipe_is_under_way_and_publishes_its_change_as_a_window_s_press_does()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        var (dispatcher, underWay, services) = Compose(log, press);
        using var _ = services;
        using var pipe = Pipe(dispatcher, log);
        var name = pipe.Serve();
        Assert.Equal(PipeName.For(HomeId, System.Diagnostics.Process.GetCurrentProcess().SessionId), name);

        await using var client = await PipeClient.ConnectAsync(name!);
        var hello = await client.HelloAsync(HomeId);
        Assert.True(hello.GetProperty("success").GetBoolean(), hello.GetRawText());
        var said = hello.GetProperty("data");
        Assert.Equal(1, said.GetProperty("protocol").GetInt32());
        Assert.Equal(HomeId, said.GetProperty("home").GetString());
        Assert.Equal("9.9.9-test", said.GetProperty("version").GetString());
        Assert.Equal(Environment.ProcessId, said.GetProperty("pid").GetInt32());

        // Pressed over the pipe: in the loop's list while it runs, as a window's press is (D169's STATEHUB1c2 note).
        var id = await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" });
        await press.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal([new PressUnderWay("MOVE", "s1a2b3c4")], underWay.Now().Presses);
        Assert.Contains(Raised, each => each.Type == StatePublisher.UnderWayType
            && each.Payload is UnderWay { Presses: [{ Route: "MOVE", Session: "s1a2b3c4" }] });

        press.Let.SetResult();
        var answer = await client.AnswerAsync(id);
        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal("ipc", answer.GetProperty("category").GetString());
        Assert.Empty(underWay.Now().Presses);
        var changed = Assert.Single(Raised, each => each.Type == StatePublisher.Type);
        Assert.Equal("""{"scopes":["sessions","landing"],"sessions":["s1a2b3c4"]}""", JsonSerializer.Serialize(changed.Payload, Wire));

        // The window's press of the same route: the same change, the same answer.
        var window = await dispatcher.DispatchAsync(new IpcRequest
        {
            Id = "window-press",
            Module = "TEST.PRESS",
            Type = "MOVE",
            Payload = JsonSerializer.SerializeToElement(new { id = "s1a2b3c4" }),
        }, CancellationToken.None);
        var both = Raised.Where(each => each.Type == StatePublisher.Type).Select(each => JsonSerializer.Serialize(each.Payload, Wire)).ToList();
        Assert.Equal(2, both.Count);
        Assert.Equal(both[0], both[1]);
        Assert.Equal(
            JsonSerializer.Serialize(window.Data, IpcJson.Options).Replace("(2)", "(1)", StringComparison.Ordinal),
            answer.GetProperty("data").GetRawText());
    }

    [Fact]
    public async Task A_person_route_a_window_route_and_a_module_nobody_classed_are_refused_and_never_run()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        var windows = new HeldModule("DAORIS.WINDOWS");
        var elsewhere = new HeldModule("TEST.ELSEWHERE");
        driver.Let.SetResult();
        var (dispatcher, _, services) = Compose(log, driver, windows, elsewhere);
        using var __ = services;
        using var pipe = Pipe(dispatcher, log);
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        var person = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "LAND_SESSION_TREE", new { id = "s1a2b3c4" }));
        var window = await client.AnswerAsync(await client.SendAsync("DAORIS.WINDOWS", "OPEN", new { name = "session:s1a2b3c4" }));
        var nobody = await client.AnswerAsync(await client.SendAsync("TEST.ELSEWHERE", "ANYTHING"));

        Assert.Equal(InstancePipe.ConfirmInWindow, Code(person));
        Assert.Equal(InstancePipe.WindowOnly, Code(window));
        Assert.Equal(InstancePipe.Unclassed, Code(nobody));
        Assert.Equal(0, driver.Ran);
        Assert.Equal(0, windows.Ran);
        Assert.Equal(0, elsewhere.Ran);

        // An open route of the same module runs, as the window's press of it does.
        var open = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "UNDER_WAY"));
        Assert.True(open.GetProperty("success").GetBoolean(), open.GetRawText());
        Assert.Equal(1, driver.Ran);

        // Each refusal is in the machine log by its code and request, said as the terminal's (§8).
        var refused = Lines().Where(line => line.GetProperty("event").GetString() == "refused").Select(line => line.GetProperty("data").GetRawText()).ToList();
        Assert.Equal(
            [
                """{"code":"confirm-in-window","request":"DAORIS.DRIVER.LAND_SESSION_TREE","door":"terminal"}""",
                """{"code":"window-only","request":"DAORIS.WINDOWS.OPEN","door":"terminal"}""",
                """{"code":"unclassed","request":"TEST.ELSEWHERE.ANYTHING","door":"terminal"}""",
            ],
            refused);
    }

    /// <summary>A refusal the route itself answers, for a request that came over the pipe, is said as the terminal's in the log too.</summary>
    [Fact]
    public async Task A_route_s_own_refusal_over_the_pipe_is_logged_as_the_terminal_s()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var (dispatcher, _, services) = Compose(log, new HeldModule("TEST.PRESS"));
        using var __ = services;
        using var pipe = new InstancePipe(dispatcher, Bus, Home, log, PipeCensus.Daoris.With("TEST.NOWHERE", "LOOK", PipeClass.Open));
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        var answer = await client.AnswerAsync(await client.SendAsync("TEST.NOWHERE", "LOOK"));

        Assert.Equal(IpcErrorCodes.NoHandler, Code(answer));
        var line = Assert.Single(Lines(), line => line.GetProperty("event").GetString() == "refused");
        Assert.Equal("""{"code":"NO_HANDLER","request":"TEST.NOWHERE.LOOK","door":"terminal"}""", line.GetProperty("data").GetRawText());
    }

    [Fact]
    public async Task A_press_whose_connection_is_lost_runs_to_its_answer_and_RESULT_says_how_it_ended()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        var (dispatcher, underWay, services) = Compose(log, press);
        using var _ = services;
        using var pipe = Pipe(dispatcher, log);
        var name = pipe.Serve()!;

        var lost = await PipeClient.ConnectAsync(name);
        await lost.HelloAsync(HomeId);
        var id = await lost.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" });
        await press.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await lost.DisposeAsync();

        await using var again = await PipeClient.ConnectAsync(name);
        await again.HelloAsync(HomeId);
        Assert.Equal("running", (await again.ResultAsync(id)).GetProperty("state").GetString());
        Assert.Equal("unknown", (await again.ResultAsync("never-sent")).GetProperty("state").GetString());

        // The press runs on with nobody to hear it: the connection's life is not the press's (§6.1).
        press.Let.SetResult();
        await UntilAsync(() => underWay.Now().Presses.Count == 0);
        var ended = await again.ResultAsync(id);
        Assert.Equal("answered", ended.GetProperty("state").GetString());
        var answer = ended.GetProperty("answer");
        Assert.Equal(id, answer.GetProperty("id").GetString());
        Assert.True(answer.GetProperty("success").GetBoolean());
        Assert.Equal("pressed MOVE (1)", answer.GetProperty("data").GetProperty("said").GetString());
        Assert.Single(Raised, each => each.Type == StatePublisher.Type);
    }

    [Fact]
    public async Task An_id_sent_twice_runs_once_and_is_answered_the_first_answer_even_by_the_next_instance()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        var (dispatcher, _, services) = Compose(log, press);
        using var __ = services;
        string first;
        string id;
        using (var pipe = Pipe(dispatcher, log))
        {
            var name = pipe.Serve()!;
            await using var one = await PipeClient.ConnectAsync(name);
            await using var two = await PipeClient.ConnectAsync(name);
            await one.HelloAsync(HomeId);
            await two.HelloAsync(HomeId);

            // Sent again while the first is under way, from another connection: it waits for the one press.
            id = await one.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" });
            await press.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await two.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, id);
            press.Let.SetResult();
            first = (await one.AnswerAsync(id)).GetRawText();
            Assert.Equal(first, (await two.AnswerAsync(id)).GetRawText());

            // And sent again once it has answered: answered from its line.
            await one.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, id);
            Assert.Equal(first, (await one.AnswerAsync(id)).GetRawText());
            Assert.Equal(1, press.Ran);
        }

        // The next instance on the home reads the lines the first left, for 24 hours (§6.1).
        using var next = Pipe(dispatcher, log);
        await using var three = await PipeClient.ConnectAsync(next.Serve()!);
        await three.HelloAsync(HomeId);
        await three.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, id);
        Assert.Equal(first, (await three.AnswerAsync(id)).GetRawText());
        Assert.Equal(first, (await three.ResultAsync(id)).GetProperty("answer").GetRawText());
        Assert.Equal(1, press.Ran);
    }

    /// <summary>
    /// A press begun by an instance that stopped before it ended is unknown: <c>RESULT</c> says so, and the id sent again is
    /// refused rather than run, since what the first did is not known (§6.1).
    /// </summary>
    [Fact]
    public async Task A_press_an_instance_never_ended_is_unknown_and_never_run_again()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        press.Let.SetResult();
        var (dispatcher, _, services) = Compose(log, press);
        using var __ = services;
        var journal = Path.Combine(Home, InstancePipe.JournalFolder);
        Directory.CreateDirectory(journal);
        var now = DateTimeOffset.UtcNow;
        File.WriteAllText(Path.Combine(journal, $"{now:yyyy-MM-dd}.jsonl"),
            $"{{\"id\":\"begun-before\",\"at\":\"{now.AddMinutes(-5):O}\",\"state\":\"running\",\"request\":\"TEST.PRESS.MOVE\"}}\n"
            + $"{{\"id\":\"long-ago\",\"at\":\"{now.AddHours(-25):O}\",\"state\":\"running\",\"request\":\"TEST.PRESS.MOVE\"}}\n"
            + "not a line\n");

        using var pipe = Pipe(dispatcher, log);
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        Assert.Equal("unknown", (await client.ResultAsync("begun-before")).GetProperty("state").GetString());
        await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, "begun-before");
        Assert.Equal(InstancePipe.Interrupted, Code(await client.AnswerAsync("begun-before")));
        Assert.Equal(0, press.Ran);

        // A line older than a day is forgotten: that id runs as a new one.
        await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, "long-ago");
        Assert.True((await client.AnswerAsync("long-ago")).GetProperty("success").GetBoolean());
        Assert.Equal(1, press.Ran);
    }

    [Fact]
    public async Task A_closing_instance_answers_every_new_hello_stopping_and_still_says_how_a_press_ended()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        press.Let.SetResult();
        var (dispatcher, _, services) = Compose(log, press);
        using var __ = services;
        using var pipe = Pipe(dispatcher, log);
        var name = pipe.Serve()!;
        await using var before = await PipeClient.ConnectAsync(name);
        await before.HelloAsync(HomeId);
        var id = await before.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" });
        await before.AnswerAsync(id);

        // An update's drain is a stop's beginning (§6.3).
        var updating = false;
        pipe.Draining = () => updating;
        updating = true;
        await using (var during = await PipeClient.ConnectAsync(name))
        {
            var refused = await during.HelloAsync(HomeId);
            Assert.Equal(InstancePipe.Stopping, Code(refused));
            Assert.Equal("update", refused.GetProperty("error").GetProperty("parameters").GetProperty("why").GetString());
        }

        updating = false;
        pipe.BeginStopping();
        await using (var after = await PipeClient.ConnectAsync(name))
        {
            var refused = await after.HelloAsync(HomeId);
            Assert.Equal(InstancePipe.Stopping, Code(refused));
            Assert.Equal("close", refused.GetProperty("error").GetProperty("parameters").GetProperty("why").GetString());
        }

        // A connection already open is refused a new press, and still told how its press ended.
        Assert.Equal(InstancePipe.Stopping, Code(await before.AnswerAsync(await before.SendAsync("TEST.PRESS", "MOVE", new { id = "s5e6f7a8" }))));
        Assert.Equal("answered", (await before.ResultAsync(id)).GetProperty("state").GetString());
        Assert.Equal(1, press.Ran);
        Assert.True(pipe.Drain(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Its_hello_comes_first_and_answers_only_this_home_on_this_wire()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        var (dispatcher, _, services) = Compose(log, press);
        using var __ = services;
        using var pipe = Pipe(dispatcher, log);
        var name = pipe.Serve()!;

        // Anything before its hello closes the connection, unanswered and unrun.
        await using (var rude = await PipeClient.ConnectAsync(name))
        {
            await rude.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" });
            Assert.Null(await rude.ReadAsync());
        }

        await using (var older = await PipeClient.ConnectAsync(name))
        {
            Assert.Equal(InstancePipe.WrongProtocol, Code(await older.HelloAsync(HomeId, protocol: 2)));
            Assert.Null(await older.ReadAsync());
        }

        await using (var stranger = await PipeClient.ConnectAsync(name))
        {
            Assert.Equal(InstancePipe.WrongHome, Code(await stranger.HelloAsync("fedcba9876543210fedcba9876543210")));
            Assert.Null(await stranger.ReadAsync());
        }

        // A line that is no request closes it too.
        await using (var garbled = await PipeClient.ConnectAsync(name))
        {
            await garbled.HelloAsync(HomeId);
            await garbled.SendLineAsync("not json");
            Assert.Null(await garbled.ReadAsync());
        }

        Assert.Equal(0, press.Ran);
    }

    /// <summary>A connection that says nothing within the hello's bound is closed; so is a line past the bound on its length.</summary>
    [Fact]
    public async Task A_silent_connection_and_an_endless_line_are_closed()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var (dispatcher, _, services) = Compose(log, new HeldModule("TEST.PRESS"));
        using var __ = services;
        using var pipe = Pipe(dispatcher, log, new PipeLimits { Hello = TimeSpan.FromMilliseconds(300), LineBytes = 4096 });
        var name = pipe.Serve()!;

        await using (var silent = await PipeClient.ConnectAsync(name))
        {
            Assert.Null(await silent.ReadAsync());
        }

        await using var endless = await PipeClient.ConnectAsync(name);
        await endless.HelloAsync(HomeId);
        await endless.SendLineAsync(new string('x', 8192), end: false);
        Assert.Null(await endless.ReadAsync());
    }

    [Fact]
    public async Task ROUTES_lists_each_route_the_pipe_accepts_with_its_class()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var (dispatcher, _, services) = Compose(log);
        using var __ = services;
        using var pipe = Pipe(dispatcher, log);
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        var answer = await client.AnswerAsync(await client.SendAsync(InstancePipe.Module, "ROUTES"));

        var routes = answer.GetProperty("data").GetProperty("routes").EnumerateArray().ToList();
        Assert.Equal(Census.Routes.Count, routes.Count);
        Assert.Contains(routes, route => route.GetRawText() == """{"module":"DAORIS.DRIVER","route":"LAND_SESSION_TREE","class":"person"}""");
        Assert.Contains(routes, route => route.GetRawText() == """{"module":"DAORIS.DRIVER","route":"SESSION_GROUPS","class":"open"}""");
        Assert.Contains(routes, route => route.GetRawText() == """{"module":"DAORIS.WINDOWS","class":"window"}""");
    }

    /// <summary>A route's progress, as the kit's tracker tells it, reaches the connection that sent the request, naming its id (§6).</summary>
    [Fact]
    public async Task A_route_s_progress_reaches_the_connection_that_sent_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        var (dispatcher, _, services) = Compose(log, press);
        using var __ = services;
        using var pipe = Pipe(dispatcher, log);
        var name = pipe.Serve()!;
        await using var client = await PipeClient.ConnectAsync(name);
        await using var other = await PipeClient.ConnectAsync(name);
        await client.HelloAsync(HomeId);
        await other.HelloAsync(HomeId);

        var id = await client.SendAsync("TEST.PRESS", "PROGRESS", new { id = "s1a2b3c4" });
        var told = await client.NotificationAsync(TimeSpan.FromSeconds(15)) ?? throw new InvalidOperationException("no progress was told");
        press.Let.SetResult();
        await client.AnswerAsync(id);

        Assert.Equal("notification", told.GetProperty("category").GetString());
        var status = told.GetProperty("payload")[0].GetProperty("payload");
        Assert.Equal(id, status.GetProperty("id").GetString());
        Assert.Equal(1, status.GetProperty("progress").GetProperty("value").GetDouble());
        Assert.Equal("half way", status.GetProperty("detail").GetProperty("text").GetString());

        // Nothing else is forwarded unasked: the other connection hears nothing of it.
        Assert.Null(await other.NotificationAsync(TimeSpan.FromMilliseconds(500)));
    }

    /// <summary>The name is this instance's alone: one already served is not shared, and the machine log says so (§4.1).</summary>
    [Fact]
    public void A_name_already_served_is_not_served_again()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var (dispatcher, _, services) = Compose(log);
        using var __ = services;
        using var first = Pipe(dispatcher, log);
        using var second = Pipe(dispatcher, log);

        Assert.NotNull(first.Serve());
        Assert.Null(second.Serve());

        Assert.Contains(Lines(), line => line.GetProperty("event").GetString() == "pipe.refused"
            && line.GetProperty("data").GetProperty("why").GetString() == "held");
    }

    private static string? Code(JsonElement answer) =>
        answer.TryGetProperty("error", out var error) ? error.GetProperty("code").GetString() : null;

    private List<JsonElement> Lines()
    {
        var folder = Path.Combine(Home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            }).Select(line => JsonDocument.Parse(line).RootElement.Clone()),
        ];
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(25);
        }
    }

}
