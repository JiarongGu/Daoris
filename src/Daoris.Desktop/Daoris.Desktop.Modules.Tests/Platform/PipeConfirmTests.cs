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

    /// <summary>1a-i's close notice (§10): a close with a press waiting ends it <c>stopping</c>, its line said, and the person is told.</summary>
    [Fact]
    public async Task A_closing_instance_ends_what_waits_runs_none_of_it_and_tells_the_person()
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

        pipe.BeginStopping();

        Assert.Equal(InstancePipe.Stopping, Code(await client.AnswerAsync(id)));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(PipeAnswered.Gone, (await pipe.Confirmations.AnswerAsync(shown.Id, confirm: true)).Outcome);
        Assert.Equal([PipeConfirmationEnd.Closed], noticed);
        Assert.True(pipe.Drain(Poll.Patience));
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
