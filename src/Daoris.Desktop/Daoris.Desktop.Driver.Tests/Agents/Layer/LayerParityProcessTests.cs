using Daoris.Driver;
using Lyntai.Agents;
using LayerEnded = Lyntai.Agents.SessionEnded;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The oracle proven on the tests' own provider (the agent-lyntai design §6.3, 1b-i): one scenario program run on the Daoris path
/// (<see cref="ParityDaorisPath"/>, today's capture and native reader on <see cref="TransportRun"/>) and on the layer path
/// (<see cref="LayerRun"/> over the tests' provider), each observed alike and compared by the oracle; then each regression 1b-i plants,
/// confined to the layer path through the tests' provider's reader, failing it. Real node processes.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LayerParityProcessTests : IDisposable
{
    private const string Prompt = "the prompt";

    private static readonly DateTimeOffset Start = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The tests' door's relation: today's unread failure against the scenario's own entries, one Daoris entry for two of the layer's.</summary>
    private static readonly ParityRelation Relation = new([new("unread", "layer:tests:fatal"), new("unread", "layer:tests:broken")]);

    private static readonly ParityPair Fatal = new("unread", "layer:tests:fatal");

    private static readonly object Worked = new
    {
        argv = true,
        echo = true,
        @out = new object[]
        {
            "the harness's own line", new { t = "think", text = "pondering" }, new { t = "text", text = "hello" },
            new { t = "end", subtype = "success" },
        },
    };

    private static readonly object Stepped = new
    {
        @out = new object[]
        {
            new { t = "call", name = "lookup", args = """{"term":"a"}""", id = "call-1" }, new { t = "result", id = "call-1", text = "found" },
            new { t = "end", subtype = "success" },
        },
    };

    private static readonly object Broke = new
    {
        @out = new object[]
        {
            new { t = "text", text = "working" }, new { t = "end", kind = "Fatal", by = "tests:fatal", subtype = Failed, why = "it broke", delivery = "Taken" },
        },
    };

    private static readonly object Unheard = new
    {
        @out = new object[] { new { t = "end", kind = "Fatal", by = "tests:fatal", subtype = Failed, why = "it broke", delivery = "NeverSent" } },
    };

    /// <summary>A failed turn's subtype, stated by the scenario as Claude Code's stream states one on every result.</summary>
    private const string Failed = "error_during_execution";

    private readonly string _scratch = ConformanceScratch.New("layer-parity");

    private readonly CancellationTokenSource _bound = new(Poll.Patience);

    public void Dispose()
    {
        _bound.Dispose();
        ConformanceScratch.Remove(_scratch);
    }

    private async Task<(ParityObservation Daoris, ParityObservation Layer)> BothAsync(
        object spec, Func<IReadOnlyList<AgentStreamEvent>, IEnumerable<AgentStreamEvent>>? plant = null)
    {
        var command = LayerTestProvider.Scenario(_scratch, spec);
        using var daoris = new ParityDaorisPath(Path.Combine(_scratch, "daoris"));
        return (await daoris.RunAsync(command, Prompt, Start, _bound.Token), await LayerAsync(command, plant));
    }

    private async Task<ParityObservation> LayerAsync(
        IReadOnlyList<string> command, Func<IReadOnlyList<AgentStreamEvent>, IEnumerable<AgentStreamEvent>>? plant)
    {
        var transcript = Path.Combine(_scratch, "layer.log");
        var seen = new ParityObservation("layer", Start);
        await using (var run = new LayerRun(
            LayerTestProvider.Registration(command, ParityDaorisPath.Door, plant),
            new LayerStart(ParityDaorisPath.Door, Prompt, transcript, TimeSpan.FromHours(1)),
            new LauncherSeat(ContainedKind.Agent, StartClass.Background, Home: null, Log: null),
            exists: _ => true))
        {
            run.HandSink(ParityObserver.Sink(seen));
            await run.OpenAsync(_bound.Token);
            var end = await run.EndAsync(_bound.Token);
            ParityObserver.Ended(seen, end, run.Stopped);
            if (run.Failure is { } failure) ParityObserver.Failed(seen, AgentErrors.OfNormalized(failure), failure.Delivery);
        }

        ParityObserver.Transcript(seen, transcript);
        return seen;
    }

    [Fact]
    public async Task A_turn_that_works_is_observed_alike_on_the_Daoris_path_and_the_layer_path()
    {
        var (daoris, layer) = await BothAsync(Worked);

        ParityOracle.Equal(daoris, layer, Relation, []);
        Assert.Contains(layer.Seen, each => each.Channel == ParityChannel.Events && each["kind"] == SessionEventKind.Thought);
    }

    /// <summary>
    /// What the oracle finds today, kept so the move that needs it meets it: a tool step's kind, which today's Claude reader derives from
    /// the tool's name and the layer's tool call does not carry, is the one difference; the rest of the step is read alike.
    /// </summary>
    [Fact]
    public async Task A_tool_step_differs_only_by_the_kind_today_s_Claude_reader_derives_from_its_name()
    {
        var (daoris, layer) = await BothAsync(Stepped);

        var found = Assert.Single(ParityOracle.Differences(daoris, layer, Relation, []));
        Assert.StartsWith("Events #1:", found, StringComparison.Ordinal);
        Assert.Contains("toolKind=other", found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_turn_is_observed_alike_its_provenance_by_the_pair_the_scenario_expects()
    {
        var (daoris, layer) = await BothAsync(Broke);

        ParityOracle.Equal(daoris, layer, Relation, [Fatal]);
        Assert.Contains(layer.Seen, each => each.Channel == ParityChannel.Failure && each[ParityProjection.By] == Fatal.Layer);
    }

    public static TheoryData<string, string> Plants => new()
    {
        { "a line the layer's reader drops", "Console" },
        { "a write reordered", "Events" },
        { "an undeclared pair", "in no pair the relation declares" },
        { "a declared pair the scenario does not expect", "the scenario expects" },
        { "a delivery read Taken where its prompt's own turn was not heard", "delivery" },
    };

    [Theory]
    [MemberData(nameof(Plants))]
    public async Task Each_regression_planted_on_the_layer_path_alone_fails_the_oracle(string plant, string named)
    {
        var (spec, planted) = plant switch
        {
            "a line the layer's reader drops" => (Worked, Dropping<TextDelta>()),
            "a write reordered" => (Worked, Swapping<Thinking, TextDelta>()),
            "an undeclared pair" => (Broke, Ending(failure => new AgentFailure(failure.Kind, "tests:unheard-of") { Delivery = failure.Delivery })),
            "a declared pair the scenario does not expect" =>
                (Broke, Ending(failure => new AgentFailure(failure.Kind, "tests:broken") { Delivery = failure.Delivery })),
            "a delivery read Taken where its prompt's own turn was not heard" =>
                (Unheard, Ending(failure => new AgentFailure(failure.Kind, failure.By) { Delivery = AgentDelivery.Taken })),
            _ => throw new ArgumentOutOfRangeException(nameof(plant), plant, null),
        };
        var expected = spec == Worked ? Array.Empty<ParityPair>() : [Fatal];

        var (daoris, honest) = await BothAsync(spec);
        var (_, layer) = await BothAsync(spec, planted);

        Assert.Empty(ParityOracle.Differences(daoris, honest, Relation, expected));
        var found = ParityOracle.Differences(daoris, layer, Relation, expected);
        Assert.True(found.Any(each => each.Contains(named, StringComparison.Ordinal)), $"{plant}: {string.Join(" | ", found)}");
    }

    private static Func<IReadOnlyList<AgentStreamEvent>, IEnumerable<AgentStreamEvent>> Dropping<T>() =>
        read => read.Where((each, at) => !(each is T && at == read.ToList().FindIndex(first => first is T)));

    private static Func<IReadOnlyList<AgentStreamEvent>, IEnumerable<AgentStreamEvent>> Swapping<TFirst, TThen>() => read =>
    {
        var list = read.ToList();
        var (first, then) = (list.FindIndex(each => each is TFirst), list.FindIndex(each => each is TThen));
        (list[first], list[then]) = (list[then], list[first]);
        return list;
    };

    private static Func<IReadOnlyList<AgentStreamEvent>, IEnumerable<AgentStreamEvent>> Ending(Func<AgentFailure, AgentFailure> change) =>
        read => read.Select(each => each is LayerEnded { Failure: { } failure } ended ? ended with { Failure = change(failure) } : each);
}
