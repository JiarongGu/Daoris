using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A Daoris route never resumes on the window's thread (UIFREEZE1): the bridge dispatches each page request there, and the
/// kit's pipeline keeps the caller's context, so every await in a route came back to the window. A sweep's git fan-out, once
/// back, started its processes there, and the window froze for minutes on .NET's one process-start lock.
/// </summary>
/// <remarks>
/// Through the kit's own dispatcher, as <see cref="UiStallWatchTests"/> is, under a context that stands in for the window's:
/// what is posted to it waits until the test, as the window's thread, takes it up.
/// </remarks>
public sealed class OffTheWindowTests
{
    /// <summary>The window's thread as a route's await sees it: what is posted runs only when the thread takes it up.</summary>
    private sealed class WindowContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Run, object? State)> _posted = new();

        public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

        public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

        public override SynchronizationContext CreateCopy() => this;

        /// <summary>Run what was posted, on this thread, as the window's pump does.</summary>
        public void Pump()
        {
            while (_posted.TryDequeue(out var each))
            {
                var previous = Current;
                SetSynchronizationContext(this);
                try
                {
                    each.Run(each.State);
                }
                finally
                {
                    SetSynchronizationContext(previous);
                }
            }
        }
    }

    /// <summary>A route that waits on something, as one waits on git, and says where it went on.</summary>
    private sealed class Waiting(string name) : ModuleBase
    {
        public override string ModuleName => name;

        public int StartedOn { get; private set; }

        public TaskCompletionSource<SynchronizationContext?> WentOnIn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            StartedOn = Environment.CurrentManagedThreadId;
            await Task.Delay(1, cancellationToken);
            WentOnIn.TrySetResult(SynchronizationContext.Current);
            return new { Done = true };
        }
    }

    private static IMessageDispatcher Compose(IIpcModule module)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(new EventBus(NullLogger<EventBus>.Instance));
        services.AddSingleton(module);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(OffTheWindow.Middleware));
        return services.BuildServiceProvider().GetRequiredService<IMessageDispatcher>();
    }

    /// <summary>Dispatch as the window's thread does: under its context, which the thread keeps once the call returns.</summary>
    private static Task<IpcResponse> DispatchOnWindow(IMessageDispatcher dispatcher, WindowContext window, string module)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(window);
        try
        {
            var answer = dispatcher.DispatchAsync(
                new IpcRequest { Id = Guid.NewGuid().ToString("N")[..8], Module = module, Type = "WAIT" }, CancellationToken.None);
            Assert.Same(window, SynchronizationContext.Current);
            return answer;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>The window's thread takes up what was posted to it until <paramref name="done"/> is.</summary>
    private static async Task PumpUntil(WindowContext window, Task done)
    {
        var until = DateTime.UtcNow + Poll.Patience;
        while (!done.IsCompleted && DateTime.UtcNow < until)
        {
            window.Pump();
            await Task.Delay(5);
        }

        window.Pump();
        Assert.True(done.IsCompleted, "it never finished");
    }

    [Fact]
    public async Task A_daoris_route_goes_on_after_its_wait_without_the_windows_thread()
    {
        var window = new WindowContext();
        var route = new Waiting("DAORIS.TEST");
        var dispatcher = Compose(route);

        var answer = DispatchOnWindow(dispatcher, window, route.ModuleName);

        // Its synchronous part ran on the dispatching thread, where the stall watch times and names it.
        Assert.Equal(Environment.CurrentManagedThreadId, route.StartedOn);
        // And it went on with nothing taken up by the window's thread.
        var wentOnIn = await route.WentOnIn.Task.WaitAsync(Poll.Patience);
        Assert.NotSame(window, wentOnIn);

        await PumpUntil(window, answer);
        Assert.True((await answer).Success);
    }

    [Fact]
    public async Task The_kits_own_route_still_goes_on_on_the_windows_thread()
    {
        var window = new WindowContext();
        var route = new Waiting("TEST.WINDOW");
        var dispatcher = Compose(route);

        var answer = DispatchOnWindow(dispatcher, window, route.ModuleName);

        // A window facade moves the form, so it is resumed where the form lives: not until the window's thread takes it up.
        await Task.Delay(100);
        Assert.False(route.WentOnIn.Task.IsCompleted);
        await PumpUntil(window, answer);
        Assert.Same(window, await route.WentOnIn.Task);
        Assert.True((await answer).Success);
    }

    [Theory]
    [InlineData("DAORIS.DRIVER", true)]
    [InlineData("DAORIS.WINDOWS", true)]
    [InlineData("daoris.log", true)]
    [InlineData("WINDOW", false)]
    [InlineData("DAORISX.DRIVER", false)]
    [InlineData("", false)]
    public void Only_daoris_modules_leave_the_window(string module, bool leaves) =>
        Assert.Equal(leaves, OffTheWindow.Leaves(module));
}
