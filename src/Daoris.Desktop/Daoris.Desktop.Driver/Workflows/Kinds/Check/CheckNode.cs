using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The declared gates Daoris runs itself: performed with no agent session, bound by its
/// place, and never skipped by its kind, since a skip would read as a verdict a stage and the line rest on.
/// </summary>
[NodeKind(NodeKinds.Check, NodeGroup.Checks)]
internal sealed class CheckNode : BuiltInNode, IJournalled
{
    /// <summary>Reconciled: its answer is the executor's, read from the attempt's folder; never retried by itself.</summary>
    private static readonly NodePerform Perform = new(Idempotent: false, Reconcile: true, Transient: []);

    /// <summary>Reached and failed by the gates' exits; the person's doors on it are a new attempt and a stop.</summary>
    public override NodeAuthority Row { get; } = new(NodeKinds.Check, NodeReach.Performed, NodeSource.Gates, NodeSource.Gates, false, false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubjects.Placed(node, afterHanded);

    public override IReadOnlyList<NodeOptionField> Options { get; } = [NodeOptionFields.Names("gates")];

    public override string? Title => "The checks";

    public override NodeLook Look => NodeLook.Check;

    public override IReadOnlyList<string> Waits => [CheckSlotWaits.HeldBy, CheckSlotWaits.Queued];

    public override NodeArrivals Arrives { get; } =
        NodeArrivals.Taking(("commit", WireValues.Commit)) with { Outputs = new Dictionary<string, string>(StringComparer.Ordinal) { ["commit"] = WireValues.Commit } };

    /// <summary>The heavy executor's port: its answer asks a pass, which reconciles it.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => wiring.Executor is { } executor
        ? new CheckExecution(
            executor, (unit, attempt, node) => VerifyRuns.SendOf(wiring.Home, unit, attempt, node) ?? RunChecks.SendOf(wiring.Home, unit, attempt, node),
            wiring.Plans is null ? null : async (send, ct) =>
            {
                // A one-scope ask's end: the full set once, and its two reviews told where they are asked.
                var end = await ScopeLanding.EndAsync(wiring.Home, send, wiring.Plans(), wiring.Reviewers?.Invoke(send.Repository) ?? [], ct).ConfigureAwait(false);
                if (end.Reviews.Count > 0) wiring.ScopeReviews?.Invoke(send.Run, end.Reviews);
                return end.Send;
            })
        : null;

    /// <summary>
    /// Handed to the heavy executor, which takes it at once and runs it on its own task: the pass never awaits its gates. Where it is not sent,
    /// has no executor, or failed at its commit and the work has not answered that red, it stays ready, and says why once (<see cref="CheckStartWaits"/>).
    /// </summary>
    public async Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct)
    {
        var wait = !starting.Governed.Dispatchable.Contains(starting.Node.Id) ? CheckStartWaits.NotSent
            : starting.Performer.PortOf(starting.Node.Kind) is null ? CheckStartWaits.NoDoor
            : await AwaitsAnswerAsync(starting, ct).ConfigureAwait(false);
        var (home, unit, node) = (starting.Home, starting.Governed.Unit.Id, starting.Node.Id);
        if (wait is not null)
        {
            if (NodeWaits.Changed(home, unit, node, wait))
            {
                starting.Performer.Doors.Say(CheckStartWaits.Event, CheckStartWaits.Sentence(starting.Governed.Run, node, wait));
            }

            return null;
        }

        NodeWaits.Tried(home, unit, node);
        return await starting.Performer.StartAsync(starting.Node, starting.Start, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Why a red at the commit it binds holds it: none where the person asked it, it holds no red there, or the run's work answered that red
    /// (<see cref="CheckRounds.Answered"/>); else the work may still move the commit, or the sessions did not read.
    /// </summary>
    private static async Task<string?> AwaitsAnswerAsync(NodeStarting starting, CancellationToken ct)
    {
        if (starting.Status.By == NodeCause.Person || starting.Start.Move.Binding is not { } binding
            || CheckRounds.RedAt(starting.Performer.RecordOf(binding.Unit), starting.Node.Id, binding.SubjectRevision) is not { } red)
        {
            return null;
        }

        if (starting.Governed.Scope is not { } scope || starting.Sessions is null) return CheckStartWaits.RedHere;
        return await starting.Sessions(ct).ConfigureAwait(false) is not { } records ? CheckStartWaits.SessionsUnread
            : CheckRounds.Answered(records, starting.Governed.Run, scope.Sessions, red.RecordedAt) ? null
            : CheckStartWaits.RedHere;
    }

    /// <summary>Its kind's perform, and 7200 seconds where it declares no <c>timeoutSeconds</c>.</summary>
    public NodeDeclaration Performed(NodeDeclaration node) =>
        node with { Perform = Perform, TimeoutSeconds = node.TimeoutSeconds ?? (int)HeavyExecutor.DefaultBound.TotalSeconds };

    /// <summary>Whether the check runs the gates its batch's changed paths reach (its <c>reach</c> option is <c>paths</c>); else every gate it names runs.</summary>
    public static bool ReachesByPaths(NodeDeclaration node)
    {
        if (!node.Options.TryGetValue("reach", out var text)) return false;
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement is { ValueKind: JsonValueKind.String } word && word.GetString() == "paths";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Whether the check's <c>gates</c> is the word <c>reached</c> alone: the hand-back, then the required commands its change reaches. A gate of that name is asked in a list, <c>["reached"]</c>.</summary>
    public static bool ReachedChoice(NodeDeclaration node)
    {
        if (!node.Options.TryGetValue("gates", out var text)) return false;
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement is { ValueKind: JsonValueKind.String } word && word.GetString() == Reached;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private const string Reached = "reached";

    /// <summary>Whether the check's <c>gates</c> is the word <c>heavy</c>: the one way a step asks the heavy tier; any other step runs the fast one.</summary>
    public static bool NamesHeavy(NodeDeclaration node)
    {
        if (!node.Options.TryGetValue("gates", out var text)) return false;
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement is { ValueKind: JsonValueKind.String } word && word.GetString() == "heavy";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>The gates a check names (its <c>gates</c> option): null for <c>heavy</c> or <c>reached</c>, else the names it lists.</summary>
    public static IReadOnlyList<string>? GatesOf(NodeDeclaration node)
    {
        if (!node.Options.TryGetValue("gates", out var text)) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String) return root.GetString() is "heavy" or Reached ? null : [root.GetString()!];
            return root.ValueKind == JsonValueKind.Array
                ? [.. root.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
