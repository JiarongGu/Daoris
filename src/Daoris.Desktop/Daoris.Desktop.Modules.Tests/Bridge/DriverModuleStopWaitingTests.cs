using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// LIMITRESUME1d (D206 §3.7, N27): *Stop waiting* over the bridge, <c>SESSION_STOP_WAITING</c>, the person's press on an ended conversation
/// its account's limit holds. The route hands the loop's chat runner the session and answers <c>{stopped, message}</c>, a refusal in the
/// runner's sentence as data. What the press does to the record is the driver's <c>LimitWaitsProcessTests</c>' (LIMITRESUME1c).
/// </summary>
public sealed class DriverModuleStopWaitingTests : DriverModuleBridge
{
    /// <summary>Before the loop's conversations come up, nothing can stop waiting: a sentence the person can wait out.</summary>
    [Fact]
    public async Task A_press_before_the_loop_is_up_says_so()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_STOP_WAITING", new { id = "c1" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>A conversation that waits for no limit is refused as data, in the runner's sentence, and nothing is marked.</summary>
    [Fact]
    public async Task One_that_waits_for_no_limit_is_refused_as_the_answer()
    {
        var root = Path.Combine(Home, "engine");
        Directory.CreateDirectory(root);
        File.WriteAllText(DriverConfigPath, """{ "adapter": "talk" }""");
        var service = new DriverModuleStartFromTests.StandIn(root);
        service.Chat("c1", root, words: false);
        var loop = Loop();
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        var chat = new ChatRunner(
            client, adapters, loop.Home, loop.Processes, loop.Output, new HarnessRoster(adapters, HarnessSettingsPath), loop.Events);
        await loop.ComeUpAsync(client, chat);

        var answer = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_STOP_WAITING", new { id = "c1" });

        Assert.False(answer.GetProperty("stopped").GetBoolean());
        Assert.Contains("does not wait for its account's limit", answer.GetProperty("message").GetString());
        Assert.Null(new GoOnMarks(loop.Home).Read("c1"));
    }

    /// <summary>A conversation's adapter on the protocol door with no toolchain to ask, so nothing is probed or spawned.</summary>
    private sealed class Talk : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("this test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("this test spawns nothing");
    }
}
