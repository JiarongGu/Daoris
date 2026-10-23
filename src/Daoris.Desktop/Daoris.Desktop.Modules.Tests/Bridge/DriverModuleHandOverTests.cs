using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// MIXAGENT1b-i (D163's note on the press): <c>SESSION_HAND_OVER</c> over the bridge, the page's *Go on with another agent…*.
/// The route reads <c>id</c>, <c>agent</c>, <c>account</c> and <c>why</c> (default <c>press</c>), asks as the person, and answers
/// <c>{sessionId, kept, why, message}</c>; a refusal is the answer, never thrown. The judgement is the driver's
/// <c>HandOverTests</c>'.
/// </summary>
public sealed class DriverModuleHandOverTests : DriverModuleBridge
{
    /// <summary>Before the loop's conversations come up, nothing is handed over: a sentence the person can wait out.</summary>
    [Fact]
    public async Task A_press_before_the_loop_is_up_says_so()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_HAND_OVER", new { id = "c1", agent = "talk" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>An agent the roster does not hold is refused by its code, as the answer, with nothing started.</summary>
    [Fact]
    public async Task An_unknown_agent_is_the_answer_by_its_code()
    {
        var (_, module, service) = await UpAsync();

        var answer = await AnswerAsync(module, "SESSION_HAND_OVER", new { id = "c1", agent = "nobody" });

        Assert.Equal(JsonValueKind.Null, answer.GetProperty("sessionId").ValueKind);
        Assert.False(answer.GetProperty("kept").GetBoolean());
        Assert.Equal(HandOver.UnknownAgent, answer.GetProperty("why").GetString());
        Assert.False(string.IsNullOrWhiteSpace(answer.GetProperty("message").GetString()));
        Assert.Equal(1, service.Count);
    }

    /// <summary>The conversation's own agent is the same agent: refused by its code, the reason defaulting to the press.</summary>
    [Fact]
    public async Task Its_own_agent_is_refused_as_the_same()
    {
        var (_, module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "SESSION_HAND_OVER", new { id = "c1", agent = "talk" });

        Assert.False(answer.GetProperty("kept").GetBoolean());
        Assert.Equal(HandOver.SameAgent, answer.GetProperty("why").GetString());
    }

    /// <summary>
    /// MIXAGENT1b-ii (D163's note on the offer): Daoris's own offer is asked as <c>daoris</c> only with <c>near</c> or
    /// <c>limit</c>; a press, an absent word or any other reason stays the person's, whatever the payload claims.
    /// </summary>
    [Theory]
    [InlineData("near", "daoris", "daoris")]
    [InlineData("limit", "daoris", "daoris")]
    [InlineData("press", "daoris", "person")]
    [InlineData("agent", "daoris", "person")]
    [InlineData("near", null, "person")]
    [InlineData("near", "agent", "person")]
    [InlineData("limit", "person", "person")]
    public void Daoris_asks_only_with_near_or_limit(string why, string? askedBy, string expected)
    {
        Assert.Equal(expected, ConversationRoutes.HandOverAskedBy(why, askedBy));
    }

    /// <summary>
    /// 🔴 HANDOVERSAID1: the person's yes in the window to a conversation's agent's ask, which the take could not go on with: the session's
    /// record says it was not taken, by the take's own reason in words and what the session does next, never that it was refused.
    /// </summary>
    [Fact]
    public async Task A_yes_the_take_cannot_go_on_with_is_said_as_not_taken_with_its_reason()
    {
        var (loop, module, _) = await UpAsync();
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Sessions));
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Sessions, "c1" + HandOverAskFiles.AskSuffix),
            """{"id":"a1b2c3d4","session":"c1","note":"Near my limit.","askedBy":"agent","at":"2026-10-20T09:00:00Z"}""");
        var files = new HandOverAskFiles(Home);
        files.WriteState("c1", new HandOverAskState("a1b2c3d4", HandOverAskStates.Waiting, null, "talk", DateTimeOffset.Parse("2026-10-20T09:01:00Z")));

        var yes = await AnswerAsync(module, "SESSION_HAND_OVER_APPROVE", new { session = "c1", ask = "a1b2c3d4" });

        Assert.False(yes.GetProperty("approved").GetBoolean());
        Assert.Equal(HandOver.SameAgent, yes.GetProperty("why").GetString());
        Assert.Equal((HandOverAskStates.Refused, HandOverAskWhy.SameAgent), (files.ReadState("c1")!.State, files.ReadState("c1")!.Code));
        var note = Assert.Single(loop.Events.After("c1", 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal("handover.not-taken", note.Parts![0].Code);
        Assert.Equal("The hand-over to talk was not taken: it already runs on that agent. This session goes on with its own agent.", note.Text);
    }

    private async Task<(DriverLoop Loop, DriverModule Module, DriverModuleStartFromTests.StandIn Service)> UpAsync()
    {
        var root = Path.Combine(Home, "engine");
        Directory.CreateDirectory(root);
        File.WriteAllText(DriverConfigPath, """{ "adapter": "talk" }""");
        var service = new DriverModuleStartFromTests.StandIn(root);
        service.Chat("c1", root, false);
        var loop = Loop();
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new NoSpawn() });
        var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        var chat = new ChatRunner(
            client, adapters, loop.Home, loop.Processes, loop.Output, new HarnessRoster(adapters, HarnessSettingsPath), loop.Events);
        await loop.ComeUpAsync(client, chat);
        return (loop, new DriverModule(Bus, loop), service);
    }

    private sealed class NoSpawn : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public System.Diagnostics.ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("this test spawns nothing");

        public System.Diagnostics.ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("this test spawns nothing");
    }
}
