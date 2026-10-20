using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// One look at a time per route (SWEEPFLIGHT1, UIFREEZE1): the frozen window's stack held 22 clean-up lists at once, each a
/// whole git fan-out, because every window and every change the page heard asked the list again while the last was still
/// running. Concurrent identical asks of the clean-up's list and of the lines share the one under way; a finished one is
/// kept for no one, and one a press overtook is joined no more.
/// </summary>
/// <remarks>
/// The fan-out is counted where it begins: the registry each ask reads first, held by the stand-in until the test lets it go.
/// </remarks>
public sealed class DriverModuleOneFlightTests : DriverModuleBridge
{
    [Theory]
    [InlineData("SWEEP_PLAN")]
    [InlineData("LINES")]
    public async Task Two_asks_while_one_is_under_way_share_it_and_the_next_asks_again(string route)
    {
        var service = new HeldService();
        var module = await ComeUpAsync(service);

        var first = AskAsync(module, route);
        await service.AskedAsync(1);
        var second = AskAsync(module, route);
        // Given the time, the second reads nothing of its own: it waits for the first's answer.
        await Task.Delay(200);
        Assert.Equal(1, service.Asked);

        service.Answer();
        var (one, two) = (await first.WaitAsync(Patience), await second.WaitAsync(Patience));
        Assert.True(one.Success && two.Success, $"{route} was refused: {one.Error?.Message ?? two.Error?.Message}");
        Assert.Equal(Wire(one), Wire(two));
        Assert.Equal(1, service.Asked);

        // Ended, so kept for no one: the next ask reads again.
        Assert.True((await AskAsync(module, route).WaitAsync(Patience)).Success);
        Assert.Equal(2, service.Asked);
    }

    [Fact]
    public async Task An_ask_after_a_press_answered_is_not_joined_to_the_look_the_press_overtook()
    {
        var service = new HeldService();
        var module = await ComeUpAsync(service);

        var before = AskAsync(module, "LINES");
        await service.AskedAsync(1);
        // A press that changes what the lines answer, answered while the look before it still runs: the page asks again
        // because of it, and an answer begun before it would say what it changed had not happened.
        await AnswerAsync(module, "SET_LINE", new { repository = "engine", branch = "develop" });
        var after = AskAsync(module, "LINES");
        await service.AskedAsync(2);

        service.Answer();
        Assert.True((await before.WaitAsync(Patience)).Success);
        Assert.True((await after.WaitAsync(Patience)).Success);
        Assert.Equal(2, service.Asked);
    }

    [Fact]
    public async Task Different_routes_never_share_a_look()
    {
        var service = new HeldService();
        var module = await ComeUpAsync(service);

        var lines = AskAsync(module, "LINES");
        await service.AskedAsync(1);
        var sweep = AskAsync(module, "SWEEP_PLAN");
        await service.AskedAsync(2);

        service.Answer();
        Assert.True((await lines.WaitAsync(Patience)).Success);
        Assert.True((await sweep.WaitAsync(Patience)).Success);
    }

    /// <summary>The reads shared are the two the frozen window's stack repeated, and neither is a press: a press is two acts.</summary>
    [Fact]
    public void Only_the_lines_and_the_clean_ups_list_are_shared_and_neither_is_a_press()
    {
        Assert.Equal(["LINES", "SWEEP_PLAN"], DriverModule.OneFlightRoutes.Order(StringComparer.Ordinal));
        Assert.All(DriverModule.OneFlightRoutes, route =>
            Assert.Null(DriverModule.Declared(new Shenora.Core.Ipc.IpcRequest { Id = "x", Module = DriverModule.Name, Type = route })));
    }

    private static readonly TimeSpan Patience = Poll.Patience;

    private async Task<DriverModule> ComeUpAsync(HeldService service)
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        // Coming up reads the registry itself: the count starts after it.
        service.Reset();
        return module;
    }

    private static string Wire(Shenora.Core.Ipc.IpcResponse response) => JsonSerializer.Serialize(response.Data);

    /// <summary>
    /// The service, standing in: one registered repository with no checkout here, no quest, no session. Each registry read
    /// is counted and held until <see cref="Answer"/>, as a slow machine holds a look's first read.
    /// </summary>
    private sealed class HeldService : HttpMessageHandler
    {
        private readonly TaskCompletionSource _go = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _holding;
        private int _asked;

        public int Asked => Volatile.Read(ref _asked);

        public void Reset()
        {
            Interlocked.Exchange(ref _asked, 0);
            _holding = true;
        }

        public void Answer() => _go.TrySetResult();

        public async Task AskedAsync(int count)
        {
            var until = DateTime.UtcNow + Poll.Patience;
            while (Asked < count)
            {
                Assert.True(DateTime.UtcNow < until, $"the registry was read {Asked} time(s), never {count}");
                await Task.Delay(10);
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var registry = request.RequestUri!.AbsolutePath == "/api/registry";
            if (registry && _holding)
            {
                Interlocked.Increment(ref _asked);
                await _go.Task.WaitAsync(ct);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    registry ? """[{"repository":"engine","workspace":"aurora"}]""" : "[]",
                    System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
