using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The second opinion's gate over the bridge (XAGENT1f, D155 points 9 and 10; the second-agent design §8.1, §8.5): beside a
/// landing's plan and press, <c>LANDING</c> and <c>LAND_SESSION_TREE</c> answer what the opinion's part of the gate says while it
/// holds the work, for the page to draw where *Accept…* would be (XAGENT1g), with the token a press sends back to answer what it
/// showed; and the presses' bridge halves (<c>OPINION_GATE</c>, <c>ASK_OPINION</c>, <c>OPINION_ANYWAY</c>, <c>OPINION_MYSELF</c>,
/// <c>STOP_OPINION</c>), each the driver's own press. The presses themselves are held by the driver's <c>OpinionLookTests</c>.
/// </summary>
public sealed class DriverModuleOpinionGateTests : DriverModuleBridge
{
    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private const string Read = "1111111111111111111111111111111111111111";

    [Fact]
    public void A_disputed_opinion_answers_its_state_reader_counts_sentence_and_the_token_a_press_sends_back()
    {
        var opinion = new OpinionView("o1", "landing", "first", "s1", "r-o1", "web-app", "0000000000000000000000000000000000000000", Read,
            "codex-acp", ReviewerLabels.AnotherMaker, "given")
        {
            Product = "Codex", Maker = "OpenAI", Findings = [new OpinionFindingView(1, "must", "src/a.ts:1", "claim", "", "", "sure")],
        };
        var gate = new OpinionGateState(OpinionGateStates.Disputed, "web-app")
        {
            Session = "s1", Tip = Read, Opinion = opinion, Disputes = new([1], []), Since = 0,
            Rule = new ResolvedOpinion(new OpinionRule(["codex-acp"], [OpinionRules.Landing], Required: true), OpinionSource.Repository),
        };

        var answer = JsonSerializer.SerializeToElement(DriverModule.Opinion(gate), Camel);

        Assert.Equal("disputed", answer.GetProperty("state").GetString());
        Assert.True(answer.GetProperty("holds").GetBoolean());
        Assert.True(answer.GetProperty("required").GetBoolean());
        Assert.Equal(("o1", "codex-acp", "Codex", "OpenAI", ReviewerLabels.AnotherMaker, "r-o1"),
            (answer.GetProperty("opinion").GetString(), answer.GetProperty("reviewer").GetString(), answer.GetProperty("product").GetString(),
             answer.GetProperty("maker").GetString(), answer.GetProperty("label").GetString(), answer.GetProperty("reviewing").GetString()));
        Assert.Equal((1, 1, 0), (answer.GetProperty("findings").GetInt32(), answer.GetProperty("disputes").GetInt32(), answer.GetProperty("since").GetInt32()));
        Assert.Equal(gate.Token, answer.GetProperty("answers").GetString());
        Assert.Equal(gate.Says, answer.GetProperty("says").GetString());
    }

    [Fact]
    public void A_level_that_asks_no_opinion_answers_nothing_and_a_state_no_press_answers_carries_no_token()
    {
        Assert.Null(DriverModule.Opinion(new OpinionGateState(OpinionGateStates.None, "web-app")));

        var reading = JsonSerializer.SerializeToElement(DriverModule.Opinion(new OpinionGateState(OpinionGateStates.NotAsked, "web-app") { Session = "s1" }), Camel);
        Assert.Equal(("not-asked", true), (reading.GetProperty("state").GetString(), reading.GetProperty("holds").GetBoolean()));
        Assert.True(!reading.TryGetProperty("answers", out var token) || token.ValueKind == JsonValueKind.Null);
    }

    [Theory]
    [InlineData("OPINION_GATE")]
    [InlineData("ASK_OPINION")]
    [InlineData("OPINION_ANYWAY")]
    [InlineData("OPINION_MYSELF")]
    public async Task Each_press_on_a_sessions_opinion_waits_for_the_driver_s_service(string route)
    {
        var refused = await RefusalAsync(Module(), route, new { id = "s1", words = "fine" });

        Assert.StartsWith(Refusals.DriverNotReady, refused, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stopping_a_reviewer_waits_for_the_driver_s_service()
    {
        var refused = await RefusalAsync(Module(), "STOP_OPINION", new { opinion = "o1" });

        Assert.StartsWith(Refusals.DriverNotReady, refused, StringComparison.Ordinal);
    }
}
