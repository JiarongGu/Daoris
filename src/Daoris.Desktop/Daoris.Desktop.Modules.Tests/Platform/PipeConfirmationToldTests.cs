using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A press made over the shell's pipe and left by its terminal is told to
/// the lease's plugin at <c>work/event</c> as <c>confirmation.ended</c> with the pipe's own id and its end, once the person answers; a
/// press its terminal waits on is told nothing. The pipe is served as <see cref="PipeConfirmTests"/> serves it and the sink is the
/// loop's own (<see cref="SettledTelling"/>); the plugin is a held channel, so no process starts.
/// </summary>
public sealed class PipeConfirmationToldTests : Bridge
{
    private const string Land = "DISCARD_SESSION_TREE";

    [Fact]
    public async Task A_press_its_terminal_left_is_told_to_the_plugin_with_the_pipe_s_id_and_its_end_and_a_waited_one_is_not()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
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
        using var provider = services.BuildServiceProvider();
        using var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, version: "9.9.9-test", clock: new HeldClock());
        pipe.Grounds = new StubGrounds().Session("s1a2b3c4", "completed").Session("s5d6e7f8", "completed");

        var channel = new HeldEventChannel();
        var plugin = Path.Combine(DaorisHome.Entry(Home, HomeEntry.Plugins), "acme.relay");
        Directory.CreateDirectory(plugin);
        File.WriteAllText(Path.Combine(plugin, "plugin.json"), JsonSerializer.Serialize(new
        {
            id = "acme.relay", version = "1.0.0", hooks = new { command = new[] { "node", "hooks.mjs" }, points = new[] { HookPoints.Event } },
        }));
        await using var set = new HookSet(Home, start: (_, _, _) => Task.FromResult<IHookChannel>(channel), log: log, holdsHome: () => true);
        await set.ReconcileAsync(PluginCatalog.Load(Home), CancellationToken.None);
        pipe.Confirmations.Settled = SettledTelling.Of(() => set, () => DateTimeOffset.UtcNow);

        var name = pipe.Serve()!;
        var homeId = HomeIdentity.Read(Home)!;
        await using var waiting = await PipeClient.ConnectAsync(name);
        await waiting.HelloAsync(homeId);
        var leaving = await PipeClient.ConnectAsync(name);
        await leaving.HelloAsync(homeId);

        await waiting.SendAsync(DriverModule.Name, Land, new { id = "s1a2b3c4" });
        await leaving.SendAsync(DriverModule.Name, Land, new { id = "s5d6e7f8" });
        await leaving.NotificationAsync(Poll.Patience);
        await leaving.DisposeAsync();
        await UntilAsync(() => pipe.Confirmations.Now().Waiting is { Count: 2 } now && now.Count(each => !each.WaitedOn) == 1);
        var left = pipe.Confirmations.Now().Waiting.Single(each => !each.WaitedOn);
        var held = pipe.Confirmations.Now().Waiting.Single(each => each.WaitedOn);

        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(held.Id, confirm: false)).Outcome);
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(left.Id, confirm: true)).Outcome);

        await channel.HeardAsync(1);
        await Task.Delay(150);
        var told = Assert.Single(await channel.HeardAsync(1));
        Assert.Equal((WorkEventKinds.ConfirmationEnded, left.Id, "confirmed"), ((string)told["kind"]!, (string)told["confirmation"]!, (string)told["end"]!));
        Assert.Matches("^[0-9a-f]{32}$", left.Id);
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

    private sealed class HeldEventChannel : IHookChannel
    {
        private readonly List<IReadOnlyDictionary<string, object?>> _frames = [];

        public IReadOnlyList<string> Points => [HookPoints.Event];

        public bool Alive { get; private set; } = true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public Task EventAsync(object frame, CancellationToken ct)
        {
            lock (_frames) _frames.Add((IReadOnlyDictionary<string, object?>)frame);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> HeardAsync(int count)
        {
            for (var n = 0; n < 300; n++)
            {
                lock (_frames) if (_frames.Count >= count) return [.. _frames];
                await Task.Delay(10);
            }

            lock (_frames) return [.. _frames];
        }

        public ValueTask DisposeAsync()
        {
            Alive = false;
            return ValueTask.CompletedTask;
        }
    }
}
