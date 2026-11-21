using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The bus's registry checked at subscribe and at emit: a subscription naming a type it does not declare never registers,
/// and a payload keeps its declared fields of their shapes, dropping the rest with their reasons and keeping the fact.
/// </summary>
public sealed class BusTypesTests
{
    /// <summary>🔴 A subscriber naming a type the registry does not declare fails at registration, naming each; a declared set registers.</summary>
    [Fact]
    public void A_subscriber_naming_an_unknown_type_fails_at_registration()
    {
        var refused = Assert.Throws<BusTypeUnknownException>(() =>
            BusTypes.Subscribe("opinion-gate", [BusTypes.OpinionGiven.Kind, "opinion.forged/1", "opinion.given/2"]));
        Assert.Equal(["opinion.forged/1", "opinion.given/2"], refused.Kinds);
        Assert.Contains("`opinion.forged/1`", refused.Message, StringComparison.Ordinal);

        var subscription = BusTypes.Subscribe("opinion-gate", [BusTypes.OpinionGiven.Kind, BusTypes.OpinionAnswered.Kind, BusTypes.OpinionGiven.Kind]);
        Assert.Equal([BusTypes.OpinionGiven, BusTypes.OpinionAnswered], subscription.Types);
        Assert.Throws<ArgumentException>(() => BusTypes.Subscribe("an opinion gate", [BusTypes.OpinionGiven.Kind]));
    }

    /// <summary>
    /// 🔴 A payload keeps each declared field in its order, its value where it has its shape; a field not declared, one named twice and a
    /// value of another shape are dropped with their reasons, the dropped value leaving its field unknown.
    /// </summary>
    [Fact]
    public void A_payload_keeps_its_declared_fields_and_drops_the_rest_with_their_reasons()
    {
        var admitted = BusTypes.CheckStarted.Admit(
        [
            ("invocation", "i-1"), ("words", "a sentence"), ("gates", "three"), ("invocation", "i-2"), ("repository", null), ("commit", "abc"),
        ]);

        Assert.Equal([("invocation", (object?)"i-1"), ("gates", null), ("repository", null), ("commit", "abc")], admitted.Kept);
        Assert.Equal([("words", BusDrops.Undeclared), ("gates", BusDrops.Shape), ("invocation", BusDrops.Twice)], admitted.Dropped);
    }

    /// <summary>
    /// 🔴 A list keeps the ids that fit, at most its bound, and counts each other one; one that had ids and kept none is unknown, never
    /// empty, and an empty one stays empty.
    /// </summary>
    [Fact]
    public void A_list_keeps_the_ids_that_fit_and_one_that_kept_none_is_unknown()
    {
        Assert.Equal("Engine.Tests|MapTests 1", Listed(["Engine.Tests|PlayerTests(speed: 2)", "Engine.Tests|MapTests"]));
        Assert.Equal("unknown 1", Listed(["Engine.Tests|PlayerTests(speed: 2)"]));
        Assert.Equal(" 0", Listed([]));
        var many = Enumerable.Range(0, BusShapes.MostListed + 4).Select(n => $"id-{n}").ToList();
        Assert.Equal($"{string.Join(',', many.Take(BusShapes.MostListed))} 4", Listed(many));
        Assert.Equal((null, 1), BusShapes.Admit(BusShapes.Ids, "id-1"));
    }

    /// <summary>
    /// 🔴 A provisional deadline stands on the bound it names, which lives above the bus (a check's own bound, a busy runner's offer, a
    /// pass's bound), so a bound that moves fails here until its deadline moves with it.
    /// </summary>
    [Fact]
    public void A_provisional_deadline_stands_on_the_bound_it_names()
    {
        Assert.Equal(HeavyExecutor.DefaultBound + TimeSpan.FromMinutes(15), BusTypes.CheckStarted.Deadline.Within);
        Assert.Equal(HeavyExecutor.BusyAgain + TimeSpan.FromMinutes(5), BusTypes.CheckRequeued.Deadline.Within);
        Assert.Equal(TimeSpan.FromMinutes(OpinionKey.DefaultMinutes + 10), BusTypes.OpinionAsked.Deadline.Within);
        Assert.True(BusTypes.CheckStarted.Deadline.Provisional);
    }

    /// <summary>What a list kept, its ids joined by commas or <c>unknown</c>, and how many it dropped.</summary>
    private static string Listed(IReadOnlyList<string> ids)
    {
        var (value, dropped) = BusShapes.Admit(BusShapes.Ids, ids);
        return $"{(value is IReadOnlyList<string> kept ? string.Join(',', kept) : "unknown")} {dropped}";
    }
}
