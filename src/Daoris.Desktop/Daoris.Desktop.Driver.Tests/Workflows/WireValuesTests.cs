using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.WireEvaluationTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The values a graph's wires carry (<see cref="WireValues"/>): a node fed one binds to it (<see cref="RunSubject.Input"/>), so a moved
/// input rebinds only what it feeds; a skipped producer skips its readers; and a value's producer, its <c>onto</c> and its stop are kept
/// on its port.
/// </summary>
public sealed class WireValuesTests
{
    private const string Run = "run:r";
    private const string Work = "unit-b1";

    private static NodeDeclaration Producer(string id, string output, string schema) => Node(id) with { Outputs = Plugs(output, schema) };

    private static NodeDeclaration Reader(string id, string input, string schema) => Node(id) with { Inputs = Plugs(input, schema) };

    private static readonly NodeDeclaration[] Nodes =
    [
        Producer("claim", "work", WireValues.Work),
        Producer("one", "moved", WireValues.Ref),
        Producer("two", "moved", WireValues.Ref),
        Reader("build", "work", WireValues.Work),
        Reader("push-one", "moved", WireValues.Ref),
        Reader("push-two", "moved", WireValues.Ref),
    ];

    private static readonly Wire[] Wires =
    [
        Data("w-work", "claim", "work", "build", "work"),
        Data("w-one", "one", "moved", "push-one", "moved"),
        Data("w-two", "two", "moved", "push-two", "moved"),
    ];

    /// <summary>The run as the pass hands it: each node fed a value on its subject and revision, the work being the branch at its tip.</summary>
    private static WireRun Pass(Graph graph, string tip, string producers = "p-1")
    {
        var subjects = new Dictionary<string, string?>(StringComparer.Ordinal) { ["one"] = "unit-one" };
        var revisions = new Dictionary<string, string?>(StringComparer.Ordinal) { ["one"] = producers };
        foreach (var node in Nodes.Where(node => RunSubjects.Of(node, new HashSet<string>()) == RunSubject.Input))
        {
            var (subject, revision) = RunInputs.Of(node, Wires, graph.Statuses, Work, tip);
            subjects[node.Id] = subject;
            if (subject is not null) revisions[node.Id] = revision;
        }

        return Base with
        {
            Revisions = new Dictionary<string, string?> { [Run] = null },
            Subjects = subjects,
            NodeRevisions = revisions,
            Units = new Dictionary<string, WorkUnit>(StringComparer.Ordinal),
        };
    }

    private static Dictionary<string, NodePort> Moved(string reference, string commit, string producer) =>
        new() { ["moved"] = new NodePort(WireValues.Ref, reference, commit, null) { Producer = new NodeProducer(producer, 1) } };

    /// <summary>
    /// A moved input rebinds only what it feeds: the work's tip moving rebinds the step fed the work and leaves the steps fed a ref bound;
    /// a producer's new ref rebinds its own reader and leaves the work's and the other ref's readers bound.
    /// </summary>
    [Fact]
    public void A_moved_input_rebinds_only_what_it_feeds()
    {
        var graph = new Graph(Nodes, Wires, Run);
        graph.Run = Pass(graph, "tip-1");
        graph.Through("claim", new() { ["work"] = new NodePort(WireValues.Work, Run, null, null) });
        graph.Through("one", Moved("refs/heads/one", "c-1", "one"));
        graph.Through("two", Moved("refs/heads/two", "c-2", "two"));
        graph.Run = Pass(graph, "tip-1");
        var ready = graph.Evaluate();
        foreach (var id in new[] { "build", "push-one", "push-two" })
        {
            graph.Start(ready, id);
            graph.Reach(id);
        }

        Assert.Equal((Work, "tip-1"), (graph.Statuses["build"].Binding!.Observes, graph.Statuses["build"].Binding!.SubjectRevision));
        Assert.Equal(("refs/heads/one", "c-1"), (graph.Statuses["push-one"].Binding!.Observes, graph.Statuses["push-one"].Binding!.SubjectRevision));

        graph.Run = Pass(graph, "tip-2");
        var worked = graph.Evaluate();
        Assert.Equal(["build"], worked.Moves.Where(move => move.Move.To == NodeStates.Invalidated).Select(move => move.Node));
        Assert.Equal((NodeStates.Reached, NodeStates.Reached), (graph.State("push-one"), graph.State("push-two")));

        graph.Through("build");
        var kept = (graph.Statuses["build"].Binding!.Id, graph.Statuses["push-two"].Binding!.Id);
        graph.Run = Pass(graph, "tip-2", producers: "p-2");
        var refed = graph.Evaluate();
        Assert.Equal(["one", "push-one"], refed.Moves.Where(move => move.Move.To == NodeStates.Invalidated).Select(move => move.Node).Order(StringComparer.Ordinal));
        graph.Through("one", Moved("refs/heads/one", "c-3", "one"));
        graph.Run = Pass(graph, "tip-2", producers: "p-2");
        graph.Start(graph.Evaluate(), "push-one");
        Assert.Equal(("refs/heads/one", "c-3"), (graph.Statuses["push-one"].Binding!.Observes, graph.Statuses["push-one"].Binding!.SubjectRevision));
        Assert.Equal((NodeStates.Reached, NodeStates.Reached), (graph.State("build"), graph.State("push-two")));
        Assert.Equal(kept, (graph.Statuses["build"].Binding!.Id, graph.Statuses["push-two"].Binding!.Id));
    }

    /// <summary>A node whose mandatory input only a skipped producer feeds is skipped with it, said naming the producer, and so is its own reader.</summary>
    [Fact]
    public void A_skipped_producers_readers_are_skipped_with_it_naming_it()
    {
        NodeDeclaration[] nodes =
        [
            Producer("make", "made", WireValues.Commit),
            Reader("check", "made", WireValues.Commit) with { Outputs = Plugs("checked", WireValues.Commit) },
            Reader("merge", "checked", WireValues.Commit),
        ];
        Wire[] wires = [Data("w1", "make", "made", "check", "made"), Data("w2", "check", "checked", "merge", "checked")];
        var graph = new Graph(nodes, wires);
        graph.Evaluate();
        graph.Skip("make");

        graph.Evaluate();
        var check = graph.Statuses["check"];
        Assert.Equal((NodeStates.Skipped, NodeSource.Path), (check.State, check.By));
        Assert.Contains("`make`", check.Words);

        graph.Evaluate();
        Assert.Equal(NodeStates.Skipped, graph.State("merge"));
        Assert.Contains("`check`", graph.Statuses["merge"].Words);
    }

    /// <summary>A port's producer, a commit's <c>onto</c> and a problem's stop are kept on the record and read back as written, and none moves an acceptance's id.</summary>
    [Fact]
    public void A_values_producer_onto_and_stop_are_kept_and_read_back()
    {
        var plain = new NodePort(WireValues.Problem, "refs/daoris/steps/q7/rebase/1", "c-1", "acc-1");
        var problem = plain with
        {
            Producer = new NodeProducer("rebase", 1),
            Onto = "c-0",
            Stop = new NodeStop(ProblemSorts.Conflict, "patch-1", ["src/a.cs", "TASKS.md"]),
        };
        var binding = new NodeAcceptance(Run, "c-1", "graph-1", "resolve", 1, 1) { Inputs = new Dictionary<string, NodePort> { ["problem"] = problem } };
        Assert.Equal(binding.Id, (binding with { Inputs = new Dictionary<string, NodePort> { ["problem"] = plain } }).Id);

        foreach (var port in new[] { plain, problem })
        {
            using var written = new MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(written)) NodePortJson.Write(writer, port);
            using var read = System.Text.Json.JsonDocument.Parse(written.ToArray());
            Assert.Equal(port, NodePortJson.Read(read.RootElement));
        }

        Assert.Equal(new NodeStop(ProblemSorts.Conflict, "patch-1", ["src/a.cs", "TASKS.md"]), problem.Stop);
    }
}
