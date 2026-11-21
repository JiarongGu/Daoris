using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A brainstorm's passes' deadline is its own, read from what it asks: the policy's minutes for a plain question, longer for a design
/// pass and for sources named whole, the request's minutes grown so the generators' window holds a pass; and every generator is told its
/// minutes and asked to give its answer so far early, marked partial.
/// </summary>
public sealed class BrainstormDeadlineTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-20T10:00:00Z");

    private static BrainstormRequest Request(string? pass = null, IReadOnlyList<string>? sources = null) =>
        new("b1", "How should the palette keep its contrast?", "engine", [], At, sources, pass);

    [Fact]
    public void Each_brainstorm_names_its_passes_deadline_from_what_it_asks()
    {
        var policy = BrainstormMinutes.Default;

        var plain = BrainstormDeadlines.Of(policy, Request());
        var design = BrainstormDeadlines.Of(policy, Request(pass: DesignPasses.Design));
        var named = BrainstormDeadlines.Of(policy, Request(sources: ["docs/palette.md"]));

        Assert.Equal(policy, plain);
        Assert.Equal(2 * policy.Pass, design.Pass);
        Assert.Equal(15, named.Pass);
        // The generators' window holds a whole pass, the synthesis's and the challenge's minutes still reserved.
        Assert.True(design.Generators >= design.Pass);
        Assert.True(named.Generators >= named.Pass);
        Assert.Equal((policy.Synthesis, policy.Challenge), (design.Synthesis, design.Challenge));
    }

    [Fact]
    public void A_generator_is_told_its_minutes_and_asked_for_its_answer_so_far_marked_partial()
    {
        var generator = DesignPasses.Generator(Request(), "sha256:00", 15);
        var designer = DesignPasses.Generator(Request(pass: DesignPasses.Design), "sha256:00", 20);

        Assert.Contains("15 minutes", generator, StringComparison.Ordinal);
        Assert.Contains("20 minutes", designer, StringComparison.Ordinal);
        Assert.All([generator, designer], said => Assert.Contains("`partial: true`", said, StringComparison.Ordinal));
    }

    [Fact]
    public void An_answer_marked_partial_reads_as_partial_and_one_not_marked_as_whole()
    {
        const string Proposal = """{ "idea": "Raise the contrast", "rationale": "It fails 4.5:1.", "effort": "small", "risk": "low", "reversible": "yes", "undo": null, "confidence": "medium", "assumptions": [], "sources": [] }""";

        var partial = BrainstormAnswers.Check($$"""{ "brainstorm": 1, "partial": true, "proposals": [{{Proposal}}], "needs": [], "none": null }""", new HashSet<string>());
        var whole = BrainstormAnswers.Check($$"""{ "brainstorm": 1, "proposals": [{{Proposal}}], "needs": [], "none": null }""", new HashSet<string>());

        Assert.True(partial.Answer!.Partial);
        Assert.False(whole.Answer!.Partial);
    }
}
