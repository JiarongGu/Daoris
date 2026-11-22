using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The bus consumers' census: no consumer (<c>IBusConsumer</c>) judges a growing set unbounded in parallel. A <c>Task.WhenAll</c> in a
/// file that declares a consumer fans out over what the consumer holds open, which grows with the home (a restart's first round of nine
/// owed opinions queued behind one another and every other git for an hour); each is listed with what bounds it. Shown red on a fan-out
/// left out of the list and on a planted one.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class BusConsumerSetsCensusTests
{
    private const string What = "fans out over a consumer's set";

    private static readonly Regex Consumer = new(@"(?s)\bclass\s+\w+[^{;]*\bIBusConsumer\b.*\bTask\.WhenAll\s*\(");

    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["OpinionGateConsumer.cs"] = "each judgment takes one place of the gate's AtOnce and its repository's one place before it asks git, so a round of "
            + "hundreds queues in the gate; the whole is awaited, none is left running",
    };

    private static SortedSet<string> Found(IEnumerable<(string File, string Source)> sources) => SourceCensus.Callers(sources, Consumer);

    [Fact]
    public void Every_fan_out_over_a_consumers_set_is_listed_with_what_bounds_it()
    {
        var problems = SourceCensus.Problems(Found(SourceCensus.Driver(typeof(BusConsumerSetsCensusTests))), SourceCensus.Listed(typeof(BusConsumerSetsCensusTests), Allowed.Keys), What);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Each_fan_out_left_out_of_the_list_turns_the_census_red_naming_it()
    {
        Assert.Empty(SourceCensus.Unproved(Found(SourceCensus.Driver(typeof(BusConsumerSetsCensusTests))), SourceCensus.Listed(typeof(BusConsumerSetsCensusTests), Allowed.Keys), What));
    }

    [Fact]
    public void A_planted_unbounded_fan_out_turns_the_census_red_naming_it()
    {
        var planted = SourceCensus.Driver(typeof(BusConsumerSetsCensusTests)).Append(("Planted.cs",
            "internal sealed class PlantedConsumer : IBusConsumer\n{\n    public Task Each(IEnumerable<string> open) => Task.WhenAll(open.Select(Judge));\n}\n"));

        Assert.Equal(["Planted.cs fans out over a consumer's set and is not listed"],
            SourceCensus.Problems(Found(planted), SourceCensus.Listed(typeof(BusConsumerSetsCensusTests), Allowed.Keys), What));
    }
}
