using Xunit.Sdk;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A named gap sets its own difference apart and nothing else, and one that sets nothing apart is closed (the agent-lyntai design §6.3, 1d).</summary>
public sealed class ParityGapTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private static ParityObservation Of(string path, params string[] shown)
    {
        var seen = new ParityObservation(path, Start);
        foreach (var text in shown) seen.Add(ParityChannel.Console, "shown", ("text", text));
        return seen;
    }

    private static ParityGap Shown(string text) => new(
        $"the line `{text}`", seen => seen.Where(each => each["text"] == text), ParityGap.None);

    [Fact]
    public void A_gap_sets_its_difference_apart_and_the_rest_is_compared_exactly()
    {
        var (daoris, layer) = ParityGap.Apart(Of("daoris", "a", "only today's", "b"), Of("layer", "a", "b"), [Shown("only today's")]);

        ParityOracle.Equal(daoris, layer, new ParityRelation([]), []);
    }

    [Fact]
    public void A_gap_hides_no_other_difference()
    {
        var (daoris, layer) = ParityGap.Apart(Of("daoris", "a", "only today's", "b"), Of("layer", "a", "c"), [Shown("only today's")]);

        Assert.Equal(["Console #2: shown.text: daoris 'b', layer 'c'"], ParityOracle.Differences(daoris, layer, new ParityRelation([]), []));
    }

    [Fact]
    public void A_gap_that_sets_nothing_apart_is_closed_and_fails_until_its_name_goes()
    {
        var closed = Assert.Throws<XunitException>(() => ParityGap.Apart(Of("daoris", "a"), Of("layer", "a"), [Shown("gone")]));

        Assert.Contains("`the line `gone``", closed.Message, StringComparison.Ordinal);
    }
}
