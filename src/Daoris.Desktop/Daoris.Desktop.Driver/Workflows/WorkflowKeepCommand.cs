namespace Daoris.Driver;

/// <summary>What <c>daoris-driver workflow keep</c> was asked (WORKFLOW1f): the session whose work it keeps, and the person's words.</summary>
public sealed record WorkflowKeepAsk(string Session, string? Words);

/// <summary>
/// <c>daoris-driver workflow keep &lt;session&gt; ["…"]</c> (WORKFLOW1f; the workflow design §4.4, D50): the person's <i>Keep</i>.
/// Where a kind chose a workflow that asks less of them, and the work changed paths outside the kind's, the gate holds it for
/// their say-so; this keeps the kind's workflow for that work, with their words, on its run, and says what the gate says after.
/// </summary>
/// <remarks>
/// <para><b>The person's alone</b>, as the second opinion's <i>Go on anyway…</i> is: no connector tool and no Ask Daoris card
/// reaches it (D110). The screen's press is owed to the editor's row (WORKFLOW1g), and so are §4.4's other two, <i>Follow the
/// other workflow</i> and <i>Choose…</i>, which bind a run again.</para>
///
/// <para><b>Exit codes</b>: 0 kept, 1 refused (no tree of its own here, no quest, nothing of a kind's paths holds it), 2 the usage
/// or a service that did not answer.</para>
/// </remarks>
public static class WorkflowKeepCommand
{
    public const string Usage =
        """
        usage: daoris-driver workflow keep <session> ["…"]
               keep the workflow a kind of task chose for a session's work, where the work changed paths outside the kind's
               and that workflow asks less of you than the one it would follow without the kind: your say-so, with your
               words, kept on its run. It prints what the landing gate says after it.
        """;

    /// <summary>The line read (its words after <c>workflow</c>), or null with why not.</summary>
    public static WorkflowKeepAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is ["keep", var session, ..] && session.Trim().Length > 0 && !session.StartsWith('-'))
        {
            return new WorkflowKeepAsk(session.Trim(), string.Join(" ", args.Skip(2)).Trim() is { Length: > 0 } said ? said : null);
        }

        problem = "`workflow keep` takes a session's id, then your words if you have any: `workflow keep <session> [\"…\"]`.";
        return null;
    }

    /// <summary>
    /// The press over a session's work already found (design §4.4): the gate read as every door reads it, the person's say-so kept on
    /// its run only where a kind's paths hold it, and the gate read again.
    /// </summary>
    /// <param name="door"><c>terminal</c> or <c>screen</c> (<see cref="ReviewDoors"/>).</param>
    public static async Task<(bool Done, string Message)> KeepAsync(
        string home, string session, string tree, string quest, IOpinionWorld world, string? words, string door, DateTimeOffset at,
        CancellationToken ct = default)
    {
        var trees = new SessionTrees(home);
        var gate = await trees.GateAsync(tree, quest, world, session, ct).ConfigureAwait(false);
        if (gate.Workflow is not { State: WorkflowGateStates.KindPaths } held || held.Process.Run is not { } run)
        {
            return (false, $"nothing a kind of task holds waits for you on session {session}'s work, so nothing was kept: "
                + $"{gate.Workflow?.Says ?? "It follows Current: the rules as they stand, read at each gate."}");
        }

        if (WorkflowRunBindings.Keep(home, run, new WorkflowKept(at, door) { Words = words }) is null)
        {
            return (false, $"session {session}'s run binding disappeared or could not be read before your words were saved, so nothing was kept. "
                + held.Says);
        }
        var after = await trees.GateAsync(tree, quest, world, session, ct).ConfigureAwait(false);
        return (true, $"Kept {held.Process.Name} for session {session}'s work. {after.Workflow!.Says}");
    }

    /// <summary>Find the session's work on this machine, press, and say what the gate says after it.</summary>
    public static async Task<int> RunAsync(WorkflowKeepAsk ask, string home, ServiceClient service, TextWriter output, CancellationToken ct = default)
    {
        var (tree, _) = await service.SessionGroundAsync(ask.Session, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tree) || SessionTrees.TreeGone(tree) || !new SessionTrees(home).Holds(tree))
        {
            output.WriteLine($"workflow: session `{ask.Session}` names no working tree of its own on this machine, so nothing of its work is kept here.");
            return 1;
        }

        if (await service.SessionQuestAsync(ask.Session, ct).ConfigureAwait(false) is not { } quest)
        {
            output.WriteLine($"workflow: session `{ask.Session}` serves no quest, so no workflow runs for it.");
            return 1;
        }

        var (done, message) = await KeepAsync(
            home, ask.Session, tree, quest, new ServiceReviewWorld(service), ask.Words, ReviewDoors.Terminal, DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        output.WriteLine($"workflow: {message}");
        return done ? 0 : 1;
    }
}
