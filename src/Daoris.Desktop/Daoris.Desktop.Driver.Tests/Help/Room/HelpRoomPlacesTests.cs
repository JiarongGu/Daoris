using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomPlaces"/>: every place a go may name, from the table a go is judged by.</summary>
public sealed class HelpRoomPlacesTests
{
    /// <summary>
    /// HELP6: the places `go_propose` may name are listed from the driver's own table, the one it judges a
    /// go by — so the room and the judge cannot disagree about which screens exist.
    /// </summary>
    [Fact]
    public void The_room_lists_every_place_a_go_may_name()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("## Where you may take the person", agents);
        foreach (var (id, _) in HelpPlaces.Views) Assert.Contains($"`{id}`", agents);
        foreach (var (id, name) in HelpPlaces.Domains) Assert.Contains($"`{id}` ({name})", agents);
        foreach (var (_, id, name) in HelpPlaces.Parts) Assert.Contains($"`{id}` ({name})", agents);
        Assert.Contains("changes nothing", agents);
    }

    /// <summary>
    /// UX6e2 and HELPSETUP1: a go names no repository and no agent, so the room says where each part lands — Repositories'
    /// Setup on the repository its list has chosen, an agent's part on the agent that has it — and the helper says which
    /// one the person means rather than promising it. UX6g2b: nor a workspace, whose parts open the page of the one in view.
    /// </summary>
    [Fact]
    public void The_room_says_a_go_names_no_repository_agent_or_workspace()
    {
        var places = new HelpRoomPlaces().Render(HelpRoomFixture.Machine);

        Assert.Contains("- Views: `overview` (Overview)", places);
        Assert.Contains("`agents` (Agents), `plugins` (Plugins), `settings` (Settings).", places);
        Assert.DoesNotContain("`agents` (Agents), `permissions`", places);
        Assert.Contains("A go names no repository, agent or workspace", places);
        Assert.Contains("`setup` opens the Setup of the repository Repositories has chosen", places);
        Assert.Contains("a part of `agents` opens the agent that has it", places);
        Assert.Contains("A `workspace-` part opens that tab or section on the page of the workspace in view", places);
    }

    /// <summary>
    /// UX6g2b (D161 §3, D150 §4.3): every place the window has is one a go can name, so a workspace's page's four tabs and
    /// its Setup's two sections are parts of Repositories, prefixed where a repository's page shares their names. Settings →
    /// Workspace and Permissions left Settings with UX6g, and the room offers neither: a go still spelled so is kept.
    /// </summary>
    [Fact]
    public void The_room_names_the_workspace_pages_parts_and_neither_retired_domain()
    {
        var places = new HelpRoomPlaces().Render(HelpRoomFixture.Machine);

        Assert.Contains(
            "- Parts of `projects`: `add` (Add repository), `import` (Import a folder), `setup` (a repository's Setup), "
            + "`workspace-details` (a workspace's Details), `workspace-branches` (a workspace's Branches), "
            + "`workspace-workflow` (a workspace's Workflow), `workspace-setup` (a workspace's Setup), "
            + "`workspace-defaults` (a workspace's Defaults), `workspace-remote` (a workspace's Remote and reach).",
            places);
        Assert.Contains(
            "- Settings domains: `start` (Get started), `appearance` (Appearance), `ai` (AI features), `driver` (Driver), "
            + "`browser` (Browser), `logs` (Machine log).",
            places);
        Assert.DoesNotContain("Parts of `workspace`", places);
        Assert.DoesNotContain("Parts of `permissions`", places);
        Assert.DoesNotContain("`workspace` (Workspace)", places);
        Assert.DoesNotContain("`permissions` (Permissions)", places);
    }

    /// <summary>
    /// ENTRY1b (D161's ENTRY1 note): a quest or a session keeps its own conversation, so Ask Daoris takes the person to what
    /// waits on them there. The room names Sessions' and Quests' groups by their headings, says a part brings its group into
    /// view and names no session or quest, and gives Overview none, since what waits on the person leads it.
    /// </summary>
    [Fact]
    public void The_room_names_the_groups_that_wait_on_the_person_and_says_a_go_names_no_item_in_them()
    {
        var places = new HelpRoomPlaces().Render(HelpRoomFixture.Machine);

        Assert.Contains("- Parts of `sessions`: `waiting` (Waiting on you), `review` (To review).", places);
        Assert.Contains("- Parts of `quests`: `asks` (Asks), `held` (Waiting on you).", places);
        Assert.Contains("A part of `sessions` or `quests` brings that group of its list into view and names no session or quest", places);
        Assert.Contains("`overview` has no part", places);
        Assert.DoesNotContain("Parts of `overview`", places);
    }

    /// <summary>
    /// UX6i2a (D150 §2): the room names the places as the window has them, Knowledge with its two modes as its parts and
    /// Plugins as a view, and lists no place that moved: a go still spelled so lands, but the helper is never offered it.
    /// </summary>
    [Fact]
    public void The_room_names_the_places_as_they_are_and_none_that_moved()
    {
        var places = new HelpRoomPlaces().Render(HelpRoomFixture.Machine);

        Assert.Contains("`map` (Map), `knowledge` (Knowledge), `agents` (Agents)", places);
        Assert.Contains("- Parts of `knowledge`: `search` (Search), `convergence` (Convergence).", places);
        Assert.Contains("`knowledge` alone opens Knowledge as the person left it", places);
        Assert.Contains("- Settings domains: `start` (Get started), ", places);
        Assert.DoesNotContain("`plugins` (Plugins), `browser`", places);
        Assert.DoesNotContain("`search` (Search), `agents`", places);
        foreach (var (was, _) in HelpPlaces.Kept)
        {
            Assert.DoesNotContain(HelpPlaces.Views, view => view.Id == was.View && was.Domain is null);
            Assert.DoesNotContain(HelpPlaces.Domains, domain => domain.Id == was.Domain);
        }
    }
}
