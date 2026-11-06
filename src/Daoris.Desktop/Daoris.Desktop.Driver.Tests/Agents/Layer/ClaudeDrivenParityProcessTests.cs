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
    /// What the harness's <c>init</c> says it loaded (its tools, servers, skills and plugins), today's <c>Loaded</c> reading: the layer's
    /// start carries the conversation alone, so this waits on a request to Lyntai.
    /// </summary>
    private static readonly ParityGap Loaded = new(
        "the init's reading of what the harness loaded, which the layer's start does not carry",
        seen => seen.Where(each => each.Channel == ParityChannel.Events && each.What == nameof(WireReading.Loaded)),
        ParityGap.None);

    /// <summary>
    /// An answer's words: the layer's door spawns with <c>--include-partial-messages</c> and reads an answer's text from its deltas alone,
    /// and these streams were recorded without them, so the layer shows none. Closed by a driven stream recorded with partial messages.
    /// </summary>
    private static readonly ParityGap Answer = new(
        "an answer's words over a stream recorded without the partial messages the layer's door asks for",
        seen =>
        {
            var answers = seen.Where(each => each.Channel == ParityChannel.Events && each["kind"] == SessionEventKind.Message).ToList();
            var words = answers.Select(each => each["text"]).ToHashSet(StringComparer.Ordinal);
            return answers.Concat(seen.Where(each => each.Channel is ParityChannel.Console or ParityChannel.Transcript && words.Contains(each["text"])));
        },
        ParityGap.None);

    /// <summary>
    /// A stream that ends on a clean exit with no result (these are cut from longer recordings): today's reads no failure, the layer's a
    /// failed turn with no terminal, by its CLI turn's rule (Lyntai's D249).
    /// </summary>
    private static readonly ParityGap Unended = new(
        "a clean exit with no result, no failure today and a failed turn on the layer",
        seen => seen.Where(each => each.Channel == ParityChannel.End),
        seen => seen.Where(each => each.Channel == ParityChannel.End
            || (each.Channel == ParityChannel.Failure && each[ParityProjection.By] == "layer:no-terminal-event")
            || (each.Channel is ParityChannel.Console or ParityChannel.Transcript && each["text"]?.EndsWith("(no terminal result)", StringComparison.Ordinal) == true)
            || (each.Channel == ParityChannel.Events && each["kind"] == SessionEventKind.Turn && each["stopReason"] == "error")));

    /// <summary>The differences the oracle finds on each recorded stream, every one a named gap.</summary>
    private static IReadOnlyList<ParityGap> Gaps(string fixture) => fixture switch
    {
        "native/claude-background-cut.stream.jsonl" => [Loaded, Answer],
        "lineage/claude-family.stream.jsonl" or "lineage/claude-kill.stream.jsonl" => [Unended],
        _ => [],
    };

    [Theory]
    [MemberData(nameof(Recorded))]
    public async Task Each_recorded_driven_stream_is_read_alike_on_both_paths_but_for_its_named_gaps(string fixture)
    {
        var (daoris, layer) = await BothAsync(fixture);

        var (rest, theirs) = ParityGap.Apart(daoris, layer, Gaps(fixture));
        ParityOracle.Equal(rest, theirs, Relation, []);
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
        return (await path.RunAsync(command, Prompt, Start, _bound.Token), await LayerAsync(Shim(command, name)));
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

    /// <summary>The layer's Claude door spawns one program with its own arguments: a shim running the scenario's command before them.</summary>
    private string Shim(IReadOnlyList<string> command, string name)
    {
        var quoted = string.Join(' ', command.Select(each => $"\"{each}\""));
        if (OperatingSystem.IsWindows())
        {
            var cmd = Path.Combine(_scratch, name + ".cmd");
            File.WriteAllText(cmd, $"@{quoted} %*\r\n");
            return cmd;
        }

        var sh = Path.Combine(_scratch, name + ".sh");
        File.WriteAllText(sh, $"#!/bin/sh\nexec {quoted} \"$@\"\n");
        File.SetUnixFileMode(sh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return sh;
    }
}
