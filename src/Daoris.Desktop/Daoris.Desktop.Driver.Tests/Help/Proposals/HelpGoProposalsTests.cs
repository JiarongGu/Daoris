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
    [InlineData("settings", "agents", null, "Open Settings → Agents.")]
    [InlineData("settings", "workspace", "lines", "Open Settings → Workspace → Lines.")]
    [InlineData("settings", "start", "helper", "Open Settings → Setup at step 2, Ask Daoris's agent.")]
    [InlineData("projects", null, "import", "Open Repositories → Import a folder.")]
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
    public void A_screen_the_window_does_not_have_is_refused(string view, string? domain, string? part, string says)
    {
        var plan = HelpProposals.Plan(Go(view, domain, part), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Go);
    }

    /// <summary>
    /// The places a go may name, as the page's twin (`help/places.ts`) holds them: the same views, Settings
    /// domains and parts, the setup guide's steps among them. The page's test holds the same table.
    /// </summary>
    [Fact]
    public void The_places_are_the_pages_twin()
    {
        Assert.Equal(["overview", "sessions", "quests", "projects", "map", "convergence", "search", "settings"], HelpPlaces.Views.Select(view => view.Id));
        Assert.Equal(["start", "appearance", "ai", "workspace", "driver", "agents", "permissions", "plugins", "browser", "logs"], HelpPlaces.Domains.Select(domain => domain.Id));
        Assert.Equal(
            [
                "projects/add", "projects/import",
                "start/agent", "start/helper", "start/repositories", "start/driven", "start/landing", "start/rules",
                "workspace/wiring", "workspace/lines", "workspace/landing", "workspace/sweep",
                "agents/usage", "permissions/proposals", "permissions/across",
            ],
            HelpPlaces.Parts.Select(part => $"{part.Within}/{part.Id}"));
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
