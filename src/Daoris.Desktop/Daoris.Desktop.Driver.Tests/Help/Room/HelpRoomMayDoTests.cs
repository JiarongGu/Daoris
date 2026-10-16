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
    /// D107: with reading across on, the room names each checkout it may read, by name, workspace and path,
    /// and how — the files where they lie, and two read-only git commands — and that it changes nothing there.
    /// </summary>
    [Fact]
    public void With_reading_on_the_room_says_which_checkouts_it_may_read_where_and_how()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("You may read the checkouts below and change nothing in them", agents);
        Assert.Contains("- `console-ui` (workspace `work`) — `/work/console-ui`", agents);
        Assert.Contains("- `reports-db` (workspace `work`) — `\"/work/reports db\"`", agents);
        Assert.Contains("`git -C <path> status`", agents);
        Assert.Contains("`git -C <path> branch --list`", agents);
        Assert.DoesNotContain("you read no checkout", agents);
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
        Assert.Contains("wait for the person's approval on the plugin's page", agents);
        Assert.Contains("`wiki` (plugin `acme.wiki`)", agents);
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
