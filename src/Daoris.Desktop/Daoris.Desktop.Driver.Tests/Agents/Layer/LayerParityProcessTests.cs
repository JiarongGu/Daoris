using Daoris.Driver;
using Lyntai.Agents;
using LayerEnded = Lyntai.Agents.SessionEnded;
using LayerStarted = Lyntai.Agents.SessionStarted;

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
    private static readonly ParityRelation Relation = new(
        [new("unread", "layer:tests:fatal"), new("unread", "layer:tests:broken"), new("unread", "layer:" + LayerTestProvider.RefusedBy)]);

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
            new { t = "text", text = "working" }, new { t = "end", kind = "Fatal", by = "tests:fatal", subtype = Failed, why = "it broke" },
        },
    };

    private static readonly object Unheard = new
    {
        @out = new object[] { new { t = "end", kind = "Fatal", by = "tests:fatal", subtype = Failed, why = "it broke" } },
    };

    private static readonly ParityPair RefusedPair = new("unread", "layer:" + LayerTestProvider.RefusedBy);

    /// <summary>The tool's own message names an unknown model and nothing answers: the door's refusal of the model it was handed (AG12).</summary>
    private static readonly object Refused = new
    {
        @out = new object[]
        {
            new { t = "refused" },
            new { t = "end", kind = "Fatal", by = LayerTestProvider.RefusedBy, restricts = "model", subtype = Failed, why = "it broke" },
        },
    };

    /// <summary>The same refusal after the agent spoke: the turn ran, so the ending's guard holds it back.</summary>
    private static readonly object RefusedAfterWords = new
    {
        @out = new object[]
        {
            new { t = "text", text = "working" }, new { t = "refused" },
            new { t = "end", kind = "Fatal", by = LayerTestProvider.RefusedBy, restricts = "model", subtype = Failed, why = "it broke" },
        },
    };

    private static object[] Spoke(object ctx, object usage, object byModel, params object[] more) =>
    [
        new { t = "text", text = "hello", model = "m1", ctx },
        .. more,
        new { t = "end", subtype = "success", model = "m1", usage, byModel },
    ];

    private static readonly object Counts = new { @in = 100, @out = 5, read = 50, write = 0 };

    /// <summary>The turn's own report: the context its last message held, the tokens by model with the window the harness names (AG33).</summary>
    private static readonly object Reported = new
    {
        @out = Spoke(Counts, Counts, new { m1 = new { @in = 100, @out = 5, read = 50, write = 0, window = 200000 } }),
    };

    /// <summary>The same, with a count written to the cache: the layer's tick carries none, so its context figure is short by it.</summary>
    private static readonly object Written = new
    {
        @out = Spoke(
            new { @in = 100, @out = 5, read = 50, write = 30 }, new { @in = 100, @out = 5, read = 50, write = 30 },
            new { m1 = new { @in = 100, @out = 5, read = 50, write = 30, window = 200000 } }),
    };

    /// <summary>A denial naming a call the record does not hold: said on the console and no card.</summary>
    private static readonly object DeniedUnseen = new
    {
        @out = new object[]
        {
            new { t = "text", text = "hello" }, new { t = "end", subtype = "success", denied = new[] { new { name = "Edit", id = "c9" } } },
        },
    };

    /// <summary>A denial of a call the turn made.</summary>
    private static readonly object DeniedSeen = new
    {
        @out = new object[]
        {
            new { t = "call", name = "lookup", args = "{}", id = "c1" }, new { t = "result", id = "c1", text = "no", error = true },
            new { t = "end", subtype = "success", denied = new[] { new { name = "lookup", id = "c1" } } },
        },
    };

    /// <summary>A status frame carrying an id before the agent's own initialization: it names nothing (AG35).</summary>
    private static readonly object Announced = new
    {
        @out = new object[]
        {
            new { t = "start", id = "status-id", announced = false }, new { t = "start", id = "conv-1" }, new { t = "text", text = "hello" },
            new { t = "end", subtype = "success" },
        },
    };

    /// <summary>A failed turn that showed a meter alone: the layer's own stamp counts it as the prompt taken, Daoris's hearing does not (AG37).</summary>
    private static readonly object Metered = new
    {
        @out = new object[]
        {
            new { t = "end", kind = "Fatal", by = "tests:fatal", subtype = Failed, why = "it broke", usage = Counts },
        },
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

    [Fact]
    public async Task A_turn_s_context_window_and_tokens_by_model_are_observed_alike()
    {
        var (daoris, layer) = await BothAsync(Reported);

        ParityOracle.Equal(daoris, layer, Relation, []);
        Assert.Contains(layer.Seen, each => each.Channel == ParityChannel.Events && each["kind"] == SessionEventKind.Usage && each["used"] == "150" && each["size"] == "200000");
        Assert.Contains(layer.Seen, each => each.Channel == ParityChannel.Events && each["kind"] == SessionEventKind.Turn && each["models"]!.Contains("modelUsage", StringComparison.Ordinal));
        Assert.Contains(layer.Seen, each => each.Channel == ParityChannel.End && each["usage"] == "AcpUsage { Used = 150, Size = 200000 }");
    }

    /// <summary>What the oracle finds today, kept so the move that needs it meets it: a count written to the cache is in Claude's context and not in the layer's tick.</summary>
    [Fact]
    public async Task A_count_written_to_the_cache_is_the_one_difference_in_the_context_the_layer_s_tick_carries()
    {
        var (daoris, layer) = await BothAsync(Written);

        // The record's figure and the ending's high-water mark, the same count short.
        Assert.Collection(
            ParityOracle.Differences(daoris, layer, Relation, []),
            each => Assert.Equal("Events #2: said.used: daoris '180', layer '150'", each),
            each => Assert.Equal(
                "End #1: end.usage: daoris 'AcpUsage { Used = 180, Size = 200000 }', layer 'AcpUsage { Used = 150, Size = 200000 }'", each));
    }

    [Fact]
    public async Task A_denied_call_the_record_holds_is_a_refusal_differing_only_by_the_kind_the_layer_s_call_does_not_carry()
    {
        var (daoris, layer) = await BothAsync(DeniedSeen);

        var found = ParityOracle.Differences(daoris, layer, Relation, []);
        Assert.NotEmpty(found);
        Assert.All(found, each => Assert.Contains("toolKind=", each, StringComparison.Ordinal));
        Assert.Contains(layer.Seen, each => each.Channel == ParityChannel.Events && each["status"] == "refused" && each["toolName"] == "lookup");
    }

    [Fact]
    public async Task A_denial_of_a_call_the_record_does_not_hold_and_an_unannounced_start_are_observed_alike()
    {
        var (daoris, layer) = await BothAsync(DeniedUnseen);
        ParityOracle.Equal(daoris, layer, Relation, []);
        Assert.Contains(layer.Seen, each => each.Channel == ParityChannel.Console && each["text"] == "  permission refused: Edit");

        var (named, started) = await BothAsync(Announced);
        ParityOracle.Equal(named, started, Relation, []);
        Assert.Equal("conv-1", Assert.Single(started.Seen, each => each.Channel == ParityChannel.Conversation)["conversation"]);
    }

    [Fact]
    public async Task A_prompt_s_delivery_is_read_from_what_its_turn_showed_a_meter_alone_being_unheard()
    {
        var (daoris, layer) = await BothAsync(Metered);

        ParityOracle.Equal(daoris, layer, Relation, [Fatal]);
        Assert.Contains(layer.Seen, each => each.Channel == ParityChannel.Failure && each["delivery"] == "NeverSent");
    }

    [Fact]
    public async Task A_refused_model_on_a_turn_that_ran_nothing_is_the_ending_s_refused_model_on_both_paths()
    {
        var (daoris, layer) = await BothAsync(Refused);

        ParityOracle.Equal(daoris, layer, Relation, [RefusedPair]);
        Assert.Contains(layer.Seen, each => each.Channel == ParityChannel.End && each["modelRefused"] == bool.TrueString);
    }

    [Fact]
    public async Task A_refusal_after_the_agent_spoke_is_no_refused_model_on_either_path()
    {
        var (daoris, layer) = await BothAsync(RefusedAfterWords);

        ParityOracle.Equal(daoris, layer, Relation, [RefusedPair]);
        Assert.Contains(layer.Seen, each => each.Channel == ParityChannel.End && each["modelRefused"] == bool.FalseString);
    }

    public static TheoryData<string, string> Plants => new()
    {
        { "a refused model's restriction dropped", "modelRefused" },
        { "a denial dropped", "Console" },
        { "the tokens by model dropped", "Events" },
        { "an unannounced start named", "Conversation" },
        { "a line the layer's reader drops", "Console" },
        { "a write reordered", "Events" },
        { "an undeclared pair", "in no pair the relation declares" },
        { "a declared pair the scenario does not expect", "the scenario expects" },
        { "a delivery read Taken where its prompt's own turn was not heard", "delivery" },
        { "the layer's own stamp read where the turn's evidence is", "delivery" },
    };

    [Theory]
    [MemberData(nameof(Plants))]
    public async Task Each_regression_planted_on_the_layer_path_alone_fails_the_oracle(string plant, string named)
    {
        var (spec, planted) = plant switch
        {
            "a denial dropped" => (DeniedUnseen, Dropping<AgentToolDenied>()),
            "the tokens by model dropped" => (Reported, Dropping<AgentModelUsage>()),
            "an unannounced start named" => (Announced, Announcing()),
            "a refused model's restriction dropped" =>
                (Refused, Ending(failure => new AgentFailure(failure.Kind, failure.By) { Delivery = failure.Delivery })),
            "a line the layer's reader drops" => (Worked, Dropping<TextDelta>()),
            "a write reordered" => (Worked, Swapping<Thinking, TextDelta>()),
            "an undeclared pair" => (Broke, Ending(failure => new AgentFailure(failure.Kind, "tests:unheard-of") { Delivery = failure.Delivery })),
            "a declared pair the scenario does not expect" =>
                (Broke, Ending(failure => new AgentFailure(failure.Kind, "tests:broken") { Delivery = failure.Delivery })),
            "a delivery read Taken where its prompt's own turn was not heard" =>
                (Unheard, Turned(new AgentTurnEvidence(null, [AgentShownKind.Text]))),
            "the layer's own stamp read where the turn's evidence is" => (Metered, Turned(null)),
            _ => throw new ArgumentOutOfRangeException(nameof(plant), plant, null),
        };
        var expected = spec == Refused ? [RefusedPair] : spec == Broke || spec == Unheard || spec == Metered ? [Fatal] : Array.Empty<ParityPair>();

        var (daoris, honest) = await BothAsync(spec);
        var (_, layer) = await BothAsync(spec, planted);

        Assert.Empty(ParityOracle.Differences(daoris, honest, Relation, expected));
        var found = ParityOracle.Differences(daoris, layer, Relation, expected);
        Assert.True(found.Any(each => each.Contains(named, StringComparison.Ordinal)), $"{plant}: {string.Join(" | ", found)}");
    }

    private static Func<IReadOnlyList<AgentStreamEvent>, IEnumerable<AgentStreamEvent>> Dropping<T>() =>
        read => read.Where((each, at) => !(each is T && at == read.ToList().FindIndex(first => first is T)));

    private static Func<IReadOnlyList<AgentStreamEvent>, IEnumerable<AgentStreamEvent>> Turned(AgentTurnEvidence? turn) =>
        read => read.Select(each => each is LayerEnded ended ? ended with { Turn = turn } : each);

    private static Func<IReadOnlyList<AgentStreamEvent>, IEnumerable<AgentStreamEvent>> Announcing() =>
        read => read.Select(each => each is LayerStarted started ? started with { Announced = true } : each);

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
