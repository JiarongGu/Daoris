using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FINDDAORIS1 (D171 point 1): the desktop leaves the address of the service it holds or adopted in its home while it runs,
/// naming its own process, and takes it away at its clean exit, so a terminal's <c>daoris-driver</c> finds the Daoris that runs.
/// </summary>
public sealed class DriverLoopAddressTests : Bridge
{
    private DriverLoop Loop() => new(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:5181/");

    [Fact]
    public void The_desktop_leaves_its_services_address_in_its_home_and_takes_it_away_as_it_stops()
    {
        var loop = Loop();
        Assert.Null(RunningAddress.Read(Home));

        loop.Announce();

        var named = RunningAddress.Read(Home);
        Assert.Equal("http://localhost:5181", named!.Url);
        Assert.Equal(Environment.ProcessId, named.Pid);
        Assert.True(RunningService.Lives(named.Pid, named.Started));

        loop.Stop();
        Assert.False(File.Exists(RunningAddress.PathIn(Home)));
    }

    /// <summary>Said once: a second word while it runs keeps the one address, and the stop still takes it away.</summary>
    [Fact]
    public void Announcing_twice_keeps_one_address_and_the_stop_takes_it()
    {
        var loop = Loop();
        loop.Announce();
        loop.Announce();

        Assert.NotNull(RunningAddress.Read(Home));
        loop.Dispose();
        Assert.False(File.Exists(RunningAddress.PathIn(Home)));
    }

    /// <summary>
    /// Held on the loop's own source, because nothing short of a running service holds it: the address is left once the
    /// supervisor says the service answers, held or adopted, and never on the path where it did not.
    /// </summary>
    [Fact]
    public void The_loop_leaves_the_address_once_the_service_answers()
    {
        var source = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Modules", "Loop", "DriverLoop.cs"));
        var run = source[source.IndexOf("private async Task RunAsync()", StringComparison.Ordinal)..];

        var ensured = run.IndexOf("supervisor.EnsureAsync(ct)", StringComparison.Ordinal);
        var down = run.IndexOf("if (!up)", StringComparison.Ordinal);
        var announced = run.IndexOf("Announce();", StringComparison.Ordinal);
        Assert.True(ensured > 0 && down > ensured && announced > down, $"ensured at {ensured}, down at {down}, announced at {announced}");
    }

    /// <summary>
    /// RPC1a (the terminal RPC design §6.3): the pipe is named in the address only once it is served; a stop answers every new
    /// hello <c>stopping</c>, lets the pipe's presses answer, and only then takes the address away, so no terminal finds the home
    /// with no instance and acts alone while a press it sent still runs.
    /// </summary>
    [Fact]
    public async Task A_stop_answers_stopping_lets_the_pipe_s_presses_answer_and_only_then_takes_the_address_away()
    {
        if (!OperatingSystem.IsWindows()) return;
        var press = new HeldModule("TEST.PRESS");
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcModule>(press);
        services.UseMessageDispatcher((_, _) => { });
        using var provider = services.BuildServiceProvider();
        var loop = Loop();
        using var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home,
            census: PipeCensus.Daoris.With("TEST.PRESS", "MOVE", PipeClass.Open));
        loop.Pipe = pipe;

        loop.Announce();
        Assert.Null(RunningAddress.Read(Home)!.Pipe);
        var name = pipe.Serve()!;
        loop.AnnouncePipe(name);
        Assert.Equal(name, RunningAddress.Read(Home)!.Pipe);

        var id = HomeIdentity.Read(Home)!;
        await using var client = await PipeClient.ConnectAsync(name);
        await client.HelloAsync(id);
        var pressed = await client.SendAsync("TEST.PRESS", "MOVE");
        await press.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var stopped = Task.Run(loop.Stop);
        await UntilAsync(() => pipe.IsStopping);
        await using (var late = await PipeClient.ConnectAsync(name))
        {
            Assert.Equal(InstancePipe.Stopping, (await late.HelloAsync(id)).GetProperty("error").GetProperty("code").GetString());
        }

        // The press still runs, so the address stays and the stop waits.
        Assert.Equal(name, RunningAddress.Read(Home)!.Pipe);
        Assert.False(stopped.IsCompleted);

        press.Let.SetResult();
        Assert.True((await client.AnswerAsync(pressed)).GetProperty("success").GetBoolean());
        await stopped.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(File.Exists(RunningAddress.PathIn(Home)));
    }

    /// <summary>
    /// RPC1i (§6.3): a stop whose drain ends with a press still under way goes on, as it always did, and says so in the machine log:
    /// <c>pipe.drained</c> with how many presses were left, so a terminal told <i>nothing was done</i> by a closing Daoris can be read
    /// against what the instance knew. A drain that ends clean says nothing.
    /// </summary>
    [Fact]
    public async Task A_stop_that_cannot_drain_says_how_many_presses_were_left()
    {
        if (!OperatingSystem.IsWindows()) return;
        var press = new HeldModule("TEST.PRESS");
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcModule>(press);
        services.UseMessageDispatcher((_, _) => { });
        using var provider = services.BuildServiceProvider();
        var loop = new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:5181/") { PipeDrain = TimeSpan.FromMilliseconds(300) };
        using var log = new MachineLog(Home, "desktop");
        using var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log,
            census: PipeCensus.Daoris.With("TEST.PRESS", "MOVE", PipeClass.Open));
        loop.Pipe = pipe;
        loop.Announce();
        var name = pipe.Serve()!;
        await using var client = await PipeClient.ConnectAsync(name);
        await client.HelloAsync(HomeId());
        await client.SendAsync("TEST.PRESS", "MOVE");
        await press.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        loop.Stop();

        var said = Assert.Single(PipeLines(), line => line.GetProperty("event").GetString() == "pipe.drained");
        Assert.Equal("""{"left":1}""", said.GetProperty("data").GetRawText());
        Assert.False(File.Exists(RunningAddress.PathIn(Home)));
        press.Let.SetResult();
    }

    /// <summary>A stop with nothing under way drains clean and says nothing of it.</summary>
    [Fact]
    public void A_stop_that_drains_says_nothing_of_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.UseMessageDispatcher((_, _) => { });
        using var provider = services.BuildServiceProvider();
        var loop = new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:5181/") { PipeDrain = TimeSpan.FromMilliseconds(300) };
        using var log = new MachineLog(Home, "desktop");
        using var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log);
        loop.Pipe = pipe;
        loop.Announce();
        pipe.Serve();

        loop.Stop();

        Assert.DoesNotContain(PipeLines(), line => line.GetProperty("event").GetString() == "pipe.drained");
    }

    private string HomeId() => HomeIdentity.Read(Home)!;

    private List<JsonElement> PipeLines()
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
            }).Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement.Clone()),
        ];
    }

    /// <summary>Held on the loop's source: the pipe is served once the loop holds the home, and named in the address after (§6.3).</summary>
    [Fact]
    public void The_loop_serves_the_pipe_once_it_holds_the_home_and_names_it_after()
    {
        var source = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Modules", "Loop", "DriverLoop.cs"));
        var run = source[source.IndexOf("private async Task RunAsync()", StringComparison.Ordinal)..];

        var held = run.IndexOf("await HoldHomeAsync(ct)", StringComparison.Ordinal);
        var served = run.IndexOf("Pipe?.Serve()", StringComparison.Ordinal);
        var named = run.IndexOf("AnnouncePipe(", StringComparison.Ordinal);
        Assert.True(held > 0 && served > held && named > served, $"held at {held}, served at {served}, named at {named}");
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

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Daoris.Desktop.Modules")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("the desktop's sources are not above the tests");
    }
}
