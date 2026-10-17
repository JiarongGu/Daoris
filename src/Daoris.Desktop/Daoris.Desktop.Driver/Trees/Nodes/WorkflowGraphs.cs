using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A D157 version read as a graph (FABRIC-WORKFLOW2): its number, its identity (<see cref="WorkflowNamed.Digest"/>, unchanged), and its
/// graph with the digest a run on it binds; or, with no graph, why it is unreadable.
/// </summary>
/// <param name="GraphDigest">What a run on the decoded graph binds (<see cref="WireRun.GraphDigest"/>): derived from the identity and the decoder's version.</param>
/// <param name="Unreadable">The reason it does not decode, said whole: the version's own problem, or its graph's first.</param>
public sealed record DecodedVersion(int Version, string? Identity, NodeGraph? Graph, string? GraphDigest, string? Unreadable)
{
    public bool Readable => Graph is not null;
}

/// <summary>
/// D157's named versions decoded as graphs, never rewritten (FABRIC-WORKFLOW2; the fabric events design §E7's migration contract, §E4).
/// Pure: it reads a version as <see cref="WorkflowNamed"/> read it and writes nothing, so a stored file and a run's binding stay byte for
/// byte. Step order is <c>after</c> wires; a group is a synthetic <c>all</c> join with the group's id, its members wired into it and it
/// into the next step; <c>failed: stop</c> is a <c>failed</c> wire to a synthetic <c>stop</c>, <c>wait</c> one to a synthetic person's
/// task, and <c>send-back</c> the adapter's one return, then the person's task; a person's skip is the node's, a group's its members'.
/// </summary>
/// <remarks>
/// A TWIN with the CLI's <c>workflowgraphs.ts</c>: both read the CLI's <c>test/fixtures/workflow-graphs.json</c>, row for row, every graph
/// and digest included (<c>WorkflowGraphsTests</c>, <c>workflowgraphs.test.ts</c>). The choices §E7 left open are D173's FABRIC-WORKFLOW2
/// note's.
/// </remarks>
public static class WorkflowGraphs
{
    /// <summary>The decoder's own version: a decoded graph's digest is derived from it, so a changed mapping binds afresh.</summary>
    public const int DecoderVersion = 1;

    /// <summary>The kind a check decodes to after its plugin: the plugin point <c>work/checks</c>, its slash a dot, which no stage's id holds.</summary>
    public const string ChecksNode = "work.checks";

    /// <summary>
    /// A stored version decoded (§E7): one that does not read in this build (<see cref="NamedVersion.Problem"/>, a kind it does not run
    /// included) is unreadable with that reason, kept as it is; one that reads is decoded whole, or is unreadable with its graph's problem.
    /// </summary>
    public static DecodedVersion Decode(NamedVersion version) =>
        version.Graph is { } graph && version.Digest is { } digest
            // A graph version (schema 2, FABRIC-GRAPH1b) is a graph already, judged where it was read: its identity is its own digest,
            // which is what a run on it binds, so nothing derives from a decoder.
            ? new DecodedVersion(version.Version, digest, graph, digest, null)
            : version.Steps is null || version.Problem is not null
                ? new DecodedVersion(version.Version, null, null, null, version.Problem ?? $"v{version.Version}: it does not read.")
                : DecodeSteps(version.Version, version.Steps);

    /// <summary>
    /// Steps decoded as a version numbered <paramref name="version"/>: the graph and its digest where the graph holds
    /// (<see cref="NodeGraphs.Problems"/>), else unreadable with the first problem, never in part.
    /// </summary>
    public static DecodedVersion DecodeSteps(int version, IReadOnlyList<NamedStep> steps)
    {
        var identity = WorkflowNamed.Digest(steps);
        var graph = Graph(steps);
        return NodeGraphs.Problems(graph) is [var first, ..]
            ? new DecodedVersion(version, null, null, null, $"v{version}: its graph does not hold: {first.Words}")
            : new DecodedVersion(version, identity, graph, GraphDigest(identity, graph), null);
    }

    /// <summary>
    /// The graph of <paramref name="steps"/> by §E7's table, as <see cref="WorkflowNamed.ReadShape"/> reads them, every kind included: the
    /// first step its entry, the last its end (a group's join where a group is last), the cleanup at <i>held</i>. Nodes in step order, each
    /// followed by its outcome's synthetic node; wires in the order they are made, each named <c>from&gt;to</c>.
    /// </summary>
    public static NodeGraph Graph(IReadOnlyList<NamedStep> steps)
    {
        var nodes = new List<NodeDeclaration>();
        var wires = new List<Wire>();
        void Wire(string kind, string from, string to) => wires.Add(new Wire($"{from}>{to}", kind, new WireEnd(from), new WireEnd(to)));

        // A step's outcome path (§E7): `stop` a sink, `wait` the person's task, and `send-back` the adapter's one return, then, as D157
        // §3.7 says after it, the person's task. A go-ahead's refusal takes its `refused` the same way, on its `rejected` wire.
        void Outcome(NamedStep step, string wire, string? policy)
        {
            if (policy is null) return;
            var id = $"{step.Id}:{wire}";
            var kind = policy == "stop" ? NodeKinds.Stop : NodeKinds.Human;
            nodes.Add(new NodeDeclaration(id, kind, 1, NodeAuthorities.BuiltIn[kind].Reach)
            {
                Synthetic = true,
                Options = new Dictionary<string, string> { ["outcome"] = WorkflowCurrent.Quoted(wire), ["step"] = WorkflowCurrent.Quoted(step.Id) },
            });
            Wire(wire, step.Id, id);
        }

        string? previous = null;
        foreach (var step in steps)
        {
            if (step.Kind == WorkflowKinds.All)
            {
                // A member's own `failed` is kept as written and takes no path: a member failed is the group failed (D157 §3.3).
                foreach (var member in step.Members)
                {
                    nodes.Add(Node(member));
                    if (previous is not null) Wire(WireKinds.After, previous, member.Id);
                }

                nodes.Add(Node(step) with { Synthetic = true });
                foreach (var member in step.Members) Wire(WireKinds.After, member.Id, step.Id);
            }
            else
            {
                nodes.Add(Node(step));
                if (previous is not null) Wire(WireKinds.After, previous, step.Id);
            }

            switch (step.Kind)
            {
                case WorkflowKinds.Check or WorkflowKinds.Stage or WorkflowKinds.All:
                    Outcome(step, WireKinds.Failed, (string?)step.Field("failed"));
                    break;
                case WorkflowKinds.GoAhead:
                    Outcome(step, WireKinds.Rejected, (string?)step.Field("refused"));
                    break;
            }

            previous = step.Id;
        }

        return new NodeGraph(nodes, wires, steps.Count > 0 ? [steps[0].Id] : [], previous ?? "");
    }

    /// <summary>A step as its node (§E4): its kind's node, its reach, and every field it takes but a group's members as an option, as written.</summary>
    private static NodeDeclaration Node(NamedStep step)
    {
        var plugin = step.Field("plugin") as string;
        var kind = step.Kind switch
        {
            WorkflowKinds.Work => NodeKinds.Claimed,
            WorkflowKinds.Opinion => NodeKinds.Opinion,
            WorkflowKinds.Look => NodeKinds.Look,
            WorkflowKinds.Landing => NodeKinds.Handed,
            WorkflowKinds.PullRequest => NodeKinds.PullRequest,
            WorkflowKinds.GoAhead => NodeKinds.GoAhead,
            WorkflowKinds.Check => $"{plugin}/{ChecksNode}",
            WorkflowKinds.Stage => $"{plugin}/{step.Field("stage")}",
            WorkflowKinds.All => NodeKinds.All,
            _ => throw new ArgumentException($"`{step.Kind}` is no kind of step."),
        };
        var reach = NodeAuthorities.BuiltIn.TryGetValue(kind, out var builtIn) ? builtIn.Reach
            : step.Kind == WorkflowKinds.Stage ? NodeReach.Performed : NodeReach.Observed;
        return new NodeDeclaration(step.Id, kind, 1, reach)
        {
            Options = step.Fields.Where(field => field.Value is not IReadOnlyList<NamedStep>).ToDictionary(field => field.Name, field => OptionText(field.Value)),
        };
    }

    /// <summary>A field's value as an option's JSON text, the CLI's <c>optionText</c>: text quoted as both twins quote it, a switch, a list, or null.</summary>
    private static string OptionText(object? value) => value switch
    {
        null => "null",
        bool flag => flag ? "true" : "false",
        string text => WorkflowCurrent.Quoted(text),
        IEnumerable<string> list => $"[{string.Join(',', list.Select(WorkflowCurrent.Quoted))}]",
        _ => throw new ArgumentException($"a field is text, a switch or a list of names, not {value.GetType().Name}."),
    };

    /// <summary>
    /// A decoded graph's digest (§E7: derived from the stored version and the decoder's version): <c>gph-</c> and 32 hex of a SHA-256 over
    /// the decoder's version, the version's identity and the graph's own <see cref="NodeGraphs.Digest"/>, so neither moves without it.
    /// </summary>
    public static string GraphDigest(string identity, NodeGraph graph) => GraphDigest(identity, graph, DecoderVersion);

    /// <summary>
    /// The digest a decoder numbered <paramref name="decoder"/> would derive (FABRIC-WORKFLOW2b): what a changed mapping binds afresh, while
    /// the version's identity and its stored bytes stay as they are.
    /// </summary>
    internal static string GraphDigest(string identity, NodeGraph graph, int decoder) =>
        "gph-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"workflow-graph\n{decoder}\n{identity}\n{NodeGraphs.Digest(graph)}\n")))[..32];

    /// <summary>
    /// The nodes a person's skip of <paramref name="step"/> moves (§E7's last row): a group's members, since its join is never skipped
    /// (NODE1c's choice 3), else the step's own node; none for a step the version does not hold.
    /// </summary>
    public static IReadOnlyList<string> Skipped(IReadOnlyList<NamedStep> steps, string step) =>
        steps.FirstOrDefault(each => each.Id == step) is { } found
            ? found.Kind == WorkflowKinds.All ? [.. found.Members.Select(member => member.Id)] : [found.Id]
            : [];

    /// <summary>
    /// The decoded version a run is bound to (§E7, <i>one governs a run, by its binding</i>; D157 §5.1): null under Current. Its identity
    /// is the binding's digest, unchanged; a file that does not read, a version it no longer holds, or one whose identity is not the
    /// binding's reads as unreadable, and the run cannot start on it (D157 §5.2).
    /// </summary>
    public static DecodedVersion? ForRun(NamedWorkflowRead workflow, WorkflowRunBinding binding)
    {
        if (binding.IsCurrent) return null;
        var number = binding.Version ?? 0;
        DecodedVersion Unread(string why) => new(number, null, null, null, why);
        if (binding.Version is null) return Unread(binding.Problem ?? $"the run `{binding.Run}` is bound to no version of `{binding.Workflow}`.");
        if (workflow.Problem is { } problem) return Unread(problem);
        if (workflow.Versions.FirstOrDefault(each => each.Version == number) is not { } stored)
        {
            return Unread($"the run `{binding.Run}` is bound to v{number}, which the workflow's file no longer holds.");
        }

        var decoded = Decode(stored);
        return decoded.Readable && decoded.Identity != binding.Digest
            ? Unread($"the run `{binding.Run}` is bound to v{number} at `{binding.Digest}`, and the file's v{number} reads as `{decoded.Identity}`: a version is "
                + "never edited, so its file is not the one the run started on.")
            : decoded;
    }
}
