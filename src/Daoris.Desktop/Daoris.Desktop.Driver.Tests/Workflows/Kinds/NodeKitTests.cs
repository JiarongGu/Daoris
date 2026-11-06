using System.Reflection;
using Daoris.Contracts.Parts;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The node kit is built once from the kinds the driver registers (D247): every id <see cref="NodeKinds"/> names has one unit and every unit one
/// id, in the editor's groups and the order their marks give; an order the marks cannot make is refused; a plugin's step reads as one adapter,
/// with Daoris's half where its step name has one; and a kit without a kind refuses a node of it, as an older build's does.
/// </summary>
public sealed class NodeKitTests
{
    private static IReadOnlyList<string> Ids() =>
    [
        .. typeof(NodeKinds).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!),
    ];

    private static PartDeclaration Declared(string name, NodeGroup group, params string[] after) => new(name, group, after, typeof(PluginStep));

    [Fact]
    public void Every_id_has_one_unit_and_every_unit_one_id()
    {
        Assert.Equal(Ids().Order(StringComparer.Ordinal), NodeKit.Kinds.Order(StringComparer.Ordinal));
        Assert.Equal(NodeKit.Kinds.Count, NodeKit.Kinds.Distinct(StringComparer.Ordinal).Count());
        foreach (var kind in NodeKit.Kinds)
        {
            var unit = Assert.IsAssignableFrom<BuiltInNode>(NodeKit.Of(kind));
            Assert.Equal(kind, unit.Row.Kind);
            Assert.Same(unit.Row, NodeKit.Row(kind));
        }

        Assert.Equal(NodeKit.Kinds.Count, NodeKit.Built.Units.Select(unit => unit.GetType()).Distinct().Count());
    }

    [Fact]
    public void The_kit_orders_its_kinds_by_the_editors_groups_each_after_the_kinds_it_names()
    {
        var order = NodeKit.Built.Order;
        Assert.Equal(
            [
                "claimed",
                "review", "look", "go-ahead", "human",
                "check", "opinion", "work.requests", "quest.holds", "content.holds",
                "land", "integrate", "push", "*/pull-request", "monitor", "tidy", "line",
                "request", "wait.unit", "quest.close", "all", "stop",
            ],
            order.Select(part => part.Name));
        Assert.Equal(
            [
                NodeGroup.Work, .. Enumerable.Repeat(NodeGroup.You, 4), .. Enumerable.Repeat(NodeGroup.Checks, 5),
                .. Enumerable.Repeat(NodeGroup.Landing, 7), .. Enumerable.Repeat(NodeGroup.Others, 5),
            ],
            order.Select(part => (NodeGroup)part.Phase));

        // Daoris's half of a plugin's step stands in the order, as the kinds after it name it, and is no kind of its own.
        Assert.Equal(order.Select(part => part.Name).Where(name => !name.StartsWith(NodeKit.AnyPlugin, StringComparison.Ordinal)), NodeKit.Kinds);
        Assert.Equal(NodeKit.Kinds, NodeKit.Built.Units.Select(unit => unit.Row.Kind));
    }

    [Fact]
    public void A_kind_declared_twice_an_unknown_after_and_a_cycle_are_refused()
    {
        Assert.Equal(
            "the part land is declared twice, by PluginStep and PluginStep",
            Assert.Throws<PartOrderException>(() => PartOrder.Of([Declared("land", NodeGroup.Landing), Declared("land", NodeGroup.Landing)])).Message);
        Assert.Equal(
            "the part push comes after deploy, which is no part",
            Assert.Throws<PartOrderException>(() => PartOrder.Of([Declared("push", NodeGroup.Landing, "deploy")])).Message);
        Assert.Equal(
            "the part claimed runs in Work and comes after land, which runs later, in Landing",
            Assert.Throws<PartOrderException>(() => PartOrder.Of([Declared("claimed", NodeGroup.Work, "land"), Declared("land", NodeGroup.Landing)])).Message);
        Assert.Equal(
            "the parts land → push → land come after each other, so none of them can run first",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("land", NodeGroup.Landing, "push"), Declared("push", NodeGroup.Landing, "land")])).Message);
    }

    [Fact]
    public void A_plugins_step_reads_as_one_adapter_and_a_pull_request_step_takes_daoriss_half()
    {
        var deploy = new NodeDeclaration("deploy", "acme.deploy/deploy", 1, NodeReach.Performed) { Rejectable = true, Skippable = false };
        var step = Assert.IsType<PluginStep>(NodeKit.Of(deploy.Kind));
        Assert.Equal(deploy.Kind, step.Kind);
        Assert.Equal(
            new NodeAuthority(deploy.Kind, NodeReach.Performed, NodeSource.Plugin, NodeSource.Plugin, true, false, true, true),
            step.Authority(deploy));
        Assert.Equal("deploy by `acme.deploy`", step.Title);
        Assert.Equal(NodeLook.Plugin, step.Look);
        Assert.Empty(step.Options);
        Assert.Equal(new NodePart(deploy.Kind, WorkflowParticipation.Automatic, WorkflowExecutor.Plugin), step.Part(deploy));
        Assert.Equal(RunSubject.Landing, step.Subject(deploy, new HashSet<string> { "deploy" }));
        Assert.Equal(RunSubject.Branch, step.Subject(deploy, new HashSet<string>()));

        var opened = new NodeDeclaration("pr", "acme.git/pull-request", 1, NodeReach.Performed);
        var pullRequest = Assert.IsType<PullRequestStep>(NodeKit.Of(opened.Kind));
        Assert.Equal(opened.Kind, pullRequest.Kind);
        Assert.Equal("pull-request by `acme.git`", pullRequest.Title);
        Assert.Equal(WorkflowKinds.PullRequest, pullRequest.Part(opened).Lowered);
        Assert.Null(step.Part(deploy).Lowered);

        // A kind no unit registers and no plugin could name is no kind: a name split once, both halves named.
        Assert.Null(NodeKit.Of("deploy"));
        Assert.Null(NodeKit.Of("acme/deploy/now"));
        Assert.Null(NodeKit.Of("acme/"));
        Assert.True(NodeKit.IsPlugin("acme.deploy/deploy"));
        Assert.False(NodeKit.IsPlugin(NodeKinds.Land));
    }

    [Fact]
    public void A_kit_without_a_kind_refuses_a_node_of_it_and_reads_every_other_as_this_one_does()
    {
        var older = NodeKit.Built.Without(NodeKinds.Monitor);
        var monitor = new NodeDeclaration("watch", NodeKinds.Monitor, 1, NodeReach.Observed);
        Assert.Equal(NodeRefusal.UnknownKind, NodeDeclarations.Problem(monitor, older));
        Assert.Null(NodeAuthorities.Of(monitor, older));
        Assert.Null(NodeDeclarations.Problem(monitor));
        Assert.DoesNotContain(NodeKinds.Monitor, older.Ids);
        Assert.Equal(NodeKit.Kinds.Count - 1, older.Ids.Count);

        var line = new NodeDeclaration("line", NodeKinds.Line, 1, NodeReach.Observed);
        Assert.Null(NodeDeclarations.Problem(line, older));
        Assert.Same(NodeKit.Row(NodeKinds.Line), NodeAuthorities.Of(line, older));
        Assert.IsType<PluginStep>(older.Find("acme.deploy/deploy"));
    }
}
