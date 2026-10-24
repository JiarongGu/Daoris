using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A person's press from a terminal is confirmed in the window (RPC1c, D156 point 4, the person-door design §4.2, the terminal
/// RPC design §4.2) and waits until the person answers (CONFIRMWAIT1a-i, the confirm-wait design §2): a <c>person</c> route sent over
/// the pipe is shown to the window as a card naming the terminal, the press and every value it carries, and runs once on the person's
/// <b>Confirm</b>, its grounds still holding; refused, changed, withdrawn, closed or past the ten that may wait, nothing runs. The
/// terminal is told it waits, and how it ended; <c>RESULT</c> says <c>waiting</c> while it does.
/// </summary>
/// <remarks>
/// Each test serves its own pipe in this process, named by its own scratch home's id, so nothing here meets a running Daoris. The
/// window's answer is the confirmations' own, as the bridge's <c>PERSON_CONFIRM</c> gives it (<c>DriverModulePersonTests</c>); the
/// grounds are read from a stand-in for the loop's readers (<see cref="StubGrounds"/>), and any clock is held (<see cref="HeldClock"/>).
/// The pipe is Windows': elsewhere a test returns at once.
/// </remarks>
public sealed class PipeConfirmTests : Bridge
{
    private const string Land = "LAND_SESSION_TREE";

    /// <summary>The dispatcher as the shell composes it, the refusal log first, over the test's modules.</summary>
    private (IMessageDispatcher Dispatcher, ServiceProvider Services) Compose(MachineLog log, params IIpcModule[] modules)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        var options = new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) };
        var tracker = new IpcRequestTracker(Bus, options);
        services.AddSingleton<IIpcRequestTracker>(tracker);
        foreach (var module in modules) services.AddSingleton(module);
        services.AddSingleton<IIpcModule>(new Shenora.Modules.Requests.IpcRequestsModule(
            tracker, options, Microsoft.Extensions.Logging.Abstractions.NullLogger<Shenora.Modules.Requests.IpcRequestsModule>.Instance));
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMessageDispatcher>(), provider);
    }

    private InstancePipe Pipe(IMessageDispatcher dispatcher, MachineLog log, TimeProvider? clock = null, StubGrounds? grounds = null, PipeCensus? census = null)
    {
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, census: census, version: "9.9.9-test", clock: clock ?? new HeldClock());
        pipe.Grounds = grounds ?? new StubGrounds();
        return pipe;
    }

    private string HomeId => HomeIdentity.Read(Home)!;

    /// <summary>A served pipe, a terminal said hello on it, and the driver held under it, each the test's.</summary>
    private async Task<(InstancePipe Pipe, PipeClient Client, HeldModule Driver, ServiceProvider Services, MachineLog Log)> ServeAsync(
        StubGrounds? grounds = null, HeldClock? clock = null, PipeCensus? census = null)
    {
        var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        var pipe = Pipe(dispatcher, log, clock, grounds, census);
        var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);
        return (pipe, client, driver, services, log);
    }

    [Fact]
    public async Task A_person_s_press_waits_for_the_window_and_runs_once_on_Confirm()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var id = await client.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4", follow = true });

        // The terminal is told it waits, by the instance, naming its request and no time (CONFIRMWAIT1a-i).
        var told = await client.NotificationAsync(Poll.Patience);
        Assert.NotNull(told);
        var waits = Assert.Single(told.Value.GetProperty("payload").EnumerateArray());
        Assert.Equal(InstancePipe.Module, waits.GetProperty("module").GetString());
        Assert.Equal(InstancePipe.Confirming, waits.GetProperty("type").GetString());
        Assert.Equal(id, waits.GetProperty("payload").GetProperty("id").GetString());
        Assert.False(waits.GetProperty("payload").TryGetProperty("expires", out var _____));

        // The window is shown the press by its route, every value it carries, and the terminal by its process; nothing ran.
        var shown = Assert.Single(pipe.Confirmations.Now().Waiting);
        Assert.Equal(DriverModule.Name, shown.Module);
        Assert.Equal(Land, shown.Route);
        Assert.Equal(
            [new PipeConfirmationField("/id", "string", "s1a2b3c4"), new PipeConfirmationField("/follow", "boolean", "true")],
            shown.Fields);
        Assert.Equal(Environment.ProcessId, shown.Asker.Pid);
        Assert.False(string.IsNullOrEmpty(shown.Asker.Process));
        Assert.Null(shown.ShownAt);
        Assert.Null(shown.Expires);
        Assert.NotEqual(id, shown.Id);
        Assert.Equal(0, driver.Ran);
        await UntilAsync(() => Raised.Any(each => each.Module == StatePublisher.Module && each.Type == PipeConfirmations.Event
            && each.Payload is PipeConfirmationsNow { Waiting: [{ Route: Land }] }));

        // RESULT says it waits, with when it was asked and that this terminal waits on it.
        var waiting = await client.ResultAsync(id);
        Assert.Equal("waiting", waiting.GetProperty("state").GetString());
        Assert.True(waiting.GetProperty("waitedOn").GetBoolean());
        Assert.Equal(shown.Asked, waiting.GetProperty("asked").GetDateTimeOffset());

        // Confirmed in the window: the press runs once, and the terminal hears its answer.
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(shown.Id, confirm: true)).Outcome);
        var answer = await client.AnswerAsync(id);
        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, driver.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(PipeAnswered.Gone, (await pipe.Confirmations.AnswerAsync(shown.Id, confirm: true)).Outcome);

        // Sent again, the id is answered from its line: never asked again, never run again.
        await client.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4", follow = true }, id: id);
        Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);

        // Said in the machine log by codes alone: the state, the route and the terminal's door; never a value or an id.
        Assert.Equal(
            [
                """{"state":"asked","route":"DAORIS.DRIVER.LAND_SESSION_TREE","door":"terminal"}""",
                """{"state":"checking","route":"DAORIS.DRIVER.LAND_SESSION_TREE","door":"terminal"}""",
                """{"state":"confirmed","route":"DAORIS.DRIVER.LAND_SESSION_TREE","door":"terminal"}""",
            ],
            Confirmations());
    }

    /// <summary>The design's first row (§10): a card left unanswered for an hour, then confirmed, makes its press and tells its asker.</summary>
    [Fact]
    public async Task A_card_left_unanswered_for_an_hour_then_confirmed_runs_once_and_its_terminal_hears_it_or_reads_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var clock = new HeldClock();
        var grounds = new StubGrounds().Session("s1a2b3c4", "awaiting-person");
        var (pipe, client, driver, services, log) = await ServeAsync(grounds, clock);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        // One whose terminal waits on its line, and one whose terminal left once told it waits (`--no-wait`).
        var online = await client.SendAsync(DriverModule.Name, "SESSION_INPUT", new { id = "s1a2b3c4", text = "go on" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var gone = await PipeClient.ConnectAsync(pipe.Name!);
        await gone.HelloAsync(HomeId);
        var left = await gone.SendAsync(DriverModule.Name, "SESSION_INPUT", new { id = "s1a2b3c4", text = "and this" });
        await gone.NotificationAsync(Poll.Patience);
        var leftKey = gone.KeyOf(left)!;
        await gone.DisposeAsync();
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 2 && pipe.Confirmations.Now().Waiting.Any(each => !each.WaitedOn));
        Assert.True(pipe.Confirmations.Show(pipe.Confirmations.Now().Waiting[0].Id));

        // An hour passes, and past a shown: both still wait.
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(2, pipe.Confirmations.Now().Waiting.Count);
        Assert.Equal("waiting", (await client.ResultAsync(online)).GetProperty("state").GetString());

        foreach (var each in pipe.Confirmations.Now().Waiting)
        {
            Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(each.Id, confirm: true)).Outcome);
        }

        Assert.True((await client.AnswerAsync(online)).GetProperty("success").GetBoolean());
        await UntilAsync(() => driver.Ran == 2);
        await using var later = await PipeClient.ConnectAsync(pipe.Name!);
        await later.HelloAsync(HomeId);
        var read = await later.ResultAsync(left, leftKey);
        Assert.Equal("answered", read.GetProperty("state").GetString());
        Assert.True(read.GetProperty("answer").GetProperty("success").GetBoolean());
        Assert.Equal(2, driver.Ran);
    }

    [Fact]
    public async Task A_press_the_person_refuses_runs_nothing_and_the_terminal_is_told_so()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(new StubGrounds().Session("s1a2b3c4", "completed"));
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var id = await client.SendAsync(DriverModule.Name, "SESSION_ARCHIVE", new { ids = new[] { "s1a2b3c4" }, archived = true });
        await client.NotificationAsync(Poll.Patience);
        var shown = Assert.Single(pipe.Confirmations.Now().Waiting);

        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(shown.Id, confirm: false)).Outcome);
        var answer = await client.AnswerAsync(id);

        Assert.Equal(InstancePipe.Refused, Code(answer));
        Assert.Equal("The person refused this in Daoris's window. Nothing was done.", answer.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(0, driver.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        // A terminal that lost its connection reads the refusal from RESULT, as any ending.
        var ended = await client.ResultAsync(id);
        Assert.Equal("answered", ended.GetProperty("state").GetString());
        Assert.Equal(InstancePipe.Refused, ended.GetProperty("answer").GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(["asked", "refused"], Confirmations().Select(line => JsonDocument.Parse(line).RootElement.GetProperty("state").GetString()));
    }

    /// <summary>
    /// The design's stale row (§10): one press per ground, each changed between its ask and its Confirm, runs nothing and its terminal
    /// hears <c>changed</c> with the reason's code and values.
    /// </summary>
    [Theory]
    [InlineData("turn")]
    [InlineData("session")]
    [InlineData("setting")]
    [InlineData("repository")]
    [InlineData("quest")]
    [InlineData("ask")]
    [InlineData("plan")]
    [InlineData("branch")]
    [InlineData("wait")]
    public async Task A_press_whose_ground_moved_while_it_waited_is_refused_at_the_Confirm_with_its_reason(string reason)
    {
        if (!OperatingSystem.IsWindows()) return;
        var grounds = new StubGrounds().Session("s1a2b3c4", "awaiting-person");
        grounds.Registered.Add("Lyntai");
        grounds.Quests["q1"] = "Open";
        grounds.Asks["a1"] = "open";
        grounds.Heads["s1a2b3c4"] = "aaaa";
        grounds.Held["go-ahead"] = "open";
        var drivable = new List<string>();
        grounds.Reads["STATE"] = _ => new { drivable = drivable.ToArray() };
        var sweep = "unlanded";
        grounds.Reads["SWEEP_PLAN"] = _ => new { branches = new[] { new { repository = "Lyntai", branch = "quest/x", kind = sweep, commits = 1 } } };
        var (route, payload, move, said) = reason switch
        {
            "turn" => ("SESSION_INPUT", (object)new { id = "s1a2b3c4", text = "go on" },
                (Action)(() => grounds.Session("s1a2b3c4", "awaiting-person", minute: 5)), (string?)null),
            "session" => ("SESSION_INPUT", new { id = "s1a2b3c4", text = "go on" }, () => grounds.Session("s1a2b3c4", "completed", minute: 5),
                "it was awaiting-person, it is completed"),
            "setting" => ("SET_DRIVABLE", new { repository = "Lyntai", drivable = true }, () => drivable.Add("Lyntai"), null),
            "repository" => ("SET_TREES", new { repository = "Lyntai", ownTree = true }, () => grounds.Registered.Remove("Lyntai"),
                "the repository Lyntai is no longer registered on this machine"),
            "quest" => ("RETRY_QUEST", new { quest = "q1" }, () => grounds.Quests["q1"] = "Taken", "it was Open, it is Taken"),
            "ask" => ("LAND_ASK", new { ask = "a1" }, () => grounds.Asks["a1"] = "closed", null),
            "plan" => ("SWEEP", new { workspace = "games" }, () => sweep = "landed", null),
            "branch" => (Land, new { id = "s1a2b3c4" }, () => grounds.Heads["s1a2b3c4"] = "bbbb", "the branch s1a2b3c4 gained or lost commits"),
            _ => ("SESSION_GO_AHEAD", new { id = "s1a2b3c4", ask = "a1", number = 1, approved = true }, () => grounds.Held["go-ahead"] = "answered", null),
        };
        var (pipe, client, driver, services, log) = await ServeAsync(grounds);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var id = await client.SendAsync(DriverModule.Name, route, payload);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var shown = pipe.Confirmations.Now().Waiting[0];
        move();

        var answer = await pipe.Confirmations.AnswerAsync(shown.Id, confirm: true);

        Assert.Equal(PipeAnswered.Changed, answer.Outcome);
        Assert.Equal(reason, answer.Change!.Reason);
        var heard = await client.AnswerAsync(id);
        Assert.Equal(InstancePipe.Changed, Code(heard));
        Assert.Equal(reason, heard.GetProperty("error").GetProperty("parameters").GetProperty("ground").GetString());
        if (said is not null) Assert.Contains(said, heard.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("Nothing was done.", heard.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(0, driver.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
    }

    [Fact]
    public async Task A_change_made_before_the_ask_is_no_change()
    {
        if (!OperatingSystem.IsWindows()) return;
        var grounds = new StubGrounds().Session("s1a2b3c4", "working");
        var (pipe, client, driver, services, log) = await ServeAsync(grounds);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        // The session moved on, then the press was asked, then confirmed with nothing moved since: it runs.
        grounds.Session("s1a2b3c4", "awaiting-person", minute: 9);
        var id = await client.SendAsync(DriverModule.Name, "SESSION_INPUT", new { id = "s1a2b3c4", text = "go on" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);

        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(pipe.Confirmations.Now().Waiting[0].Id, confirm: true)).Outcome);
        Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran);
    }

    [Fact]
    public async Task A_setting_already_as_asked_is_answered_at_once_with_no_card_and_a_route_with_no_grounds_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var grounds = new StubGrounds();
        grounds.Registered.Add("Lyntai");
        grounds.Reads["STATE"] = _ => new { drivable = new[] { "Lyntai" } };
        var census = PipeCensus.Daoris.With(DriverModule.Name, "NEW_PRESS", PipeClass.Person);
        var (pipe, client, driver, services, log) = await ServeAsync(grounds, census: census);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var already = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "SET_DRIVABLE", new { repository = "Lyntai", drivable = true }));
        Assert.Equal(InstancePipe.Already, Code(already));
        Assert.Empty(pipe.Confirmations.Now().Waiting);

        var reads = grounds.Count;
        var ungrounded = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "NEW_PRESS", new { id = "x" }));
        Assert.Equal(InstancePipe.Ungrounded, Code(ungrounded));
        Assert.Equal("Daoris does not know what this press rests on, so it cannot wait for the person; nothing was asked.",
            ungrounded.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(reads, grounds.Count);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, driver.Ran);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_read_that_fails_or_passes_its_bound_at_the_ask_asks_nothing(bool late)
    {
        if (!OperatingSystem.IsWindows()) return;
        var clock = new HeldClock();
        var grounds = new StubGrounds().Session("s1a2b3c4", "completed");
        if (late) grounds.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        else grounds.Fails = new IOException("the service is reconnecting");
        var (pipe, client, driver, services, log) = await ServeAsync(grounds, clock);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var id = await client.SendAsync(DriverModule.Name, "SESSION_DELETE", new { id = "s1a2b3c4" });
        if (late)
        {
            await UntilAsync(() => grounds.Count == 1);
            clock.Advance(PipeGround.StateBound);
        }

        Assert.Equal(InstancePipe.UnreadGrounds, Code(await client.AnswerAsync(id)));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, driver.Ran);
        // Its place went with it.
        Assert.NotNull(pipe.Confirmations.Hold());
    }

    [Fact]
    public async Task A_read_that_fails_at_the_Confirm_leaves_it_waiting_and_a_second_Confirm_with_the_reader_mended_runs_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var grounds = new StubGrounds().Session("s1a2b3c4", "completed");
        var (pipe, client, driver, services, log) = await ServeAsync(grounds);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var id = await client.SendAsync(DriverModule.Name, "SESSION_DELETE", new { id = "s1a2b3c4" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var shown = pipe.Confirmations.Now().Waiting[0];

        grounds.Fails = new IOException("a git lock");
        Assert.Equal(PipeAnswered.Unread, (await pipe.Confirmations.AnswerAsync(shown.Id, confirm: true)).Outcome);
        Assert.False(Assert.Single(pipe.Confirmations.Now().Waiting).Checking);
        Assert.Equal("waiting", (await client.ResultAsync(id)).GetProperty("state").GetString());

        grounds.Fails = null;
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(shown.Id, confirm: true)).Outcome);
        Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran);
    }

    [Fact]
    public async Task Ten_wait_at_once_and_an_eleventh_is_refused_naming_the_oldest_with_no_reader_called()
    {
        if (!OperatingSystem.IsWindows()) return;
        var clock = new HeldClock();
        var grounds = new StubGrounds();
        var (pipe, client, driver, services, log) = await ServeAsync(grounds, clock);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        await using var two = await PipeClient.ConnectAsync(pipe.Name!);
        await two.HelloAsync(HomeId);

        // Ten, from two terminals: the bound is the window's, not a connection's.
        for (var each = 0; each < 6; each++) await client.SendAsync(DriverModule.Name, "SESSION_DELETE", new { id = $"s000000{each}" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 6);
        clock.Advance(TimeSpan.FromHours(2));
        for (var each = 6; each < 10; each++) await two.SendAsync(DriverModule.Name, "SESSION_DELETE", new { id = $"s000000{each}" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 10);
        var reads = grounds.Count;

        var eleventh = await two.SendAsync(DriverModule.Name, "SESSION_DELETE", new { id = "s0000010" });
        var refused = await two.AnswerAsync(eleventh);
        Assert.Equal(InstancePipe.Full, Code(refused));
        Assert.Equal(
            "Ten presses already wait for the person's confirmation, the oldest asked 2 hours ago; this one was not asked. Nothing was kept.",
            refused.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(reads, grounds.Count);
        Assert.Equal(10, pipe.Confirmations.Now().Waiting.Count);

        // One answered frees its place, and the eleventh sent again with its own id waits.
        await pipe.Confirmations.AnswerAsync(pipe.Confirmations.Now().Waiting[0].Id, confirm: false);
        await two.SendAsync(DriverModule.Name, "SESSION_DELETE", new { id = "s0000010" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 10);
        Assert.Equal(0, driver.Ran);
    }

    /// <summary>
    /// CONFIRMWAIT1a-ii (§7.2, §10's drain row): a close with a press waiting answers its line <c>waiting</c>, <c>why: close</c>, keeps its
    /// record and ends nothing; the drain is not spent on it, nothing is logged as left, a Confirm meanwhile takes nothing, and its card
    /// says no terminal waits on it now.
    /// </summary>
    [Fact]
    public async Task A_close_answers_a_waiting_line_waiting_keeps_its_record_and_drains_at_once()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(new StubGrounds().Session("s1a2b3c4", "completed"));
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var noticed = new List<PipeConfirmationEnd>();
        pipe.Confirmations.Noticed = (_, end) => noticed.Add(end);
        var id = await client.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var shown = pipe.Confirmations.Now().Waiting[0];
        var record = Assert.Single(Records());

        pipe.BeginStopping();

        var answer = await client.AnswerAsync(id);
        Assert.Equal(InstancePipe.Waiting, Code(answer));
        Assert.Equal("close", answer.GetProperty("error").GetProperty("parameters").GetProperty("why").GetString());
        Assert.Equal($"Daoris is closing; this still waits for your confirmation (request {id}).",
            answer.GetProperty("error").GetProperty("message").GetString());
        var drained = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(pipe.Drain(TimeSpan.FromSeconds(15)));
        Assert.True(drained.Elapsed < TimeSpan.FromSeconds(5), $"the drain took {drained.Elapsed}");
        Assert.Empty(Logged("pipe.drained"));
        Assert.Equal(record, Assert.Single(Records()));
        Assert.Equal("waiting", ReadRecord(record).GetProperty("state").GetString());
        Assert.Empty(noticed);
        Assert.Equal(PipeAnswered.Draining, (await pipe.Confirmations.AnswerAsync(shown.Id, confirm: true)).Outcome);
        Assert.Equal(shown.Id, Assert.Single(pipe.Confirmations.Now().Waiting, each => !each.WaitedOn).Id);
        Assert.Equal(0, driver.Ran);
    }

    /// <summary>
    /// The design's restart row (§10): a press asked on one pipe over a home, the pipe closed: its line answers <c>waiting</c>; a second pipe
    /// on the same home lists it again, restored and said as its record's, Confirm runs it once, and <c>RESULT</c> from a new connection
    /// with the first key answers it. Its record is gone once it ran.
    /// </summary>
    [Fact]
    public async Task A_press_waiting_at_a_close_waits_again_after_the_restart_and_runs_once_on_Confirm()
    {
        if (!OperatingSystem.IsWindows()) return;
        var clock = new HeldClock();
        var grounds = new StubGrounds().Session("s1a2b3c4", "completed");
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var _ = services;
        var first = Pipe(dispatcher, log, clock, grounds);
        var client = await PipeClient.ConnectAsync(first.Serve()!);
        await client.HelloAsync(HomeId);
        var id = await client.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4", follow = true });
        var key = client.KeyOf(id)!;
        await UntilAsync(() => first.Confirmations.Now().Waiting.Count == 1);
        var shown = first.Confirmations.Now().Waiting[0];
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(first.Confirmations.Show(shown.Id));
        var seen = first.Confirmations.Now().Waiting[0].ShownAt;

        first.BeginStopping();
        Assert.Equal(InstancePipe.Waiting, Code(await client.AnswerAsync(id)));
        Assert.True(first.Drain(Poll.Patience));
        await client.DisposeAsync();
        first.Dispose();

        using var next = Pipe(dispatcher, log, clock, grounds);
        next.Restore();
        var card = Assert.Single(next.Confirmations.Now().Waiting);
        Assert.Equal((shown.Id, shown.Asked, seen, shown.Asker), (card.Id, card.Asked, card.ShownAt, card.Asker));
        Assert.Equal(shown.Fields, card.Fields);
        Assert.True(card.Restored);
        Assert.False(card.WaitedOn);
        await using var later = await PipeClient.ConnectAsync(next.Serve()!);
        await later.HelloAsync(HomeId);
        Assert.Equal("waiting", (await later.ResultAsync(id, key)).GetProperty("state").GetString());

        Assert.Equal(PipeAnswered.Answered, (await next.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);

        await UntilAsync(() => driver.Ran == 1);
        await UntilAsync(() => Records().Count == 0);
        var read = await later.ResultAsync(id, key);
        Assert.Equal("answered", read.GetProperty("state").GetString());
        Assert.True(read.GetProperty("answer").GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran);
    }

    /// <summary>
    /// The design's crash row (§10): a press asked on a first pipe that is never disposed, and a second made over the same home, whose
    /// name the first still holds: the record is restored, its card back, though the second pipe serves nothing.
    /// </summary>
    [Fact]
    public async Task A_press_waiting_at_a_crash_is_restored_though_the_pipe_s_name_is_still_held()
    {
        if (!OperatingSystem.IsWindows()) return;
        var grounds = new StubGrounds().Session("s1a2b3c4", "completed");
        var (crashed, client, driver, services, log) = await ServeAsync(grounds);
        using var _ = services;
        using var __ = log;
        await using var ___ = client;
        await client.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4" });
        await UntilAsync(() => crashed.Confirmations.Now().Waiting.Count == 1);
        var shown = crashed.Confirmations.Now().Waiting[0];

        using var next = Pipe(Compose(log, driver).Dispatcher, log, grounds: grounds);
        next.Restore();

        Assert.Null(next.Serve());
        var card = Assert.Single(next.Confirmations.Now().Waiting);
        Assert.Equal((shown.Id, Land, true), (card.Id, card.Route, card.Restored));
        Assert.Equal(PipeAnswered.Answered, (await next.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);
        await UntilAsync(() => driver.Ran == 1);
        crashed.Dispose();
    }

    /// <summary>
    /// The design's file name row (§10): a press whose terminal's id is a path is kept under the confirmation's own id, 32 hexadecimal
    /// characters, in <c>data/pipe-waiting/</c>; nothing is written or removed anywhere else, and it asks and ends as any.
    /// </summary>
    [Fact]
    public async Task A_record_is_named_by_the_confirmation_s_id_never_the_terminal_s()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(new StubGrounds().Session("s1a2b3c4", "completed"));
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var before = Files();
        const string Path = @"..\..\data\standing-confirmation";

        var id = await client.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4" }, id: Path);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);

        var record = Assert.Single(Records());
        Assert.Matches("^[0-9a-f]{32}\\.json$", System.IO.Path.GetFileName(record));
        Assert.Equal(pipe.Confirmations.Now().Waiting[0].Id + ".json", System.IO.Path.GetFileName(record));
        Assert.Equal(Path, ReadRecord(record).GetProperty("request").GetString());
        Assert.Equal([record], Files().Except(before));
        Assert.Empty(before.Except(Files()));
        Assert.False(File.Exists(DaorisHome.Entry(Home, HomeEntry.StandingConfirmation)));

        await pipe.Confirmations.AnswerAsync(pipe.Confirmations.Now().Waiting[0].Id, confirm: false);
        Assert.Equal(InstancePipe.Refused, Code(await client.AnswerAsync(id)));
        Assert.Empty(Records());
        Assert.Empty(before.Except(Files()));
        Assert.Equal(InstancePipe.Refused, Code((await client.ResultAsync(id)).GetProperty("answer")));
    }

    /// <summary>
    /// The design's keyed row (§10, §7.1): <c>DAORIS.REMOTES</c> <c>SET</c> carries the workspace's deployment key, so its record holds
    /// neither the key nor the payload, its card says it does not wait across a restart, and a close ends it lost: its line told so,
    /// the person told, its record gone.
    /// </summary>
    [Fact]
    public async Task A_keyed_press_keeps_no_payload_and_a_close_ends_it_lost()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var noticed = new List<PipeConfirmationEnd>();
        pipe.Confirmations.Noticed = (_, end) => noticed.Add(end);
        const string Key = "deployment-key-never-kept-0123456789";

        var id = await client.SendAsync("DAORIS.REMOTES", "SET", new { workspace = "games", url = "https://example.invalid", key = Key });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);

        Assert.True(pipe.Confirmations.Now().Waiting[0].Keyed);
        var bytes = File.ReadAllText(Assert.Single(Records()));
        Assert.DoesNotContain(Key, bytes, StringComparison.Ordinal);
        Assert.DoesNotContain("example.invalid", bytes, StringComparison.Ordinal);
        Assert.True(ReadRecord(Records()[0]).GetProperty("keyed").GetBoolean());
        Assert.False(ReadRecord(Records()[0]).TryGetProperty("payload", out var _____));

        pipe.BeginStopping();

        Assert.Equal(InstancePipe.Lost, Code(await client.AnswerAsync(id)));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal([PipeConfirmationEnd.Lost], noticed);
        Assert.Empty(Records());
    }

    /// <summary>The design's keyed row (§10): a keyed press's record left by a crash ends lost at the next start, said, and is never asked.</summary>
    [Fact]
    public async Task A_keyed_press_left_by_a_crash_ends_lost_at_the_next_start()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (crashed, client, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        await using var ___ = client;
        await client.SendAsync("DAORIS.REMOTES", "SET", new { workspace = "games", url = "https://example.invalid", key = "deployment-key-0123456789" });
        await UntilAsync(() => crashed.Confirmations.Now().Waiting.Count == 1);

        using var next = Pipe(Compose(log, driver).Dispatcher, log);
        var told = new List<(int, int)>();
        next.Confirmations.Restarted = (lost, newer) => told.Add((lost, newer));
        next.Restore();

        Assert.Empty(next.Confirmations.Now().Waiting);
        Assert.Empty(Records());
        Assert.Equal([(1, 0)], told);
        crashed.Dispose();
    }

    /// <summary>
    /// The design's restore judgement row (§10, §7.2): a record is judged again as a press is judged at its ask, from its module, route and
    /// payload alone. A route the census makes open or the window's, a payload whose fingerprint is not its binding's, one of 257 values,
    /// a route with no grounds row, the eleventh waiting, a record that does not read: each ends lost, never asked, one notice for all. A
    /// passing record's card is made from its payload, whatever the record holds beside. A record begun answers <c>interrupted</c>; one
    /// ended whose journal line was never written answers its ending; a newer one is left in place, not asked; a name that is no
    /// confirmation's id is removed.
    /// </summary>
    [Fact]
    public async Task The_restore_judges_each_record_again_as_a_press_is_judged_at_its_ask()
    {
        if (!OperatingSystem.IsWindows()) return;
        var grounds = new StubGrounds().Session("s1a2b3c4", "completed");
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var _ = services;
        var census = PipeCensus.Daoris.With(DriverModule.Name, "NEW_PRESS", PipeClass.Person);
        const string Key = "the-first-terminal-s-key-0123456789";
        var land = new { id = "s1a2b3c4" };
        var lost = new List<string>
        {
            WriteRecord("open", DriverModule.Name, "STATE", new { }, Key),
            WriteRecord("window", DriverModule.Name, "PERSON_CONFIRM", new { id = "x", confirm = true }, Key),
            WriteRecord("forged", DriverModule.Name, Land, land, Key, fingerprint: Sha("{\"id\":\"s5e6f7a8\"}")),
            WriteRecord("wide", DriverModule.Name, "SESSION_ARCHIVE", new { ids = Enumerable.Range(0, 257).Select(each => $"s{each:D7}").ToArray(), archived = true }, Key),
            WriteRecord("ungrounded", DriverModule.Name, "NEW_PRESS", new { id = "s1a2b3c4" }, Key),
        };
        var cut = System.IO.Path.Combine(RecordsFolder, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(cut, "{\"version\":1,\"confirm");
        var passing = new List<string>();
        for (var each = 0; each < 11; each++)
        {
            passing.Add(WriteRecord($"waiting-{each}", DriverModule.Name, Land, land, Key, minutes: each,
                extra: each == 0 ? new { fields = new[] { new { name = "/id", type = "string", value = "s9999999" } } } : null));
        }

        var begun = WriteRecord("begun", DriverModule.Name, Land, land, Key, state: "begun");
        var ended = WriteRecord("ended", DriverModule.Name, Land, land, Key, state: "ended",
            answer: """{"id":"ended","success":false,"error":{"code":"refused","message":"The person refused this in Daoris's window. Nothing was done."}}""");
        var newer = WriteRecord("newer", DriverModule.Name, Land, land, Key, version: 99);
        var misnamed = System.IO.Path.Combine(RecordsFolder, "..-standing.json");
        File.WriteAllText(misnamed, "{}");

        using var pipe = new InstancePipe(dispatcher, Bus, Home, log, census: census, version: "9.9.9-test", clock: new HeldClock()) { Grounds = grounds };
        var told = new List<(int Lost, int Newer)>();
        pipe.Confirmations.Restarted = (lostCount, newerCount) => told.Add((lostCount, newerCount));
        pipe.Restore();

        var waiting = pipe.Confirmations.Now().Waiting;
        Assert.Equal(10, waiting.Count);
        Assert.All(waiting, card => Assert.Equal(new PipeConfirmationField("/id", "string", "s1a2b3c4"), Assert.Single(card.Fields)));
        Assert.Equal(passing.Take(10).Select(System.IO.Path.GetFileNameWithoutExtension), waiting.Select(card => card.Id));
        Assert.Equal([(lost.Count + 2, 1)], told);
        Assert.Equal(passing.Take(10).Append(newer).Order(StringComparer.Ordinal), Records());
        Assert.False(File.Exists(misnamed));
        Assert.Equal(0, driver.Ran);

        await using var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);
        foreach (var each in new[] { "open", "window", "forged", "wide", "ungrounded" })
        {
            Assert.Equal(InstancePipe.Lost, Code((await client.ResultAsync(each, Key)).GetProperty("answer")));
        }

        Assert.Equal(InstancePipe.Lost, Code((await client.ResultAsync("waiting-10", Key)).GetProperty("answer")));
        Assert.Equal(InstancePipe.Interrupted, Code((await client.ResultAsync("begun", Key)).GetProperty("answer")));
        Assert.Equal(InstancePipe.Refused, Code((await client.ResultAsync("ended", Key)).GetProperty("answer")));
        Assert.Equal("unknown", (await client.ResultAsync("newer", Key)).GetProperty("state").GetString());
        Assert.Equal("waiting", (await client.ResultAsync("waiting-0", Key)).GetProperty("state").GetString());
        Assert.Equal(0, driver.Ran);
    }

    /// <summary>
    /// The design's journal row (§10, §7.1): a refused press whose journal line cannot be written still has its record removed, since
    /// the record said it ended first, and a restart asks nothing again.
    /// </summary>
    [Fact]
    public async Task A_press_whose_journal_line_fails_is_still_removed_and_not_asked_again()
    {
        if (!OperatingSystem.IsWindows()) return;
        // The journal's folder is a file: no line can be written there.
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DaorisHome.Entry(Home, HomeEntry.Pipe))!);
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Pipe), "not a folder");
        var grounds = new StubGrounds().Session("s1a2b3c4", "completed");
        var (pipe, client, driver, services, log) = await ServeAsync(grounds);
        using var _ = services;
        using var __ = log;
        await using var ___ = client;
        var id = await client.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        Assert.Single(Records());

        await pipe.Confirmations.AnswerAsync(pipe.Confirmations.Now().Waiting[0].Id, confirm: false);

        Assert.Equal(InstancePipe.Refused, Code(await client.AnswerAsync(id)));
        Assert.Empty(Records());
        pipe.Dispose();
        using var next = Pipe(Compose(log, driver).Dispatcher, log, grounds: grounds);
        next.Restore();
        Assert.Empty(next.Confirmations.Now().Waiting);
    }

    /// <summary>§7.1: a press whose record cannot be written is refused <c>unrecorded</c>, nothing asked, its place given back.</summary>
    [Fact]
    public async Task A_press_whose_record_cannot_be_written_is_refused_and_nothing_is_asked()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(RecordsFolder)!);
        File.WriteAllText(RecordsFolder, "not a folder");
        var (pipe, client, driver, services, log) = await ServeAsync(new StubGrounds().Session("s1a2b3c4", "completed"));
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var id = await client.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4" });

        Assert.Equal(InstancePipe.Unrecorded, Code(await client.AnswerAsync(id)));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.NotNull(pipe.Confirmations.Hold());
        Assert.Equal(0, driver.Ran);
    }

    /// <summary>
    /// The design's drain row (§10): during an update's drain a Confirm is refused on the card and the press still waits; after the
    /// restart it is restored.
    /// </summary>
    [Fact]
    public async Task A_Confirm_during_an_update_s_drain_is_refused_and_the_press_is_restored_after_the_restart()
    {
        if (!OperatingSystem.IsWindows()) return;
        var grounds = new StubGrounds().Session("s1a2b3c4", "completed");
        var (pipe, client, driver, services, log) = await ServeAsync(grounds);
        using var _ = services;
        using var __ = log;
        await using var ___ = client;
        var id = await client.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = pipe.Confirmations.Now().Waiting[0];
        pipe.Draining = () => true;

        Assert.Equal(PipeAnswered.Draining, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);

        Assert.Equal(card.Id, Assert.Single(pipe.Confirmations.Now().Waiting).Id);
        Assert.Equal("waiting", (await client.ResultAsync(id)).GetProperty("state").GetString());
        pipe.BeginStopping();
        Assert.Equal(InstancePipe.Waiting, Code(await client.AnswerAsync(id)));
        pipe.Dispose();
        using var next = Pipe(Compose(log, driver).Dispatcher, log, grounds: grounds);
        next.Restore();
        Assert.Equal(card.Id, Assert.Single(next.Confirmations.Now().Waiting).Id);
        Assert.Equal(0, driver.Ran);
    }

    /// <summary>The design's withdraw rows (§10): a waiting press taken back, the race with a Confirm either way, and another key's.</summary>
    [Fact]
    public async Task Withdraw_takes_back_only_a_press_that_still_waits_and_never_a_confirmed_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        var grounds = new StubGrounds().Session("s1a2b3c4", "completed");
        var (pipe, client, driver, services, log) = await ServeAsync(grounds);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var noticed = new List<PipeConfirmationEnd>();
        pipe.Confirmations.Noticed = (_, end) => noticed.Add(end);

        // Waiting, never shown: withdrawn, nothing run, its card gone, no notice.
        var first = await client.SendAsync(DriverModule.Name, "SESSION_DELETE", new { id = "s1a2b3c4" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var another = await client.AnswerAsync(await client.SendAsync(InstancePipe.Module, "WITHDRAW", new { id = first, key = "another-key-0123456789abcdef" }));
        Assert.Equal(InstancePipe.IdTaken, Code(another));
        Assert.Single(pipe.Confirmations.Now().Waiting);
        var withdrawn = await Withdraw(client, first);
        Assert.Equal("withdrawn", withdrawn.GetProperty("state").GetString());
        Assert.Equal(InstancePipe.Withdrawn, Code(await client.AnswerAsync(first)));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Empty(noticed);
        // Ended: said so, with its ending.
        var ended = await Withdraw(client, first);
        Assert.Equal("ended", ended.GetProperty("state").GetString());
        Assert.Equal(InstancePipe.Withdrawn, ended.GetProperty("answer").GetProperty("error").GetProperty("code").GetString());

        // Shown, then withdrawn: the person is told.
        var second = await client.SendAsync(DriverModule.Name, "SESSION_DELETE", new { id = "s1a2b3c4" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = pipe.Confirmations.Now().Waiting[0];
        Assert.True(pipe.Confirmations.Show(card.Id));
        Assert.Equal("withdrawn", (await Withdraw(client, second)).GetProperty("state").GetString());
        Assert.Equal([PipeConfirmationEnd.Withdrawn], noticed);
        // Withdraw then Confirm: the Confirm finds it gone.
        Assert.Equal(PipeAnswered.Gone, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);

        // Confirm, its grounds read held open, then withdraw: running, nothing cancelled, the press's answer the route's.
        var third = await client.SendAsync(DriverModule.Name, "SESSION_DELETE", new { id = "s1a2b3c4" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        grounds.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirming = pipe.Confirmations.AnswerAsync(pipe.Confirmations.Now().Waiting[0].Id, confirm: true);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting is [{ Checking: true }]);
        Assert.Equal("running", (await Withdraw(client, third)).GetProperty("state").GetString());
        Assert.Equal("running", (await client.ResultAsync(third)).GetProperty("state").GetString());
        grounds.Hold.SetResult();
        Assert.Equal(PipeAnswered.Answered, (await confirming).Outcome);
        Assert.True((await client.AnswerAsync(third)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran);
    }

    [Fact]
    public async Task A_terminal_whose_line_ends_leaves_its_press_waiting_and_its_card_says_no_terminal_waits_on_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(new StubGrounds().Session("s1a2b3c4", "completed"));
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var other = await PipeClient.ConnectAsync(pipe.Name!);
        await other.HelloAsync(HomeId);
        var id = await other.SendAsync(DriverModule.Name, "SESSION_DELETE", new { id = "s1a2b3c4" });
        var key = other.KeyOf(id)!;
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        Assert.True(pipe.Confirmations.Now().Waiting[0].WaitedOn);

        await other.DisposeAsync();

        await UntilAsync(() => pipe.Confirmations.Now().Waiting is [{ WaitedOn: false }]);
        var read = await client.ResultAsync(id, key);
        Assert.Equal("waiting", read.GetProperty("state").GetString());
        Assert.False(read.GetProperty("waitedOn").GetBoolean());
        Assert.Equal(0, driver.Ran);
    }

    [Fact]
    public async Task A_terminal_s_cancel_takes_its_card_back_and_nothing_runs()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(new StubGrounds().Session("s1a2b3c4", "completed"));
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var id = await client.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4" });
        var key = client.KeyOf(id)!;
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);

        // Another key cancels nothing; the sender's takes the card back.
        var another = await client.SendAsync(InstancePipe.RequestsModule, "CANCEL", new { requestId = id }, key: "another-key-0123456789abcdef");
        Assert.Equal(InstancePipe.IdTaken, Code(await client.AnswerAsync(another)));
        Assert.Single(pipe.Confirmations.Now().Waiting);
        var cancel = await client.SendAsync(InstancePipe.RequestsModule, "CANCEL", new { requestId = id }, key: key);
        var cancelled = await client.AnswerAsync(cancel);

        Assert.True(cancelled.GetProperty("data").GetProperty("cancelled").GetBoolean(), cancelled.GetRawText());
        Assert.Equal(IpcErrorCodes.OperationCancelled, Code(await client.AnswerAsync(id)));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, driver.Ran);
        Assert.Equal(["asked", "withdrawn"], Confirmations().Select(line => JsonDocument.Parse(line).RootElement.GetProperty("state").GetString()));
    }

    [Fact]
    public async Task A_payload_that_names_a_field_twice_is_refused_asking_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        // The card would show one and the handler might read the other, as the door reads names ignoring case.
        await client.SendLineAsync(
            """{"id":"twice-1","module":"DAORIS.DRIVER","type":"SESSION_DELETE","payload":{"id":"s1a2b3c4","ID":"s5e6f7a8"},"key":"0123456789abcdef0123"}""");

        Assert.Equal(InstancePipe.Shape, Code(await client.AnswerAsync("twice-1")));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, driver.Ran);
    }

    private static async Task<JsonElement> Withdraw(PipeClient client, string id)
    {
        var answer = await client.AnswerAsync(await client.SendAsync(InstancePipe.Module, "WITHDRAW", new { id, key = client.KeyOf(id) }));
        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        return answer.GetProperty("data");
    }

    private static string? Code(JsonElement answer) =>
        answer.TryGetProperty("error", out var error) ? error.GetProperty("code").GetString() : null;

    /// <summary>Where the waiting presses' records are (CONFIRMWAIT1a-ii): the home's <c>pipe-waiting</c> entry.</summary>
    private string RecordsFolder => DaorisHome.Entry(Home, HomeEntry.PipeWaiting);

    /// <summary>Each record kept now, by its path.</summary>
    private List<string> Records() =>
        Directory.Exists(RecordsFolder) ? [.. Directory.GetFiles(RecordsFolder).Order(StringComparer.Ordinal)] : [];

    private static JsonElement ReadRecord(string path) => JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();

    /// <summary>Every file under the home but the machine log's and the journal's, which a press writes as it always did.</summary>
    private List<string> Files() =>
    [
        .. Directory.GetFiles(Home, "*", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(DaorisHome.Entry(Home, HomeEntry.Logs), StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith(DaorisHome.Entry(Home, HomeEntry.Pipe), StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal),
    ];

    private static string Sha(string text) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    /// <summary>
    /// A record as a Daoris that stopped left it (§7.1), written by hand: <paramref name="request"/> its terminal's id, sent with
    /// <paramref name="key"/>, its payload as its JSON, its binding's fingerprint the payload's unless one is named, asked
    /// <paramref name="minutes"/> past nine, and <paramref name="extra"/>'s fields beside, which no restore reads. Its path.
    /// </summary>
    private string WriteRecord(
        string request, string module, string route, object payload, string key, string state = "waiting", string? answer = null,
        string? fingerprint = null, int minutes = 0, int version = 1, object? extra = null)
    {
        var text = JsonSerializer.Serialize(payload, Shenora.Core.Ipc.IpcJson.Options);
        var id = Guid.NewGuid().ToString("N");
        var record = JsonSerializer.SerializeToNode(new
        {
            version,
            confirmation = id,
            request,
            binding = new { request = $"{module}.{route}", payload = fingerprint ?? Sha(text), key = Sha(key) },
            module,
            route,
            payload = text,
            grounds = new[] { new { kind = "session", subject = "s1a2b3c4", value = "completed|" + new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero).UtcTicks, words = "completed" } },
            asker = new { process = "pwsh", pid = 7 },
            asked = new DateTimeOffset(2026, 10, 20, 9, minutes, 0, TimeSpan.Zero),
            keyed = false,
            state,
        })!.AsObject();
        if (answer is not null) record["answer"] = System.Text.Json.Nodes.JsonNode.Parse(answer);
        if (extra is not null)
        {
            foreach (var each in JsonSerializer.SerializeToNode(extra)!.AsObject().ToList()) record[each.Key] = each.Value?.DeepClone();
        }

        Directory.CreateDirectory(RecordsFolder);
        var path = System.IO.Path.Combine(RecordsFolder, id + ".json");
        File.WriteAllText(path, record.ToJsonString());
        return path;
    }

    /// <summary>The machine log's lines of <paramref name="event"/>, their data.</summary>
    private List<string> Logged(string @event)
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
            })
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Where(line => line.GetProperty("event").GetString() == @event)
            .Select(line => line.GetProperty("data").GetRawText()),
        ];
    }

    /// <summary>The machine log's confirmation lines' data, in order.</summary>
    private List<string> Confirmations()
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
            })
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Where(line => line.GetProperty("event").GetString() == PipeConfirmations.LogEvent)
            .Select(line => line.GetProperty("data").GetRawText()),
        ];
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(25);
        }
    }
}
