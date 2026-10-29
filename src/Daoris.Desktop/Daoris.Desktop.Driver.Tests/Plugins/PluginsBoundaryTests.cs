using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xvii, the Plugins cuts: what they keep. A plugin's manifest and the node declarations the workflows read from it, the
/// plugin page's read of the same, and the plugins the workflow editor lists, each through the surface that stood on both sides of the cut.
/// The seams the cuts add are <c>PluginsPortsTests</c>'.
/// </summary>
public sealed class PluginsBoundaryTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "plugins-boundary", Guid.NewGuid().ToString("N")[..8]);

    public PluginsBoundaryTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private void Install(string id, string manifest)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), manifest);
    }

    private const string Deploy = """
        { "id": "acme.deploy", "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/perform", "work/reconcile"] },
          "steps": [
            { "id": "deploy", "version": 2, "reach": "performed", "inputs": { "release": "acme.build/artifact@1" }, "outputs": { "preview": "acme.deploy/preview@1" },
              "retry": { "maxAttempts": 3, "delaysSeconds": [5, 30] }, "timeoutSeconds": 600, "skippable": false, "rejectable": true,
              "perform": { "reconcile": "work/reconcile", "transient": ["unavailable", "rate-limited"] } },
            { "id": "deployed", "reach": "observed" } ],
          "resources": [ { "schema": "acme.deploy/preview@1", "cleanup": "work/cleanup" } ] }
        """;

    [Fact]
    public void A_manifest_s_steps_are_the_declarations_the_workflows_read_and_each_is_judged_by_the_one_table()
    {
        Install("acme.deploy", Deploy);

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        Assert.Null(entry.Problem);
        Assert.Equal(["acme.deploy/deploy", "acme.deploy/deployed"], entry.Manifest.Steps.Select(step => step.Kind));

        var deploy = new NodeDeclaration("acme.deploy/deploy", "acme.deploy/deploy", 2, NodeReach.Performed)
        {
            Inputs = new Dictionary<string, string> { ["release"] = "acme.build/artifact@1" },
            Outputs = new Dictionary<string, string> { ["preview"] = "acme.deploy/preview@1" },
            Retry = new NodeRetry(3, [5, 30]),
            TimeoutSeconds = 600,
            Skippable = false,
            Rejectable = true,
            Perform = new NodePerform(false, true, ["unavailable", "rate-limited"]),
        };
        Assert.Equal(deploy, entry.Manifest.Steps[0]);
        Assert.Equal(new NodeDeclaration("acme.deploy/deployed", "acme.deploy/deployed", 1, NodeReach.Observed), entry.Manifest.Steps[1]);
        Assert.All(entry.Manifest.Steps, step => Assert.Null(NodeDeclarations.Problem(step)));
        Assert.Equal("deploy", PluginSteps.NameOf(entry.Manifest.Steps[0]));

        // A declaration the table refuses refuses the plugin, by the table's own code.
        Install("acme.broken", """
            { "id": "acme.broken", "hooks": { "command": ["node", "x.mjs"], "points": ["work/perform"] },
              "steps": [ { "id": "deploy", "reach": "performed", "timeoutSeconds": 0 } ] }
            """);
        var broken = PluginCatalog.Load(_home).Plugins.Single(each => each.Manifest.Id == "acme.broken");
        Assert.NotNull(broken.Problem);
        Assert.Empty(broken.Manifest.Steps);
    }

    [Fact]
    public void A_plugin_page_reads_the_same_steps_and_resources_as_the_manifest_declares_them()
    {
        Install("acme.deploy", Deploy);

        var page = PluginPage.Read(_home, "acme.deploy");
        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Equal(entry.Manifest.Steps, page.Steps);
        Assert.Equal([new PluginResource("acme.deploy/preview@1", "temporary", "work/cleanup")], page.Resources);
        var shown = PluginsCommand.Show(page);
        Assert.Contains("  steps      deploy (performed, version 2;", shown);
        Assert.Contains("             deployed (observed, version 1)\n", shown);
    }

    [Fact]
    public void The_plugins_the_workflow_editor_lists_are_those_on_and_sound_in_catalogue_order_with_their_points()
    {
        Install("acme.deploy", Deploy);
        Install("acme.idle", """{ "id": "acme.idle" }""");
        Install("acme.refused", """{ "id": "acme.refused", "hooks": { "command": ["node", "x.mjs"] } }""");

        var plugins = WorkflowCurrent.PluginsOf(PluginCatalog.Load(_home));

        Assert.Equal(["acme.deploy", "acme.idle"], plugins.Select(plugin => plugin.Id));
        Assert.Equal(["work/perform", "work/reconcile"], plugins[0].Points);
        Assert.Empty(plugins[1].Points);
    }
}
