using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The review's gate beside a landing's plan and press (REVIEWENV1c, D154 point 7; the review environment design §3.1): the
/// <c>LANDING</c> and <c>LAND_SESSION_TREE</c> routes read the gate for the session's tree and hand it to the landing, which
/// refuses what it holds; each answers what the gate says while it holds the work, for the page to say *Waits for your review in
/// `environment`* where *Accept…* would be (REVIEWENV1g), and nothing where nothing waits, so a landing no review touches answers as
/// it did. The landing's own refusal is held over real git by the driver's <c>ReviewLandingTests</c>.
/// </summary>
public sealed class DriverModuleReviewGateTests
{
    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void A_gate_that_holds_the_work_answers_its_state_environment_level_step_and_sentence()
    {
        var step = new QuestView("q2", "ask #a1", "web-app", "Show #q1 in `local` for review", "", "Taken") { SetUpIn = "local" };
        var held = new ReviewGateState(ReviewStates.BeingSetUp,
            new ReviewDecision(ReviewLevels.SetUpStep, "local") { Repository = "web-app", Work = "q1", SetUpStep = step });

        var answer = JsonSerializer.SerializeToElement(DriverModule.Waits(held), Camel);

        Assert.Equal("being-set-up", answer.GetProperty("state").GetString());
        Assert.Equal("local", answer.GetProperty("environment").GetString());
        Assert.Equal("set-up-step", answer.GetProperty("level").GetString());
        Assert.Equal("q2", answer.GetProperty("quest").GetString());
        Assert.Equal(
            "Waits for your review in `local`: set-up step `#q2` is taken: it shows the work there, then waits for your look.",
            answer.GetProperty("says").GetString());
    }

    [Fact]
    public void A_gate_that_lets_the_work_go_answers_nothing()
    {
        Assert.Null(DriverModule.Waits(new ReviewGateState(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null))));
        Assert.Null(DriverModule.Waits(new ReviewGateState(ReviewStates.Skipped, new ReviewDecision(ReviewLevels.Skip, null))));
    }
}
