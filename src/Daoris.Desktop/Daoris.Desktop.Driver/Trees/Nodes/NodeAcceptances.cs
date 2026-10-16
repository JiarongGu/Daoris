using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A node's acceptance (FABRIC-NODE1; the fabric events design §E2, generalizing §D4's evidence binding): what a reach is bound to,
/// <c>{ unit, subjectRevision, graphDigest, nodeVersion, inputs, prerequisiteReceipts, attempt }</c>, and the evidence it rests on.
/// A changed dependency invalidates it and everything downstream (<see cref="NodeAcceptances.Invalidate"/>).
/// </summary>
/// <param name="Unit">The unit it acts on.</param>
/// <param name="SubjectRevision">The unit's revision it was accepted at: a branch's full tip, which names its repository by the unit.</param>
/// <param name="GraphDigest">The graph that bound the run.</param>
/// <param name="Node">The node's id in that graph.</param>
/// <param name="NodeVersion">The node's declared version.</param>
/// <param name="Attempt">The attempt that reached it.</param>
public sealed record NodeAcceptance(string Unit, string? SubjectRevision, string GraphDigest, string Node, int NodeVersion, int Attempt)
{
    /// <summary>Its inputs by name: references to upstream outputs, each naming the acceptance that produced it.</summary>
    public IReadOnlyDictionary<string, NodePort> Inputs { get; init; } = new Dictionary<string, NodePort>();

    /// <summary>The acceptances of the nodes it came after (<see cref="Id"/>s): the receipts its eligibility rested on.</summary>
    public IReadOnlyList<string> PrerequisiteReceipts { get; init; } = [];

    /// <summary>The immutable evidence reference it was reached on; not part of the binding.</summary>
    public NodePort? Evidence { get; init; }

    /// <summary>Its declared outputs, each a reference naming this acceptance; not part of the binding.</summary>
    public IReadOnlyDictionary<string, NodePort> Outputs { get; init; } = new Dictionary<string, NodePort>();

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

        return text.ToString();
    }
}

/// <summary>Why an acceptance no longer binds (<see cref="NodeInvalidation.Because"/>).</summary>
public static class NodeLapse
{
    /// <summary>The unit's revision moved: a new tip, or one no longer read.</summary>
    public const string Revision = "revision";

    /// <summary>The graph that binds the run changed.</summary>
    public const string Graph = "graph";

    /// <summary>The node's declared version changed.</summary>
    public const string Version = "version";

    /// <summary>An acceptance it rests on, by a prerequisite or an input, was invalidated.</summary>
    public const string Upstream = "upstream";
}

/// <summary>One acceptance invalidated: why, and, where it was carried from upstream, the acceptance it rested on.</summary>
public sealed record NodeInvalidation(string Acceptance, string Because, string? Upstream);

/// <summary>
/// The acceptances' binding and its transitive invalidation (§E2: <i>a changed dependency invalidates every acceptance downstream,
/// transitively</i>). Pure: nothing is removed. Old answers and resource receipts stay accountable (§D3); an invalidation never reverses
/// an external effect and never reopens a closed quest by itself.
/// </summary>
public static class NodeAcceptances
{
    /// <summary>Why <paramref name="acceptance"/> no longer binds against what is current for its unit and node, or null while it does.</summary>
    public static string? Lapsed(NodeAcceptance acceptance, string? revision, string graphDigest, int nodeVersion) =>
        !string.Equals(acceptance.SubjectRevision, revision, StringComparison.Ordinal) ? NodeLapse.Revision
        : !string.Equals(acceptance.GraphDigest, graphDigest, StringComparison.Ordinal) ? NodeLapse.Graph
        : acceptance.NodeVersion != nodeVersion ? NodeLapse.Version
        : null;

    /// <summary>The roots a unit's new revision makes: every acceptance on that unit bound to another revision.</summary>
    public static Func<NodeAcceptance, string?> Moved(string unit, string? revision) => acceptance =>
        acceptance.Unit == unit && !string.Equals(acceptance.SubjectRevision, revision, StringComparison.Ordinal) ? NodeLapse.Revision : null;

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
            foreach (var acceptance in acceptances)
            {
                if (named.Contains(acceptance.Id) || !RestsOn(acceptance, upstream)) continue;
                named.Add(acceptance.Id);
                found.Add(new NodeInvalidation(acceptance.Id, NodeLapse.Upstream, upstream));
            }
        }

        return found;
    }

    private static bool RestsOn(NodeAcceptance acceptance, string upstream) =>
        acceptance.PrerequisiteReceipts.Contains(upstream, StringComparer.Ordinal)
        || acceptance.Inputs.Values.Any(port => string.Equals(port.Acceptance, upstream, StringComparison.Ordinal));
}
