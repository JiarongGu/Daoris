using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A node's acceptance: what a reach is bound to,
/// <c>{ unit, subjectRevision, graphDigest, nodeVersion, inputs, prerequisiteReceipts, attempt }</c>, and a run's node its <c>subject</c>,
/// and the evidence it rests on.
/// A changed dependency invalidates it and everything downstream (<see cref="NodeAcceptances.Invalidate"/>).
/// </summary>
/// <param name="Unit">The unit it acts on: its node record's.</param>
/// <param name="SubjectRevision">
/// The revision of the unit it observes it is accepted at (<see cref="Observes"/>): a branch's full tip, which names its repository by the unit.
/// </param>
/// <param name="GraphDigest">
/// The graph that bound the run: on a live graph its node's place (<c>plc-</c>, <see cref="NodeGraphs.Places"/>), so an edit
/// elsewhere leaves it bound; else the whole graph's digest (<c>gph-</c>).
/// </param>
/// <param name="Node">The node's id in that graph.</param>
/// <param name="NodeVersion">The node's declared version.</param>
/// <param name="Attempt">The attempt that reached it.</param>
public sealed record NodeAcceptance(string Unit, string? SubjectRevision, string GraphDigest, string Node, int NodeVersion, int Attempt)
{
    /// <summary>No ports: an acceptance's inputs or outputs before any are named.</summary>
    public static readonly IReadOnlyDictionary<string, NodePort> NoPorts = ImmutableSortedDictionary.Create<string, NodePort>(StringComparer.Ordinal);

    // Each collection is copied when it is set, so an issued id never moves with a caller's dictionary or list.
    private readonly IReadOnlyDictionary<string, NodePort> inputs = NoPorts;
    private readonly IReadOnlyList<string> prerequisiteReceipts = ImmutableArray<string>.Empty;
    private readonly IReadOnlyDictionary<string, NodePort> outputs = NoPorts;

    /// <summary>Its inputs by name: references to upstream outputs, each naming the acceptance that produced it. A copy of what is set.</summary>
    public IReadOnlyDictionary<string, NodePort> Inputs { get => inputs; init => inputs = Frozen(value); }

    /// <summary>The acceptances of the nodes it came after (<see cref="Id"/>s): the receipts its eligibility rested on. A copy of what is set.</summary>
    public IReadOnlyList<string> PrerequisiteReceipts
    {
        get => prerequisiteReceipts;
        init => prerequisiteReceipts = ImmutableArray.CreateRange(value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>
    /// The unit its node observes where that is not <see cref="Unit"/>: a run's node, kept on the
    /// run's record, observes the quest, the branch or the run its kind names. Null where the node observes its record's own unit: it is in
    /// the id only when set, so an absent one keeps its id and reads as the unit.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>The unit it observes: its <see cref="Subject"/> where set, else its <see cref="Unit"/>.</summary>
    public string Observes => Subject ?? Unit;

    /// <summary>The immutable evidence reference it is reached on; not part of the binding.</summary>
    public NodePort? Evidence { get; init; }

    /// <summary>Its declared outputs, each a reference naming this acceptance; not part of the binding. A copy of what is set.</summary>
    public IReadOnlyDictionary<string, NodePort> Outputs { get => outputs; init => outputs = Frozen(value); }

    /// <summary>
    /// The outcome a decision is reached with, one of its node's <see cref="NodeDeclaration.Outcomes"/>; null on any other reach.
    /// Not part of the binding, as the evidence is not: a start's binding is the one its reach names whichever outcome it chose.
    /// </summary>
    public string? Outcome { get; init; }

    /// <summary>
    /// Whether <paramref name="other"/> is the same result: the same binding, and the same evidence, outputs and outcome, which the id leaves out.
    /// </summary>
    public bool SameResult(NodeAcceptance other) =>
        Id == other.Id && Equals(Evidence, other.Evidence) && string.Equals(Outcome, other.Outcome, StringComparison.Ordinal)
        && Outputs.Count == other.Outputs.Count
        && Outputs.All(output => other.Outputs.TryGetValue(output.Key, out var port) && Equals(port, output.Value));

    private static IReadOnlyDictionary<string, NodePort> Frozen(IReadOnlyDictionary<string, NodePort> ports) =>
        (ports ?? throw new ArgumentNullException(nameof(ports))).ToImmutableSortedDictionary(StringComparer.Ordinal);

    /// <summary>The binding's digest: the same binding is the same acceptance, so a receipt names it and a replay records it once.</summary>
    public string Id => "acc-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical())))[..32];

    private string Canonical()
    {
        var text = new StringBuilder();
        void Line(string? value) => text.Append(value?.Replace("\\", "\\\\").Replace("\n", "\\n") ?? "\\0").Append('\n');
        Line(Unit);
        Line(SubjectRevision);
        Line(GraphDigest);
        Line(Node);
        Line(NodeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Line(Attempt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var (name, port) in Inputs.OrderBy(input => input.Key, StringComparer.Ordinal))
        {
            Line("input");
            Line(name);
            Line(port.Schema);
            Line(port.Ref);
            Line(port.Revision);
            Line(port.Acceptance);
        }

        foreach (var receipt in PrerequisiteReceipts.Order(StringComparer.Ordinal))
        {
            Line("after");
            Line(receipt);
        }

        // Last and marked, only when set, so an acceptance naming none keeps its id.
        if (Subject is not null)
        {
            Line("subject");
            Line(Subject);
        }

        return text.ToString();
    }
}

/// <summary>Why an acceptance stops binding (<see cref="NodeInvalidation.Because"/>).</summary>
public static class NodeLapse
{
    /// <summary>The unit's revision moved: a new tip, or one not read.</summary>
    public const string Revision = "revision";

    /// <summary>
    /// The unit the node observes moved: a run's chain went on to its next quest, or its next session
    /// to another branch, at the same revision or not. Judged before the revision.
    /// </summary>
    public const string Subject = "subject";

    /// <summary>The graph that binds the run changed: on a live graph, the node's place in it.</summary>
    public const string Graph = "graph";

    /// <summary>The node's declared version changed.</summary>
    public const string Version = "version";

    /// <summary>An acceptance it rests on, by a prerequisite or an input, is invalidated.</summary>
    public const string Upstream = "upstream";

    /// <summary>
    /// A reached decision's outcome does not read as one of its node's: a writer rewrote the record without it, so the
    /// decision is judged again rather than routed on a guess.
    /// </summary>
    public const string Outcome = "outcome";
}

/// <summary>One acceptance invalidated: why, and, where it was carried from upstream, the acceptance it rests on.</summary>
public sealed record NodeInvalidation(string Acceptance, string Because, string? Upstream);

/// <summary>The results a record writer keeps after one more (<see cref="NodeAcceptances.Keep"/>), or as they stand with the refusal's code.</summary>
public sealed record NodeResults(IReadOnlyList<NodeAcceptance> Kept, string? Refused);

/// <summary>
/// The acceptances' binding and its transitive invalidation: a changed dependency invalidates every acceptance downstream,
/// transitively. Pure: nothing is removed. Answers and resource receipts stay accountable; an invalidation never reverses
/// an external effect and never reopens a closed quest by itself.
/// </summary>
public static class NodeAcceptances
{
    /// <summary>Why <paramref name="acceptance"/> does not bind against what is current for its unit and node, or null while it does.</summary>
    public static string? Lapsed(NodeAcceptance acceptance, string? revision, string graphDigest, int nodeVersion) =>
        !string.Equals(acceptance.SubjectRevision, revision, StringComparison.Ordinal) ? NodeLapse.Revision
        : !string.Equals(acceptance.GraphDigest, graphDigest, StringComparison.Ordinal) ? NodeLapse.Graph
        : acceptance.NodeVersion != nodeVersion ? NodeLapse.Version
        : null;

    /// <summary>
    /// Why <paramref name="acceptance"/> does not bind against the subject its node observes now and that subject's revision:
    /// another subject first, whatever the revision, so a branch that replaces another at the same commit is new work; then as <see cref="Lapsed(NodeAcceptance, string?, string, int)"/>.
    /// </summary>
    public static string? Lapsed(NodeAcceptance acceptance, string subject, string? revision, string graphDigest, int nodeVersion) =>
        !string.Equals(acceptance.Observes, subject, StringComparison.Ordinal) ? NodeLapse.Subject : Lapsed(acceptance, revision, graphDigest, nodeVersion);

    /// <summary>
    /// The roots a unit's new revision makes: every acceptance observing that unit bound to another revision, a run's node whose subject it is
    /// included: a run's roots name the subject, never the run's record.
    /// </summary>
    public static Func<NodeAcceptance, string?> Moved(string unit, string? revision) => acceptance =>
        acceptance.Observes == unit && !string.Equals(acceptance.SubjectRevision, revision, StringComparison.Ordinal) ? NodeLapse.Revision : null;

    /// <summary>
    /// Every acceptance <paramref name="root"/> lapses, in the order given, then every one resting on an invalidated one, by a
    /// prerequisite receipt or an input's acceptance, however far and on whatever unit, breadth first. Each is named once, with the first
    /// reason found.
    /// </summary>
    public static IReadOnlyList<NodeInvalidation> Invalidate(IReadOnlyList<NodeAcceptance> acceptances, Func<NodeAcceptance, string?> root)
    {
        var found = new List<NodeInvalidation>();
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var acceptance in acceptances)
        {
            if (root(acceptance) is { } because && named.Add(acceptance.Id)) found.Add(new NodeInvalidation(acceptance.Id, because, null));
        }

        for (var next = 0; next < found.Count; next++)
        {
            var upstream = found[next].Acceptance;
            // A route rests on its decision's outcome receipt, never on the acceptance's id alone.
            var routes = acceptances.Where(each => each.Id == upstream && each.Outcome is not null)
                .Select(each => WireEvaluation.OutcomeReceipt(upstream, each.Outcome!)).ToHashSet(StringComparer.Ordinal);
            foreach (var acceptance in acceptances)
            {
                if (named.Contains(acceptance.Id) || !RestsOn(acceptance, upstream, routes)) continue;
                named.Add(acceptance.Id);
                found.Add(new NodeInvalidation(acceptance.Id, NodeLapse.Upstream, upstream));
            }
        }

        return found;
    }

    /// <summary>
    /// One more result, as a record writer keeps it: one immutable result per binding. A binding with none takes it; the
    /// same result again is a replay, kept once; a different one for a binding that has one is refused (<see cref="NodeRefusal.OtherResult"/>),
    /// never overwritten, because the evidence and the outputs are not in the id. Pure: what is kept is returned, and stored by its writer.
    /// </summary>
    public static NodeResults Keep(IReadOnlyList<NodeAcceptance> kept, NodeAcceptance result)
    {
        var held = kept.FirstOrDefault(each => each.Id == result.Id);
        return held is null ? new([.. kept, result], null)
            : held.SameResult(result) ? new(kept, null)
            : new(kept, NodeRefusal.OtherResult);
    }

    private static bool RestsOn(NodeAcceptance acceptance, string upstream, HashSet<string> routes) =>
        acceptance.PrerequisiteReceipts.Any(receipt => string.Equals(receipt, upstream, StringComparison.Ordinal) || routes.Contains(receipt))
        || acceptance.Inputs.Values.Any(port => string.Equals(port.Acceptance, upstream, StringComparison.Ordinal));
}
