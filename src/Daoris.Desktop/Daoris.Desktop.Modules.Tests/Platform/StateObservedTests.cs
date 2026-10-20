using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A plugin observes the pipeline beside it (STATEHUB1g, D169 point 9): each <c>STATE_CHANGED</c> the publisher puts on the kit's bus
/// is told to the plugins listening at <c>state/changed</c>, and a slow or failing plugin never delays a press's answer, nor changes it.
/// </summary>
/// <remarks>
/// Composed as the shell composes it: the publisher in the dispatcher's application slot over the kit's real bus, and the loop's
/// observation subscribed to that bus, telling the loop's hook set. The plugin is a channel in memory: no process starts here.
/// </remarks>
public sealed class StateObservedTests : DriverModuleBridge
{
    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>A route that answers at once with what it was asked to say.</summary>
    private sealed class Moving : ModuleBase
    {
        public override string ModuleName => "TEST.OBSERVED";

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken) =>
            Task.FromResult<object?>(new { Done = true, Said = "landed `s1a2b3c4` into `main`." });
    }

    /// <summary>A plugin at <c>state/changed</c>: it hears a change only once the test lets it, or it fails as the test says.</summary>
    private sealed class Watching(Func<Task> answer) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.StateChanged];

        public bool Alive => true;

        public List<string> Heard { get; } = [];

        public int Asked;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) =>
            throw new DriverException("the loop never asks a plugin to land work");

        public async Task ChangedAsync(object payload, CancellationToken ct)
        {
            Interlocked.Increment(ref Asked);
            await answer().WaitAsync(ct);
            lock (Heard) Heard.Add(JsonSerializer.Serialize(payload));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private IMessageDispatcher Compose(IIpcModule module)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton(module);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(StatePublisher.Middleware(Bus, request =>
            request.Type == "MOVE" ? new StateChange(StateScopes.Sessions | StateScopes.Landing, "id") : null)));
        return services.BuildServiceProvider().GetRequiredService<IMessageDispatcher>();
    }

    private static Task<IpcResponse> Dispatch(IMessageDispatcher dispatcher, string module, string type, object payload) =>
        dispatcher.DispatchAsync(
            new IpcRequest
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Module = module,
                Type = type,
                Payload = JsonSerializer.SerializeToElement(payload),
            },
            CancellationToken.None);

    /// <summary>The loop's set, with one plugin listening at <c>state/changed</c>, its channel <paramref name="plugin"/>.</summary>
    private async Task<HookSet> PluginAsync(IHookChannel plugin)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins,"acme.watch");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
        {
            id = "acme.watch",
            hooks = new { command = new[] { "node", "hooks.mjs" }, points = new[] { HookPoints.StateChanged } },
        }));
        var set = new HookSet(Home, start: (_, _, _) => Task.FromResult(plugin));
        await set.ReconcileAsync(PluginCatalog.Load(Home), CancellationToken.None);
        Assert.Equal(["acme.watch"], set.Running);
        return set;
    }

    /// <summary>What the route answers with no plugin in the way: what every answer below is held to.</summary>
    private async Task<string> AnsweredAloneAsync()
    {
        var route = new Moving();
        var answer = await Dispatch(Compose(route), route.ModuleName, "MOVE", new { id = "s1a2b3c4" });
        return JsonSerializer.Serialize(answer.Data, Wire);
    }

    [Fact]
    public async Task A_slow_plugin_never_delays_a_press_s_answer_nor_changes_it_and_hears_the_change_after()
    {
        var alone = await AnsweredAloneAsync();
        var let = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var plugin = new Watching(() => let.Task);
        await using var set = await PluginAsync(plugin);
        using var observing = StatePublisher.Observe(Bus, set.Tell);
        var route = new Moving();

        // The plugin never answers while the press is asked: an answer that waited on it would never come.
        var answer = await Dispatch(Compose(route), route.ModuleName, "MOVE", new { id = "s1a2b3c4" }).WaitAsync(Poll.Patience);

        Assert.True(answer.Success);
        Assert.Equal(alone, JsonSerializer.Serialize(answer.Data, Wire));
        await UntilAsync(() => Volatile.Read(ref plugin.Asked) == 1);
        Assert.Empty(plugin.Heard);

        let.SetResult();
        await UntilAsync(() => { lock (plugin.Heard) return plugin.Heard.Count == 1; });
        // The change the windows heard, in the frame the plugin is sent: what moved and about whom, nothing else.
        Assert.Equal("""{"scopes":["sessions","landing"],"sessions":["s1a2b3c4"]}""", Assert.Single(plugin.Heard));
    }

    [Fact]
    public async Task A_failing_plugin_never_changes_a_press_s_answer_and_the_next_press_is_told_too()
    {
        var alone = await AnsweredAloneAsync();
        var plugin = new Watching(() => throw new DriverException("plugin `acme.watch` refused the call: it fell over"));
        await using var set = await PluginAsync(plugin);
        using var observing = StatePublisher.Observe(Bus, set.Tell);
        var route = new Moving();
        var dispatcher = Compose(route);

        var first = await Dispatch(dispatcher, route.ModuleName, "MOVE", new { id = "s1a2b3c4" }).WaitAsync(Poll.Patience);
        var second = await Dispatch(dispatcher, route.ModuleName, "MOVE", new { id = "s5e6f7a8" }).WaitAsync(Poll.Patience);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(alone, JsonSerializer.Serialize(first.Data, Wire));
        Assert.Equal(alone, JsonSerializer.Serialize(second.Data, Wire));
        // Asked at each, failing at each, and the set still keeps it: contained.
        await UntilAsync(() => Volatile.Read(ref plugin.Asked) == 2);
        Assert.Empty(plugin.Heard);
        Assert.Equal(["acme.watch"], set.Running);
    }

    [Fact]
    public async Task Only_the_publisher_s_change_is_told_and_an_observation_that_has_ended_hears_nothing()
    {
        var plugin = new Watching(() => Task.CompletedTask);
        await using var set = await PluginAsync(plugin);
        var observing = StatePublisher.Observe(Bus, set.Tell);

        // Another of the driver's events is the windows' alone.
        await Bus.EmitAsync(StatePublisher.Module, "DRIVER_TICK", new { Events = Array.Empty<string>() });
        await Bus.EmitAsync(StatePublisher.Module, StatePublisher.Type, new StateChanged(["sweep"], null));
        await UntilAsync(() => { lock (plugin.Heard) return plugin.Heard.Count == 1; });
        Assert.Equal("""{"scopes":["sweep"],"sessions":null}""", plugin.Heard[0]);

        observing.Dispose();
        await Bus.EmitAsync(StatePublisher.Module, StatePublisher.Type, new StateChanged(["landing"], null));
        await Task.Delay(100);
        Assert.Single(plugin.Heard);
    }
}
