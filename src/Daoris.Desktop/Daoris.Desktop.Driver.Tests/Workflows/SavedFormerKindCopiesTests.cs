using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What an earlier build saved with a <c>human</c> step (a shipped preset's copy, a quest's kept closure) is still known by this build: a copy of the
/// preset is still the preset's copy, so Setup plans nothing for it, and a kept closure still reads at the digest its binding holds.
/// </summary>
public sealed class SavedFormerKindCopiesTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-former-copy-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>The preset's graph as the earlier build spelled it: its tasks <c>human</c>.</summary>
    private static string Earlier(WorkflowDefault shipped) =>
        WorkflowDefaults.Variant(shipped, false, false).ToJsonString().Replace("\"kind\":\"task\"", "\"kind\":\"human\"", StringComparison.Ordinal);

    private WorkflowDefault SaveEarlierCopy(string id)
    {
        var shipped = WorkflowDefaults.Of("pull-request-release")!;
        var graph = Earlier(shipped);
        Assert.Contains("\"kind\":\"human\"", graph, StringComparison.Ordinal);
        Directory.CreateDirectory(WorkflowStore.FolderOf(_home));
        File.WriteAllText(WorkflowStore.PathOf(_home, id),
            $$"""{"id":"{{id}}","name":"Release","versions":[{"version":1,"at":"2026-10-21T09:00:00Z","door":"terminal","schema":2,"from":{"default":"pull-request-release","digest":"gph-0"},"graph":{{graph}}}]}""");
        return shipped;
    }

    [Fact]
    public void A_copy_saved_by_the_earlier_build_is_still_the_presets_copy_and_Setup_plans_nothing_for_it()
    {
        var shipped = SaveEarlierCopy("release");

        Assert.Equal("release", new PlannedWorkflows(_home).CopyOf(shipped, opinion: false, look: false));
        var save = new PlaceOperation("save", PlaceOperations.WorkflowSave, null, "", PlaceOutcomes.Pending) { Workflow = "release", Default = shipped.Id };
        Assert.True(PlaceApplies.CopySaved(_home, save));
        var newest = Assert.Single(new WorkflowFiles(_home).Load("release")!.Read.Versions);
        Assert.Empty(WorkflowDefaults.Differs(shipped, newest.Graph!));
        // Its own digest stays as saved: a run bound to it holds that identity.
        Assert.NotEqual(NodeGraphs.Digest(newest.Graph!), newest.Digest);
    }

    [Fact]
    public void A_closure_kept_by_the_earlier_build_reads_at_the_digest_its_binding_holds()
    {
        const string Closure = """
            {"schemaVersion":2,"entries":["sign"],"end":"close",
             "nodes":[{"id":"sign","kind":"human","version":1,"reach":"pressed","outcomes":["approved"]},
                      {"id":"close","kind":"quest.close","version":1,"reach":"performed"}],
             "wires":[{"id":"s>c","kind":"after","from":"sign","to":"close"}]}
            """;
        var digest = NodeGraphs.ReadSaved(Closure).Digest!;

        ClosureGraphs.Keep(_home, digest, Closure);

        var kept = ClosureGraphs.Read(_home, digest);
        Assert.NotNull(kept);
        Assert.Equal(NodeKinds.Task, kept.Nodes.Single(node => node.Id == "sign").Kind);
        Assert.Null(ClosureGraphs.Read(_home, "gph-" + new string('a', 16)));
    }
}
