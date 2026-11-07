using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The oracle over Claude Code's recorded driven streams: each recorded stream under the
/// suite's <c>fixtures/</c> said by one program, read on the Daoris path (Claude Code's shipped door, its dialect and tables, on
/// <see cref="TransportRun"/>) and on the layer path (<see cref="LayerRun"/> over the driven binding's registration, Lyntai's Claude door,
/// held as a driven start holds it), each handed the same session, and compared completely: every difference is a named gap below or none.
/// Real node processes.
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

    private static IEnumerable<Observed> Messages(IReadOnlyList<Observed> seen) =>
        seen.Where(each => each.Channel == ParityChannel.Events && each["kind"] == SessionEventKind.Message);

    /// <summary>
    /// The differences the oracle finds on each recorded stream, every one a named gap: none, since Lyntai's <c>TextDelta</c> carries the
    /// message's id the layer reads onto its words, as today's reader keeps it.
    /// </summary>
    private static IReadOnlyList<ParityGap> Gaps(string fixture) => [];

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

    /// <summary>A subagent's lineage, as the stream says its start and end, is the same lines on both paths.</summary>
    [Fact]
    public async Task A_family_s_lineage_is_written_alike_on_both_paths()
    {
        var (daoris, layer) = await BothAsync("lineage/claude-family.stream.jsonl");

        foreach (var seen in new[] { daoris, layer })
        {
            var lineage = seen.Seen.Where(each => each.Channel == ParityChannel.Lineage).ToList();
            Assert.Equal(6, lineage.Count);
            Assert.Contains(lineage, each => each["event"] == "started" && each["agent"] == "af7e45b4d6f2967a9" && each["parent"] == "a9b1456decf181940");
            Assert.All(lineage, each => Assert.NotNull(each[ParityProjection.Process]));
        }
    }

    /// <summary>
    /// A subagent's words and calls ride the session's stdout marked by the call that spawned it (the console3 evidence): on both paths they
    /// are said on its own stream, never on the session's console or in its record, and a backgrounded command is a stream of its own.
    /// </summary>
    [Fact]
    public async Task A_child_s_words_and_calls_are_said_on_its_own_stream_alike_on_both_paths()
    {
        var (daoris, layer) = await BothAsync("claude-child-words", ChildWords);

        ParityOracle.Equal(daoris, layer, Relation, []);
        foreach (var seen in new[] { daoris, layer })
        {
            var streams = seen.Seen.Where(each => each.Channel == ParityChannel.Streams).Select(each => each["reading"]!).ToList();
            Assert.Contains(streams, each => each.Contains("subagent/a1") && each.Contains("# console3 room"));
            Assert.Contains(streams, each => each.Contains("subagent/a1") && each.Contains("README.md"));
            Assert.Contains(streams, each => each.Contains("task/b1"));
            Assert.DoesNotContain(seen.Seen, each => each.Channel is ParityChannel.Console or ParityChannel.Events && each["text"] == "# console3 room");
        }
    }

    /// <summary>
    /// A command its update ended after the turn, whose notification never came (a process Daoris ended at its turn's end before the harness
    /// wrote it): both paths end it at the stream's close in the update's word, and the run's ending cuts it.
    /// </summary>
    [Fact]
    public async Task A_command_its_update_ended_with_no_notification_after_is_read_alike_on_both_paths()
    {
        var (daoris, layer) = await BothAsync("claude-update-cut", UpdateCut);

        ParityOracle.Equal(daoris, layer, Relation, []);
        Assert.Contains(daoris.Seen, each => each is { Channel: ParityChannel.End } && each["cut"] == "npm run gates");
        Assert.Contains(layer.Seen, each => each is { Channel: ParityChannel.End } && each["cut"] == "npm run gates");
    }

    /// <summary>A backgrounded command whose result names its output file: the file's lines are on the command's stream on both paths.</summary>
    [Fact]
    public async Task A_running_command_s_output_file_is_followed_alike_on_both_paths()
    {
        var file = Path.Combine(_scratch, "b1.output");
        await File.WriteAllTextAsync(file, "listening on 4200\nready\n", _bound.Token);
        var lines = OutputFile(file);
        // The follower's first read runs beside the stream, so the turn's result waits for it: the file's lines then come in the same place on both paths.
        var (daoris, layer) = await BothAsync("claude-output-file", lines[..^1], lines[^1..]);

        ParityOracle.Equal(daoris, layer, Relation, []);
        foreach (var seen in new[] { daoris, layer })
        {
            var streams = seen.Seen.Where(each => each.Channel == ParityChannel.Streams).Select(each => each["reading"]!).ToList();
            Assert.Contains(streams, each => each.Contains("task/b1") && each.Contains("listening on 4200"));
            Assert.Contains(streams, each => each.Contains("task/b1") && each.Contains("ready"));
        }
    }

    /// <summary>A command backgrounded and its result naming the file its output is written to; the turn ends with the command running.</summary>
    private static string[] OutputFile(string file) =>
    [
        Said("init", new { session_id = "c0ffee00-1111-2222-3333-444455556668", tools = new[] { "Bash" } }),
        Frame(new
        {
            type = "assistant",
            message = new
            {
                id = "msg_dev", role = "assistant",
                content = new object[] { new { type = "tool_use", id = "toolu_bg", name = "Bash", input = new { command = "npm run dev", run_in_background = true, description = "dev server" } } },
            },
        }),
        Said("task_started", new { task_id = "b1", tool_use_id = "toolu_bg", description = "dev server", is_backgrounded = true, task_type = "local_bash" }),
        Frame(new
        {
            type = "user",
            message = new
            {
                role = "user",
                content = new object[]
                {
                    new
                    {
                        tool_use_id = "toolu_bg", type = "tool_result",
                        content = $"Command running in background with ID: b1. Output is being written to: {file}. You will be notified when it completes. To check interim output, use Read on that file path.",
                    },
                },
            },
            tool_use_result = new { stdout = "", stderr = "", backgroundTaskId = "b1" },
        }),
        Frame(new { type = "result", subtype = "success", is_error = false, result = "The server is running.", session_id = "c0ffee00-1111-2222-3333-444455556668" }),
    ];

    private static string Frame(object value) => System.Text.Json.JsonSerializer.Serialize(value);

    private static string Said(string subtype, object fields)
    {
        var node = System.Text.Json.JsonSerializer.SerializeToNode(fields)!.AsObject();
        node["type"] = "system";
        node["subtype"] = subtype;
        return node.ToJsonString();
    }

    /// <summary>A session that backgrounds a command and spawns a subagent, whose words and call the stream marks as its own.</summary>
    private static readonly string[] ChildWords =
    [
        Said("init", new { session_id = "c0ffee00-1111-2222-3333-444455556666", tools = new[] { "Bash", "Read", "Agent" } }),
        Said("task_started", new { task_id = "b1", tool_use_id = "toolu_bg", description = "dev server", is_backgrounded = true, task_type = "local_bash" }),
        Said("task_started", new { task_id = "a1", tool_use_id = "toolu_ag", description = "Read README first line", task_type = "local_agent", spawn_depth = 1, subagent_type = "general-purpose" }),
        Frame(new
        {
            type = "assistant", parent_tool_use_id = "toolu_ag",
            message = new
            {
                id = "msg_child", role = "assistant",
                content = new object[]
                {
                    new { type = "tool_use", id = "toolu_read", name = "Read", input = new { file_path = "README.md" } },
                },
            },
        }),
        Frame(new
        {
            type = "user", parent_tool_use_id = "toolu_ag",
            message = new { role = "user", content = new object[] { new { tool_use_id = "toolu_read", type = "tool_result", content = "# console3 room" } } },
        }),
        Frame(new
        {
            type = "assistant", parent_tool_use_id = "toolu_ag",
            message = new { id = "msg_child_2", role = "assistant", content = new object[] { new { type = "text", text = "# console3 room" } } },
        }),
        Said("task_notification", new { task_id = "a1", tool_use_id = "toolu_ag", status = "completed", summary = "# console3 room" }),
        Frame(new
        {
            type = "assistant",
            message = new { id = "msg_own", role = "assistant", content = new object[] { new { type = "text", text = "The first line is the room's name." } } },
        }),
        Frame(new { type = "result", subtype = "success", is_error = false, result = "The first line is the room's name.", session_id = "c0ffee00-1111-2222-3333-444455556666" }),
        Said("task_notification", new { task_id = "b1", tool_use_id = "toolu_bg", status = "stopped" }),
    ];

    /// <summary>A command backgrounded, the turn's result, and the command's update ending it: no notification follows.</summary>
    private static readonly string[] UpdateCut =
    [
        Said("init", new { session_id = "c0ffee00-1111-2222-3333-444455556667", tools = new[] { "Bash" } }),
        Frame(new
        {
            type = "assistant",
            message = new
            {
                id = "msg_gates", role = "assistant",
                content = new object[] { new { type = "tool_use", id = "toolu_gates", name = "Bash", input = new { command = "npm run gates", run_in_background = true, description = "npm run gates" } } },
            },
        }),
        Said("task_started", new { task_id = "b1gates", tool_use_id = "toolu_gates", description = "npm run gates", is_backgrounded = true, task_type = "local_bash" }),
        Frame(new { type = "result", subtype = "success", is_error = false, result = "The gates are running.", session_id = "c0ffee00-1111-2222-3333-444455556667" }),
        Said("task_updated", new { task_id = "b1gates", patch = new { status = "killed" } }),
    ];

    private Task<(ParityObservation Daoris, ParityObservation Layer)> BothAsync(string fixture) => BothAsync(
        Path.GetFileNameWithoutExtension(fixture), [.. File.ReadAllLines(Path.Combine(Fixtures, fixture)).Where(each => each.Length > 0)]);

    private async Task<(ParityObservation Daoris, ParityObservation Layer)> BothAsync(string name, string[] lines, string[]? after = null)
    {
        var command = LayerTestProvider.Scenario(_scratch, new { @out = lines, wait = after is null ? 0 : 1500, after = after ?? [] }, name);
        using var daoris = new ParityDaorisPath(Path.Combine(_scratch, "daoris-" + name), ParityDaorisPath.Shipped(ClaudeDrivenLayer.Door));
        using var layer = new ParityLayerPath(Path.Combine(_scratch, "layer-" + name));
        var program = LayerTestProvider.Shim(_scratch, command, name);
        var start = new LayerStart(ClaudeDrivenLayer.Door, Prompt, Path.Combine(_scratch, Path.GetFileNameWithoutExtension(program) + ".log"), TimeSpan.FromHours(1));
        return (
            await daoris.RunAsync(command, Prompt, Start, _bound.Token),
            await layer.RunAsync(ClaudeDrivenLayer.Registration(program), start, Start, _bound.Token));
    }
}
