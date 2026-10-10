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
    // ENTRY1b (D161's ENTRY1 note): a go reaches what waits on the person below Sessions and Quests, each group by the name
    // its list's heading shows; it names no session and no quest.
    [InlineData("sessions", null, "waiting", "Open Sessions → Waiting on you.")]
    [InlineData("sessions", null, "review", "Open Sessions → To review.")]
    [InlineData("quests", null, "asks", "Open Quests → Asks.")]
    [InlineData("quests", null, "held", "Open Quests → Waiting on you.")]
    // UX6e2 (D150 §3.1): Agents is a place, and its parts are the agent's page's.
    [InlineData("agents", null, null, "Open Agents.")]
    [InlineData("agents", null, "rules", "Open Agents → What it may do.")]
    // UX6g2b (D161 §3): a workspace's page's tabs and its Setup's sections are parts of Repositories, as a repository's
    // Setup is; a go names no workspace.
    [InlineData("projects", null, "workspace-details", "Open Repositories → a workspace's Details.")]
    [InlineData("projects", null, "workspace-branches", "Open Repositories → a workspace's Branches.")]
    [InlineData("projects", null, "workspace-workflow", "Open Repositories → a workspace's Workflow.")]
    [InlineData("projects", null, "workspace-setup", "Open Repositories → a workspace's Setup.")]
    [InlineData("projects", null, "workspace-defaults", "Open Repositories → a workspace's Defaults.")]
    [InlineData("projects", null, "workspace-remote", "Open Repositories → a workspace's Remote and reach.")]
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
    [InlineData("quests", null, "drawer", "no part `drawer`")]
    [InlineData("quests", "agents", null, "a domain is a part of Settings")]
    // UX6e2: the Settings Agents domain and Permissions' Proposals left Settings for the Agents place.
    [InlineData("settings", "agents", null, "no Settings domain `agents`")]
    [InlineData("agents", null, "workspaces", "no part `workspaces` of `agents`")]
    // UX6i2a: a moved place is kept as it was named, and nothing beneath it: it had no parts.
    [InlineData("convergence", null, "findings", "no view `convergence`")]
    [InlineData("settings", "plugins", "kit", "no Settings domain `plugins`")]
    [InlineData("knowledge", null, "findings", "no part `findings` of `knowledge`")]
    [InlineData("plugins", null, "kit", "no part `kit` of `plugins`")]
    // UX6g2b: Settings → Workspace and Permissions are kept only as their parts were named; one no kept row names is refused.
    [InlineData("settings", "workspace", "colours", "no Settings domain `workspace`")]
    [InlineData("settings", "permissions", "proposals", "no Settings domain `permissions`")]
    [InlineData("settings", "workspace", "workspace-defaults", "no Settings domain `workspace`")]
    // A workspace's parts are Repositories', unprefixed names are a repository's, and the page has no other.
    [InlineData("projects", null, "defaults", "no part `defaults` of `projects`")]
    [InlineData("projects", null, "workspace-colours", "no part `workspace-colours` of `projects`")]
    // ENTRY1b: a part is the group's go name, not the reader's (`you`); a group that waits on nobody is no part; and
    // Overview stays the view alone, since what waits leads it.
    [InlineData("sessions", null, "you", "no part `you` of `sessions` — one of `review`, `waiting`.")]
    [InlineData("sessions", null, "working", "no part `working` of `sessions`")]
    [InlineData("quests", null, "open", "no part `open` of `quests` — one of `asks`, `held`.")]
    [InlineData("overview", null, "waiting", "no part `waiting` of `overview`.")]
    public void A_screen_the_window_does_not_have_is_refused(string view, string? domain, string? part, string says)
    {
        var plan = HelpProposals.Plan(Go(view, domain, part), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Go);
    }

    /// <summary>
    /// UX6i2a (D150 §2): a go spelled as a place was before it moved, kept in an earlier conversation or sent by a service
    /// that still lists it, lands where the place went, said and handed to the page by its name now. UX6g2b: Settings →
    /// Workspace and Permissions are kept with each part they had, each its own row, on the workspace's page where the part
    /// went, and Permissions alone on what agents may do.
    /// </summary>
    [Theory]
    [InlineData("search", null, null, "knowledge", null, "search", "Open Knowledge → Search.")]
    [InlineData("convergence", null, null, "knowledge", null, "convergence", "Open Knowledge → Convergence.")]
    [InlineData("settings", "plugins", null, "plugins", null, null, "Open Plugins.")]
    [InlineData("settings", "workspace", null, "projects", null, "workspace-details", "Open Repositories → a workspace's Details.")]
    [InlineData("settings", "workspace", "wiring", "projects", null, "workspace-remote", "Open Repositories → a workspace's Remote and reach.")]
    [InlineData("settings", "workspace", "lines", "projects", null, "workspace-defaults", "Open Repositories → a workspace's Defaults.")]
    [InlineData("settings", "workspace", "landing", "projects", null, "workspace-defaults", "Open Repositories → a workspace's Defaults.")]
    [InlineData("settings", "workspace", "sweep", "projects", null, "workspace-branches", "Open Repositories → a workspace's Branches.")]
    [InlineData("settings", "permissions", null, "agents", null, "rules", "Open Agents → What it may do.")]
    [InlineData("settings", "permissions", "across", "projects", null, "workspace-defaults", "Open Repositories → a workspace's Defaults.")]
    public void A_go_to_a_place_that_moved_lands_where_it_went(
        string view, string? domain, string? part, string now, string? nowDomain, string? nowPart, string says)
    {
        var plan = HelpProposals.Plan(Go(view, domain, part), DriverConfig.Empty, Machine);

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
        Assert.Equal(["start", "appearance", "ai", "driver", "browser", "logs"], HelpPlaces.Domains.Select(domain => domain.Id));
        Assert.Equal(
            [
                "sessions/waiting", "sessions/review", "quests/asks", "quests/held",
                "projects/add", "projects/import", "projects/setup",
                "projects/workspace-details", "projects/workspace-branches", "projects/workspace-workflow",
                "projects/workspace-setup", "projects/workspace-defaults", "projects/workspace-remote",
                "knowledge/search", "knowledge/convergence",
                "start/agent", "start/helper", "start/repositories", "start/driven", "start/landing", "start/rules",
                "agents/accounts", "agents/rules", "agents/usage",
            ],
            HelpPlaces.Parts.Select(part => $"{part.Within}/{part.Id}"));
        static string Spelled(HelpPlace place) => string.Join("/", new[] { place.View, place.Domain, place.Part }.OfType<string>());
        Assert.Equal(
            [
                "search → knowledge/search", "convergence → knowledge/convergence", "settings/plugins → plugins",
                "settings/workspace → projects/workspace-details", "settings/workspace/wiring → projects/workspace-remote",
                "settings/workspace/lines → projects/workspace-defaults", "settings/workspace/landing → projects/workspace-defaults",
                "settings/workspace/sweep → projects/workspace-branches", "settings/permissions → agents/rules",
                "settings/permissions/across → projects/workspace-defaults",
            ],
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
