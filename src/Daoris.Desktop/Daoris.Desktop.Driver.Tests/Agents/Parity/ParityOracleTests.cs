using Xunit.Sdk;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The oracle's own rules (the agent-lyntai design §6.3), over observations written here: the projection's correspondences let through
/// only what names the path, and each regression the design plants is caught. The proof over the two real paths is
/// <c>LayerParityProcessTests</c>, where the layer is built.
/// </summary>
public sealed class ParityOracleTests
{
    private static readonly DateTimeOffset DaorisStart = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset LayerStart = DaorisStart.AddMinutes(3);

    // One layer entry standing for two of Daoris's, and one of Daoris's for two of the layer's: legitimate pairs, the grain differing.
    private static readonly ParityRelation Relation = new(
    [
        new("owner:words:limit", "layer:acp.data.errorKind=rate_limit"),
        new("owner:kind:rate_limit", "layer:acp.data.errorKind=rate_limit"),
        new("owner:words:limit", "layer:acp.words.limit"),
    ]);

    private static readonly ParityPair Expected = new("owner:words:limit", "layer:acp.data.errorKind=rate_limit");

    /// <summary>One scenario as a path saw it: <paramref name="by"/> its provenance, its ids, times and failure its own.</summary>
    private static ParityObservation Scenario(
        string path, DateTimeOffset start, string by, string call = "call-1", string? clock = null, string delivery = "Taken",
        string hit = "the five-hour limit", int? until = 5, string access = "none", string? proc = "4345b86d1eb9f5b2")
    {
        var seen = new ParityObservation(path, start);
        seen.Add(ParityChannel.Conversation, "named", ("conversation", "conv-1"));
        seen.Add(ParityChannel.Transcript, "line", ("text", "hello"));
        seen.Add(ParityChannel.Events, "said", ("kind", "Message"), ("text", "hello"));
        seen.Add(ParityChannel.Transcript, "line", ("text", "→ Read"));
        seen.Add(ParityChannel.Events, "said", ("kind", "Tool"), (ParityProjection.Id, call), ("status", "in_progress"));
        seen.Add(ParityChannel.Events, "said", ("kind", "Tool"), (ParityProjection.Id, call), ("status", "completed"));
        seen.Add(ParityChannel.Events, "ChildSeen", ("reading", "{\"Child\":\"a1\",\"Run\":\"s1\"}"), (ParityProjection.Process, proc));
        seen.Add(ParityChannel.Stdin, "written", ("text", "the prompt"), (ParityProjection.At, start.AddSeconds(1).ToString("O")));
        List<(string, string?)> failure =
        [
            ("kind", "Limit"), ("phase", "result"), ("access", access), ("hit", hit),
            (ParityProjection.Until, until is { } hours ? start.AddHours(hours).ToString("O") : null), (ParityProjection.By, by),
            ("delivery", delivery),
        ];
        if (clock is not null) failure.Add((ParityProjection.Clock, clock));
        seen.Add(ParityChannel.Failure, "error", [.. failure]);
        seen.Add(ParityChannel.End, "end", ("exit", "1"), ("stopped", "False"));
        return seen;
    }

    private static ParityObservation Daoris() => Scenario("daoris", DaorisStart, "owner:words:limit");

    private static ParityObservation Layer(Func<ParityObservation, ParityObservation>? plant = null)
    {
        var layer = Scenario("layer", LayerStart, "layer:acp.data.errorKind=rate_limit", call: "toolu_9", proc: "440b003a79032646");
        return plant is null ? layer : plant(layer);
    }

    private static IReadOnlyList<string> Found(ParityObservation layer, IReadOnlyList<ParityPair>? expected = null) =>
        ParityOracle.Differences(Daoris(), layer, Relation, expected ?? [Expected]);

    [Fact]
    public void Two_paths_that_differ_only_in_the_projection_are_equal_by_its_correspondences()
    {
        // A renamed id and lineage token, a provenance by a declared pair the scenario expects, and times at the same interval from each
        // run's start.
        ParityOracle.Equal(Daoris(), Layer(), Relation, [Expected]);
    }

    [Fact]
    public void A_library_clock_the_projection_names_holds_its_time_to_its_interval_and_no_further()
    {
        var near = Scenario("layer", LayerStart.AddSeconds(4), Expected.Layer, clock: ParityProjection.ClaudeReaderClock);
        var far = Scenario("layer", LayerStart.AddSeconds(30), Expected.Layer, clock: ParityProjection.ClaudeReaderClock);
        var unnamed = Scenario("layer", LayerStart, Expected.Layer, clock: "lyntai:some-other-clock");

        Assert.Empty(ParityOracle.Differences(Daoris(), Rebased(near), Relation, [Expected]));
        Assert.Contains(ParityOracle.Differences(Daoris(), Rebased(far), Relation, [Expected]), each => each.Contains("until", StringComparison.Ordinal));
        Assert.Contains(ParityOracle.Differences(Daoris(), unnamed, Relation, [Expected]), each => each.Contains("no library clock", StringComparison.Ordinal));
    }

    /// <summary>The layer's library-clock times seen against the run's own start, so only the limit's end drifts.</summary>
    private static ParityObservation Rebased(ParityObservation drifted) =>
        new ParityObservation("layer", LayerStart).With(_ => drifted.Seen.Select(each => each.Channel == ParityChannel.Failure ? each : Shift(each, drifted.Start - LayerStart)));

    private static Observed Shift(Observed each, TimeSpan by) => each with
    {
        Fields = [.. each.Fields.Select(field => field.Name == ParityProjection.At ? (field.Name, (string?)DateTimeOffset.Parse(field.Value!).Add(-by).ToString("O")) : field)],
    };

    public static TheoryData<string, string> Plants => new()
    {
        { "a line the layer's reader drops", "Transcript #2" },
        { "a reading dropped", "Events #2" },
        { "a write reordered", "the writes' order" },
        { "a limit's end lost", "until" },
        { "a delivery read Taken where its prompt's own turn was not heard", "delivery" },
        { "what was hit changed", "hit" },
        { "an access disposition changed", "access" },
        { "one id of one path mapped onto two of the other's", "first appearance" },
        { "a reading its run did not stamp with its process", "first appearance" },
        { "an undeclared pair", "in no pair the relation declares" },
        { "a declared pair the scenario does not expect", "the scenario expects" },
    };

    [Theory]
    [MemberData(nameof(Plants))]
    public void Each_planted_regression_on_the_layer_path_fails_the_oracle(string plant, string named)
    {
        var daoris = plant.StartsWith("a delivery", StringComparison.Ordinal)
            ? Scenario("daoris", DaorisStart, Expected.Daoris, delivery: "NeverSent")
            : Daoris();
        var layer = plant switch
        {
            "a line the layer's reader drops" => Layer(seen => seen.With(all => Without(all, ParityChannel.Transcript, "→ Read"))),
            "a reading dropped" => Layer(seen => seen.With(all => all.Where(each => !(each.Channel == ParityChannel.Events && each["status"] == "in_progress")))),
            "a write reordered" => Layer(seen => seen.With(Reordered)),
            "a limit's end lost" => Scenario("layer", LayerStart, Expected.Layer, call: "toolu_9", until: null),
            "a delivery read Taken where its prompt's own turn was not heard" => Layer(),
            "what was hit changed" => Scenario("layer", LayerStart, Expected.Layer, call: "toolu_9", hit: "the weekly limit"),
            "an access disposition changed" => Scenario("layer", LayerStart, Expected.Layer, call: "toolu_9", access: "Refused"),
            "one id of one path mapped onto two of the other's" => Layer(seen => seen.With(all => SecondCall(all, "toolu_10"))),
            "a reading its run did not stamp with its process" => Scenario("layer", LayerStart, Expected.Layer, call: "toolu_9", proc: null),
            "an undeclared pair" => Scenario("layer", LayerStart, "layer:acp.data.errorKind=overloaded", call: "toolu_9"),
            "a declared pair the scenario does not expect" => Scenario("layer", LayerStart, "layer:acp.words.limit", call: "toolu_9"),
            _ => throw new ArgumentOutOfRangeException(nameof(plant), plant, null),
        };

        var found = ParityOracle.Differences(daoris, layer, Relation, [Expected]);

        Assert.True(found.Any(each => each.Contains(named, StringComparison.Ordinal)), $"{plant}: {string.Join(" | ", found)}");
        Assert.Throws<XunitException>(() => ParityOracle.Equal(daoris, layer, Relation, [Expected]));
    }

    [Fact]
    public void A_scenario_names_the_pair_it_expects_and_each_path_s_source_is_checked_against_it()
    {
        // The Daoris path's own source against the scenario's: a pair the relation declares is not enough.
        var other = new ParityPair("owner:kind:rate_limit", Expected.Layer);

        Assert.Contains(Found(Layer(), [other]), each => each.Contains("not its expected 'owner:kind:rate_limit'", StringComparison.Ordinal));
        Assert.Contains(Found(Layer(), []), each => each.Contains("the scenario expects none", StringComparison.Ordinal));
        Assert.Contains(Found(Layer(), [Expected, Expected]), each => each.Contains("produced none", StringComparison.Ordinal));
    }

    [Fact]
    public void A_held_case_holds_alike_only_where_both_paths_miss_the_same_reader()
    {
        ParityOracle.HeldAlike(new NotAnsweredYet("the door's IProtocolDialect"), new NotAnsweredYet("the door's IProtocolDialect"));

        Assert.Throws<XunitException>(() => ParityOracle.HeldAlike(new NotAnsweredYet("row l's channel"), new NotAnsweredYet("row n's offers")));
        Assert.Throws<XunitException>(() => ParityOracle.HeldAlike(new NotAnsweredYet("row l's channel"), null));
        Assert.Throws<XunitException>(() => ParityOracle.HeldAlike(new NotAnsweredYet("row l's channel"), new InvalidOperationException("other")));
    }

    private static IEnumerable<Observed> Without(IReadOnlyList<Observed> all, ParityChannel channel, string text) =>
        all.Where(each => !(each.Channel == channel && each["text"] == text));

    /// <summary>The second event written before the transcript's line it follows: each channel's own order kept, the writes' order not.</summary>
    private static IEnumerable<Observed> Reordered(IReadOnlyList<Observed> all)
    {
        var list = all.ToList();
        var line = list.FindIndex(each => each.Channel == ParityChannel.Transcript && each["text"] == "→ Read");
        (list[line], list[line - 1]) = (list[line - 1], list[line]);
        return list;
    }

    private static IEnumerable<Observed> SecondCall(IReadOnlyList<Observed> all, string id)
    {
        var last = all.ToList().FindLastIndex(each => each[ParityProjection.Id] is not null);
        return all.Select((each, at) => at == last
            ? each with { Fields = [.. each.Fields.Select(field => field.Name == ParityProjection.Id ? (field.Name, (string?)id) : field)] }
            : each);
    }
}
