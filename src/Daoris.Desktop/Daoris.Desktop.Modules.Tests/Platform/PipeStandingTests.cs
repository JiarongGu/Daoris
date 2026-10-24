using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// TERMTRUST1a part (ii) over the real pipe (D201 points 5 and 7, the terminal-trust design §5 and §8 case 5; <c>PipeConfirmTests</c>'
/// neighbour): a press the person's standing confirmation covers runs with no card, the terminal told <c>STANDING</c> and the log
/// <c>person.confirmation</c> <c>state: standing</c>; it is admitted once, so a revoke after its admission lets it run and makes the next
/// ask; a press asked before a grant keeps its card; and one the grant names that asks anyway says why, to the terminal and in the log.
/// </summary>
/// <remarks>
/// Each test serves its own pipe in this process, named by its own scratch home's id; where the terminal's process stands is a stand-in
/// (<see cref="StandingConfirmationTests.Placed"/>), the system's look being <c>StandingCallerTests</c>'. Windows only.
/// </remarks>
public sealed partial class PipeStandingTests : Bridge
{
    private (IMessageDispatcher Dispatcher, ServiceProvider Services) Compose(MachineLog log, params IIpcModule[] modules)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        var options = new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) };
        var tracker = new IpcRequestTracker(Bus, options);
        services.AddSingleton<IIpcRequestTracker>(tracker);
        foreach (var module in modules) services.AddSingleton(module);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMessageDispatcher>(), provider);
    }

    private string HomeId => HomeIdentity.Read(Home)!;

    private (InstancePipe Pipe, StandingConfirmationTests.Placed Caller) Pipe(IMessageDispatcher dispatcher, MachineLog log, bool grant = true)
    {
        var caller = new StandingConfirmationTests.Placed(StandingPlace.Outside);
        var standing = new StandingConfirmation(Home, log, caller, offered: true);
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing);
        Assert.NotNull(pipe.Serve());
        if (grant) Assert.True(standing.Set(StandingPresses.Built).Done);
        return (pipe, caller);
    }

    private static IEnumerable<JsonElement> Each(IEnumerable<JsonElement> told) =>
        told.SelectMany(line => line.GetProperty("payload").EnumerateArray());

    private static bool Is(JsonElement each, string type) =>
        each.GetProperty("module").GetString() == InstancePipe.Module && each.GetProperty("type").GetString() == type;

    [Fact]
    public async Task A_covered_stop_runs_with_no_card_and_the_terminal_hears_it_ran_under_the_grant()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _) = Pipe(dispatcher, log);
        using var served = pipe;
        var used = new List<EventMessage>();
        using var heard = Bus.Subscribe(StatePublisher.Module, StandingConfirmation.UsedEvent, message =>
        {
            lock (used) used.Add(message);
            return Task.CompletedTask;
        });
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        var id = await client.SendAsync(DriverModule.Name, "STOP_SESSION", new { id = "s1a2b3c4", said = true });
        var answer = await client.AnswerAsync(id);

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, driver.Ran);
        var told = Each(client.Told).ToList();
        Assert.DoesNotContain(told, each => Is(each, InstancePipe.Confirming));
        var standing = Assert.Single(told, each => Is(each, InstancePipe.Standing));
        Assert.Equal(id, standing.GetProperty("payload").GetProperty("id").GetString());
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal("answered", (await client.ResultAsync(id)).GetProperty("state").GetString());
        Assert.Equal(1, pipe.StandingConfirmation.Now().Used);
        await Until(() => { lock (used) return used.Count == 1; }, () => "no window was told the use");

        // The log says it by codes: the state, the route and the door, never the session or the asker.
        var lines = Lines(PipeConfirmations.LogEvent);
        var line = Assert.Single(lines);
        Assert.Equal(
            """{"state":"standing","route":"DAORIS.DRIVER.STOP_SESSION","door":"terminal"}""", line.GetProperty("data").GetRawText());
        Assert.DoesNotContain(lines, each => each.GetRawText().Contains("s1a2b3c4", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_revoke_after_the_admission_lets_the_admitted_press_run_and_the_next_one_asks()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _) = Pipe(dispatcher, log);
        using var served = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        var admitted = await client.SendAsync(DriverModule.Name, "SET_HOLD", new { repository = "engine", held = true });
        await driver.Started.Task.WaitAsync(Poll.Patience);
        Assert.True(pipe.StandingConfirmation.Set([]).Done);
        driver.Let.SetResult();

        Assert.True((await client.AnswerAsync(admitted)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran);

        var next = await client.SendAsync(DriverModule.Name, "SET_HOLD", new { repository = "engine", held = true });
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the next press was not asked");
        Assert.Equal(1, driver.Ran);
        // Off, it says nothing of standing: the plain ask.
        var asked = Lines(PipeConfirmations.LogEvent).Last();
        Assert.Equal("asked", asked.GetProperty("data").GetProperty("state").GetString());
        Assert.False(asked.GetProperty("data").TryGetProperty("standing", out _));
        pipe.Confirmations.Answer(pipe.Confirmations.Now().Waiting[0].Id, confirm: false);
        Assert.False((await client.AnswerAsync(next)).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task A_press_asked_before_the_grant_keeps_its_card()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _) = Pipe(dispatcher, log, grant: false);
        using var served = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        var id = await client.SendAsync(DriverModule.Name, "STOP_SESSION", new { id = "s1a2b3c4", said = true });
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the press was not asked");
        Assert.True(pipe.StandingConfirmation.Set(StandingPresses.Built).Done);

        Assert.Single(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, driver.Ran);
        pipe.Confirmations.Answer(pipe.Confirmations.Now().Waiting[0].Id, confirm: true);
        Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(0, pipe.StandingConfirmation.Now().Used);
    }

    [Fact]
    public async Task A_press_the_grant_names_that_asks_anyway_says_why_to_the_terminal_and_in_the_log()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, caller) = Pipe(dispatcher, log);
        using var served = pipe;
        caller.Answer = StandingPlace.InAgentJob;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        await client.SendAsync(DriverModule.Name, "STOP_SESSION", new { id = "s1a2b3c4", said = true });

        var told = await client.NotificationAsync(Poll.Patience);
        var waits = Assert.Single(told!.Value.GetProperty("payload").EnumerateArray());
        Assert.Equal(InstancePipe.Confirming, waits.GetProperty("type").GetString());
        Assert.Equal(StandingWhy.InAgentJob, waits.GetProperty("payload").GetProperty("standing").GetString());
        var asked = Lines(PipeConfirmations.LogEvent).Single();
        Assert.Equal(
            """{"state":"asked","route":"DAORIS.DRIVER.STOP_SESSION","door":"terminal","standing":"in-agent-job"}""",
            asked.GetProperty("data").GetRawText());
        Assert.Equal(0, driver.Ran);
    }

    /// <summary>§4.1, §8 case 9: a terminal cannot grant it, nor ask for a card that would; refused before it is dispatched.</summary>
    [Fact]
    public async Task A_terminal_is_refused_the_grant_before_it_is_dispatched()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _) = Pipe(dispatcher, log, grant: false);
        using var served = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeId);

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "SET_STANDING_CONFIRMATION", new { presses = new[] { "stop" } }));

        Assert.Equal(InstancePipe.WindowOnly, answer.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, driver.Ran);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.False(pipe.StandingConfirmation.Now().On);
    }

    /// <summary>Waits for <paramref name="condition"/>, bounded by the patience a signal that only fails to come when the code is wrong gets.</summary>
    private static async Task Until(Func<bool> condition, Func<string> because)
    {
        var until = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, because());
            await Task.Delay(10);
        }
    }

    private List<JsonElement> Lines(string @event)
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
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(line => line.GetProperty("event").GetString() == @event),
        ];
    }
}
