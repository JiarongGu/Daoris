using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Over the real pipe: an agent
/// outside Daoris asks to join a channel, a person's press that waits for their Confirm on a card that names the asking process, the
/// channel, the agent and the repository it claims; one from a process in an agents' job is refused <c>inside</c> before any card; one
/// whose place the system would not tell is asked, and says so.
/// </summary>
/// <remarks>Each test serves its own pipe, where the terminal's process stands being a stand-in. Windows only.</remarks>
public sealed class ChannelJoinPipeTests : Bridge
{
    private static readonly object Join = new { ask = "a1b2c3", agent = "codex", repository = "Keeper", said = true };

    private (IMessageDispatcher Dispatcher, ServiceProvider Services) Compose(MachineLog log, params IIpcModule[] modules)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(Bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        foreach (var module in modules) services.AddSingleton(module);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMessageDispatcher>(), provider);
    }

    private async Task<(InstancePipe Pipe, StandingConfirmationTests.Placed Caller, HeldModule Driver, ServiceProvider Services, PipeClient Client)> ServeAsync(
        MachineLog log, StandingPlace place)
    {
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var (dispatcher, services) = Compose(log, driver);
        var caller = new StandingConfirmationTests.Placed(place);
        var standing = new StandingConfirmation(Home, log, caller);
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing) { Grounds = new StubGrounds() };
        Assert.NotNull(pipe.Serve());
        var client = await PipeClient.ConnectAsync(pipe.Name!);
        await client.HelloAsync(HomeIdentity.Read(Home)!);
        return (pipe, caller, driver, services, client);
    }

    [Fact]
    public async Task A_join_from_outside_waits_for_the_Confirm_on_a_card_that_says_who_asks()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var (pipe, _, driver, services, client) = await ServeAsync(log, StandingPlace.Outside);
        using var _s = services;
        using var _p = pipe;
        await using var _c = client;

        var id = await client.SendAsync(DriverModule.Name, "CHANNEL_JOIN", Join);

        var told = await client.NotificationAsync(Poll.Patience);
        var waits = Assert.Single(told!.Value.GetProperty("payload").EnumerateArray());
        Assert.Equal(InstancePipe.Confirming, waits.GetProperty("type").GetString());
        Assert.Equal(id, waits.GetProperty("payload").GetProperty("id").GetString());
        Assert.False(waits.GetProperty("payload").TryGetProperty("standing", out _));
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the join was never asked");
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);
        Assert.Equal("CHANNEL_JOIN", card.Route);
        Assert.Equal(Environment.ProcessId, card.Asker.Pid);
        Assert.False(string.IsNullOrEmpty(card.Asker.Process));
        Assert.Equal(
            [("/ask", "a1b2c3"), ("/agent", "codex"), ("/repository", "Keeper")],
            card.Fields.Where(field => field.Name != "/said").Select(field => (field.Name, field.Value)));
        Assert.Equal(0, driver.Ran);
    }

    [Fact]
    public async Task A_join_from_an_agents_job_is_refused_inside_before_any_card()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var (pipe, _, driver, services, client) = await ServeAsync(log, StandingPlace.InAgentJob);
        using var _s = services;
        using var _p = pipe;
        await using var _c = client;

        var id = await client.SendAsync(DriverModule.Name, "CHANNEL_JOIN", Join);
        var answer = await client.AnswerAsync(id);

        Assert.Equal(PipeOutsideOnly.Inside, answer.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, driver.Ran);
    }

    [Fact]
    public async Task A_join_whose_place_is_untold_is_asked_and_says_so()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var (pipe, _, _, services, client) = await ServeAsync(log, StandingPlace.Untold);
        using var _s = services;
        using var _p = pipe;
        await using var _c = client;

        await client.SendAsync(DriverModule.Name, "CHANNEL_JOIN", Join);

        var told = await client.NotificationAsync(Poll.Patience);
        var waits = Assert.Single(told!.Value.GetProperty("payload").EnumerateArray());
        Assert.Equal(StandingWhy.Untold, waits.GetProperty("payload").GetProperty("standing").GetString());
        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the join was never asked");
        // The card record carries it, so the window words it beside the asker (no confirmation id is kept for it).
        Assert.Equal(StandingWhy.Untold, Assert.Single(pipe.Confirmations.Now().Waiting).Standing);
    }

    [Fact]
    public async Task A_join_from_a_place_told_outside_carries_no_standing_on_its_card()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var (pipe, _, _, services, client) = await ServeAsync(log, StandingPlace.Outside);
        using var _s = services;
        using var _p = pipe;
        await using var _c = client;

        await client.SendAsync(DriverModule.Name, "CHANNEL_JOIN", Join);

        await Until(() => pipe.Confirmations.Now().Waiting.Count == 1, () => "the join was never asked");
        Assert.Null(Assert.Single(pipe.Confirmations.Now().Waiting).Standing);
    }

    private static async Task Until(Func<bool> condition, Func<string> because)
    {
        var until = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, because());
            await Task.Delay(10);
        }
    }
}
