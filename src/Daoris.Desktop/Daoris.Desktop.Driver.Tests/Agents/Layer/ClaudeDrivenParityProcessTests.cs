using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The oracle over Claude Code's recorded driven streams (AGENTLYNTAI1d, the agent-lyntai design §6.3): each recorded stream under the
/// suite's <c>fixtures/</c> said by one program, read on the Daoris path (Claude Code's shipped door, its dialect and tables, on
/// <see cref="TransportRun"/>) and on the layer path (<see cref="LayerRun"/> over the driven binding's registration, Lyntai's Claude door),
/// and compared completely: every difference is a named gap below or none. Real node processes.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ClaudeDrivenParityProcessTests : IDisposable
{
    private const string Prompt = "the prompt";

    private static readonly DateTimeOffset Start = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private static readonly string Fixtures = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures");

    /// <summary>Claude Code's door relates no entry yet: a pair is declared where a recorded failure meets one.</summary>
    private static readonly ParityRelation Relation = new([]);

    private readonly string _scratch = ConformanceScratch.New("claude-driven-parity");

    private readonly CancellationTokenSource _bound = new(Poll.Patience);

    public void Dispose()
    {
        _bound.Dispose();
        ConformanceScratch.Remove(_scratch);
    }

    /// <summary>Every recorded Claude Code stream, by its path under <c>fixtures/</c>.</summary>
    public static TheoryData<string> Recorded =>
    [
        .. Directory.EnumerateFiles(Fixtures, "claude-*.stream.jsonl", SearchOption.AllDirectories)
            .Select(each => Path.GetRelativePath(Fixtures, each).Replace('\\', '/'))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// A message's id: today's reader keeps the message's id on its words, a delta's and a whole message's alike, and the layer's words
    /// carry none (Lyntai's <c>TextDelta</c> names no message), so this waits on a request to Lyntai. The words themselves are alike
    /// (<see cref="An_answer_s_words_are_said_alike_on_both_paths"/>).
    /// </summary>
    private static readonly ParityGap MessageId = new(
        "a message's id on its words, which the layer's words do not carry",
        Messages,
        Messages);

    private static IEnumerable<Observed> Messages(IReadOnlyList<Observed> seen) =>
        seen.Where(each => each.Channel == ParityChannel.Events && each["kind"] == SessionEventKind.Message);

    /// <summary>The differences the oracle finds on each recorded stream, every one a named gap.</summary>
    private static IReadOnlyList<ParityGap> Gaps(string fixture) =>
        fixture == "native/claude-background-cut.stream.jsonl" ? [MessageId] : [];

    /// <summary>
    /// Each stream is read alike but for its named gaps: what the init says the harness loaded (Lyntai's <c>AgentLoaded</c>), an answer's
    /// words no delta streamed (a whole message's words yielded once), and a clean exit with no result, which says nothing and fails nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Recorded))]
    public async Task Each_recorded_driven_stream_is_read_alike_on_both_paths_but_for_its_named_gaps(string fixture)
    {
        var (daoris, layer) = await BothAsync(fixture);

        var (rest, theirs) = ParityGap.Apart(daoris, layer, Gaps(fixture));
        ParityOracle.Equal(rest, theirs, Relation, []);
    }

    [Fact]
    public async Task An_answer_s_words_are_said_alike_on_both_paths()
    {
        var (daoris, layer) = await BothAsync("native/claude-background-cut.stream.jsonl");

        Assert.Equal(Messages(daoris.Seen).Select(each => each["text"]), Messages(layer.Seen).Select(each => each["text"]));
        Assert.NotEmpty(Messages(layer.Seen));
    }

    /// <summary>A tool step's title and kind, which 1b-i's oracle found apart, are read alike through the door's own tool vocabulary.</summary>
    [Fact]
    public async Task A_tool_step_is_shown_by_its_title_and_kind_on_both_paths()
    {
        var (daoris, layer) = await BothAsync("native/claude-background-cut.stream.jsonl");

        foreach (var seen in new[] { daoris, layer })
        {
            Assert.Contains(seen.Seen, each => each.Channel == ParityChannel.Events && each["title"] == "git fetch" && each["toolKind"] == "execute");
            Assert.Contains(seen.Seen, each => each.Channel == ParityChannel.Console && each["text"] == "  ✓ npm run gates");
        }
    }

    [Fact]
    public void Every_recorded_stream_is_read_by_the_oracle()
    {
        Assert.Equal(3, Recorded.Count);
    }

    private async Task<(ParityObservation Daoris, ParityObservation Layer)> BothAsync(string fixture)
    {
        var lines = File.ReadAllLines(Path.Combine(Fixtures, fixture)).Where(each => each.Length > 0).ToArray();
        var name = Path.GetFileNameWithoutExtension(fixture);
        var command = LayerTestProvider.Scenario(_scratch, new { @out = lines }, name);
        using var path = new ParityDaorisPath(Path.Combine(_scratch, "daoris-" + name), ParityDaorisPath.Shipped(ClaudeDrivenLayer.Door));
        return (await path.RunAsync(command, Prompt, Start, _bound.Token), await LayerAsync(LayerTestProvider.Shim(_scratch, command, name)));
    }

    private async Task<ParityObservation> LayerAsync(string program)
    {
        var transcript = Path.Combine(_scratch, Path.GetFileNameWithoutExtension(program) + ".log");
        var seen = new ParityObservation("layer", Start);
        await using (var run = new LayerRun(
            ClaudeDrivenLayer.Registration(program),
            new LayerStart(ClaudeDrivenLayer.Door, Prompt, transcript, TimeSpan.FromHours(1)),
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
}
