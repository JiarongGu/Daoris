using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A press made while the service misses its bound is refused in the busy sentence, not as a bare <c>UNKNOWN_ERROR</c>, and the
/// read that stalled leaves no unobserved task behind.
/// </summary>
public sealed class DriverModuleBusyServiceTests : DriverModuleBridge
{
    [Fact]
    public async Task A_press_whose_service_read_timed_out_is_refused_as_busy_and_leaves_no_unobserved_task()
    {
        var unobserved = new List<Exception>();
        void Seen(object? sender, UnobservedTaskExceptionEventArgs e) => unobserved.AddRange(e.Exception.Flatten().InnerExceptions);
        TaskScheduler.UnobservedTaskException += Seen;
        try
        {
            var loop = Loop();
            var module = new DriverModule(Bus, loop);
            using var http = new HttpClient(new StalledService()) { Timeout = TimeSpan.FromMilliseconds(200) };
            await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, http));

            var refusal = await RefusalAsync(module, "LANDING", new { id = "s1a2b3c4" });

            Assert.StartsWith(Refusals.ServiceBusy, refusal);
            Assert.DoesNotContain("UNKNOWN_ERROR", refusal);
            // A read that joins a flight is refused alike.
            Assert.StartsWith(Refusals.ServiceBusy, await RefusalAsync(module, "SWEEP_PLAN"));
            for (var attempt = 0; attempt < 5; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(20);
            }

            Assert.Empty(unobserved);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Seen;
        }
    }

    /// <summary>A service that never answers: the client's own bound ends each ask.</summary>
    private sealed class StalledService : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            throw new InvalidOperationException("unreachable");
        }
    }
}
