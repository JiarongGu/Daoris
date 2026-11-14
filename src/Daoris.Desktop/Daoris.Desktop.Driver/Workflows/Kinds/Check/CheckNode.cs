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

    public override NodeArrivals Arrives { get; } =
        NodeArrivals.Taking(("commit", WireValues.Commit)) with { Outputs = new Dictionary<string, string>(StringComparer.Ordinal) { ["commit"] = WireValues.Commit } };

    /// <summary>The heavy executor's port: its answer asks a pass, which reconciles it.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => wiring.Executor is { } executor
        ? new CheckExecution(executor, (unit, attempt, node) => VerifyRuns.SendOf(wiring.Home, unit, attempt, node) ?? AskRunChecks.SendOf(wiring.Home, unit, attempt, node))
        : null;

    /// <summary>
    /// Handed to the heavy executor, which takes it at once and runs it on its own task: the pass never awaits its gates. With no executor it
    /// stays ready.
    /// </summary>
    public async Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) =>
        !starting.Governed.Dispatchable.Contains(starting.Node.Id) || starting.Performer.PortOf(starting.Node.Kind) is null
            ? null
            : await starting.Performer.StartAsync(starting.Node, starting.Start, ct).ConfigureAwait(false);

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

    /// <summary>The gates a check names (its <c>gates</c> option): null for <c>heavy</c>, else the names it lists.</summary>
    public static IReadOnlyList<string>? GatesOf(NodeDeclaration node)
    {
        if (!node.Options.TryGetValue("gates", out var text)) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String) return root.GetString() == "heavy" ? null : [root.GetString()!];
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
