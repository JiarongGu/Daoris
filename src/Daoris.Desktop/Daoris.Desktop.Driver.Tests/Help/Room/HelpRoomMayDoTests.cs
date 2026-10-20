using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomMayDo"/>: it reads, advises and proposes, and routes what it cannot reach.</summary>
public sealed class HelpRoomMayDoTests
{
    /// <summary>
    /// HELP4: asked to tidy a repository's branches, the helper's first move was a shell command, refused
    /// before it ran, and it then rebuilt the repository's branches from its quests and presented the
    /// guess as the tree. The room says it has no shell; that a repository's own work is routed there, as
    /// an ask or a conversation in that repository; that what it could not see is said as such; and it
    /// points at the cleanup the person wanted, which is a door of Daoris's own.
    /// HELP5: its fourth move on a real conversation was a fetch of a ticket's URL, refused the same way,
    /// so the same sentence says it has no web either.
    /// </summary>
    [Fact]
    public void The_room_says_it_has_no_shell_nor_web_and_routes_a_repositorys_own_work_there()
    {
        foreach (var machine in new[] { HelpRoomFixture.Machine, HelpRoomFixture.Machine with { Reads = [] } })
        {
            var agents = HelpRoom.Render(machine);

            Assert.Contains("You have no shell", agents);
            Assert.Contains("no web fetch or search", agents);
            Assert.Contains("never try one", agents);
            Assert.Contains("a repository's own work", agents);
            Assert.Contains("`ask_propose`", agents);
            // NEW1d-a: NEW1b removed Start a session; a conversation is begun with New conversation.
            Assert.Contains("New conversation (Ctrl+N)", agents);
            Assert.DoesNotContain("Start a session", agents);
            Assert.Contains("say what you could not see", agents);
            Assert.Contains("Session branches", agents);
        }
    }

    /// <summary>
    /// D107, as HELPMODE1c4 serves it: with reading across on, the room names each checkout it may read, by name and workspace and
    /// never a path, and how: <c>checkout_read</c> alone, a repository and a path from its top, a file or a folder, <c>status</c> and
    /// <c>branches</c>, each read judged by Daoris; and that it changes nothing there. No <c>git -C</c>, which it has no rule for.
    /// </summary>
    [Fact]
    public void With_reading_on_the_room_says_which_checkouts_it_may_read_and_how()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("You may read the checkouts below, and change nothing in them, with `checkout_read` alone", agents);
        Assert.Contains("- `console-ui` (workspace `work`)\n", agents);
        Assert.Contains("- `reports-db` (workspace `work`)\n", agents);
        Assert.Contains("`status`", agents);
        Assert.Contains("`branches`", agents);
        Assert.Contains("anything git ignores", agents);
        Assert.DoesNotContain("/work/console-ui", agents);
        Assert.DoesNotContain("git -C", agents);
        Assert.DoesNotContain("you read no checkout", agents);
    }

    /// <summary>
    /// HELPMODE1c4: a checkout the person turned off under configured access is not listed, and is named apart with where it is turned
    /// on; every one turned off reads as none to read, said so, never as reading switched off by the repositories.
    /// </summary>
    [Fact]
    public void A_checkout_turned_off_is_named_apart_and_every_one_off_reads_as_none()
    {
        var some = HelpRoom.Render(HelpRoomFixture.Machine with { CheckoutsOff = ["reports-db"] });

        Assert.Contains("- `console-ui` (workspace `work`)\n", some);
        Assert.DoesNotContain("- `reports-db`", some);
        Assert.Contains("The person turned reading off for you in `reports-db` under Ask Daoris's configured access", some);

        var none = HelpRoom.Render(HelpRoomFixture.Machine with { CheckoutsOff = ["console-ui", "Reports-DB"] });
        Assert.Contains("you read no checkout", none);
        Assert.Contains("turned reading off for you in every checkout here", none);
        Assert.Contains("Settings → AI features", none);
        Assert.DoesNotContain("Reading the checkouts here is switched off", none);
    }

    /// <summary>
    /// D107: with checkouts here and none it may read, the room says it reads none and why — reading is
    /// switched off — naming both doors, so the person knows where it is turned on.
    /// </summary>
    [Fact]
    public void With_reading_off_everywhere_the_room_says_so_and_names_both_doors()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine with { Reads = [] });

        Assert.Contains("you read no checkout", agents);
        Assert.Contains("Reading the checkouts here is switched off", agents);
        // HELPSETUP1: a repository's reading is on its Setup since UX6f; a workspace's is on its own page since UX6g.
        Assert.Contains("for a repository under\nRepositories → the repository's page → Setup → Reach", agents);
        Assert.Contains("for a whole workspace under\nRepositories → the workspace's page → Setup → Defaults → Read by agents outside", agents);
        Assert.Contains("`daoris driver across <repository> read on`", agents);
    }

    /// <summary>A machine with no checkout here has nothing switched off to speak of.</summary>
    [Fact]
    public void With_no_checkout_here_nothing_is_said_about_reading()
    {
        var agents = HelpRoom.Render(new HelpMachine());

        Assert.Contains("you read no checkout", agents);
        Assert.DoesNotContain("switched off", agents);
    }

    /// <summary>
    /// HELPPLUG1a (D170 point 3, and its tightening): the servers plugins hand it, as the person approved them, are named with the
    /// tools it may call on each, a write outside Daoris said as one the person accepted; what such a tool does is the plugin's
    /// act, and a change to Daoris itself is still a proposal. One waiting for the person's approval is named as handed nothing.
    /// </summary>
    [Fact]
    public void The_room_names_the_servers_plugins_hand_it_and_the_tools_it_may_call()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            PluginServers =
            [
                new("acme.tickets", "tickets", ["search", "get_issue"]) { Writes = ["create_issue"] }, new("acme.docs", "docs", []),
            ],
            PluginServersWaiting = [new("acme.wiki", "wiki", ["lookup"])],
        });

        Assert.Contains("Plugins installed here hand you these servers, as the person approved them", agents);
        Assert.Contains(
            "- `tickets` (plugin `acme.tickets`): reads with `search`, `get_issue`; changes things outside Daoris with `create_issue`, "
            + "a write the person accepted", agents);
        Assert.Contains("- `docs` (plugin `acme.docs`): none of its tools", agents);
        Assert.Contains("the plugin's own act", agents);
        Assert.Contains("a change to Daoris itself is still a proposal", agents);
        Assert.Contains("you are handed none of them until the person approves", agents);
        Assert.Contains("- `wiki` (plugin `acme.wiki`): would read with `lookup`", agents);
    }

    /// <summary>
    /// HELPPLUG1b (D170 point 4): the skills plugins hand it are named as the harness names them, <c>plugin:skill</c>; with none,
    /// nothing is said. A skill declared for sessions alone, one with no skill file, and one that would allow itself tools are
    /// not among them.
    /// </summary>
    [Fact]
    public void The_room_names_the_skills_plugins_hand_it()
    {
        Assert.Contains(
            "Plugins installed here also hand you these skills: `acme.tickets:triage`, `acme.tickets:write-up`.",
            HelpRoom.Render(HelpRoomFixture.Machine with { PluginSkills = ["acme.tickets:triage", "acme.tickets:write-up"] }));
        Assert.DoesNotContain("hand you these skills", HelpRoom.Render(HelpRoomFixture.Machine));

        var home = Path.Combine(Path.GetTempPath(), "daoris-help-skills-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var folder = DaorisHome.Entry(home, HomeEntry.Plugins, "acme.tickets");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
                { "id": "acme.tickets", "skills": [ { "name": "triage", "to": ["help"] }, { "name": "write-up" },
                    { "name": "wide", "to": ["help"] }, { "name": "missing", "to": ["help"] } ] }
                """);
            foreach (var (skill, text) in new[] { ("triage", "---\nname: triage\n---\n"), ("write-up", "---\nname: write-up\n---\n"), ("wide", "---\nallowed-tools: Bash\n---\n") })
            {
                Directory.CreateDirectory(Path.Combine(folder, "skills", skill));
                File.WriteAllText(Path.Combine(folder, "skills", skill, SpawnSkills.SkillFile), text);
            }

            var machine = HelpRoom.Describe(
                DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0, plugins: PluginCatalog.Load(home));

            Assert.Equal(["acme.tickets:triage"], machine.PluginSkills);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>With no plugin handing it a server, nothing is said of one.</summary>
    [Fact]
    public void With_no_plugin_server_nothing_is_said_of_one()
    {
        Assert.DoesNotContain("hand you these servers", HelpRoom.Render(HelpRoomFixture.Machine));
        Assert.DoesNotContain("Not yours yet", HelpRoom.Render(HelpRoomFixture.Machine));
    }

    /// <summary>
    /// HELPPLUG1c: a server a plugin offers it that the person has not approved, or whose tools an update changed since, is never
    /// taken away in silence. The room names what it would have, reads and writes apart, says it once at the start, and where it is
    /// approved: the plugin's page, or its own proposal (`plugin_propose`'s `approve`), rather than only opening the page. One the
    /// installed copy lacks and the install's newer copy offers is said with its update first.
    /// </summary>
    [Fact]
    public void A_server_waiting_for_approval_is_said_once_at_the_start_with_what_it_would_have_and_where_it_is_approved()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            PluginServersWaiting =
            [
                new("in-app-browser", "browser", ["browser_navigate", "browser_snapshot"]) { Writes = ["browser_click"] },
                new("acme.wiki", "wiki", ["lookup"]) { Changed = true },
            ],
            PluginServersOffered = [new HelpPluginServerOffered("acme.pages", "pages", "1.0.0", "1.1.0")],
        });

        Assert.Contains("### Not yours yet", room);
        Assert.Contains("- `browser` (plugin `in-app-browser`): would read with `browser_navigate`, `browser_snapshot`; would change "
            + "things outside Daoris with `browser_click`, each write only as the person accepts it", room);
        Assert.Contains("- `wiki` (plugin `acme.wiki`): would read with `lookup`; approved once, and an update has changed its tools since, so it asks again", room);
        Assert.Contains("- `pages` (plugin `acme.pages`): this install's own `acme.pages` 1.1.0 offers it you, and the 1.0.0 installed "
            + "here does not, so it is updated first (`plugin_propose`, `update`), then approved", room);
        Assert.Contains("Say so once, at the start of your first answer", room);
        Assert.Contains("Plugins → the plugin's page → Ask Daoris → Approve", room);
        Assert.Contains("propose the approval with `plugin_propose` (`approve`", room);
        Assert.DoesNotContain("These servers wait for the person's approval", room);
    }

    /// <summary>
    /// HELPPLUG1c: the waiting servers are described from the person's approvals, each write apart and one changed since marked;
    /// an installed offer whose newer copy offers a server the installed one lacks is named with both versions.
    /// </summary>
    [Fact]
    public void Waiting_servers_and_ones_an_update_would_bring_are_described_from_the_approvals_and_the_offers()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-help-waiting-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var installed = DaorisHome.Entry(home, HomeEntry.Plugins, "acme.tools");
            Directory.CreateDirectory(installed);
            File.WriteAllText(Path.Combine(installed, PluginCatalog.ManifestName), """
                { "id": "acme.tools", "version": "1.0.0", "servers": [
                    { "name": "wiki", "command": ["wiki"], "to": ["help"], "allow": ["lookup"], "writes": ["edit_page"] } ] }
                """);
            var old = DaorisHome.Entry(home, HomeEntry.Plugins, "in-app-browser");
            Directory.CreateDirectory(old);
            File.WriteAllText(Path.Combine(old, PluginCatalog.ManifestName), """
                { "id": "in-app-browser", "version": "1.0.0", "servers": [ { "name": "browser", "command": ["b"] } ] }
                """);
            var shipped = Path.Combine(home + "-offers", "in-app-browser");
            Directory.CreateDirectory(shipped);
            File.WriteAllText(Path.Combine(shipped, PluginCatalog.ManifestName), """
                { "id": "in-app-browser", "version": "1.1.0", "servers": [
                    { "name": "browser", "command": ["b"], "to": ["sessions", "help"], "allow": ["browser_navigate"] } ] }
                """);
            var catalog = PluginCatalog.Load(home);
            HelpGrants.Approve(home, catalog, "acme.tools", "wiki", readsOnly: true);
            File.WriteAllText(Path.Combine(installed, PluginCatalog.ManifestName), """
                { "id": "acme.tools", "version": "1.1.0", "servers": [
                    { "name": "wiki", "command": ["wiki"], "to": ["help"], "allow": ["lookup", "list_pages"], "writes": ["edit_page"] } ] }
                """);
            catalog = PluginCatalog.Load(home);
            var (manifest, _) = PluginCatalog.ReadAsWritten("in-app-browser", Path.Combine(shipped, PluginCatalog.ManifestName));

            var machine = HelpRoom.Describe(
                DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0, plugins: catalog,
                offers: [new PluginOffer("in-app-browser", shipped, manifest!, null, [], Installed: true)],
                grants: HelpGrants.Read(home, catalog));

            var waiting = Assert.Single(machine.PluginServersWaiting);
            Assert.Equal(("acme.tools", "wiki", true), (waiting.Plugin, waiting.Server, waiting.Changed));
            Assert.Equal(["lookup", "list_pages"], waiting.Tools);
            Assert.Equal(["edit_page"], waiting.Writes);
            Assert.Equal(new HelpPluginServerOffered("in-app-browser", "browser", "1.0.0", "1.1.0"), Assert.Single(machine.PluginServersOffered));
        }
        finally
        {
            foreach (var folder in new[] { home, home + "-offers" })
            {
                try { Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>
    /// The servers it is handed are the grants' (<see cref="HelpGrants"/>): a session's are not its, one the person has not
    /// approved waits, and one approved with its writes says the writes accepted.
    /// </summary>
    [Fact]
    public void The_servers_it_is_handed_are_described_from_the_persons_approvals()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-help-servers-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(DaorisHome.Entry(home, HomeEntry.Plugins, "acme.tools"));
            File.WriteAllText(DaorisHome.Entry(home, HomeEntry.Plugins, "acme.tools", PluginCatalog.ManifestName), """
                { "id": "acme.tools", "servers": [
                    { "name": "tickets", "command": ["tickets"], "to": ["help"], "allow": ["search"], "writes": ["create_issue"] },
                    { "name": "wiki", "command": ["wiki"], "to": ["help"], "allow": ["lookup"] },
                    { "name": "docs", "command": ["docs"] } ] }
                """);
            var catalog = PluginCatalog.Load(home);
            HelpGrants.Approve(home, catalog, "acme.tools", "tickets", readsOnly: false);

            var machine = HelpRoom.Describe(
                DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0, plugins: catalog,
                grants: HelpGrants.Read(home, catalog));

            var server = Assert.Single(machine.PluginServers);
            Assert.Equal(("acme.tools", "tickets"), (server.Plugin, server.Server));
            Assert.Equal(["search"], server.Tools);
            Assert.Equal(["create_issue"], server.Writes);
            Assert.Equal("wiki", Assert.Single(machine.PluginServersWaiting).Server);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// HELPMODE1a1 (D183 point 2): in restricted mode the room says its limits are the mode's, that full access is the person's own
    /// choice and where it is made, and that it is never the helper's to propose.
    /// </summary>
    [Fact]
    public void In_restricted_mode_the_room_says_its_limits_are_the_modes_and_where_the_person_chooses_full()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("This is restricted mode", room);
        Assert.Contains("`daoris-driver help mode full`", room);
        Assert.Contains("never yours to propose", room);
        Assert.Contains("You change nothing yourself", room);
    }

    /// <summary>
    /// HELPMODE1a1 (D183 points 1, 5): in full access the room says it acts on this machine itself, with every server the plugins
    /// here declare and nothing to approve; it says nothing that is restricted mode's alone (no shell, nothing changed, servers
    /// waiting, reading switched off); and the doctrine still speaks: a repository's work through a quest, Daoris's housekeeping its
    /// own, a push and a key the person's.
    /// </summary>
    [Fact]
    public void In_full_access_the_room_says_it_acts_itself_and_drops_what_is_restricted_modes_alone()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            Mode = HelpModes.Full,
            Reads = [],
            PluginServers = [new("acme.tickets", "tickets", ["search"]), new("in-app-browser", "browser", [])],
            PluginServersWaiting = [new("acme.wiki", "wiki", ["lookup"])],
        });

        Assert.Contains("You act on this machine yourself, as the person chose", room);
        Assert.Contains("say what you will do before you do it", room);
        Assert.Contains("`ask_propose`", room);
        Assert.Contains("Daoris's own housekeeping is yours to do", room);
        Assert.Contains("Never push, sign in, or handle a key", room);
        Assert.Contains("every server they declare, with nothing to approve: `tickets` (plugin `acme.tickets`), `browser` (plugin `in-app-browser`)", room);
        foreach (var restricted in new[]
                 {
                     "You have no shell", "You change nothing yourself", "Not yours yet", "Reading the checkouts here is switched off",
                     "This is restricted mode",
                 })
        {
            Assert.DoesNotContain(restricted, room);
        }
    }

    /// <summary>
    /// HELPMODE1a1: in full access the servers it is said to be handed are every one the plugins here declare, for it or for
    /// sessions, approved or not, as its spawn is handed them (<c>ChatRunner.HandServersAsync</c>); none waits.
    /// </summary>
    [Fact]
    public void In_full_access_the_servers_it_is_handed_are_every_one_declared_here()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-help-full-servers-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(DaorisHome.Entry(home, HomeEntry.Plugins, "acme.tools"));
            File.WriteAllText(DaorisHome.Entry(home, HomeEntry.Plugins, "acme.tools", PluginCatalog.ManifestName), """
                { "id": "acme.tools", "servers": [
                    { "name": "tickets", "command": ["tickets"], "to": ["help"], "allow": ["search"], "writes": ["create_issue"] },
                    { "name": "docs", "command": ["docs"] } ] }
                """);
            var catalog = PluginCatalog.Load(home);

            var machine = HelpRoom.Describe(
                DriverConfig.Empty.WithHelpMode(HelpModes.Full), new Snapshot([], [], []), [], [], _ => null, asks: 0, plugins: catalog,
                grants: HelpGrants.Read(home, catalog));

            Assert.Equal(["tickets", "docs"], machine.PluginServers.Select(server => server.Server));
            Assert.Equal(["create_issue"], machine.PluginServers[0].Writes);
            Assert.Empty(machine.PluginServersWaiting);
            Assert.Empty(machine.PluginServersOffered);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The checkouts it may read are the driver's own answer: each readable one, in every workspace.</summary>
    [Fact]
    public void The_checkouts_it_may_read_are_described_from_the_drivers_setting_and_registry()
    {
        var config = DriverConfig.Empty.WithReadAcross("secret", false);
        var snapshot = new Snapshot(
            [],
            [
                new RepoView("console-ui", Adopted: false, Root: "/work/console-ui", Workspace: "work"),
                new RepoView("secret", Adopted: false, Root: "/work/secret", Workspace: "work"),
                new RepoView("engine", Adopted: true, Root: null),
                new RepoView("notes", Adopted: true, Root: "/srv/notes", Workspace: "home"),
            ],
            []);

        var machine = HelpRoom.Describe(config, snapshot, [], [], _ => null, asks: 0);

        Assert.Equal(
            [new HelpRead("console-ui", "work", "/work/console-ui"), new HelpRead("notes", "home", "/srv/notes")],
            machine.Reads);
    }
}
