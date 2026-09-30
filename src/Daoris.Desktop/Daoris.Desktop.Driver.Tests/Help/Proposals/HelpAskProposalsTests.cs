using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>ask</c> proposal (HELP1c): something to start, made through the ask door.</summary>
public sealed class HelpAskProposalsTests : HelpProposalsFixture
{
    [Fact]
    public void An_ask_is_planned_as_the_ask_door_and_its_terminal_twin()
    {
        var ask = new HelpProposal("p2", "ask", "ask", null, "work", null, "fix the cold-cache stall", "why", "h1", "proposed");

        var plan = HelpProposals.Plan(ask, DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver ask --workspace work \"fix the cold-cache stall\"", plan.Terminal);
        Assert.Contains("fix the cold-cache stall", plan.Describe);
        Assert.Null(plan.Apply); // an ask is made through the ask door, not an edit to the file
        Assert.Contains("no workspace `nope`", HelpProposals.Plan(ask with { Workspace = "nope" }, DriverConfig.Empty, Facts).Refusal);
    }

    [Fact]
    public async Task An_ask_is_applied_through_the_ask_door()
    {
        var (ask, asked, _) = await ApplyAsync(new HelpProposal("p7", "ask", "ask", null, "work", null, "start it", "why", "h1", "proposed"));

        Assert.Equal(["ask work start it"], asked.Calls);
        Assert.Contains("Applied", ask.Told);
    }
}

public sealed partial class HelpStandInDoors
{
    public Task<AskAnswer> AskAsync(string workspace, string sentence, CancellationToken ct)
    {
        Calls.Add($"ask {workspace} {sentence}");
        return Task.FromResult(new AskAnswer(true, "Asked.", "a1", null));
    }
}
