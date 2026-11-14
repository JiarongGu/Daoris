using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class PluginBriefsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "plugin-briefs-" + Guid.NewGuid().ToString("N"));
    private static readonly PluginPlace Place = new("engine", "games");
    private const string Line = "File a report with acme-report_report, with evidence.";

    public void Dispose() => GitSeed.Discard(_home);

    private void Install(string id = "acme-report", string brief = "\"  " + Line + "  \"")
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), $$"""
            { "id": "{{id}}", "brief": {{brief}},
              "hooks": { "command": ["node", "plugin.mjs"], "points": ["session/ended"] },
              "interfaces": { "commands": [{ "name": "report", "about": "Files a report.", "tool": true }] } }
            """);
    }

    private IReadOnlyList<ServerEntry> Handed() => [Assert.IsType<ServerEntry>(PluginToolsHand.Entry(_home, new("s-1", Place, "en"), "driver"))];

    private string Brief(IReadOnlyList<ServerEntry> servers) => StandingBlock.Compose(new StandingFacts
    {
        Repository = Place.Repository,
        Workspace = Place.Workspace,
        PluginBriefs = PluginBriefs.At(_home, Place, servers),
    }).Text;

    [Fact]
    public void A_manifest_line_reaches_the_session_s_brief_once_with_its_tools()
    {
        Install();
        Assert.Equal(Line, Assert.Single(PluginCatalog.Load(_home).Contributing).Manifest.Brief);
        Assert.Contains(Line, Brief(Handed()));
        Assert.Equal([Line], PluginBriefs.At(_home, Place, Handed()));
        Assert.DoesNotContain(Line, Brief([]));
    }

    [Theory]
    [InlineData("machine", "tools")]
    [InlineData("workspace:games", "tools")]
    [InlineData("repository:engine", "tools")]
    [InlineData("repository:engine", "commands")]
    public void The_line_follows_the_same_place_switches_as_the_tools(string place, string what)
    {
        Install();
        var servers = Handed();
        PluginSwitches.Set(_home, "acme-report", what, place, on: false);
        Assert.DoesNotContain(Line, Brief(servers));
        PluginSwitches.Set(_home, "acme-report", what, "repository:engine", on: true);
        Assert.Contains(Line, Brief(servers));
        Assert.Empty(PluginBriefs.At(_home, new("elsewhere", "games"), []));
    }

    [Fact]
    public void A_step_gets_only_its_plugin_s_line()
    {
        Install();
        Install("acme-other", "\"Other plugin guidance.\"");
        var server = PluginToolsHand.StepEntry(_home, new("s-1", Place, "en"), "driver", "acme-report", "step-1");
        Assert.Equal([Line], PluginBriefs.At(_home, Place, [server]));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\" \"")]
    [InlineData("\"one\\ntwo\"")]
    public void An_unusable_optional_line_contributes_nothing(string brief)
    {
        Install(brief: brief);
        Assert.Null(Assert.Single(PluginCatalog.Load(_home).Contributing).Manifest.Brief);
        Assert.Empty(PluginBriefs.At(_home, Place, Handed()));
    }

    [Fact]
    public void Updating_the_line_is_shown_in_the_update_plan()
    {
        var before = PluginManifest.Empty("acme-report");
        var change = Assert.Single(PluginChange.Between(before, before with { Brief = Line }));
        Assert.Equal("brief", change.What);
        Assert.Equal(Line, change.Now);
    }

    [Fact]
    public void A_disabled_plugin_contributes_no_line()
    {
        Install();
        var servers = Handed();
        PluginState.Disable(_home, "acme-report");
        Assert.DoesNotContain(Line, Brief(servers));
    }

    [Fact]
    public void The_development_report_offer_declares_when_to_file_with_evidence()
    {
        var folder = Path.Combine(GitSeed.RepoRoot(), "examples", "plugins", "daoris-dev-report");
        var (manifest, problem) = PluginCatalog.ReadFolder(folder);
        Assert.Null(problem);
        Assert.Equal("File a report with daoris-dev-report_report when you find a defect in Daoris itself or something the person's development agent should know, with its evidence.", manifest.Brief);
    }

    [Fact]
    public void A_line_past_the_bound_contributes_nothing()
    {
        Install(brief: System.Text.Json.JsonSerializer.Serialize(new string('x', PluginBriefs.Limit + 1)));
        Assert.Empty(PluginBriefs.At(_home, Place, Handed()));
    }
}
