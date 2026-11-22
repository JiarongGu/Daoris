using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The bus consumers' census: what a consumer needs of ordering, bounds, folding and acknowledgement is the dispatch's
/// (<c>BusFlowConsumer</c>), never a consumer's own. Two classes of defect each review of the first consumers found, held by source:
/// (1) no file that declares a consumer (<c>IBusConsumer</c> or <c>BusFlowConsumer</c>) fans out over what it holds open with a
/// <c>Task.WhenAll</c> unbounded (a restart's first round of nine owed opinions queued behind one another and every other git for an hour);
/// (2) none keeps its own <c>SemaphoreSlim</c>, its own fold (<c>Folds</c>, <c>Folded</c>) or its own acknowledgement choice
/// (<c>BusHandled.Ended</c>, <c>BusHandled.Judged</c>). Each allowance is listed with what bounds or owns it. Shown red on one left out of
/// the list and on a planted one.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class BusConsumerSetsCensusTests
{
    private const string FanOut = "fans out over a consumer's set";

    private const string Own = "keeps its own semaphore, fold or acknowledgement choice";

    private const string Declares = @"(\bclass\s+\w+[^{;]*|\binterface\s+)(\bIBusConsumer\b|\bBusFlowConsumer<)";

    private static readonly Regex Consumer = new(@"(?s)\bclass\s+\w+[^{;]*(\bIBusConsumer\b|\bBusFlowConsumer<).*\bTask\.WhenAll\s*\(");

    private static readonly Regex Hand = new(@"(?s)" + Declares + @".*(\bSemaphoreSlim\b|\bFolds?\s*\(|\bFolded\s*\(|\bBusHandled\.(Ended|Judged)\b|\bnew\s+BusHandled\b|\block\s*\(|\bLock\b|\bChannel\.Create|\bTaskCompletionSource\b|\bHandleAsync\s*\(\s*IReadOnlyList<BusEnvelope>)");

    private static readonly Dictionary<string, string> FanOuts = new(StringComparer.Ordinal)
    {
        ["BusFlow.cs"] = "an entry's pieces each take the key's one pass, then the group's place and one of the overall places before the handler runs, "
            + "so a fact naming hundreds queues in the layer; the whole is awaited, none is left running",
    };

    private static readonly Dictionary<string, string> Owned = new(StringComparer.Ordinal)
    {
        ["BusQueue.cs"] = "the bus layer: its lane bound, its folded rows and its acknowledgement are the queue's, which the dispatch stands on",
        ["BusFlow.cs"] = "the dispatch: the one place a key's pass, the bounds, the coalescing and the acknowledgement from an outcome are written",
    };

    private static SortedSet<string> FoundFanOut(IEnumerable<(string File, string Source)> sources) => SourceCensus.Callers(sources, Consumer);

    private static SortedSet<string> FoundOwn(IEnumerable<(string File, string Source)> sources) => SourceCensus.Callers(sources, Hand);

    [Fact]
    public void Every_fan_out_over_a_consumers_set_is_listed_with_what_bounds_it()
    {
        var problems = SourceCensus.Problems(FoundFanOut(SourceCensus.Driver(typeof(BusConsumerSetsCensusTests))), SourceCensus.Listed(typeof(BusConsumerSetsCensusTests), FanOuts.Keys), FanOut);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void No_consumer_keeps_its_own_semaphore_fold_or_acknowledgement_choice_but_the_bus_and_the_dispatch()
    {
        var problems = SourceCensus.Problems(FoundOwn(SourceCensus.Driver(typeof(BusConsumerSetsCensusTests))), SourceCensus.Listed(typeof(BusConsumerSetsCensusTests), Owned.Keys), Own);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Each_one_left_out_of_a_list_turns_the_census_red_naming_it()
    {
        Assert.Empty(SourceCensus.Unproved(FoundFanOut(SourceCensus.Driver(typeof(BusConsumerSetsCensusTests))), SourceCensus.Listed(typeof(BusConsumerSetsCensusTests), FanOuts.Keys), FanOut));
        Assert.Empty(SourceCensus.Unproved(FoundOwn(SourceCensus.Driver(typeof(BusConsumerSetsCensusTests))), SourceCensus.Listed(typeof(BusConsumerSetsCensusTests), Owned.Keys), Own));
    }

    [Fact]
    public void A_planted_unbounded_fan_out_turns_the_census_red_naming_it()
    {
        var planted = SourceCensus.Driver(typeof(BusConsumerSetsCensusTests)).Append(("Planted.cs",
            "internal sealed class PlantedConsumer : IBusConsumer\n{\n    public Task Each(IEnumerable<string> open) => Task.WhenAll(open.Select(Judge));\n}\n"));

        Assert.Equal(["Planted.cs fans out over a consumer's set and is not listed"],
            SourceCensus.Problems(FoundFanOut(planted), SourceCensus.Listed(typeof(BusConsumerSetsCensusTests), FanOuts.Keys), FanOut));
    }

    [Theory]
    [InlineData("BusFlowConsumer<string>", "private readonly SemaphoreSlim _round = new(1, 1);")]
    [InlineData("BusFlowConsumer<IReadOnlyList<string>>", "private readonly SemaphoreSlim _round = new(1, 1);")]
    [InlineData("BusFlowConsumer<(string, int)>", "public bool Folds(string kind) => true;")]
    [InlineData("BusFlowConsumer<string>", "public void Folded(BusEnvelope away) { }")]
    [InlineData("BusFlowConsumer<string>", "return Ends(entry) ? BusHandled.Ended : BusHandled.Judged;")]
    [InlineData("IBusConsumer", "return new BusHandled(true);")]
    [InlineData("BusFlowConsumer<string>", "private readonly object _gate = new(); void Go() { lock (_gate) { } }")]
    [InlineData("BusFlowConsumer<string>", "private readonly Lock _gate = new();")]
    [InlineData("IBusConsumer", "private readonly Channel<int> _in = Channel.CreateUnbounded<int>();")]
    [InlineData("IBusConsumer", "private readonly TaskCompletionSource _gate = new();")]
    [InlineData("BusFlowConsumer<string>", "Task<BusHandled> IBusConsumer.HandleAsync(IReadOnlyList<BusEnvelope> row, CancellationToken ct) => null!;")]
    public void A_planted_consumer_with_its_own_handling_turns_the_census_red_naming_it(string basis, string own)
    {
        var planted = SourceCensus.Driver(typeof(BusConsumerSetsCensusTests)).Append(("Planted.cs", $"internal sealed class PlantedConsumer : {basis}\n{{\n    {own}\n}}\n"));

        Assert.Equal(["Planted.cs keeps its own semaphore, fold or acknowledgement choice and is not listed"],
            SourceCensus.Problems(FoundOwn(planted), SourceCensus.Listed(typeof(BusConsumerSetsCensusTests), Owned.Keys), Own));
    }
}
