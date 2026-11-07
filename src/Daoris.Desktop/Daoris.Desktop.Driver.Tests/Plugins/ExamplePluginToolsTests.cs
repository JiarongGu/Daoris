using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each pull-request example plugin opens and reads its pull request through its
/// service's REST API, so it declares Node.js alone, the one program it starts. No <c>git</c>, <c>gh</c> or <c>az</c> is declared, checked
/// or started. Each floor is read by the driver's catalogue as written, and the README's <i>Why each version</i> says where it comes from.
/// </summary>
public sealed class ExamplePluginToolsTests
{
    private static string Examples => Path.Combine(WorkspaceRoot.Folder, "examples", "plugins");

    public static TheoryData<string> Plugins() => new() { "github-pull-request", "azure-devops-pull-request" };

    private static PluginManifest Read(string plugin)
    {
        var (manifest, problem) = PluginCatalog.ReadAsWritten(plugin, Path.Combine(Examples, plugin, PluginCatalog.ManifestName));
        Assert.Null(problem);
        return manifest;
    }

    [Theory]
    [MemberData(nameof(Plugins))]
    public void Each_plugin_declares_node_alone_with_its_floor_and_no_check(string plugin)
    {
        var manifest = Read(plugin);

        Assert.Null(manifest.ToolsProblem);
        var node = Assert.Single(manifest.Tools);
        Assert.Null(node.Problem);
        Assert.Equal("node", node.Id);
        Assert.Equal(">=18.0.0", node.Versions);
        Assert.False(string.IsNullOrWhiteSpace(node.For));
        Assert.Empty(node.Ready);
    }

    [Theory]
    [MemberData(nameof(Plugins))]
    public void Each_plugin_starts_no_program_but_its_own_node_and_speaks_the_work_wire_alone(string plugin)
    {
        var manifest = Read(plugin);
        var source = File.ReadAllText(Path.Combine(Examples, plugin, "plugin.mjs"));

        Assert.Equal("node", manifest.Hooks!.Command[0]);
        Assert.DoesNotMatch(new Regex(@"child_process|\bspawn|\bexecFile|\bexecSync"), source);
        Assert.Equal(["work/watch", "work/step", "work/perform", "work/state"], manifest.Hooks.Points);
        Assert.False(File.Exists(Path.Combine(Examples, plugin, "land.mjs")), "the landing wire's entry left with work/land");
    }

    [Theory]
    [MemberData(nameof(Plugins))]
    public void Each_plugin_s_readme_points_at_its_manifest_for_what_it_needs_and_says_where_each_floor_was_read(string plugin)
    {
        var folder = Path.Combine(Examples, plugin);
        var manifest = Read(plugin);
        var needs = PluginOffers.Needs(folder);
        Assert.Contains(needs, need => need.Contains("`plugin.json`", StringComparison.Ordinal) && need.Contains("`tools`", StringComparison.Ordinal));
        Assert.Contains(needs, need => need.Contains("API token", StringComparison.Ordinal));
        // No floor is written twice: the manifest holds each, and the README's needs name none.
        foreach (var tool in manifest.Tools)
        {
            Assert.DoesNotContain(needs, need => need.Contains(tool.Versions![2..], StringComparison.Ordinal));
        }

        var readme = File.ReadAllText(Path.Combine(folder, "README.md")).ReplaceLineEndings("\n");
        var start = readme.IndexOf("\n## Why each version\n", StringComparison.Ordinal);
        Assert.True(start >= 0, "the README has its Why each version section");
        var why = readme[(start + 1)..].Split("\n## ")[0];
        foreach (var tool in manifest.Tools)
        {
            Assert.Matches(new Regex($@"(?m)^- \*\*{Regex.Escape(tool.Name!)} {Regex.Escape(tool.Versions![2..])}\*\*"), why);
        }
    }
}
