using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>daoris plugin add|update|enable|disable|remove</c> and <c>daoris plugins approve|withdraw</c> as their press's card shows them: judged at
/// the ask as the Plugins place judges them, refused with no card, and what the plugin runs, or every tool an approval names, carried for
/// the page to word.
/// </summary>
public sealed class PluginViewsTests : DriverModuleBridge, IDisposable
{
    private const string Quiet = """{ "id": "quiet-hours", "name": "Quiet hours", "version": "1.0.0", "hooks": { "command": ["node", "${plugin}/run.mjs"], "points": ["work/land"] } }""";

    private const string Browser = """
        { "id": "in-app-browser", "name": "In-app browser", "version": "1.1.0", "servers": [
            { "name": "browser", "command": ["npx", "@playwright/mcp"], "to": ["sessions", "help"],
              "allow": ["browser_navigate"], "writes": ["browser_click", "browser_type"] } ] }
        """;

    private readonly string _outside;

    public PluginViewsTests() => _outside = Home + "-outside";

    void IDisposable.Dispose()
    {
        if (Directory.Exists(_outside)) Directory.Delete(_outside, recursive: true);
        Dispose();
    }

    private string Folder(string under, string manifest)
    {
        var folder = Path.Combine(_outside, under);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), manifest);
        return folder;
    }

    private void Installed(string manifest, bool enabled = true)
    {
        var added = PluginInstall.Add(Home, Folder("added-" + Guid.NewGuid().ToString("N")[..6], manifest), []);
        if (!enabled) PluginState.Disable(Home, added.Id);
    }

    private Task<PressViewed> View(IPressView view, object payload)
    {
        var grounds = new StubGrounds();
        grounds.Facts = grounds.Facts with { Home = Home, Plugins = PluginCatalog.Load(Home), OffersFolder = Path.Combine(_outside, "offers") };
        return view.ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);
    }

    [Fact]
    public void Each_plugin_route_the_person_presses_has_its_view()
    {
        foreach (var route in new[] { "PLUGIN_ADD", "PLUGIN_INSTALL", "PLUGIN_UPDATE", "PLUGIN_ACTION" }) Assert.NotNull(PressViews.Find(DriverModule.Name, route));
    }

    [Fact]
    public async Task An_add_is_shown_with_what_the_plugin_runs_as_its_manifest_writes_it_and_the_command()
    {
        var folder = Folder("quiet-hours", Quiet);

        var viewed = await View(new PluginAddView(), new { folder });

        Assert.Null(viewed.Refusal);
        var view = viewed.View!;
        Assert.Equal(("plugin-add", "quiet-hours"), (view.Kind, view.Facts["id"]));
        Assert.Equal(["node", "${plugin}/run.mjs"], view.Plugin!.Command);
        Assert.Equal(["work/land"], view.Plugin.Points);
        Assert.Equal($"daoris plugin add {folder}", view.Command);
    }

    [Fact]
    public async Task An_add_of_a_folder_with_no_plugin_or_of_one_installed_is_refused_with_no_card()
    {
        Installed(Quiet);
        var empty = Path.Combine(_outside, "empty");
        Directory.CreateDirectory(empty);

        var again = await View(new PluginAddView(), new { folder = Folder("quiet-again", Quiet) });
        var nothing = await View(new PluginAddView(), new { folder = empty });
        var inside = await View(new PluginAddView(), new { folder = Home });

        Assert.Contains("already installed", again.Refusal);
        Assert.Null(again.View);
        Assert.NotNull(nothing.Refusal);
        Assert.NotNull(inside.Refusal);
    }

    [Fact]
    public async Task An_offer_is_shown_with_what_it_needs_and_one_not_offered_is_refused_naming_those_offered()
    {
        var offer = Folder(Path.Combine("offers", "quiet-hours"), Quiet);
        File.WriteAllText(Path.Combine(offer, "README.md"), "# Quiet hours\n\n## What it needs\n\n- Node 22 or newer\n");

        var viewed = await View(new PluginInstallView(), new { offer = "quiet-hours" });
        var ghost = await View(new PluginInstallView(), new { offer = "ghost" });

        Assert.Equal(("plugin-install", "daoris plugin add --offer quiet-hours"), (viewed.View!.Kind, viewed.View.Command));
        Assert.Equal(["Node 22 or newer"], viewed.View.Plugin!.Needs);
        Assert.Null(ghost.View);
        Assert.Contains("`quiet-hours`", ghost.Refusal);
    }

    [Fact]
    public async Task An_update_is_shown_with_what_changes_from_its_source_and_one_of_no_plugin_is_refused()
    {
        var source = Folder("quiet-source", Quiet);
        PluginInstall.Add(Home, source, []);
        File.WriteAllText(Path.Combine(source, PluginCatalog.ManifestName), Quiet.Replace("1.0.0", "1.1.0", StringComparison.Ordinal));

        var viewed = await View(new PluginUpdateView(), new { id = "quiet-hours", apply = true });
        var ghost = await View(new PluginUpdateView(), new { id = "ghost", apply = true });

        Assert.Equal(("plugin-update", "daoris plugin update quiet-hours --yes"), (viewed.View!.Kind, viewed.View.Command));
        var change = Assert.Single(viewed.View.Plugin!.Changes);
        Assert.Equal(("1.0.0", "1.1.0"), (change.Was, change.Now));
        Assert.Null(ghost.View);
        Assert.Contains("no plugin `ghost`", ghost.Refusal);
    }

    [Theory]
    [InlineData("enable", false, "plugin-enable")]
    [InlineData("disable", true, "plugin-disable")]
    [InlineData("remove", true, "plugin-remove")]
    public async Task A_switch_and_a_remove_are_shown_by_what_they_do(string action, bool enabled, string kind)
    {
        Installed(Quiet, enabled);

        var viewed = await View(new PluginActionView(), new { action, id = "quiet-hours" });

        Assert.Equal((kind, $"daoris plugin {action} quiet-hours"), (viewed.View!.Kind, viewed.View.Command));
    }

    [Fact]
    public async Task A_switch_to_where_it_stands_or_of_no_plugin_is_refused_with_no_card()
    {
        Installed(Quiet);

        var on = await View(new PluginActionView(), new { action = "enable", id = "quiet-hours" });
        var ghost = await View(new PluginActionView(), new { action = "disable", id = "ghost" });

        Assert.Contains("already on", on.Refusal);
        Assert.Contains("installed here: `quiet-hours`", ghost.Refusal);
    }

    [Fact]
    public async Task An_approval_names_every_tool_and_the_writes_it_accepts_with_the_command()
    {
        Installed(Browser);

        var some = await View(new PluginActionView(), new { action = "help-approve", id = "in-app-browser", server = "browser", writes = new[] { "browser_click" } });
        var reads = await View(new PluginActionView(), new { action = "help-reads", id = "in-app-browser", server = "browser" });

        Assert.Equal("plugin-approve", some.View!.Kind);
        var grant = some.View.Grant!;
        Assert.Equal(["browser_navigate"], grant.Reads);
        Assert.Equal(["browser_click", "browser_type"], grant.Writes);
        Assert.Equal(["browser_click"], grant.Accepted);
        Assert.Equal("daoris plugins approve in-app-browser browser --writes browser_click", some.View.Command);
        Assert.Empty(reads.View!.Grant!.Accepted);
        Assert.Equal("daoris plugins approve in-app-browser browser --reads-only", reads.View.Command);
    }

    [Fact]
    public async Task An_approval_of_an_undeclared_write_a_withdrawal_of_none_and_an_approval_again_are_refused()
    {
        Installed(Browser);

        var unknown = await View(new PluginActionView(), new { action = "help-approve", id = "in-app-browser", server = "browser", writes = new[] { "rm" } });
        var none = await View(new PluginActionView(), new { action = "help-withdraw", id = "in-app-browser", server = "browser" });
        HelpGrants.Approve(Home, PluginCatalog.Load(Home), "in-app-browser", "browser", readsOnly: true);
        var again = await View(new PluginActionView(), new { action = "help-reads", id = "in-app-browser", server = "browser" });
        var withdraw = await View(new PluginActionView(), new { action = "help-withdraw", id = "in-app-browser", server = "browser" });

        Assert.Contains("`rm` is not a write", unknown.Refusal);
        Assert.Contains("nothing to withdraw", none.Refusal);
        Assert.Contains("nothing would change", again.Refusal);
        Assert.Equal(("plugin-withdraw", true), (withdraw.View!.Kind, withdraw.View.Grant!.Withdraw));
    }

    [Fact]
    public async Task An_approval_left_stale_by_an_update_that_changed_the_tools_can_still_be_withdrawn()
    {
        Installed(Browser);
        HelpGrants.Approve(Home, PluginCatalog.Load(Home), "in-app-browser", "browser", readsOnly: true);
        var manifest = Path.Combine(DaorisHome.Entry(Home, HomeEntry.Plugins, "in-app-browser"), PluginCatalog.ManifestName);
        File.WriteAllText(manifest, Browser.Replace("browser_navigate", "browser_snapshot", StringComparison.Ordinal));

        var withdraw = await View(new PluginActionView(), new { action = "help-withdraw", id = "in-app-browser", server = "browser" });

        Assert.Equal("plugin-withdraw", withdraw.View!.Kind);
    }
}
