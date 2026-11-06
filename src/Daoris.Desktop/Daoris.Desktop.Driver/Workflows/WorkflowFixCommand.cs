namespace Daoris.Driver;

/// <summary>What <c>daoris workflow fix</c> was asked (FABRIC-PIPELINE1): the verification run, and the check node whose failure to ask a fix of.</summary>
/// <param name="Run">The run, as <c>workflow run</c> prints it.</param>
/// <param name="Node">The check node's id; null for the run's own check.</param>
public sealed record WorkflowFixAsk(string Run, string? Node);

/// <summary>
/// <c>daoris workflow fix &lt;run&gt; [&lt;node&gt;]</c> (FABRIC-PIPELINE1; the pipeline design §4.5): the terminal's twin of the window's
/// <i>Ask for a fix…</i> on a failed verification, the same door (<see cref="VerifyFixAsks"/>), reached through <c>WORK_HELD</c> with the
/// word <see cref="HeldResolutions.AskFix"/>, whose route answers this verb's own line (<c>said</c>).
/// </summary>
/// <remarks>
/// <para><b>The person's press</b>: where Daoris runs, it is sent to it and confirmed in its window; alone, it is refused from a session's
/// terminal (D194 point 5) and otherwise asks the service itself. It writes nothing on a node record: a repeat finds the ask made.</para>
/// <para>Exit codes are the family's: 0 an ask stands for the failure · 1 refused (not a failed verification, a session's terminal, the
/// person's no in the window) · 2 the usage, or a Daoris or service that did not answer.</para>
/// </remarks>
public static class WorkflowFixCommand
{
    public const string Usage =
        """
        usage: daoris workflow fix <run> [<node>]
               ask for a fix of a verification run's failed gate, as the window's Ask for a fix… does: one ask for the gate
               with its log, and a repeat finds the ask already made. `daoris work held` names the failed verifications.
        """;

    /// <summary>Whether the words after <c>workflow</c> ask for a fix, rightly or not.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["fix", ..];

    /// <summary>The words after <c>workflow</c> read, or null with what is wrong with them.</summary>
    public static WorkflowFixAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is ["fix", var run, ..] && args.Count <= 3 && run.TrimStart('#').Trim().Length > 0 && !run.StartsWith('-')
            && (args.Count == 2 || (args[2].Trim().Length > 0 && !args[2].StartsWith('-'))))
        {
            return new WorkflowFixAsk(run.Trim().TrimStart('#'), args.Count == 3 ? args[2].Trim() : null);
        }

        problem = "`workflow fix` names a verification run and, if it has more than one check, the node: `workflow fix <run> [<node>]`.";
        return null;
    }

    /// <summary>
    /// The held attempt the ask means: the run's unit, the node (the run's check where none is named) and that node's newest attempt, 0 where it
    /// has none, which the door refuses in its words.
    /// </summary>
    public static (string Unit, string Node, int Attempt) Resolve(NodeRecordFiles records, WorkflowFixAsk ask)
    {
        var unit = RunUnits.IdOf(ask.Run);
        var attempts = records.Read(unit)?.Attempts ?? [];
        var node = ask.Node ?? attempts.FirstOrDefault(each => each.Plugin == NodeKinds.Check)?.Node ?? "";
        return (unit, node, attempts.Where(each => each.Node == node).Select(each => each.Attempt).DefaultIfEmpty(0).Max());
    }

    /// <summary>The door's answer in the verb's line.</summary>
    public static string Said(VerifyFixAsks.Pressed pressed) => $"workflow: {pressed.Message}";

    /// <summary>The ask for the attempt, said on <paramref name="output"/>: what the route and the alone path both run.</summary>
    public static async Task<int> AskAsync(
        ServiceClient service, NodeRecordFiles records, string unit, string node, int attempt, TextWriter output, CancellationToken ct)
    {
        var pressed = await VerifyFixAsks.AskAsync(service, records, unit, node, attempt, ct).ConfigureAwait(false);
        output.WriteLine(Said(pressed));
        return pressed.Done ? 0 : 1;
    }

    /// <summary>
    /// The verb, as the running Daoris's client where <paramref name="instance"/> names one to look for: the person's press, sent and confirmed
    /// in its window, its lines the route's (<c>said</c>); else alone, refused from a session's terminal.
    /// </summary>
    /// <param name="service">The service the alone path asks; made only when it asks.</param>
    /// <param name="environment">The variables the alone path reads: the instance world's where none is handed, else the process's.</param>
    public static Task<int> RunAsync(
        WorkflowFixAsk ask, NodeRecordFiles records, Func<ServiceClient> service, TextWriter output, InstanceWorld? instance,
        Func<string, string?>? environment = null, CancellationToken ct = default)
    {
        var variables = environment ?? instance?.Environment ?? Environment.GetEnvironmentVariable;
        var (unit, node, attempt) = Resolve(records, ask);
        var payload = InstanceDoor.Said(("unit", unit), ("node", node), ("attempt", attempt), ("resolution", HeldResolutions.AskFix));
        return InstanceDoor.ThroughAsync(instance, PersonPresses.WorkHeld, payload, async () =>
        {
            if (variables(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
            {
                output.WriteLine($"workflow: {DriverChoicesCommand.SessionsTerminal}");
                return 1;
            }

            return await AskAsync(service(), records, unit, node, attempt, output, ct).ConfigureAwait(false);
        }, output, ct);
    }
}
