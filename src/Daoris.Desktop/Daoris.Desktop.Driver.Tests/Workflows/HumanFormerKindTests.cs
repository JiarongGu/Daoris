using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A saved version's former <c>human</c> step reads as a <c>task</c> under its role, the person holding it by the owner's cast, at the digest of
/// the graph as saved; a save never writes the former kind, and the census that ends the reader counts the versions still spelling it.
/// </summary>
public sealed class HumanFormerKindTests : IDisposable
{
    private const string Saved = """
        {"schemaVersion":2,"entries":["claimed"],"end":"sign",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"sign","kind":"human","version":1,"reach":"pressed","outcomes":["approved"]}],
         "wires":[{"id":"c>s","kind":"after","from":"claimed","to":"sign"}]}
        """;

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-former-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void A_saved_human_step_reads_as_a_task_under_its_role_at_the_digest_it_was_saved_with()
    {
        var digest = RunHome.SaveGraph(_home, Saved);

        var version = Assert.Single(new WorkflowFiles(_home).Load("docs")!.Read.Versions);
        var sign = version.Graph!.Nodes.Single(node => node.Id == "sign");
        Assert.Equal((NodeKinds.Task, StepRoles.Owner), (sign.Kind, StepRoles.Of(sign)));
        Assert.Equal(digest, NodeGraphs.ReadSaved(Saved).Digest);
        Assert.NotEqual(NodeGraphs.Digest(version.Graph!), digest);

        // The editor is handed the step as a task, so the next save writes the kind now; a save naming the former kind is refused.
        var drawn = PluginFormerKinds.Drawn(JsonNode.Parse(Saved)!, version.Graph!);
        Assert.Equal(NodeKinds.Task, (string?)drawn["nodes"]![1]!["kind"]);
        Assert.Null(NodeGraphs.Read(Saved, onSave: true).Graph);

        Assert.Equal(["docs v1"], HumanFormerKind.Census(_home));
        RunHome.SaveGraph(_home, Saved.Replace("\"human\"", "\"task\"", StringComparison.Ordinal));
        Assert.Empty(HumanFormerKind.Census(_home));
    }

    [Fact]
    public void A_save_naming_the_former_kind_writes_nothing()
    {
        var add = new WorkflowVersionAdded("docs", "Docs", "terminal", "2026-11-17T09:00:00Z", null, JsonNode.Parse(Saved));

        var plan = WorkflowNamed.PlanAdd(null, add, []);

        Assert.Null(plan.Result);
        Assert.Contains("`sign`", plan.Problem, StringComparison.Ordinal);
    }
}
