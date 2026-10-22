namespace Daoris.Driver;

/// <summary>What <c>daoris-driver workflow skip|stop</c> was asked (FABRIC-CUTOVER1f): the act, the run, its node and the person's words.</summary>
/// <param name="Act">One of <see cref="NodeWordActs"/>.</param>
/// <param name="Run">The run, as its binding names it and <c>workflow run</c> prints it.</param>
/// <param name="Node">The node's id in the run's graph.</param>
/// <param name="Words">Every word after the node's, joined as said; null where none.</param>
public sealed record WorkflowNodeAsk(string Act, string Run, string Node, string? Words);

/// <summary>
/// <c>daoris-driver workflow skip &lt;run&gt; &lt;node&gt; "…"</c> and <c>workflow stop &lt;run&gt; &lt;node&gt; ["…"]</c> (FABRIC-CUTOVER1f; D190
/// point 6, the cutover design §5.2, D50): the terminal's twin of the run's view's presses, the person's word on a node of a run bound to a graph
/// version, kept on the run's record by <see cref="RunNodeWords.Word"/>, which the page's <c>WORKFLOW_NODE</c> calls too, and applied by
/// Daoris's next look.
/// </summary>
/// <remarks>
/// <para><b>The person's press</b> (RPC1e, D156 §4.2): where Daoris runs, it is sent to it and confirmed in its window, the route answering
/// this verb's own lines; alone, it is refused from a session's terminal (D194 point 5), and otherwise keeps the word under the home's lease,
/// which the host holds for it (<see cref="VerbLeases"/>).</para>
/// <para>Exit codes are the family's: 0 kept · 1 refused (a word the run or the table does not take, a session's terminal, the person's no in
/// the window) · 2 the usage, or a Daoris that did not answer.</para>
/// </remarks>
public static class WorkflowNodeCommand
{
    public const string Usage = RunNodeWords.Usage;

    /// <summary>Whether the words after <c>workflow</c> ask for a skip or a stop, rightly or not.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is [NodeWordActs.Skip or NodeWordActs.Stop, ..];

    /// <summary>The words after <c>workflow</c> read, or null with what is wrong with them.</summary>
    public static WorkflowNodeAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is [var act, var run, var node, ..] && Asks(args) && run.TrimStart('#').Trim().Length > 0 && node.Trim().Length > 0)
        {
            var words = string.Join(' ', args.Skip(3)).Trim();
            return new WorkflowNodeAsk(act, run.Trim().TrimStart('#'), node.Trim(), words.Length > 0 ? words : null);
        }

        problem = "`workflow skip` and `workflow stop` name a run and one of its nodes: `workflow skip <run> <node> \"…\"`.";
        return null;
    }

    /// <summary>
    /// The verb, as the running Daoris's client where <paramref name="instance"/> names one to look for: the person's press, sent and confirmed
    /// in its window, its lines the route's (<c>said</c>); else alone, on <paramref name="records"/>, refused from a session's terminal.
    /// </summary>
    /// <param name="environment">The variables the alone path reads: the instance world's where none is handed, else the process's.</param>
    public static Task<int> RunAsync(
        WorkflowNodeAsk ask, NodeRecordFiles records, TextWriter output, InstanceWorld? instance, Func<string, string?>? environment = null,
        Func<DateTimeOffset>? clock = null, CancellationToken ct = default)
    {
        var variables = environment ?? instance?.Environment ?? Environment.GetEnvironmentVariable;
        var payload = InstanceDoor.Said(("run", ask.Run), ("node", ask.Node), ("act", ask.Act), ("words", ask.Words));
        return InstanceDoor.ThroughAsync(instance, PersonPresses.WorkflowNode, payload, () => Task.FromResult(Alone(ask, records, output, variables, clock)), output, ct);
    }

    /// <summary>The press acting in this process: refused from a session's terminal; else the door's own path, its sentence printed.</summary>
    private static int Alone(WorkflowNodeAsk ask, NodeRecordFiles records, TextWriter output, Func<string, string?> environment, Func<DateTimeOffset>? clock)
    {
        if (environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
        {
            output.WriteLine($"workflow: {DriverChoicesCommand.SessionsTerminal}");
            return 1;
        }

        var done = RunNodeWords.Word(records, ask.Run, ask.Node, ask.Act, ask.Words, clock);
        output.WriteLine(RunNodeWords.Said(done));
        return done.Done ? 0 : 1;
    }
}
