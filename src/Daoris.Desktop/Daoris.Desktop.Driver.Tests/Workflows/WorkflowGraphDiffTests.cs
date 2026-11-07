using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A proposal against the open draft said as its changes. Steps match by id, ways by
/// structure then id, and nothing that runs differently is folded away: a way whose id changed is shown, and so are the entries and the end.
/// Each change names the changes it needs with it.
/// </summary>
public sealed class WorkflowGraphDiffTests
{
    private static NodeGraph Shipped(string id) => NodeGraphs.Read(WorkflowDefaults.Of(id)!.GraphJson).Graph!;

    /// <summary>A graph as written, judged by nothing: a draft is diffed whatever it holds.</summary>
    private static NodeGraph Parsed(string json) =>
        NodeGraphs.Parts(json, out _) is { } parts ? new NodeGraph(parts.Nodes, parts.Wires, parts.Entries, parts.End ?? "") : throw new InvalidOperationException(json);

    [Fact]
    public void Every_shipped_graph_against_itself_says_nothing()
    {
        Assert.NotEmpty(WorkflowDefaults.All);
        Assert.All(WorkflowDefaults.All, shipped => Assert.Empty(WorkflowGraphDiff.Of(Shipped(shipped.Id), Shipped(shipped.Id))));
    }

    /// <summary>The release workflow from the Merge workflow: four steps added, their ways needing them, and each way renumbered shown.</summary>
    [Fact]
    public void The_release_workflow_from_the_merge_workflow_is_its_steps_its_ways_and_each_id_that_moved()
    {
        var changes = WorkflowGraphDiff.Of(Shipped("merge-after-accept"), Shipped("release"));
        var by = changes.ToDictionary(change => change.Id);

        Assert.Equal(
            ["step:+check", "step:+find", "step:+release", "step:+publish",
             "way:~l", "way:~m", "way:~b", "way:+c", "way:~d", "way:~e", "way:+f", "way:+g", "way:+h", "way:+i", "way:+j", "way:+k"],
            changes.Select(change => change.Id));
        Assert.DoesNotContain(changes, change => change.Change == GraphChanges.Removed);

        // A renumbered way is shown by its id, never folded away: an id orders an anyOf's witness and keys a trigger.
        Assert.Equal(["id"], by["way:~l"].Fields);
        Assert.Equal("h", by["way:~l"].Was);
        Assert.Equal("Change the way `l` (after): `requests` → `holds`; its id was `h`.", by["way:~l"].Said);
        Assert.Equal(["to"], by["way:~b"].Fields);
        Assert.Null(by["way:~b"].Was);
        Assert.Equal("Change the way `b` (after): `content` → `check`; it was `content` → `review`.", by["way:~b"].Said);

        // Each way needs the steps it is added between, and an id it takes from a way renumbered away.
        Assert.Equal(["step:+check"], by["way:~b"].Needs);
        Assert.Equal(["step:+check", "way:~e"], by["way:+c"].Needs);
        Assert.Equal(["step:+find", "step:+release", "way:~l"], by["way:+h"].Needs);
        Assert.Equal(["step:+find", "step:+release", "way:~m"], by["way:+i"].Needs);
        Assert.Equal(["step:+release", "step:+publish"], by["way:+k"].Needs);
        Assert.Equal("Add the way `i` (data): `find.release` → `release.release`.", by["way:+i"].Said);
        Assert.Equal("Add the way `h` (when `release`): `find` → `release`.", by["way:+h"].Said);
        Assert.Equal("Add the way `f` (back on `rejected`): `review` → `work`.", by["way:+f"].Said);
        Assert.Equal(("step", "find", "local-release/find"), (by["step:+find"].What, by["step:+find"].Step, by["step:+find"].Kind));
        Assert.Equal("Add the step `find` (`local-release/find`).", by["step:+find"].Said);
    }

    /// <summary>The other way: a removed step needs its ways' removals, which go with it.</summary>
    [Fact]
    public void A_removed_step_needs_the_removal_or_change_of_each_way_it_held()
    {
        var changes = WorkflowGraphDiff.Of(Shipped("release"), Shipped("merge-after-accept"));
        var by = changes.ToDictionary(change => change.Id);

        Assert.Equal(["step:-check", "step:-find", "step:-release", "step:-publish"], changes.Where(change => change.What == GraphChanges.Step).Select(change => change.Id));
        Assert.Equal(["way:~b", "way:-c"], by["step:-check"].Needs);
        Assert.Equal(["way:-g", "way:-h", "way:-i"], by["step:-find"].Needs);
        Assert.Equal("Remove the way `g` (after): `line` → `find`.", by["way:-g"].Said);
        Assert.Equal("Remove the step `find` (`local-release/find`).", by["step:-find"].Said);
    }

    /// <summary>A step's options, a way's group, the entries and the end are each a change of their own, said by field.</summary>
    [Fact]
    public void A_step_s_option_a_way_s_group_the_entries_and_the_end_are_each_shown()
    {
        var before = Parsed("""
            {"schemaVersion":2,"entries":["a"],"end":"c","nodes":[{"id":"a","kind":"claimed","version":1,"reach":"observed"},
              {"id":"b","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}},{"id":"c","kind":"line","version":1,"reach":"observed"}],
             "wires":[{"id":"w1","kind":"after","from":"a","to":"b"},{"id":"w2","kind":"after","from":"b","to":"c"}]}
            """);
        var after = Parsed("""
            {"schemaVersion":2,"entries":["a"],"end":"d","nodes":[{"id":"a","kind":"claimed","version":1,"reach":"observed"},
              {"id":"b","kind":"land","version":1,"reach":"performed","options":{"method":"rebase"}},{"id":"c","kind":"line","version":1,"reach":"observed"},
              {"id":"d","kind":"line","version":1,"reach":"observed"}],
             "wires":[{"id":"w1","kind":"after","from":"a","to":"b","group":{"id":"in","mode":"anyOf"}},{"id":"w2","kind":"after","from":"b","to":"c"},
              {"id":"w3","kind":"after","from":"c","to":"d"}]}
            """);

        var by = WorkflowGraphDiff.Of(before, after).ToDictionary(change => change.Id);

        Assert.Equal(["options.method"], by["step:~b"].Fields);
        Assert.Equal("Change the step `b` (`land`): its `options.method`.", by["step:~b"].Said);
        Assert.Equal(["group"], by["way:~w1"].Fields);
        Assert.Equal(GraphChanges.Graph, by["graph:~end"].What);
        Assert.Equal(["end"], by["graph:~end"].Fields);
        Assert.Equal(["step:+d"], by["graph:~end"].Needs);
        Assert.Equal("The work ends at `d` now, not `c`.", by["graph:~end"].Said);
        Assert.False(by.ContainsKey("graph:~entries"));
    }
}
