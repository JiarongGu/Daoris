using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// METHODS1 a: a design pass's closed question answered becomes a decision draft for the person to keep: the repository's next decision
/// number, the choice stated as the comparison worded it, and what it rejected; never the person's own words.
/// </summary>
public sealed class DesignDecisionDraftTests
{
    private static readonly DesignQuestion Question = new(
        "Q1", "Evolve the engine in place?", "One engine, evolved to the target in one scope.", "A second engine beside the first.", "docs/areas/workflows-review-landing.md");

    [Fact]
    public void The_next_number_follows_the_highest_decision_the_repository_keeps()
    {
        Assert.Equal(310, DecisionDrafts.Next(["D308.md", "D309.md", "README.md", "D12.md", "Dx.md"]));
        Assert.Equal(1, DecisionDrafts.Next([]));
    }

    [Fact]
    public void A_yes_drafts_the_yes_choice_and_rejects_the_no_one_in_the_comparisons_words()
    {
        var draft = DecisionDrafts.Of(Question, yes: true, number: 310, route: "Evolve in place.", on: new DateOnly(2026, 11, 16));

        Assert.Equal("D310", draft.Number);
        Assert.Equal("One engine, evolved to the target in one scope.", draft.Chosen);
        Assert.Equal("A second engine beside the first.", draft.Rejected);
        Assert.StartsWith("## D310 — One engine, evolved to the target in one scope (2026-11-16)", draft.Text);
        Assert.Contains("**Rejected:** a second engine beside the first.", draft.Text);
        Assert.Contains("Evolve in place.", draft.Text);
    }

    [Fact]
    public void A_no_drafts_the_other_way_round()
    {
        var draft = DecisionDrafts.Of(Question, yes: false, number: 7, route: "Evolve in place.", on: new DateOnly(2026, 11, 16));

        Assert.Equal(("D7", "A second engine beside the first.", "One engine, evolved to the target in one scope."), (draft.Number, draft.Chosen, draft.Rejected));
    }
}
