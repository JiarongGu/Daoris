using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xvii, the Plugins cuts: the seams they add. The step rules Workflows installs being the node table's, a supplied
/// operation consulted for each step a manifest declares, and the work runtime's ports being the plugins' own (the hook set implements
/// them). What the cuts keep is <c>PluginsBoundaryTests</c>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class PluginsPortsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "plugins-ports", Guid.NewGuid().ToString("N")[..8]);

    public PluginsPortsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    /// <summary>Answers as the installed operation does and counts what it was asked, so a class running beside this one reads no difference.</summary>
    private sealed class Counting(IPluginStepRules actual) : IPluginStepRules
    {
        private int _asked;

        public int Asked => Volatile.Read(ref _asked);

        public string? Problem(NodeDeclaration step)
        {
            Interlocked.Increment(ref _asked);
            return actual.Problem(step);
        }
    }

    [Fact]
    public void The_installed_step_rules_are_workflows_and_judge_a_declaration_as_the_node_table_does()
    {
        Assert.IsType<PluginStepRulesOperations>(PluginStepRules.Current);

        NodeDeclaration[] declarations =
        [
            new("acme.deploy/deployed", "acme.deploy/deployed", 1, NodeReach.Observed),
            new("acme.deploy/deploy", "acme.deploy/deploy", 1, NodeReach.Performed),
            new("acme.deploy/deploy", "acme.deploy/deploy", 0, NodeReach.Performed),
            new("acme.deploy/deploy", "acme.deploy/deploy", 1, NodeReach.Pressed),
            new("acme.deploy/deploy", "acme.deploy/deploy", 1, NodeReach.Performed) { TimeoutSeconds = 0 },
            new("acme.deploy/deploy", "acme.deploy/deploy", 1, NodeReach.Performed) { Retry = new NodeRetry(0, []) },
            new("acme.deploy/deploy", "acme.deploy/deploy", 1, NodeReach.Performed) { Perform = new NodePerform(true, false, ["Not-A-Code"]) },
            new("deploy", "unknown-kind", 1, NodeReach.Observed),
        ];
        foreach (var declaration in declarations)
        {
            Assert.Equal(NodeDeclarations.Problem(declaration), PluginStepRules.Current.Problem(declaration));
        }

        Assert.Null(PluginStepRules.Current.Problem(declarations[0]));
        Assert.Equal("version", PluginStepRules.Current.Problem(declarations[2]));
        Assert.Equal("reach", PluginStepRules.Current.Problem(declarations[3]));
        Assert.Equal("timeoutSeconds", PluginStepRules.Current.Problem(declarations[4]));
    }

    [Fact]
    public void A_supplied_operation_is_asked_once_for_each_step_a_manifest_declares_and_the_plugin_is_refused_by_its_answer()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.deploy");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "acme.deploy", "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/perform"] },
              "steps": [ { "id": "deploy", "reach": "performed" }, { "id": "deployed", "reach": "observed" } ] }
            """);

        var actual = PluginStepRules.Current;
        var counting = new Counting(actual);
        try
        {
            PluginStepRules.Current = counting;
            var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
            Assert.Null(entry.Problem);
            Assert.Equal(2, entry.Manifest.Steps.Count);
            Assert.True(counting.Asked >= 2, "each declared step was asked of the supplied operation");
        }
        finally { PluginStepRules.Current = actual; }

        Assert.Same(actual, PluginStepRules.Current);
    }

    [Fact]
    public void The_work_runtime_s_ports_are_the_plugins_and_the_hook_set_is_the_one_runtime_the_workflows_are_handed()
    {
        Assert.True(typeof(IWorkRuntimePort).IsAssignableFrom(typeof(HookSet)));
        Assert.True(typeof(IWorkPerformPort).IsAssignableFrom(typeof(IWorkRuntimePort)));
        Assert.True(typeof(IWorkPerformPort).IsAssignableFrom(typeof(HookSet)));
    }
}
