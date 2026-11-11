using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomPlaces"/>: every place a go may name, from the table a go is judged by.</summary>
public sealed class HelpRoomPlacesTests
{
    /// <summary>
    /// The places a go link may name are listed from the driver's twin of the page's table, so the room and the window
    /// that opens the link cannot disagree about which screens exist; the link is spelled as the page reads it.
    /// </summary>
    [Fact]
    public void The_room_lists_every_place_a_go_may_name()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("## Where you may take the person", agents);
        Assert.Contains("`[the words they read](daoris:go?view=<view>&…)`", agents);
        Assert.Contains("`[Settings → Git](daoris:go?view=settings&domain=git)`", agents);
        Assert.DoesNotContain("go_propose", agents);
        foreach (var (id, _) in HelpPlaces.Views) Assert.Contains($"`{id}`", agents);
        foreach (var (id, name) in HelpPlaces.Domains) Assert.Contains($"`{id}` ({name})", agents);
        foreach (var (_, id, name) in HelpPlaces.Parts) Assert.Contains($"`{id}` ({name})", agents);
        Assert.Contains("changes nothing", agents);
    }

    /// <summary>
    /// A go names no repository and no agent, so the room says where each part lands — Repositories'
    /// Setup on the repository its list has chosen, an agent's part on the agent that has it — and the helper says which
    /// one the person means rather than promising it. A workspace's parts open the page of the one in view.
    /// Add repository and Import a folder may name the workspace the drawer opens with, a name and
    /// never a folder, which stays the person's pick there.
    /// </summary>
    [Fact]
    public void The_room_says_a_go_names_no_repository_or_agent_and_a_workspace_only_to_add_or_import()
    {
        var places = new HelpRoomPlaces().Render(HelpRoomFixture.Machine);

        Assert.Contains("- Views: `overview` (Overview)", places);
        Assert.Contains("`agents` (Agents), `plugins` (Plugins), `settings` (Settings).", places);
        Assert.DoesNotContain("`agents` (Agents), `permissions`", places);
        Assert.Contains("A go names no repository or agent:", places);
        Assert.DoesNotContain("A go names no repository, agent or workspace", places);
        Assert.Contains("`setup` opens the Setup of the repository Repositories has chosen", places);
        Assert.Contains("`branches` and `history` open its Branches and History tabs", places);
        Assert.Contains("a part of `agents` opens the agent that has it", places);
        Assert.Contains("A `workspace-` part opens that tab or section on the page of the workspace in view", places);
        Assert.Contains("`add` and `import` may name the go's `workspace`, one this room lists or a new name:", places);
        Assert.Contains("the drawer opens with it filled, and the person picks the folder there and registers it.", places);
        Assert.Contains("Never name a folder.", places);
        // New conversation is a part of `sessions`, which may carry a repository and words.
        Assert.Contains("`new` (New conversation)", places);
        Assert.Contains("`sessions`'s `new` may name the go's `repository`, as this room's registry lists it with a checkout here, and its `sentence`:", places);
        Assert.Contains("the composer opens with them filled, and the person reads and sends them. With neither it opens empty.", places);
    }

    /// <summary>
    /// Every place the window has is one a go can name, so a workspace's page's four tabs and
    /// its Setup's two sections are parts of Repositories, prefixed where a repository's page shares their names. Settings →
    /// Workspace and Permissions are no longer parts of Settings, and the room offers neither: a go still spelled so is kept.
    /// </summary>
    [Fact]
    public void The_room_names_the_workspace_pages_parts_and_neither_retired_domain()
    {
        var places = new HelpRoomPlaces().Render(HelpRoomFixture.Machine);

        Assert.Contains(
            "- Parts of `projects`: `add` (Add repository), `import` (Import a folder), `setup` (a repository's Setup), "
            + "`branches` (a repository's Branches), `history` (a repository's History), "
            + "`workspace-details` (a workspace's Details), `workspace-branches` (a workspace's Branches), "
            + "`workspace-workflow` (a workspace's Workflow), "
            + "`workspace-defaults` (a workspace's Defaults), `workspace-remote` (a workspace's Remote and reach).",
            places);
        Assert.Contains(
            "- Settings domains: `start` (Get started), `appearance` (Appearance), `driver` (Driver), `ai` (AI features), `git` (Git), "
            + "`browser` (Browser), `space` (Disk space), `updates` (Updates), `wiring` (This Daoris), `logs` (Machine log).",
            places);
        Assert.DoesNotContain("Parts of `workspace`", places);
        Assert.DoesNotContain("Parts of `permissions`", places);
        Assert.DoesNotContain("`workspace` (Workspace)", places);
        Assert.DoesNotContain("`permissions` (Permissions)", places);
    }

    /// <summary>
    /// A quest or a session keeps its own conversation, so Ask Daoris takes the person to what
    /// waits on them there. The room names Sessions' and Quests' groups by their headings, says a part brings its group into
    /// view and names no session or quest, and gives Overview none, since what waits on the person leads it.
    /// </summary>
    [Fact]
    public void The_room_names_the_groups_that_wait_on_the_person_and_says_a_go_names_no_item_in_them()
    {
        var places = new HelpRoomPlaces().Render(HelpRoomFixture.Machine);

        Assert.Contains("- Parts of `sessions`: `waiting` (Waiting on you), `review` (To review), `new` (New conversation).", places);
        Assert.Contains("- Parts of `quests`: `asks` (Asks), `held` (Waiting on you).", places);
        Assert.Contains("A part of `sessions` or `quests` brings that group of its list into view and names no session or quest", places);
        Assert.Contains("`overview` has no part", places);
        Assert.DoesNotContain("Parts of `overview`", places);
    }

    /// <summary>
    /// To take the person to the one quest or ask that waits, a go on Quests names it as its
    /// item, with no part: a quest by its id and an ask as <c>ask:&lt;id&gt;</c>, each as the room lists it, so the helper
    /// names only what it is shown.
    /// </summary>
    /// <remarks>
    /// And the one session, on <c>sessions</c> by its id: one the room lists as waiting on the person, or another
    /// the person names, never Ask Daoris's own conversation.
    /// </remarks>
    [Fact]
    public void The_room_says_a_go_may_name_one_session_quest_or_ask_as_its_item()
    {
        var places = new HelpRoomPlaces().Render(HelpRoomFixture.Machine);

        Assert.Contains("To open one session, quest, ask or plugin, name it as the go's item on its view, with no part:", places);
        Assert.Contains("on `sessions` a session by its id, one this room lists as waiting on the person or another the person names,", places);
        Assert.Contains("never Ask Daoris's own conversation; on `quests` a quest by its id, an ask as `ask:<id>`, each one this room lists;", places);
        // A plugin by its id, an offer as `offer:<id>`.
        Assert.Contains("on `plugins` an installed plugin by its id, or one of Daoris's own not installed as `offer:<id>`, by the ids this room lists.", places);
    }

    /// <summary>
    /// The room names the places as the window has them, Knowledge with its two modes as its parts and
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
        Assert.DoesNotContain(HelpPlaces.Views, view => view.Id is "search" or "convergence");
        Assert.DoesNotContain(HelpPlaces.Domains, domain => domain.Id is "plugins" or "workspace" or "permissions");
    }
}
