using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SWITCHGAPS1a-b: what <c>workflow apply</c> says of a new version beside what the install holds: a step kind no installed plugin declares
/// (with the plugin to update where this build ships a newer one), and each declared value its node ids no longer reach, with the
/// printed <c>workflow values</c> line that carries it to each new node of the same plugin that takes none. It writes nothing.
/// Seen failing first.
/// </summary>
public sealed class WorkflowApplyNotesTests
{
    private static NodeDeclaration Node(string id, string kind) => new(id, kind, 1, NodeReach.Performed);

    private static NodeGraph Graph(params NodeDeclaration[] nodes) => new(nodes, [], [nodes[0].Id], nodes[^1].Id);

    private static DriverConfig Config(params (string Node, string Name, string Json)[] values) =>
        DriverConfig.Empty with
        {
            WorkflowValues = new Dictionary<string, WorkflowValuesKey.Values>
            {
                ["engine"] = new(new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>
                {
                    ["ship"] = values.GroupBy(value => value.Node).ToDictionary(
                        node => node.Key, node => (IReadOnlyDictionary<string, string>)node.ToDictionary(value => value.Name, value => value.Json)),
                }),
            },
        };

    private static readonly WorkflowApplyNotes.Plugin Installed = new("acme.cloud", "4.1.0", ["acme.cloud/pull-request"]);

    [Fact]
    public void A_kind_no_installed_plugin_declares_is_said_with_the_plugin_to_update_where_this_build_ships_a_newer_one()
    {
        var next = Graph(Node("open", "acme.cloud/pull-request"), Node("release-create", "acme.cloud/release-create"));
        WorkflowApplyNotes.Plugin shipped = new("acme.cloud", "4.6.0", ["acme.cloud/pull-request", "acme.cloud/release-create"]);

        var said = WorkflowApplyNotes.Undeclared(next, [Installed], [shipped]);

        var line = Assert.Single(said);
        Assert.Contains("`release-create`", line);
        Assert.Contains("acme.cloud/release-create", line);
        Assert.Contains("4.1.0", line);
        Assert.Contains("4.6.0", line);
        Assert.Contains("daoris plugin update acme.cloud --yes", line);
    }

    [Fact]
    public void A_kind_nothing_declares_names_no_update_and_a_declared_or_built_in_kind_is_not_said()
    {
        var next = Graph(Node("open", "acme.cloud/pull-request"), Node("go", "claimed"), Node("far", "other.thing/step"));

        var said = WorkflowApplyNotes.Undeclared(next, [Installed], []);

        var line = Assert.Single(said);
        Assert.Contains("`far`", line);
        Assert.DoesNotContain("plugin update", line);
    }

    [Fact]
    public void A_value_the_new_version_leaves_unread_is_said_and_carried_by_a_printed_line_to_each_new_node_of_its_plugin_that_takes_none()
    {
        var previous = Graph(Node("release", "acme.cloud/release"));
        var next = Graph(Node("release-create", "acme.cloud/release-create"), Node("release-wait", "acme.cloud/release-wait"), Node("other", "elsewhere/step"));
        var config = Config(("release", "target", "\"main\""), ("release", "channel", "{\"a\":1}"));

        var said = WorkflowApplyNotes.Unread(config, "ship", 8, previous, next);

        Assert.Contains(said, line => line.Contains("`engine`") && line.Contains("`release`") && line.Contains("unread"));
        Assert.Contains("daoris workflow values engine ship release-create channel='{\"a\":1}' target='\"main\"' --why", string.Join('\n', said));
        Assert.Contains("daoris workflow values engine ship release-wait ", string.Join('\n', said));
        Assert.DoesNotContain("ship other ", string.Join('\n', said));
    }

    [Fact]
    public void Values_the_new_version_reads_are_not_said_and_a_new_node_that_has_values_is_not_offered_the_carry()
    {
        var previous = Graph(Node("release", "acme.cloud/release"));
        var next = Graph(Node("release", "acme.cloud/release"), Node("release-wait", "acme.cloud/release-wait"));

        Assert.Empty(WorkflowApplyNotes.Unread(Config(("release", "target", "\"main\"")), "ship", 8, previous, next));
        Assert.Empty(WorkflowApplyNotes.Unread(DriverConfig.Empty, "ship", 8, previous, next));
    }
}
