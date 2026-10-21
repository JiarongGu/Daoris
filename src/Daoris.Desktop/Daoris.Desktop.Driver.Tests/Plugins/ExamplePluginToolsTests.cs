using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PLUGTOOL1b (D150 point 7, the UX6 design §7.4): each example plugin that lands work declares the tools its process runs, read by the
/// driver's catalogue as written: Node.js for its <c>land.mjs</c>, Git for its push, and its platform's CLI with the checks a person puts
/// right with its <c>fix</c>. Each floor is the oldest release that has everything the plugin runs with that tool, and its README's
/// <i>Why each version</i> says where each was read. Held by the CLI's <c>landing-plugins.test.ts</c> through its own reader until that
/// reader left with <c>daoris plugin</c>'s TypeScript (PUSH1f2f); the plugins' wire is still that test's.
/// </summary>
public sealed partial class ExamplePluginToolsTests
{
    private static string Examples => Path.Combine(WorkspaceRoot.Folder, "examples", "plugins");

    /// <summary>Each plugin's tools: id, kind, range, and each check's run, says and fix.</summary>
    public static TheoryData<string, string> Declared() => new()
    {
        {
            "github-pull-request",
            "node own >=16.6.0 | git own >=1.7.0 | gh known >=1.9.0 [gh auth status; Signed in; gh auth login]"
        },
        {
            "azure-devops-pull-request",
            "node own >=16.6.0 | git own >=1.7.0 | az known >=2.0.79 [az extension show --name azure-devops --output none; Its devops "
            + "extension is added; az extension add --name azure-devops] [az account show --output none; Signed in; az login]"
        },
    };

    private static PluginManifest Read(string plugin)
    {
        var (manifest, problem) = PluginCatalog.ReadAsWritten(plugin, Path.Combine(Examples, plugin, PluginCatalog.ManifestName));
        Assert.Null(problem);
        return manifest;
    }

    [Theory]
    [MemberData(nameof(Declared))]
    public void Each_plugin_declares_node_git_and_its_platform_s_cli_each_with_its_floor_read_with_no_problem(string plugin, string declared)
    {
        var manifest = Read(plugin);

        Assert.Null(manifest.ToolsProblem);
        Assert.All(manifest.Tools, tool => Assert.Null(tool.Problem));
        var said = string.Join(" | ", manifest.Tools.Select(tool =>
            $"{tool.Id} {tool.Kind!.Value.ToString().ToLowerInvariant()} {tool.Versions}"
            + string.Concat(tool.Ready.Select(check => $" [{string.Join(' ', check.Run)}; {check.Says}; {check.Fix}]"))));
        Assert.Equal(declared, said);
        foreach (var tool in manifest.Tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.For), $"{tool.Id} says why the plugin runs it");
            // A check runs its own tool, so it is found as that tool is.
            Assert.All(tool.Ready, check => Assert.Equal(tool.Id, check.Run[0]));
        }
    }

    [Theory]
    [MemberData(nameof(Declared))]
    public void Each_plugin_declares_exactly_the_programs_it_starts(string plugin, string declared)
    {
        _ = declared;
        var manifest = Read(plugin);
        var source = File.ReadAllText(Path.Combine(Examples, plugin, "land.mjs"));
        var started = new SortedSet<string>(StringComparer.Ordinal) { manifest.Hooks!.Command[0] };
        foreach (Match found in Started().Matches(source)) started.Add(found.Groups[1].Value);

        Assert.Equal(started, new SortedSet<string>(manifest.Tools.Select(tool => tool.Id!), StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Declared))]
    public void Each_plugin_s_readme_points_at_its_manifest_for_what_it_needs_and_says_where_each_floor_was_read(string plugin, string declared)
    {
        _ = declared;
        var folder = Path.Combine(Examples, plugin);
        var manifest = Read(plugin);
        var needs = PluginOffers.Needs(folder);
        Assert.Contains(needs, need => need.Contains("`plugin.json`", StringComparison.Ordinal) && need.Contains("`tools`", StringComparison.Ordinal));
        // No floor is written twice: the manifest holds each, and the README's needs name none.
        foreach (var tool in manifest.Tools)
        {
            var floor = tool.Versions![2..];
            Assert.DoesNotContain(needs, need => need.Contains(floor, StringComparison.Ordinal));
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

    /// <summary>A program <c>land.mjs</c> starts: its <c>run('&lt;program&gt;'</c> calls.</summary>
    [GeneratedRegex(@"\brun\('([^']+)'")]
    private static partial Regex Started();
}
