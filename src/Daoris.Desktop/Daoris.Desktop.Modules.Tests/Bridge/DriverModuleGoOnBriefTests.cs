using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SESSCTX1c part B (D189 point 3): *Go on with Daoris's brief* over the bridge, <c>SESSION_GO_ON_BRIEF</c>, the person's press beside *Start
/// a conversation with these words*. The route hands the loop's chat runner the session and answers <c>{sessionId, sent, why, message}</c>;
/// the new part's end reaches the page as any start's does. The judgement and the act are the driver's <c>GoOnBriefedTests</c>'; the
/// runner's adapter here spawns nothing, so a part that opens ends <c>failed</c> at its spawn.
/// </summary>
public sealed class DriverModuleGoOnBriefTests : DriverModuleBridge
{
    /// <summary>Before the loop's conversations come up, nothing can go on: a sentence the person can wait out.</summary>
    [Fact]
    public async Task A_press_before_the_loop_is_up_says_so()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_GO_ON_BRIEF", new { id = "c1" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>A refusal is the answer, by its code, with nothing opened: here no words wait on the record.</summary>
    [Fact]
    public async Task A_refusal_is_the_answer_by_its_code()
    {
        var (_, module, service) = await UpAsync(words: false);

        var answer = await AnswerAsync(module, "SESSION_GO_ON_BRIEF", new { id = "c1" });

        Assert.Equal(JsonValueKind.Null, answer.GetProperty("sessionId").ValueKind);
        Assert.False(answer.GetProperty("sent").GetBoolean());
        Assert.Equal(StartFrom.NoWords, answer.GetProperty("why").GetString());
        Assert.False(string.IsNullOrWhiteSpace(answer.GetProperty("message").GetString()));
        Assert.Equal(1, service.Count);
    }

    /// <summary>
    /// The press opens the new part through the runner, and its end reaches the page as a start's does; one that failed at its spawn
    /// leaves the words on the record they were said to, and the answer says why.
    /// </summary>
    [Fact]
    public async Task The_press_opens_a_new_part_whose_end_reaches_the_page()
    {
        var (loop, module, service) = await UpAsync(words: true);

        var answer = await AnswerAsync(module, "SESSION_GO_ON_BRIEF", new { id = "c1" });

        Assert.Equal(JsonValueKind.Null, answer.GetProperty("sessionId").ValueKind);
        Assert.False(answer.GetProperty("sent").GetBoolean());
        Assert.Contains("this test spawns nothing", answer.GetProperty("message").GetString());
        await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_ENDED"));
        Assert.Equal("""{"Session":"c2","State":"failed"}""", JsonSerializer.Serialize(Raised.Single(m => m.Type == "SESSION_ENDED").Payload));
        Assert.Equal(["w1"], service.Said("c1"));
        Assert.True(loop.Nudges >= 1);
    }

    private async Task<(DriverLoop Loop, DriverModule Module, DriverModuleStartFromTests.StandIn Service)> UpAsync(bool words)
    {
        var root = Path.Combine(Home, "engine");
        Directory.CreateDirectory(root);
        File.WriteAllText(DriverConfigPath, """{ "adapter": "talk" }""");
        var service = new DriverModuleStartFromTests.StandIn(root);
        service.Chat("c1", root, words);
        var loop = Loop();
        // Its words already judged unable to go on, as a refused resume leaves them: the line the press stands under. The loop's own
        // look as it comes up then leaves them waiting, so the press is judged alone (FLAKE2).
        if (words) new GoOnMarks(loop.Home).Mark("c1", ["w1"], ContinueWhy.Of(ContinueWhy.Refused), DateTimeOffset.UtcNow);
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        var chat = new ChatRunner(
            client, adapters, loop.Home, loop.Processes, loop.Output, new HarnessRoster(adapters, HarnessSettingsPath), loop.Events);
        await loop.ComeUpAsync(client, chat);
        return (loop, new DriverModule(Bus, loop), service);
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
