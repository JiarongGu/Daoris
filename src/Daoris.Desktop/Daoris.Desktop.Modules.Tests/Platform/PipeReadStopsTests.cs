using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// An open read a terminal asked over the pipe is its asker's: once its connection is gone its work stops, and the git walks under it
/// with it; a press runs on (<c>InstancePipeTests</c>). Windows only.
/// </summary>
public sealed class PipeReadStopsTests : Bridge
{
    private sealed class WaitingRead : ModuleBase
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string ModuleName => DriverModule.Name;

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Stopped.TrySetResult();
                throw;
            }

            return null;
        }
    }

    [Fact]
    public async Task A_read_whose_connection_is_lost_stops_its_work()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var read = new WaitingRead();
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(Bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton<IIpcModule>(read);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        using var provider = services.BuildServiceProvider();
        using var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, version: "9.9.9-test") { Grounds = new StubGrounds() };
        var name = pipe.Serve()!;

        var lost = await PipeClient.ConnectAsync(name);
        await lost.HelloAsync(HomeIdentity.Read(Home)!);
        await lost.SendAsync(DriverModule.Name, "SESSION_GROUPS", new { });
        await read.Started.Task.WaitAsync(Poll.Patience);
        Assert.Equal(1, pipe.ReadsUnderWay);
        await lost.DisposeAsync();

        await read.Stopped.Task.WaitAsync(Poll.Patience);
        await Poll.Until(() => pipe.ReadsUnderWay == 0, () => "the read's stop was never let go", Poll.Patience);
    }
}
