using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>go</c> proposal (HELP6): a place the window has, which changes nothing.</summary>
public sealed class HelpGoProposalsTests : HelpProposalsFixture
{
    private static HelpProposal Go(string view, string? domain = null, string? part = null, string? item = null, string? workspace = null) =>
        Of("go", "go", view) with { Domain = domain, Part = part, Item = item, Workspace = workspace };

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
    // Setup is; a go to them names no workspace, and the one in view opens.
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
    /// ENTRY1f1 (D161's ENTRY1f note): a go on Quests may name the one quest, by its id, or the one ask, as
    /// <c>ask:&lt;id&gt;</c>, that waits on the person, judged against the records the <c>delete</c> kind reads: every quest
    /// and ask, closed ones included, so a person asking where one went is taken to it and told it closed. A <c>#</c> before
    /// the id is the room's spelling and stripped, the id matched without case, and the page handed the record's own.
    /// </summary>
    [Theory]
    [InlineData("q1a2b3c4", "q1a2b3c4", "Open Quests → quest `#q1a2b3c4` “Cap the chunk budget”.")]
    [InlineData(" #Q1A2B3C4 ", "q1a2b3c4", "Open Quests → quest `#q1a2b3c4` “Cap the chunk budget”.")]
    [InlineData("ask:a2none00", "ask:a2none00", "Open Quests → ask `#a2none00` “a test ask”.")]
    [InlineData("ask:#A2NONE00", "ask:a2none00", "Open Quests → ask `#a2none00` “a test ask”.")]
    [InlineData("q3done00", "q3done00", "Open Quests → quest `#q3done00` “Fix the stall”, done.")]
    [InlineData("q4declin", "q4declin", "Open Quests → quest `#q4declin` “Rewrite it all”, declined.")]
    public void A_go_may_name_one_quest_or_ask_the_machine_holds(string item, string handed, string says)
    {
        var plan = HelpProposals.Plan(Go("quests", item: item), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(says, plan.Describe);
        Assert.Equal(new HelpPlace("quests", null, null) { Item = handed }, plan.Go);
    }

    /// <summary>ENTRY1f1: a closed ask is the person's to find as a closed quest is, and the card says it closed.</summary>
    [Fact]
    public void A_go_to_a_closed_ask_says_it_closed()
    {
        var facts = Machine with { Asks = [new HelpAskFacts("a4shut00", "an ask set aside", "work", "Closed", [], Deletable: true)] };

        var plan = HelpProposals.Plan(Go("quests", item: "ask:a4shut00"), DriverConfig.Empty, facts);

        Assert.Equal("Open Quests → ask `#a4shut00` “an ask set aside”, closed.", plan.Describe);
    }

    /// <summary>
    /// ENTRY1f1: an item the machine's records do not hold is refused on the card, in the <c>delete</c> kind's words, never
    /// handed to the page as a dead place; an ask's id named as a quest's is told its spelling. An item is a quest or an ask
    /// on Quests alone, with no part beside it, and one of another shape is said so.
    /// </summary>
    [Theory]
    [InlineData("quests", null, "q9", "there is no quest `#q9` on this machine.")]
    [InlineData("quests", null, "ask:a9", "there is no ask `#a9` on this machine.")]
    [InlineData("quests", null, "#a2none00", "there is no quest `#a2none00` on this machine — `#a2none00` is an ask: name it as `ask:a2none00`.")]
    [InlineData("quests", null, "#", "`#` is no item of `quests` — a quest by its id, or an ask as `ask:<id>`.")]
    [InlineData("quests", null, "ask:", "`ask:` is no item of `quests` — a quest by its id, or an ask as `ask:<id>`.")]
    [InlineData("quests", null, "session:s1", "`session:s1` is no item of `quests` — a quest by its id, or an ask as `ask:<id>`.")]
    [InlineData("quests", "held", "q1a2b3c4", "a go names a part of `quests` or an item in it, not both")]
    // ENTRY1f2: an item is named on Sessions too, and on no other view.
    [InlineData("overview", null, "ask:a2none00", "a go names an item only on `quests`, `sessions` — on Sessions a session by its id")]
    public void An_item_the_machine_does_not_hold_or_cannot_be_is_refused(string view, string? part, string item, string says)
    {
        var plan = HelpProposals.Plan(Go(view, part: part, item: item), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Go);
    }

    /// <summary>
    /// The machine's own session records, as the Sessions list places them (ENTRY1f2): a park waiting on the person, one
    /// working, one ended, a park the person answered, an intake's park, Ask Daoris's own conversation and a teammate's park.
    /// </summary>
    private static readonly HelpMachineFacts WithSessions = Machine with
    {
        Sessions =
        [
            new SessionRecord("s1a2b3c4", "console-ui", "awaiting-person") { Quest = "q1a2b3c4" },
            new SessionRecord("s2b3c4d5", "engine", "working") { Quest = "q2taken0" },
            new SessionRecord("s3c4d5e6", "game", "completed") { Quest = "q3done00" },
            new SessionRecord("s4d5e6f7", "engine", "awaiting-person") { Quest = "q5start0", Answer = "Port 8080." },
            new SessionRecord("s7a8b9c0", "ask #a2none00", "awaiting-person") { Ask = "a2none00" },
            new SessionRecord("s5e6f7a8", HelpRoom.Repository, "awaiting-person") { Kind = "chat" },
            new SessionRecord("laptop/s6f7a8b9", "engine", "awaiting-person"),
        ],
    };

    /// <summary>
    /// ENTRY1f2 (D161's ENTRY1f note): a go on Sessions may name one session by its id, judged against the machine's own
    /// records, since only the desktop knows its sessions. Any the Sessions list shows may be named, the one waiting on the
    /// person or another the person asks for; a <c>#</c> before the id is stripped, the id matched without case, and the
    /// page handed the record's own. The card says where it ran and whether it waits on the person or ended.
    /// </summary>
    [Theory]
    [InlineData("s1a2b3c4", "s1a2b3c4", "Open Sessions → session `s1a2b3c4` in `console-ui`, waiting on you.")]
    [InlineData(" #S1A2B3C4 ", "s1a2b3c4", "Open Sessions → session `s1a2b3c4` in `console-ui`, waiting on you.")]
    [InlineData("s2b3c4d5", "s2b3c4d5", "Open Sessions → session `s2b3c4d5` in `engine`.")]
    [InlineData("s3c4d5e6", "s3c4d5e6", "Open Sessions → session `s3c4d5e6` in `game`, completed.")]
    // An answered park goes on at the driver's next look, so it waits on nobody (ANSWER1c), as the list says.
    [InlineData("s4d5e6f7", "s4d5e6f7", "Open Sessions → session `s4d5e6f7` in `engine`.")]
    [InlineData("s7a8b9c0", "s7a8b9c0", "Open Sessions → session `s7a8b9c0` answering ask `#a2none00`, waiting on you.")]
    public void A_go_may_name_one_session_the_machine_holds(string item, string handed, string says)
    {
        var plan = HelpProposals.Plan(Go("sessions", item: item), DriverConfig.Empty, WithSessions);

        Assert.Null(plan.Refusal);
        Assert.Equal(says, plan.Describe);
        Assert.Equal(new HelpPlace("sessions", null, null) { Item = handed }, plan.Go);
    }

    /// <summary>
    /// ENTRY1f2: a session the machine's records do not hold is refused on the card, as Ask Daoris's own conversation is,
    /// which opens in Ask Daoris and not in Sessions, and a teammate's, which nothing here reaches. An item on Sessions is a
    /// bare id: the prefixes Quests' items take are no item of it, and a part beside it is refused as on Quests.
    /// </summary>
    [Theory]
    [InlineData(null, "s9", "there is no session `s9` on this machine.")]
    [InlineData(null, "S5E6F7A8", "`s5e6f7a8` is Ask Daoris's own conversation; it opens here, not in Sessions.")]
    [InlineData(null, "laptop/s6f7a8b9", "`laptop/s6f7a8b9` is a teammate's session: it runs on their machine, and nothing here reaches it.")]
    [InlineData(null, "ask:a2none00", "`ask:a2none00` is no item of `sessions` — a session by its id.")]
    [InlineData(null, "session:s1a2b3c4", "`session:s1a2b3c4` is no item of `sessions` — a session by its id.")]
    [InlineData(null, "#", "`#` is no item of `sessions` — a session by its id.")]
    [InlineData("waiting", "s1a2b3c4", "a go names a part of `sessions` or an item in it, not both")]
    public void A_session_the_machine_does_not_hold_or_the_person_cannot_open_there_is_refused(string? part, string item, string says)
    {
        var plan = HelpProposals.Plan(Go("sessions", part: part, item: item), DriverConfig.Empty, WithSessions);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Go);
    }

    /// <summary>The registry as Repositories lists it (ENTRY1d2a): `engine` in `work`, and `game` in none, so the default.</summary>
    private static readonly HelpMachineFacts WithRegistry = Machine with
    {
        Registered = [("engine", "work", "/checkouts/engine"), ("game", null, null)],
    };

    /// <summary>
    /// ENTRY1d2a (D161's ENTRY1d note): a go to Repositories' Add repository or Import a folder may name the workspace the
    /// drawer opens with, since the folder stays the person's pick there. It is normalized as the route stores a name, a
    /// workspace the registry holds handed in the registry's spelling, and a name no repository is in yet allowed, as the
    /// drawer's free text allows it, the card saying so.
    /// </summary>
    [Theory]
    [InlineData("add", "work", "work", "Open Repositories → Add repository, in workspace `work`.")]
    [InlineData("import", " WORK ", "work", "Open Repositories → Import a folder, in workspace `work`.")]
    [InlineData("add", "Default", "default", "Open Repositories → Add repository, in workspace `default`.")]
    [InlineData("import", " Team Alpha ", "Team Alpha",
        "Open Repositories → Import a folder, in workspace `Team Alpha`, which no repository is in yet.")]
    public void A_go_to_add_or_import_may_name_the_workspace(string part, string workspace, string handed, string says)
    {
        var plan = HelpProposals.Plan(Go("projects", part: part, workspace: workspace), DriverConfig.Empty, WithRegistry);

        Assert.Null(plan.Refusal);
        Assert.Equal(says, plan.Describe);
        Assert.Equal(new HelpPlace("projects", null, part) { Workspace = handed }, plan.Go);
    }

    /// <summary>
    /// ENTRY1d2a: a workspace is named on Repositories' Add and Import alone, the drawers that take one, never with an item,
    /// and never as a folder: a path the conversation was not given never enters it (D48 §3/§7).
    /// </summary>
    [Theory]
    [InlineData("projects", null, null, null, "work", "a go names a workspace only with `projects` and its part `add` or `import`")]
    [InlineData("projects", null, "setup", null, "work", "a go names a workspace only with `projects` and its part `add` or `import`")]
    [InlineData("projects", null, "workspace-details", null, "work", "a go names a workspace only with `projects` and its part `add` or `import`")]
    [InlineData("settings", "start", "repositories", null, "work", "a go names a workspace only with `projects` and its part `add` or `import`")]
    [InlineData("quests", null, null, "q1a2b3c4", "work", "a go names a workspace or an item, not both")]
    [InlineData("projects", null, "add", null, "C:\\work", "`C:\\work` is a folder, never a workspace's name")]
    [InlineData("projects", null, "import", null, "work/engine", "`work/engine` is a folder, never a workspace's name")]
    [InlineData("projects", null, "add", null, "D:", "`D:` is a folder, never a workspace's name")]
    public void A_workspace_anywhere_but_add_or_import_or_as_a_folder_is_refused(
        string view, string? domain, string? part, string? item, string workspace, string says)
    {
        var plan = HelpProposals.Plan(Go(view, domain, part, item, workspace), DriverConfig.Empty, WithRegistry);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Go);
    }

    /// <summary>ENTRY1f2: a session named on Quests is no quest there, and a quest named on Sessions no session.</summary>
    [Fact]
    public void An_item_is_judged_against_its_own_views_records()
    {
        Assert.Equal("there is no quest `#s1a2b3c4` on this machine.",
            HelpProposals.Plan(Go("quests", item: "s1a2b3c4"), DriverConfig.Empty, WithSessions).Refusal);
        Assert.Equal("there is no session `q1a2b3c4` on this machine.",
            HelpProposals.Plan(Go("sessions", item: "q1a2b3c4"), DriverConfig.Empty, WithSessions).Refusal);
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
        // ENTRY1f1: the views a go may name an item in, and how an ask's item is told from a quest's. ENTRY1f2: Sessions too.
        Assert.Equal(["sessions", "quests"], HelpPlaces.ItemViews);
        Assert.Equal("ask:", HelpPlaces.AskItem);
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

    /// <summary>ENTRY1f1: a go naming an ask hands the page the place with the ask's item, as the page's list names one.</summary>
    [Fact]
    public async Task A_go_naming_an_ask_hands_the_page_its_item()
    {
        var (go, doors, _) = await ApplyAsync(Go("quests", item: "ask:#a2none00"));

        Assert.True(go.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal(new HelpPlace("quests", null, null) { Item = "ask:a2none00" }, go.Go);
    }

    /// <summary>ENTRY1f2: a go naming a session hands the page the place with the session's id, as its record spells it.</summary>
    [Fact]
    public async Task A_go_naming_a_session_hands_the_page_its_id()
    {
        var (go, doors, _) = await ApplyAsync(Go("sessions", item: "#S1A2B3C4"), facts: WithSessions);

        Assert.True(go.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal(new HelpPlace("sessions", null, null) { Item = "s1a2b3c4" }, go.Go);
    }

    /// <summary>ENTRY1d2a: a go to Add repository hands the page the workspace the drawer opens with, and calls no door.</summary>
    [Fact]
    public async Task A_go_to_add_hands_the_page_its_workspace()
    {
        var (go, doors, _) = await ApplyAsync(Go("projects", part: "add", workspace: " Work "), facts: WithRegistry);

        Assert.True(go.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal(new HelpPlace("projects", null, "add") { Workspace = "work" }, go.Go);
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
        Assert.Null(go.Item);
        Assert.Null(go.Account);
    }

    /// <summary>ENTRY1f1: a go's item, read from the file as the service's box writes it, spelled as named.</summary>
    [Fact]
    public void A_gos_item_is_read_from_the_file()
    {
        var node = new JsonObject
        {
            ["id"] = "k3", ["proposed"] = "2026-10-11T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "go", ["door"] = "go", ["target"] = "quests", ["workspace"] = null, ["value"] = null,
            ["sentence"] = null, ["domain"] = null, ["part"] = null, ["item"] = "ask:#a2none00",
            ["why"] = "the person asked where their ask went", ["state"] = "proposed", ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "k3.json"), node.ToJsonString());

        var go = Assert.Single(HelpProposals.Pending(_home, "h1"), proposal => proposal.Kind == "go");

        Assert.Equal("ask:#a2none00", go.Item);
    }

    /// <summary>
    /// ENTRY1d2a: a go's workspace is the file's <c>workspace</c>, which every proposal's reader already reads, spelled as
    /// written, so the kind's own reader adds nothing for it.
    /// </summary>
    [Fact]
    public void A_gos_workspace_is_read_from_the_file()
    {
        var node = new JsonObject
        {
            ["id"] = "k4", ["proposed"] = "2026-10-11T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "go", ["door"] = "go", ["target"] = "projects", ["workspace"] = "Team Alpha", ["value"] = null,
            ["sentence"] = null, ["domain"] = null, ["part"] = "import", ["item"] = null,
            ["why"] = "the person asked to import a folder into it", ["state"] = "proposed", ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "k4.json"), node.ToJsonString());

        var go = Assert.Single(HelpProposals.Pending(_home, "h1"), proposal => proposal.Kind == "go");

        Assert.Equal(("import", "Team Alpha"), (go.Part, go.Workspace));
        Assert.Null(go.Item);
    }
}
