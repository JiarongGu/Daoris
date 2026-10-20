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

    /// <summary>
    /// <see cref="Compose"/> with the kit's own requests module beside the others, as the application's builder registers it, so a
    /// <c>CANCEL</c> the pipe passes on reaches the tracker the dispatcher records its requests in.
    /// </summary>
    private (IMessageDispatcher Dispatcher, PressesUnderWay UnderWay, ServiceProvider Services) ComposeCancelling(MachineLog log, params IIpcModule[] modules)
    {
        var underWay = new PressesUnderWay();
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        var options = new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) };
        var tracker = new IpcRequestTracker(Bus, options);
        services.AddSingleton<IIpcRequestTracker>(tracker);
        foreach (var module in modules) services.AddSingleton(module);
        services.AddSingleton<IIpcModule>(new Shenora.Modules.Requests.IpcRequestsModule(
            tracker, options, Microsoft.Extensions.Logging.Abstractions.NullLogger<Shenora.Modules.Requests.IpcRequestsModule>.Instance));
        services.UseMessageDispatcher((_, dispatcher) => dispatcher
            .Use(RefusalLog.Middleware(log))
            .Use(StatePublisher.Middleware(Bus, Declared, underWay)));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMessageDispatcher>(), underWay, provider);
    }

    private InstancePipe Pipe(IMessageDispatcher dispatcher, MachineLog log, PipeLimits? limits = null, PipeCensus? census = null) =>
        new(dispatcher, Bus, Home, log, census ?? Census, version: "9.9.9-test", limits: limits);

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

    /// <summary>
    /// A window's route and a module nobody classed are refused before anything runs; a person's route waits for the window instead
    /// (RPC1c), and <c>PipeConfirmTests</c> holds how, so here it is shown and never run.
    /// </summary>
    [Fact]
    public async Task A_window_route_and_a_module_nobody_classed_are_refused_and_a_person_route_runs_nothing_unconfirmed()
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

        await client.SendAsync(DriverModule.Name, "LAND_SESSION_TREE", new { id = "s1a2b3c4" });
        var window = await client.AnswerAsync(await client.SendAsync("DAORIS.WINDOWS", "OPEN", new { name = "session:s1a2b3c4" }));
        var nobody = await client.AnswerAsync(await client.SendAsync("TEST.ELSEWHERE", "ANYTHING"));

        Assert.Equal(InstancePipe.WindowOnly, Code(window));
        Assert.Equal(InstancePipe.Unclassed, Code(nobody));
        Assert.Equal("LAND_SESSION_TREE", Assert.Single(pipe.Confirmations.Now().Waiting).Route);
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
        var key = lost.KeyOf(id)!;
        await press.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await lost.DisposeAsync();

        // Asked again from another connection, with the key only its sender holds.
        await using var again = await PipeClient.ConnectAsync(name);
        await again.HelloAsync(HomeId);
        Assert.Equal("running", (await again.ResultAsync(id, key)).GetProperty("state").GetString());
        Assert.Equal("unknown", (await again.ResultAsync("never-sent", "0123456789abcdef0123456789abcdef")).GetProperty("state").GetString());

        // The press runs on with nobody to hear it: the connection's life is not the press's (§6.1).
        press.Let.SetResult();
        await UntilAsync(() => underWay.Now().Presses.Count == 0);
        // The press leaves the under-way list inside the dispatch, a moment before its answer's line is kept.
        var ended = await again.ResultAsync(id, key);
        for (var asked = 0; ended.GetProperty("state").GetString() == "running" && asked < 200; asked++)
        {
            await Task.Delay(25);
            ended = await again.ResultAsync(id, key);
        }

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
        string key;
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
            key = one.KeyOf(id)!;
            await two.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, id, key);
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
        await three.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, id, key);
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
        var journal = DaorisHome.Entry(Home, HomeEntry.Pipe);
        Directory.CreateDirectory(journal);
        var now = DateTimeOffset.UtcNow;
        // Each id's line binds it to its request, a fingerprint of its payload and one of its sender's key (SHA-256, hex).
        const string key = "begun-before-key-0123456789";
        var bound = $"\"request\":\"TEST.PRESS.MOVE\",\"payload\":\"{Fingerprint("""{"id":"s1a2b3c4"}""")}\",\"key\":\"{Fingerprint(key)}\"";
        File.WriteAllText(Path.Combine(journal, $"{now:yyyy-MM-dd}.jsonl"),
            $"{{\"id\":\"begun-before\",\"at\":\"{now.AddMinutes(-5):O}\",\"state\":\"running\",{bound}}}\n"
            + $"{{\"id\":\"long-ago\",\"at\":\"{now.AddHours(-25):O}\",\"state\":\"running\",{bound}}}\n"
            + "not a line\n");

        using var pipe = Pipe(dispatcher, log);
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        Assert.Equal("unknown", (await client.ResultAsync("begun-before", key)).GetProperty("state").GetString());
        await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, "begun-before", key);
        Assert.Equal(InstancePipe.Interrupted, Code(await client.AnswerAsync("begun-before")));
        Assert.Equal(0, press.Ran);

        // A line older than a day is forgotten: that id runs as a new one.
        await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, "long-ago", key);
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

    private static string Fingerprint(string text) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    /// <summary>
    /// An id is bound to the request it was first sent with: its module and route, its payload and its sender's key. Sent again
    /// with any of them changed it is refused, never answered with another request's answer, and <c>RESULT</c> answers only the key
    /// that sent it, whichever connection asks; across a restart too (a second agent's review of RPC1a).
    /// </summary>
    [Fact]
    public async Task An_id_bound_to_another_request_is_refused_and_never_answered_for_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        press.Let.SetResult();
        var (dispatcher, _, services) = Compose(log, press);
        using var __ = services;
        string id;
        string key;
        using (var pipe = Pipe(dispatcher, log))
        {
            await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
            await client.HelloAsync(HomeId);
            id = await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" });
            key = client.KeyOf(id)!;
            Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());

            await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s5e6f7a8" }, id, key);
            Assert.Equal(InstancePipe.IdTaken, Code(await client.AnswerAsync(id)));
            await client.SendAsync("TEST.PRESS", "PROGRESS", new { id = "s1a2b3c4" }, id, key);
            Assert.Equal(InstancePipe.IdTaken, Code(await client.AnswerAsync(id)));
            await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, id, "another-key-0123456789abcdef");
            Assert.Equal(InstancePipe.IdTaken, Code(await client.AnswerAsync(id)));

            Assert.Equal(InstancePipe.IdTaken, Code(await client.ResultAnswerAsync(id, "another-key-0123456789abcdef")));
            Assert.Equal(InstancePipe.KeyMissing, Code(await client.ResultAnswerAsync(id, "")));
            Assert.Equal("answered", (await client.ResultAsync(id, key)).GetProperty("state").GetString());
        }

        using var next = Pipe(dispatcher, log);
        await using var later = await PipeClient.ConnectAsync(next.Serve()!);
        await later.HelloAsync(HomeId);
        await later.SendAsync("TEST.PRESS", "MOVE", new { id = "s5e6f7a8" }, id, key);
        Assert.Equal(InstancePipe.IdTaken, Code(await later.AnswerAsync(id)));
        Assert.Equal(InstancePipe.IdTaken, Code(await later.ResultAnswerAsync(id, "another-key-0123456789abcdef")));
        Assert.Equal("answered", (await later.ResultAsync(id, key)).GetProperty("state").GetString());
        Assert.Equal(1, press.Ran);
    }

    /// <summary>
    /// RPC1h (§6.2): a terminal's Ctrl+C is the kit's <c>CANCEL</c> by the request's id, taken only for a press this pipe began and
    /// only with the key that sent it, and passed on to the kit's tracker; the press then ends as a cancelled one, and <c>RESULT</c>
    /// says so. A window's id, another key's and an id nobody sent are refused as an id bound elsewhere is, and nothing is cancelled.
    /// </summary>
    [Fact]
    public async Task A_cancel_for_a_press_the_pipe_began_with_its_key_cancels_it_and_RESULT_says_how_it_ended()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        var window = new HeldModule("TEST.WINDOW");
        var (dispatcher, _, services) = ComposeCancelling(log, press, window);
        using var _ = services;
        using var pipe = Pipe(dispatcher, log, census: Census.With("TEST.WINDOW", "MOVE", PipeClass.Open));
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        // A press a window began, held: its id is not this pipe's, so a terminal cannot cancel it.
        var windowPress = dispatcher.DispatchAsync(new IpcRequest
        {
            Id = "window-held",
            Module = "TEST.WINDOW",
            Type = "MOVE",
            Payload = JsonSerializer.SerializeToElement(new { id = "s1a2b3c4" }),
        }, CancellationToken.None);
        await window.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var id = await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s5e6f7a8" });
        var key = client.KeyOf(id)!;
        await press.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var strangers = await client.SendAsync("SHENORA.REQUESTS", "CANCEL", new { requestId = "window-held" });
        Assert.Equal(InstancePipe.IdTaken, Code(await client.AnswerAsync(strangers)));
        var nobodys = await client.SendAsync("SHENORA.REQUESTS", "CANCEL", new { requestId = "never-sent" });
        Assert.Equal(InstancePipe.IdTaken, Code(await client.AnswerAsync(nobodys)));
        var another = await client.SendAsync("SHENORA.REQUESTS", "CANCEL", new { requestId = id }, key: "another-key-0123456789abcdef");
        Assert.Equal(InstancePipe.IdTaken, Code(await client.AnswerAsync(another)));
        var keyless = await client.SendAsync("SHENORA.REQUESTS", "CANCEL", new { requestId = id }, key: "short");
        Assert.Equal(InstancePipe.KeyMissing, Code(await client.AnswerAsync(keyless)));
        Assert.False(windowPress.IsCompleted);
        Assert.Equal("running", (await client.ResultAsync(id)).GetProperty("state").GetString());

        // The sender's own key cancels it: the kit's tracker aborts the token the press runs under.
        var cancel = await client.SendAsync("SHENORA.REQUESTS", "CANCEL", new { requestId = id }, key: key);
        var cancelled = await client.AnswerAsync(cancel);
        Assert.True(cancelled.GetProperty("success").GetBoolean(), cancelled.GetRawText());
        var answer = await client.AnswerAsync(id);
        Assert.False(answer.GetProperty("success").GetBoolean());
        Assert.Equal(IpcErrorCodes.OperationCancelled, Code(answer));

        // RESULT says how it ended; a cancel sent again says it already had, and runs nothing.
        var ended = await client.ResultAsync(id);
        Assert.Equal("answered", ended.GetProperty("state").GetString());
        Assert.Equal(IpcErrorCodes.OperationCancelled, ended.GetProperty("answer").GetProperty("error").GetProperty("code").GetString());
        var again = await client.SendAsync("SHENORA.REQUESTS", "CANCEL", new { requestId = id }, key: key);
        var settled = await client.AnswerAsync(again);
        Assert.True(settled.GetProperty("success").GetBoolean());
        Assert.False(settled.GetProperty("data").GetProperty("cancelled").GetBoolean());

        window.Let.SetResult();
        Assert.True((await windowPress).Success);
        Assert.Equal(1, press.Ran);
    }

    /// <summary>A request sent with no key, or one too short to be a secret, is refused before anything runs.</summary>
    [Fact]
    public async Task A_request_without_its_key_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        press.Let.SetResult();
        var (dispatcher, _, services) = Compose(log, press);
        using var __ = services;
        using var pipe = Pipe(dispatcher, log);
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        await client.SendLineAsync("""{"id":"keyless-1","module":"TEST.PRESS","type":"MOVE","payload":{"id":"s1a2b3c4"}}""");
        Assert.Equal(InstancePipe.KeyMissing, Code(await client.AnswerAsync("keyless-1")));
        await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" }, "short-key-1", "short");
        Assert.Equal(InstancePipe.KeyMissing, Code(await client.AnswerAsync("short-key-1")));
        Assert.Equal(0, press.Ran);
    }

    /// <summary>
    /// "Never twice" holds across a restart only for an id the journal holds, so a request whose line cannot be written is
    /// refused and never dispatched (a second agent's review of RPC1a).
    /// </summary>
    [Fact]
    public async Task A_request_the_journal_cannot_hold_is_refused_and_never_run()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        press.Let.SetResult();
        var (dispatcher, _, services) = Compose(log, press);
        using var __ = services;
        // A file where the journal's folder goes: no line can be written under it.
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Pipe), "not a folder");
        using var pipe = Pipe(dispatcher, log);
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        var id = await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" });

        Assert.Equal(InstancePipe.Unrecorded, Code(await client.AnswerAsync(id)));
        Assert.Equal(0, press.Ran);
        Assert.Equal("unknown", (await client.ResultAsync(id)).GetProperty("state").GetString());
    }

    /// <summary>
    /// A route that is a look in one form and a press in another runs over the pipe as asked only in its open form: its press waits
    /// for the window (RPC1c), and runs nothing until the person confirms it there.
    /// </summary>
    [Fact]
    public async Task A_route_classed_by_its_payload_runs_only_in_its_open_form()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, _, services) = Compose(log, driver);
        using var __ = services;
        using var pipe = Pipe(dispatcher, log);
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        await client.SendAsync(DriverModule.Name, "TOOLS_GIT", new { way = "file", file = "C:/elsewhere/git.exe" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        Assert.Equal(0, driver.Ran);

        var system = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "TOOLS_GIT", new { way = "system" }));
        Assert.True(system.GetProperty("success").GetBoolean(), system.GetRawText());
        await client.SendAsync(DriverModule.Name, "PLUGIN_UPDATE", new { id = "acme.watch", apply = true });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 2);
        var preview = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "PLUGIN_UPDATE", new { id = "acme.watch" }));
        Assert.True(preview.GetProperty("success").GetBoolean(), preview.GetRawText());
        Assert.Equal(2, driver.Ran);
        Assert.Equal(["TOOLS_GIT", "PLUGIN_UPDATE"], pipe.Confirmations.Now().Waiting.Select(each => each.Route));
    }

    /// <summary>A connection's requests under way are bounded: past the bound, one is answered busy and never run.</summary>
    [Fact]
    public async Task Past_its_requests_in_flight_a_connection_is_answered_busy()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var press = new HeldModule("TEST.PRESS");
        var (dispatcher, _, services) = Compose(log, press);
        using var __ = services;
        using var pipe = Pipe(dispatcher, log, new PipeLimits { InFlight = 2 });
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        var first = await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s1a2b3c4" });
        var second = await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s5e6f7a8" });
        await UntilAsync(() => press.Ran == 2);
        var third = await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s9a0b1c2" });

        Assert.Equal(InstancePipe.Busy, Code(await client.AnswerAsync(third)));
        press.Let.SetResult();
        Assert.True((await client.AnswerAsync(first)).GetProperty("success").GetBoolean());
        Assert.True((await client.AnswerAsync(second)).GetProperty("success").GetBoolean());
        Assert.Equal(2, press.Ran);

        // Its place free again, a request runs.
        Assert.True((await client.AnswerAsync(await client.SendAsync("TEST.PRESS", "MOVE", new { id = "s9a0b1c2" }))).GetProperty("success").GetBoolean());
    }

    /// <summary>
    /// The lines a connection sends ahead of what it has read are bounded too: one that sends far past its answers without
    /// reading them is closed, so its lines never pile up in the instance.
    /// </summary>
    [Fact]
    public async Task A_connection_that_runs_far_ahead_of_its_answers_is_closed()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var (dispatcher, _, services) = Compose(log);
        using var __ = services;
        using var pipe = Pipe(dispatcher, log, new PipeLimits { Queued = 4 });
        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);

        // Two hundred asks of the census, each answered with every route, sent before one answer is read.
        var asks = string.Join("\n", Enumerable.Range(0, 200).Select(at =>
            $$"""{"id":"ask-{{at}}","module":"{{InstancePipe.Module}}","type":"ROUTES","key":"0123456789abcdef0123"}"""));
        await client.SendLineAsync(asks);

        var read = 0;
        while (await client.ReadLineAsync() is not null) read++;
        Assert.True(read < 200, $"{read} answers came, and the connection was never closed");
    }

    private List<JsonElement> Lines()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
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
