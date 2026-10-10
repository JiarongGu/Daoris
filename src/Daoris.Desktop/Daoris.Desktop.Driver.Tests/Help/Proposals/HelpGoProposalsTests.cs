using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>go</c> proposal (HELP6): a place the window has, which changes nothing.</summary>
public sealed class HelpGoProposalsTests : HelpProposalsFixture
{
    private static HelpProposal Go(string view, string? domain = null, string? part = null) =>
        Of("go", "go", view) with { Domain = domain, Part = part };

    [Theory]
    [InlineData("quests", null, null, "Open Quests.")]
    // UX6e2 (D150 §3.1): Agents is a place, and its parts are the agent's page's.
    [InlineData("agents", null, null, "Open Agents.")]
    [InlineData("agents", null, "rules", "Open Agents → What it may do.")]
    [InlineData("settings", "workspace", "lines", "Open Settings → Workspace → Lines.")]
    // UX6i2a (D150 §2): the guide is Get started since UX6j, Setup naming a repository's tab.
    [InlineData("settings", "start", "helper", "Open Settings → Get started at step 2, Ask Daoris's agent.")]
    [InlineData("projects", null, "import", "Open Repositories → Import a folder.")]
    // HELPSETUP1: a repository's own values are on its Setup.
    [InlineData("projects", null, "setup", "Open Repositories → a repository's Setup.")]
    // UX6i2a: Knowledge is a place since UX6i, its parts its two modes; Knowledge alone opens as it was left.
    [InlineData("knowledge", null, null, "Open Knowledge.")]
    [InlineData("knowledge", null, "convergence", "Open Knowledge → Convergence.")]
    // UX6i2a: Plugins is a place since PLUGUI1b, and Settings holds none of it since UX6j.
    [InlineData("plugins", null, null, "Open Plugins.")]
    public void A_screen_is_a_go_that_changes_nothing(string view, string? domain, string? part, string says)
    {
        var plan = HelpProposals.Plan(Go(view, domain, part), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(says, plan.Describe);
        Assert.Equal("", plan.Terminal);
        Assert.Null(plan.Apply);
        Assert.Equal(new HelpPlace(view, domain, part), plan.Go);
    }

    [Theory]
    [InlineData("dashboard", null, null, "no view `dashboard`")]
    [InlineData("settings", "billing", null, "no Settings domain `billing`")]
    [InlineData("settings", "workspace", "colours", "no part `colours`")]
    [InlineData("quests", null, "drawer", "no part `drawer`")]
    [InlineData("quests", "agents", null, "a domain is a part of Settings")]
    // UX6e2: the Settings Agents domain and Permissions' Proposals left Settings for the Agents place.
    [InlineData("settings", "agents", null, "no Settings domain `agents`")]
    [InlineData("settings", "permissions", "proposals", "no part `proposals` of `permissions`")]
    [InlineData("agents", null, "workspaces", "no part `workspaces` of `agents`")]
    // UX6i2a: a moved place is kept as it was named, and nothing beneath it: it had no parts.
    [InlineData("convergence", null, "findings", "no view `convergence`")]
    [InlineData("settings", "plugins", "kit", "no Settings domain `plugins`")]
    [InlineData("knowledge", null, "findings", "no part `findings` of `knowledge`")]
    [InlineData("plugins", null, "kit", "no part `kit` of `plugins`")]
    public void A_screen_the_window_does_not_have_is_refused(string view, string? domain, string? part, string says)
    {
        var plan = HelpProposals.Plan(Go(view, domain, part), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Go);
    }

    /// <summary>
    /// UX6i2a (D150 §2): a go spelled as a place was before it moved, kept in an earlier conversation or sent by a service
    /// that still lists it, lands where the place went, said and handed to the page by its name now.
    /// </summary>
    [Theory]
    [InlineData("search", null, "knowledge", null, "search", "Open Knowledge → Search.")]
    [InlineData("convergence", null, "knowledge", null, "convergence", "Open Knowledge → Convergence.")]
    [InlineData("settings", "plugins", "plugins", null, null, "Open Plugins.")]
    public void A_go_to_a_place_that_moved_lands_where_it_went(
        string view, string? domain, string now, string? nowDomain, string? nowPart, string says)
    {
        var plan = HelpProposals.Plan(Go(view, domain), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(says, plan.Describe);
        Assert.Equal(new HelpPlace(now, nowDomain, nowPart), plan.Go);
    }

    /// <summary>
    /// The places a go may name, as the page's twin (`help/places.ts`) holds them: the same views, Settings
    /// domains and parts, the setup guide's steps among them, and the places a go named before they moved. The page's test
    /// holds the same table.
    /// </summary>
    [Fact]
    public void The_places_are_the_pages_twin()
    {
        Assert.Equal(["overview", "sessions", "quests", "projects", "map", "knowledge", "agents", "plugins", "settings"], HelpPlaces.Views.Select(view => view.Id));
        Assert.Equal(["start", "appearance", "ai", "workspace", "driver", "permissions", "browser", "logs"], HelpPlaces.Domains.Select(domain => domain.Id));
        Assert.Equal(
            [
                "projects/add", "projects/import", "projects/setup",
                "knowledge/search", "knowledge/convergence",
                "start/agent", "start/helper", "start/repositories", "start/driven", "start/landing", "start/rules",
                "workspace/wiring", "workspace/lines", "workspace/landing", "workspace/sweep",
                "agents/accounts", "agents/rules", "agents/usage",
                "permissions/across",
            ],
            HelpPlaces.Parts.Select(part => $"{part.Within}/{part.Id}"));
        static string Spelled(HelpPlace place) => string.Join("/", new[] { place.View, place.Domain, place.Part }.OfType<string>());
        Assert.Equal(
            ["search → knowledge/search", "convergence → knowledge/convergence", "settings/plugins → plugins"],
            HelpPlaces.Kept.Select(kept => $"{Spelled(kept.Was)} → {Spelled(kept.Now)}"));
    }

    [Fact]
    public async Task A_go_changes_nothing_and_hands_the_page_the_place()
    {
        var (go, doors, _) = await ApplyAsync(Go("settings", "start", "helper"));

        Assert.True(go.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal(new HelpPlace("settings", "start", "helper"), go.Go);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
    }

    /// <summary>A go's fields, read from the file the service's box writes — and an account's, which it lacks, read as not named.</summary>
    [Fact]
    public void A_gos_fields_are_read_from_the_file()
    {
        var node = new JsonObject
        {
            ["id"] = "k2", ["proposed"] = "2026-09-30T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "go", ["door"] = "go", ["target"] = "settings", ["workspace"] = null, ["value"] = null,
            ["sentence"] = null, ["domain"] = "start", ["part"] = "helper",
            ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "k2.json"), node.ToJsonString());

        var go = Assert.Single(HelpProposals.Pending(_home, "h1"), proposal => proposal.Kind == "go");

        Assert.Equal(("start", "helper"), (go.Domain, go.Part));
        Assert.Null(go.Account);
    }
}
