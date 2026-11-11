using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The kinds of place each node kind runs in (design §3.6): git's own steps in a git repository alone, the apply in a system alone, the
/// isolate in a git repository and a folder, the return in a folder alone, every other kind and a plugin's step anywhere; and the bind's refusal (<c>place-kind</c>) of a graph with a step its place cannot run.
/// </summary>
public sealed class NodePlacesTests
{
    private static readonly string[] GitAlone =
    [
        NodeKinds.Land, NodeKinds.Push, NodeKinds.Monitor, NodeKinds.Tidy, NodeKinds.Integrate, NodeKinds.Line, NodeKinds.ContentHolds,
        NodeKinds.Rebase, NodeKinds.Resolve, NodeKinds.Merge,
    ];

    /// <summary>Every built-in kind says where it runs, each place one the registration names, so a kind added says so too.</summary>
    [Fact]
    public void Every_built_in_kind_declares_the_places_it_runs_in()
    {
        foreach (var unit in NodeKit.Built.Units)
        {
            var kind = unit.Row.Kind;
            Assert.NotEmpty(unit.Places);
            Assert.All(unit.Places, place => Assert.Contains(place, PlaceKind.Every));
            IReadOnlyList<string> expected = GitAlone.Contains(kind) ? [PlaceKind.Git]
                : kind == NodeKinds.Apply ? [PlaceKind.System]
                : kind == NodeKinds.Isolate ? [PlaceKind.Git, PlaceKind.Folder]
                : kind == NodeKinds.Return ? [PlaceKind.Folder]
                : PlaceKind.Every;
            Assert.True(expected.SequenceEqual(unit.Places), $"`{kind}` runs in {string.Join(", ", unit.Places)}");
        }
    }

    /// <summary>A plugin's step runs anywhere, since no plugin declares where; Daoris's half of a pull request is git's.</summary>
    [Fact]
    public void A_plugin_step_runs_anywhere_and_a_pull_request_in_git_alone()
    {
        Assert.Equal(PlaceKind.Every, NodeKit.Of("acme.deploy/deploy")!.Places);
        Assert.Equal([PlaceKind.Git], NodeKit.Of("example.github/pull-request")!.Places);
    }

    private static NodeGraph Graph(params string[] kinds)
    {
        var nodes = kinds.Select((kind, at) => new NodeDeclaration($"s{at}", kind, 1, NodeReach.Observed)).ToArray();
        return new NodeGraph(nodes, [], [nodes[0].Id], nodes[^1].Id);
    }

    [Theory]
    [InlineData(PlaceKind.Folder, NodeKinds.Land, "a folder", "a git repository")]
    [InlineData(PlaceKind.Folder, NodeKinds.Push, "a folder", "a git repository")]
    [InlineData(PlaceKind.Folder, NodeKinds.Rebase, "a folder", "a git repository")]
    [InlineData(PlaceKind.Git, NodeKinds.Apply, "a git repository", "a system")]
    [InlineData(PlaceKind.Git, NodeKinds.Return, "a git repository", "a folder")]
    public void A_step_its_place_cannot_run_refuses_the_bind_naming_the_step_and_the_place(string place, string kind, string placeSaid, string servesSaid)
    {
        var problem = PlaceKindUnserved.Of(Graph(NodeKinds.Claimed, kind), place, "flow", 3);

        Assert.NotNull(problem);
        Assert.Equal(BindingProblem.PlaceKindUnserved, problem.Code);
        Assert.Equal(("s1", kind, place), (problem.Values["step"], problem.Values["kind"], problem.Values["place"]));
        Assert.Equal($"`flow` v3's step `s1` is a `{kind}`, which {placeSaid} cannot run: it runs in {servesSaid} alone.", problem.Said);
    }

    [Theory]
    [InlineData(PlaceKind.Git)]
    [InlineData(PlaceKind.Folder)]
    [InlineData(PlaceKind.System)]
    public void Steps_every_place_runs_bind_anywhere(string place) =>
        Assert.Null(PlaceKindUnserved.Of(Graph(NodeKinds.Claimed, NodeKinds.WorkRequests, NodeKinds.QuestHolds, NodeKinds.Review, "acme.deploy/deploy"), place, "flow", 1));

    [Theory]
    [InlineData(PlaceKind.Git, true)]
    [InlineData(PlaceKind.Folder, true)]
    [InlineData(PlaceKind.System, false)]
    public void The_isolate_step_binds_in_a_git_repository_and_a_folder(string place, bool binds) =>
        Assert.Equal(binds, PlaceKindUnserved.Of(Graph(NodeKinds.Isolate, NodeKinds.Claimed), place, "flow", 1) is null);

    /// <summary>The shipped *Work in place* sets nothing apart and lands nothing, so every kind of department can follow it; no other shipped default can a folder.</summary>
    [Theory]
    [InlineData(PlaceKind.Git)]
    [InlineData(PlaceKind.Folder)]
    [InlineData(PlaceKind.System)]
    public void Work_in_place_binds_every_kind_of_department(string place)
    {
        static NodeGraph Read(WorkflowDefault shipped) => NodeGraphs.Read(shipped.GraphJson).Graph!;
        Assert.Null(PlaceKindUnserved.Of(Read(WorkflowDefaults.Of("in-place")!), place, "in-place", 1));
        if (place != PlaceKind.Git)
        {
            Assert.All(WorkflowDefaults.All.Where(each => each.Id != "in-place"),
                shipped => Assert.NotNull(PlaceKindUnserved.Of(Read(shipped), place, shipped.Id, 1)));
        }
    }

    [Fact]
    public void A_place_whose_kind_is_not_known_binds_anything() =>
        Assert.Null(PlaceKindUnserved.Of(Graph(NodeKinds.Claimed, NodeKinds.Land), place: null, "flow", 1));
}
