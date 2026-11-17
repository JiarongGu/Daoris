namespace Daoris.Driver;

/// <summary>What <c>daoris workflow take|hand|give-back</c> asks: the act, the run, its step, the holder handed to and the reason.</summary>
public sealed record WorkflowHoldAsk(string Act, string Run, string Node, string? Holder, string? Why);

/// <summary>
/// <c>daoris workflow take &lt;run&gt; &lt;node&gt; ["…"]</c>, <c>workflow hand &lt;run&gt; &lt;node&gt; &lt;holder&gt; ["…"]</c> and <c>workflow give-back
/// &lt;run&gt; &lt;node&gt; ["…"]</c>: the terminal's twin of a step's <i>Take</i>, <i>Hand to…</i> and <i>Give back</i> (<see cref="StepHolding"/>),
/// which the page's <c>WORKFLOW_HOLD</c> makes too. Where Daoris runs it is sent to it and confirmed in its window; alone it is refused from a
/// session's terminal, and otherwise written under the home's lease. Exit codes: 0 handed · 1 refused · 2 the usage.
/// </summary>
public static class WorkflowHoldCommand
{
    public const string Usage = "usage: daoris workflow take <run> <node> [\"why\"] | hand <run> <node> person|agent|agent:<name> [\"why\"] | give-back <run> <node> [\"why\"]";

    /// <summary>Whether the words after <c>workflow</c> ask for an act on a step's holder, rightly or not.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is [var act, ..] && CastActs.OnStep.Contains(act, StringComparer.Ordinal);

    /// <summary>The words after <c>workflow</c> read, or null with what is wrong with them.</summary>
    public static WorkflowHoldAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is not [var act, var run, var node, ..] || !Asks(args) || run.TrimStart('#').Trim().Length == 0 || node.Trim().Length == 0)
        {
            problem = "`workflow take`, `hand` and `give-back` name a run and one of its steps: `workflow take <run> <node> \"…\"`.";
            return null;
        }

        var rest = args.Skip(3).ToList();
        string? holder = null;
        if (act == CastActs.Hand)
        {
            if (rest.Count == 0 || !CastHolders.Is(rest[0].Trim()))
            {
                problem = "`workflow hand` names whom it hands the step to: `person`, `agent` or `agent:<name>`.";
                return null;
            }

            holder = rest[0].Trim();
            rest.RemoveAt(0);
        }

        var why = string.Join(' ', rest).Trim();
        return new WorkflowHoldAsk(act, run.Trim().TrimStart('#'), node.Trim(), holder, why.Length > 0 ? why : null);
    }

    /// <summary>The verb, as the running Daoris's client where <paramref name="instance"/> names one; else alone, on <paramref name="records"/>.</summary>
    public static Task<int> RunAsync(
        WorkflowHoldAsk ask, NodeRecordFiles records, Func<PlaceRiskBook> risk, Func<DateTimeOffset> clock, TextWriter output, InstanceWorld? instance,
        Func<string, string?>? environment = null, CancellationToken ct = default)
    {
        var variables = environment ?? instance?.Environment ?? Environment.GetEnvironmentVariable;
        var payload = InstanceDoor.Said(("run", ask.Run), ("node", ask.Node), ("act", ask.Act), ("holder", ask.Holder), ("why", ask.Why));
        return InstanceDoor.ThroughAsync(instance, PersonPresses.WorkflowHold, payload, () => Task.FromResult(Alone(ask, records, risk, output, variables, clock)), output, ct);
    }

    private static int Alone(WorkflowHoldAsk ask, NodeRecordFiles records, Func<PlaceRiskBook> risk, TextWriter output, Func<string, string?> environment, Func<DateTimeOffset> clock)
    {
        if (environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
        {
            output.WriteLine($"workflow: {DriverChoicesCommand.SessionsTerminal}");
            return 1;
        }

        var held = AgentSettles.Hold(records, new PlaceCastFiles(records.Home, records.HoldsLease), risk(), ask.Run, ask.Node, ask.Act, ask.Holder, ask.Why, clock());
        output.WriteLine(AgentSettles.Said(held));
        return held.Done ? 0 : 1;
    }
}
