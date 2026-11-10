using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A person's press whose route has a view: judged at the ask, so one its route would refuse is answered to its terminal in the
/// route's words with no card and nothing run, and one it takes waits on a card that carries what it means beside every value it carries.
/// </summary>
/// <remarks>
/// Each test serves its own pipe in this process, named by its own scratch home's id, so nothing here meets a running Daoris. The
/// registry is a stand-in for the loop's readers (<see cref="StubGrounds"/>). The pipe is Windows': elsewhere a test returns at once.
/// </remarks>
public sealed class PipeViewTests : Bridge
{
    private const string Rewire = "REGISTRY_REWIRE";

    private async Task<(InstancePipe Pipe, PipeClient Client, HeldModule Driver, ServiceProvider Services, MachineLog Log)> ServeAsync(StubGrounds grounds)
    {
        var log = new MachineLog(Home, "desktop");
        var driver = new HeldModule(DriverModule.Name);
        driver.Let.SetResult();
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        var options = new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) };
        var tracker = new IpcRequestTracker(Bus, options);
        services.AddSingleton<IIpcRequestTracker>(tracker);
        services.AddSingleton<IIpcModule>(driver);
        services.AddSingleton<IIpcModule>(new Shenora.Modules.Requests.IpcRequestsModule(
            tracker, options, Microsoft.Extensions.Logging.Abstractions.NullLogger<Shenora.Modules.Requests.IpcRequestsModule>.Instance));
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, version: "9.9.9-test", clock: new HeldClock())
        {
            Grounds = grounds,
        };
        var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeIdentity.Read(Home)!);
        return (pipe, client, driver, provider, log);
    }

    private static StubGrounds Registry()
    {
        var grounds = new StubGrounds();
        grounds.Registered.Add("engine");
        grounds.Placed["engine"] = "default";
        return grounds;
    }

    [Fact]
    public async Task A_move_the_route_takes_waits_on_a_card_carrying_its_view()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(Registry());
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        await client.SendAsync(DriverModule.Name, Rewire, new { repository = "engine", workspace = "work", said = true });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);

        var card = pipe.Confirmations.Now().Waiting[0];
        Assert.Equal("repository-move-starts", card.View!.Kind);
        Assert.Equal("daoris repository wire engine --workspace work", card.View.Command);
        // The values it carries are still shown whole beside what it means.
        Assert.Contains(card.Fields, field => field.Name == "/workspace" && field.Value == "work");
        Assert.Equal(0, driver.Ran);
    }

    [Fact]
    public async Task A_move_the_route_would_refuse_is_answered_in_its_words_with_no_card()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(Registry());
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var refused = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, Rewire, new { repository = "engine", workspace = "default", said = true }));

        Assert.Equal(InstancePipe.NotAllowed, refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("`engine` is already in workspace `default`, so there is nothing to move.", refused.GetProperty("error").GetProperty("message").GetString());
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, driver.Ran);
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
