using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// How late the window's own thread answers, and what held it (FREEZE1, D56's note): the frameless window's caption is
/// moved, maximised and closed on that thread, so a late thread is a frozen bar while the page goes on working.
/// </summary>
/// <remarks>
/// <para><b>Driven by hand, never by a clock.</b> The test is the timer (<see cref="UiStallWatch.Look"/>) and the window's
/// thread (it runs what the watch posted, when it says), so a stall of a minute takes no time and no thread.</para>
///
/// <para><b>Through the kit's own dispatcher</b>, as <see cref="RefusalLogTests"/> is: the route that holds the thread is
/// named by the middleware in the pipeline every module's answer travels, so what is asserted is what the shell
/// composes.</para>
/// </remarks>
public sealed class UiStallWatchTests : Bridge
{
    private DateTimeOffset _now = new(2026, 10, 8, 8, 22, 47, TimeSpan.Zero);
    private readonly Queue<Action> _posted = new();
    private int _collections;

    private void Wait(int milliseconds) => _now += TimeSpan.FromMilliseconds(milliseconds);

    private UiStallWatch Watch(MachineLog log, Func<Action, bool>? post = null) =>
        new(log, post ?? (action =>
        {
            _posted.Enqueue(action);
            return true;
        }), Bus, () => _now, () => _collections);

    /// <summary>The window's thread takes up what was posted to it.</summary>
    private void Answer()
    {
        while (_posted.TryDequeue(out var action)) action();
    }

    /// <summary>A healthy beat: the timer looks, and the thread answers a moment later.</summary>
    private void Beat(UiStallWatch watch)
    {
        watch.Look();
        Wait(5);
        Answer();
        Wait(995);
    }

    private MachineLog Log() => new(Home, "desktop", () => _now);

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

    private List<JsonElement> Stalls() =>
        [.. Lines().Where(line => line.GetProperty("event").GetString() == UiStallWatch.Event)];

    private static JsonElement Data(JsonElement line) => line.GetProperty("data");

    /// <summary>A module whose route holds the window's thread while the timer looks, as a slow synchronous route does.</summary>
    private sealed class Holding(Action whileHeld) : ModuleBase
    {
        public override string ModuleName => "TEST.HOLDING";

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            whileHeld();
            return Task.FromResult<object?>(new { Held = true });
        }
    }

    private IMessageDispatcher Compose(UiStallWatch watch, Action whileHeld)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcModule>(new Holding(whileHeld));
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(watch.Middleware));
        return services.BuildServiceProvider().GetRequiredService<IMessageDispatcher>();
    }

    private static Task<IpcResponse> Dispatch(IMessageDispatcher dispatcher, string type) =>
        dispatcher.DispatchAsync(
            new IpcRequest { Id = Guid.NewGuid().ToString("N")[..8], Module = "TEST.HOLDING", Type = type },
            CancellationToken.None);

    [Fact]
    public void A_thread_that_answers_in_time_writes_nothing()
    {
        using var log = Log();
        using var watch = Watch(log);

        for (var beat = 0; beat < 30; beat++) Beat(watch);

        Assert.Empty(Stalls());
    }

    [Fact]
    public async Task A_route_that_holds_the_thread_is_named_with_how_long_it_held_it()
    {
        using var log = Log();
        using var watch = Watch(log);
        Beat(watch);

        // The timer looks and posts; before the thread answers, a route runs on it for 1.6 s, the timer looking twice.
        watch.Look();
        var dispatcher = Compose(watch, () =>
        {
            Wait(800);
            watch.Look();
            Wait(800);
            watch.Look();
        });
        var answer = await Dispatch(dispatcher, "SESSION_HISTORY");
        Assert.True(answer.Success);
        Answer();
        Assert.Empty(Stalls()); // still in its stall: said once it ends, whole

        Beat(watch);

        var stall = Assert.Single(Stalls());
        Assert.Equal("warn", stall.GetProperty("level").GetString());
        var data = Data(stall);
        Assert.Equal(1600, data.GetProperty("ms").GetInt64());
        Assert.Equal("TEST.HOLDING.SESSION_HISTORY", data.GetProperty("during").GetString());
        Assert.Equal("TEST.HOLDING.SESSION_HISTORY", data.GetProperty("slowest").GetString());
        Assert.Equal(1600, data.GetProperty("slowestMs").GetInt64());
        Assert.Equal(1, data.GetProperty("requests").GetInt32());
        Assert.False(data.GetProperty("ongoing").GetBoolean());
    }

    [Fact]
    public void A_stall_no_route_held_says_what_else_ran_meanwhile()
    {
        using var log = Log();
        using var watch = Watch(log);
        Beat(watch);

        // The thread is late with no route on it: what the kit's flush serialized meanwhile, and the full collections.
        watch.Look();
        for (var i = 0; i < 3; i++) Bus.Emit("DAORIS", "SESSION_EVENTS", new { Session = "s1" });
        _collections += 2;
        Wait(700);
        Answer();
        Beat(watch);

        var data = Data(Assert.Single(Stalls()));
        Assert.Equal(700, data.GetProperty("ms").GetInt64());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("during").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("slowest").ValueKind);
        Assert.Equal(0, data.GetProperty("requests").GetInt32());
        Assert.Equal(3, data.GetProperty("events").GetInt32());
        Assert.Equal(2, data.GetProperty("gc2").GetInt32());
    }

    [Fact]
    public void A_thread_that_stays_late_is_said_once_a_minute_while_it_lasts()
    {
        using var log = Log();
        using var watch = Watch(log);
        Beat(watch);

        // The thread never answers: the timer goes on looking, once a second, for two and a half minutes.
        watch.Look();
        for (var second = 0; second < 150; second++)
        {
            Wait(1000);
            watch.Look();
        }

        var stalls = Stalls();
        Assert.Equal(2, stalls.Count);
        Assert.All(stalls, stall => Assert.True(Data(stall).GetProperty("ongoing").GetBoolean()));
        Assert.True(Data(stalls[0]).GetProperty("ms").GetInt64() >= 60_000);
    }

    [Fact]
    public async Task A_name_that_is_no_identifier_is_not_written()
    {
        using var log = Log();
        using var watch = Watch(log);
        Beat(watch);

        watch.Look();
        var dispatcher = Compose(watch, () =>
        {
            Wait(600);
            watch.Look();
        });
        await Dispatch(dispatcher, "C:\\some\\path with words");
        Answer();
        Beat(watch);

        var data = Data(Assert.Single(Stalls()));
        Assert.Equal(JsonValueKind.Null, data.GetProperty("during").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("slowest").ValueKind);
        Assert.Equal(1, data.GetProperty("requests").GetInt32());
    }

    [Fact]
    public void With_no_window_to_post_to_nothing_is_measured()
    {
        using var log = Log();
        using var watch = Watch(log, _ => false);

        for (var second = 0; second < 90; second++)
        {
            watch.Look();
            Wait(1000);
        }

        Assert.Empty(Stalls());
    }

    [Fact]
    public void A_watch_composed_before_its_window_looks_at_the_thread_its_start_names()
    {
        using var log = Log();
        // As the shell composes it: with the dispatcher, before any window exists, so with nowhere to post yet.
        using var watch = new UiStallWatch(log, bus: Bus, clock: () => _now, collections: () => _collections)
        {
            Every = TimeSpan.FromHours(1),
        };
        watch.Look();
        Assert.Empty(_posted);

        watch.Start(action =>
        {
            _posted.Enqueue(action);
            return true;
        });
        watch.Look();
        Wait(900);
        Answer();
        Beat(watch);

        Assert.Equal(900, Data(Assert.Single(Stalls())).GetProperty("ms").GetInt64());
    }

    [Fact]
    public void A_stall_still_open_as_the_window_closes_is_said()
    {
        using var log = Log();
        var watch = Watch(log);
        Beat(watch);
        watch.Look();
        Wait(2000);
        watch.Look();

        watch.Dispose();

        var data = Data(Assert.Single(Stalls()));
        Assert.True(data.GetProperty("ongoing").GetBoolean());
        Assert.Equal(2000, data.GetProperty("ms").GetInt64());
    }
}
