using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CUTOVER1f (D190 point 6, the cutover design §5.2; D194 point 5): <c>daoris-driver workflow skip|stop &lt;run&gt; &lt;node&gt;</c>, the
/// terminal's twin of the run's view's presses: its words read, its alone path keeping the word on the run's record as the door does, and
/// refused from a session's terminal before anything is read. Each seen failing first.
/// </summary>
public sealed class WorkflowNodeCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-node-words-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private const string GoAheadBeforeHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"go","kind":"go-ahead","version":1,"reach":"pressed","options":{"act":"deploy the release"}},
                  {"id":"handed","kind":"handed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>g","kind":"after","from":"claimed","to":"go"},{"id":"g>h","kind":"after","from":"go","to":"handed"}]}
        """;

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private NodeRecordFiles Files => new(_home, () => true);

    private static readonly Func<string, string?> NoVariables = _ => null;

    [Fact]
    public void The_words_name_the_act_the_run_the_node_and_the_persons_words()
    {
        var skip = WorkflowNodeCommand.Read(["skip", "#q7", "go", "nobody", "raises", "it"], out var none);
        Assert.Null(none);
        Assert.Equal(new WorkflowNodeAsk(NodeWordActs.Skip, "q7", "go", "nobody raises it"), skip);
        Assert.Equal(new WorkflowNodeAsk(NodeWordActs.Stop, "q7", "go", null), WorkflowNodeCommand.Read(["stop", "q7", "go"], out _));

        Assert.Null(WorkflowNodeCommand.Read(["skip", "q7"], out var problem));
        Assert.Equal("`workflow skip` and `workflow stop` name a run and one of its nodes: `workflow skip <run> <node> \"…\"`.", problem);
        Assert.True(WorkflowNodeCommand.Asks(["stop", "q7"]));
        Assert.False(WorkflowNodeCommand.Asks(["run", "--quest", "q7"]));
        Assert.Contains("\n  workflow skip <run> <node> \"…\"\n  workflow stop <run> <node> [\"…\"]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
    }

    /// <summary>Alone, the word is kept on the run's record by the door's own path, its sentence printed; a refusal is its sentence and exit 1.</summary>
    [Fact]
    public async Task Alone_the_word_is_kept_on_the_runs_record_and_a_refusal_is_said()
    {
        RunHome.Bind(_home, "q7", RunHome.SaveGraph(_home, GoAheadBeforeHanded), Now);
        var output = new StringWriter();

        var kept = await WorkflowNodeCommand.RunAsync(new(NodeWordActs.Skip, "q7", "go", "nobody raises it"), Files, output, instance: null, NoVariables, () => Now);

        Assert.Equal(0, kept);
        Assert.Equal("workflow: your skip of `go` on run q7 is kept; Daoris's next look applies it.\n", output.ToString().ReplaceLineEndings("\n"));
        Assert.Equal("nobody raises it", Assert.Single(Files.Read(RunUnits.IdOf("q7"))!.Pending).Words);

        output = new StringWriter();
        var refused = await WorkflowNodeCommand.RunAsync(new(NodeWordActs.Skip, "q7", "go", null), Files, output, instance: null, NoVariables, () => Now);
        Assert.Equal(1, refused);
        Assert.StartsWith("workflow: `go` on run q7 already has your word", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>D194 point 5: a person's press acting alone from a session's terminal is refused before anything is read or kept.</summary>
    [Fact]
    public async Task Alone_from_a_sessions_terminal_it_is_refused_and_nothing_is_kept()
    {
        RunHome.Bind(_home, "q7", RunHome.SaveGraph(_home, GoAheadBeforeHanded), Now);
        var output = new StringWriter();

        var exit = await WorkflowNodeCommand.RunAsync(
            new(NodeWordActs.Stop, "q7", "go", null), Files, output, instance: null,
            name => name == DriverChoicesCommand.SessionVariable ? "s-agent" : null, () => Now);

        Assert.Equal(1, exit);
        Assert.Equal($"workflow: {DriverChoicesCommand.SessionsTerminal}\n", output.ToString().ReplaceLineEndings("\n"));
        Assert.False(File.Exists(NodeRecordFiles.PathOf(_home, RunUnits.IdOf("q7"))));
    }
}
