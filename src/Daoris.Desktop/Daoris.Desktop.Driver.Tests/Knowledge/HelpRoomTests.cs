using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's room: a folder under the home, re-rendered at every open, that tells the
/// session what Daoris is, which screen and which terminal command does each thing, and what this
/// machine holds now — names and states, never a key. It reads; it changes nothing.
/// </summary>
/// <remarks>
/// What the room is and what it allows. Each section's own sentences are held beside it under
/// <c>Knowledge/Room/</c>, and the room whole by <see cref="HelpRoomGoldenTests"/>.
/// </remarks>
[Trait(Category.Name, Category.Census)]
public sealed class HelpRoomTests : IDisposable
{
    private readonly string _home = Path.Combine(
        TestHomes.Root, "daoris-help-room-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpRoomTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static HelpMachine Machine => HelpRoomFixture.Machine;

    [Fact]
    public void The_room_is_under_the_home_and_says_what_the_machine_holds_now()
    {
        var room = HelpRoom.Prepare(_home, Machine);

        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.HelpRoom), room);
        var agents = File.ReadAllText(Path.Combine(room, "AGENTS.md"));
        // The circles and their repositories, each with how it is driven, its line and how its work lands.
        Assert.Contains("### Workspace `work`", agents);
        const string held = "not landed: no saved workflow is chosen for it, so its work is held";
        Assert.Contains($"| `console-ui` | driven, in its own tree | `feature/app` (set for it) | {held} |", agents);
        Assert.Contains($"| `reports-db` | driven, held | `main` (the checkout's own) | {held} |", agents);
        Assert.Contains($"| `engine` | not driven, no checkout here | none git can name | {held} |", agents);
        // What drives, and what waits on the person.
        Assert.Contains("quests run on `claude-code`", agents);
        Assert.Contains("asks are answered on `claude-code-acp`", agents);
        Assert.Contains("up to 4 sessions at once", agents);
        // What the cap counts, so the room does not tell a person their own conversation holds a slot.
        Assert.Contains("never a conversation the person holds", agents);
        Assert.Contains("2 sessions wait on the person", agents);
        Assert.Contains("1 ask waits for an answer", agents);
        // The agents, by the tool's own answer — and one that is not installed is said, not dropped.
        Assert.Contains("- `claude-code` (Claude Code 2.1.0): account `work` signed out; no Daoris account is signed in, so its starts wait for a sign-in", agents);
        Assert.DoesNotContain("tool's own home", agents);
        Assert.Contains("- `codex`: not installed", agents);
        // One harness reads AGENTS.md, another CLAUDE.md.
        Assert.Equal("@AGENTS.md\n", File.ReadAllText(Path.Combine(room, "CLAUDE.md")));
        Assert.DoesNotContain("\r", agents);
    }

    /// <summary>
    /// Where the person granted families of acts, the room written at the start names each one's verbs, read from the record then,
    /// and that the grant is read at each press; a discard granted no longer sits among the presses that stay the person's. Nothing
    /// granted, and the room says nothing new; an older record's housekeeping and settings lists grant nothing until converted.
    /// </summary>
    [Fact]
    public void The_room_names_the_verbs_of_the_families_the_person_granted_at_its_start()
    {
        var plain = File.ReadAllText(Path.Combine(HelpRoom.Prepare(_home, Machine), "AGENTS.md"));
        Assert.DoesNotContain("families of acts", plain);
        Assert.Contains("A push, a merge or a discard the person asks for goes on their card like any press.", plain);
        Assert.Contains("Never sign in or handle a key", plain);

        File.WriteAllText(ConfiguredAccess.PathOf(_home), """{ "acts": ["discard", "settings"] }""");
        var granted = File.ReadAllText(Path.Combine(HelpRoom.Prepare(_home, Machine), "AGENTS.md"));

        Assert.Contains("discard: `daoris tree remove <session|branch> [--repository <name>]`", granted);
        Assert.Contains("settings: `daoris settings notify|cap|cooloff|quiet …`", granted);
        Assert.DoesNotContain("clean-up: `daoris tree clean", granted);
        Assert.Contains("read at each press", granted);
        Assert.Contains("A push or a merge the person asks for goes on their card like any press.", granted);
        Assert.DoesNotContain("housekeeping_", granted);
        Assert.DoesNotContain("`settings_", granted);
        // The room is written with the home's supervisor rules beside the access, so the render it is held to reads them too.
        var rules = HelpRuleFacts.Read(_home);
        Assert.Equal(HelpRoom.Render(Machine with { Acts = [ActFamilies.Discard, ActFamilies.Settings], Rules = rules }), granted);

        File.WriteAllText(ConfiguredAccess.PathOf(_home), """{ "housekeeping": ["inspect", "clean-up"], "settings": ["cap"] }""");
        var older = File.ReadAllText(Path.Combine(HelpRoom.Prepare(_home, Machine), "AGENTS.md"));
        Assert.Equal(plain, older);
    }

    [Fact]
    public void The_room_says_do_it_or_send_it_on_in_both_modes_and_full_access_presses_every_run_step()
    {
        var restricted = HelpRoom.Render(Machine with { Mode = HelpModes.Restricted });
        var full = HelpRoom.Render(Machine with { Mode = HelpModes.Full });

        Assert.Contains("Do it, or send it on", restricted);
        Assert.Contains("you put on their card for their yes", restricted);
        Assert.Contains("Do it, or send it on", full);
        Assert.Contains("you finish yourself, never handing it back as still theirs", full);
        Assert.Contains("daoris workflow accept|changes|skip|stop|done|approve|reject|fix", full);
        Assert.Contains("say which rule refused it", full);
        Assert.Equal(2, new[] { restricted, full }.Count(room => room.Contains("goes to that repository as a quest", StringComparison.Ordinal)));
        Assert.DoesNotContain("it is yours alone", full);
        Assert.DoesNotContain("never stops a step", full);
    }

    /// <summary>
    /// A folder beside an agent's accounts that is no account is said in the room, so the helper can answer the
    /// person who misses it: one holding a sign-in with the way to keep it, renamed to an account's name.
    /// </summary>
    [Fact]
    public void A_stray_folder_beside_the_accounts_is_said_with_its_sign_in()
    {
        IReadOnlyList<HarnessReport> roster =
        [
            new("claude-code", true, "2.1.0", null, null, null, [new ProfileReport("account-3", "/profiles/account-3", LoginState.In)])
            {
                Strays = [new StrayFolder("Gmail.lock", SignedIn: false), new StrayFolder("account-3.lock", SignedIn: true)],
            },
        ];

        var machine = HelpRoom.Describe(
            DriverConfig.Empty, new Snapshot([], [], []), [], roster, asks: 0, [], facts: ProviderRegistry.Built().Facts);
        var room = HelpRoom.Render(machine);

        Assert.Contains(
            "- `claude-code` (Claude Code 2.1.0): account `account-3` signed in; the folder `Gmail.lock` beside its accounts is a lock's"
            + " name, so it is no account; the folder `account-3.lock` beside its accounts holds a sign-in but is a lock's name, so it"
            + " is no account: rename it to an account's name while nothing runs on it, and it is one again",
            room);
    }

    /// <summary>
    /// It reads the family and nothing else: over the protocol door an unlisted tool is refused by
    /// construction, which is the point.
    /// </summary>
    [Fact]
    public void The_room_allows_reading_the_family_and_nothing_that_writes()
    {
        var room = HelpRoom.Prepare(_home, Machine);

        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(room, ".claude", "settings.json")));
        var allowed = settings.RootElement.GetProperty("permissions").GetProperty("allow")
            .EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Equal(HelpRoom.Allowed, allowed);
        Assert.Contains("mcp__daoris-knowledge__registry", allowed);
        Assert.Contains("mcp__daoris-knowledge__knowledge_search", allowed);
        Assert.DoesNotContain("mcp__daoris-knowledge__quest_publish", allowed);
        // The person presses or applies every change: no proposal tool of any kind is the room's own to call.
        Assert.DoesNotContain(allowed, rule => rule.EndsWith("_propose", StringComparison.Ordinal));
        Assert.Contains("Edit(./drafts/**)", allowed);
        Assert.True(Directory.Exists(Path.Combine(room, "drafts")));
        Assert.DoesNotContain(allowed, rule => (rule.StartsWith("Bash", StringComparison.Ordinal) && !HelpRoom.Verb.Contains(rule))
            || (rule.StartsWith("Edit", StringComparison.Ordinal) && rule != HelpRoom.DraftsRule) || rule.StartsWith("Write", StringComparison.Ordinal));
    }

    /// <summary>
    /// What it is handed at spawn: the room's allows and a read of its conversation's files — never the
    /// person's own allows, which are for work in a repository — and every deny the person wrote.
    /// </summary>
    [Fact]
    public void Its_rules_are_the_rooms_allows_and_the_persons_denies()
    {
        var file = PermissionFile.Empty with
        {
            Machine = new RuleLists(["Bash(npm test:*)"], ["Bash(git commit:*)"], ["WebFetch(domain:secrets.example)"]),
        };

        var rules = HelpRoom.Rules(file, Path.Combine(_home, "chats", "s1"));

        Assert.Equal([.. HelpRoom.Allowed, PermissionRules.ReadRule(Path.Combine(_home, "chats", "s1"))], rules.Allow);
        Assert.Empty(rules.Ask);
        Assert.Contains("WebFetch(domain:secrets.example)", rules.Deny);
        Assert.DoesNotContain("Bash(npm test:*)", rules.Allow);
    }

    /// <summary>
    /// With a checkout it may read, what it is handed adds the connector's <c>checkout_read</c>, which
    /// the driver judges at every call, and no <c>Read</c> rule or git prefix on any checkout, so no command but Daoris's own verb and
    /// nothing that writes. The refusal of a shell stands: in the agent's asking mode anything else is asked, and refused.
    /// </summary>
    [Fact]
    public void Its_rules_read_the_checkouts_through_the_tool_and_run_no_command()
    {
        var kept = Path.Combine(_home, "chats", "s1");

        var rules = HelpRoom.Rules(PermissionFile.Empty, kept, [new("console-ui", "work", "/work/console-ui")]);

        Assert.Equal([.. HelpRoom.Allowed, PermissionRules.ReadRule(kept), $"mcp__{KnowledgeConnector.ServerName}__checkout_read"], rules.Allow);
        Assert.DoesNotContain(rules.Allow, rule => (rule.StartsWith("Edit", StringComparison.Ordinal) && rule != HelpRoom.DraftsRule) || rule.StartsWith("Write", StringComparison.Ordinal));
        Assert.DoesNotContain(rules.Allow, rule => (rule.StartsWith("Bash", StringComparison.Ordinal) && !HelpRoom.Verb.Contains(rule))
            || rule.Contains("console-ui", StringComparison.Ordinal));
    }

    /// <summary>
    /// A plugin's server declared for Ask Daoris is allowed only the tools the plugin declares, each
    /// as the harness spells a server's tool, after the room's own; the person's denies still win, and nothing else is added.
    /// </summary>
    [Fact]
    public void Its_rules_allow_a_plugins_tools_only_as_the_plugin_declares_them()
    {
        var kept = Path.Combine(_home, "chats", "s1");
        var file = PermissionFile.Empty with { Machine = new RuleLists([], [], ["mcp__tickets__delete"]) };

        var rules = HelpRoom.Rules(file, kept, [], ["mcp__tickets__search", "mcp__tickets__get_issue"], ["mcp__tickets__create_issue"]);

        Assert.Equal([.. HelpRoom.Allowed, PermissionRules.ReadRule(kept), "mcp__tickets__search", "mcp__tickets__get_issue"], rules.Allow);
        // A write the person has not accepted is asked, and held for the person.
        Assert.Equal(["mcp__tickets__create_issue"], rules.Ask);
        Assert.Contains("mcp__tickets__delete", rules.Deny);
        Assert.DoesNotContain(rules.Allow, rule => (rule.StartsWith("Bash", StringComparison.Ordinal) && !HelpRoom.Verb.Contains(rule))
            || (rule.StartsWith("Edit", StringComparison.Ordinal) && rule != HelpRoom.DraftsRule) || rule.StartsWith("Write", StringComparison.Ordinal));
    }

    /// <summary>
    /// Where its conversation runs takes the plugins' servers declared for Ask Daoris (never a session's), and its
    /// rules allow their tools only once the person approved them (<see cref="HelpGrants"/>), read under the home at each
    /// spawn; still its own asking mode.
    /// </summary>
    [Fact]
    public void Its_place_allows_a_plugins_tools_only_once_the_person_approved_them()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.tools"));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.tools", PluginCatalog.ManifestName), """
            { "id": "acme.tools", "servers": [
                { "name": "tickets", "command": ["tickets"], "to": ["help"], "allow": ["search"] },
                { "name": "docs", "command": ["docs"] } ] }
            """);
        var place = HelpPlacement.In(_home, HelpRoom.PathOf(_home), []);

        Assert.True(place.Plugins);
        Assert.Equal(PluginServerTo.Help, place.ServersTo);
        Assert.Equal(HelpRoom.Posture, place.Posture);
        Assert.DoesNotContain("mcp__tickets__search", place.Rules(PermissionFile.Empty, "s1").Allow);

        HelpGrants.Approve(_home, PluginCatalog.Load(_home), "acme.tools", "tickets", readsOnly: false);
        var rules = place.Rules(PermissionFile.Empty, "s1");

        Assert.Contains("mcp__tickets__search", rules.Allow);
        Assert.DoesNotContain(rules.Allow, rule => rule.StartsWith("mcp__docs", StringComparison.Ordinal));
    }

    /// <summary>
    /// The machine the room describes is read from what the driver already holds — its file, the
    /// registry, the lines and the roster — so the room and the screens cannot disagree.
    /// </summary>
    [Fact]
    public void The_machine_is_described_from_the_drivers_own_answers()
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["console-ui", "reports-db"], Holds = ["reports-db"],            Cap = 4, Actors = StaffedRoles.On("claude-code", "claude-code-acp", "claude-code-acp"),
        };
        var snapshot = new Snapshot(
            [],
            [
                new RepoView("console-ui", Adopted: false, Root: "/work/console-ui", Workspace: "work"),
                new RepoView("reports-db", Adopted: false, Root: "/work/reports-db", Workspace: "work"),
                new RepoView("engine", Adopted: true, Root: null),
            ],
            [
                new SessionView("s1", "console-ui", "awaiting-person"),
                new SessionView("s2", "reports-db", "working"),
            ]).Apart("console-ui");
        IReadOnlyList<RepositoryLine> lines =
        [
            new("console-ui", "work", "feature/app", LineSource.Repository),
            new("reports-db", "work", "main", LineSource.Checkout),
            new("engine", "default", null, LineSource.None),
        ];
        IReadOnlyList<HarnessReport> roster =
        [
            new("claude-code", true, "2.1.0", null, null, null,
                [new ProfileReport("work", "/profiles/work", LoginState.Out, Key: "sk-…")], LoginState.In),
            new("codex", false, null, "not found", null, null, []),
        ];

        IReadOnlyList<AskView> standing =
        [
            new("a1", "work", "cap the chunk budget", "Proposed", "declarations"),
            new("a2", "work", "stream the tiles", "Published", "named") { Quests = ["q1"] },
        ];
        var machine = HelpRoom.Describe(
            config, snapshot, lines, roster, asks: 1, standing, facts: ProviderRegistry.Built().Facts);

        var report = Assert.Single(machine.Repositories, repository => repository.Name == "console-ui");
        Assert.True(report.Drivable && report.OwnTree && report.Checkout && !report.Held);
        Assert.Equal("feature/app", report.Line.Branch);
        Assert.True(Assert.Single(machine.Repositories, repository => repository.Name == "reports-db").Held);
        Assert.False(Assert.Single(machine.Repositories, repository => repository.Name == "engine").Checkout);
        Assert.Equal([new HelpWaitingSession("s1", "console-ui")], machine.Waiting);
        Assert.Equal(1, machine.Asks);
        // The asks by id, with the quests each became.
        Assert.Equal(["a1", "a2"], machine.OpenAsks.Select(ask => ask.Id));
        Assert.Equal(["q1"], machine.OpenAsks[1].Quests);
        Assert.Equal(("claude-code", "claude-code-acp", "claude-code-acp", 4), (machine.Adapter, machine.Intake, machine.Helper, machine.Cap));
        var agent = Assert.Single(machine.Agents, agent => agent.Name == "claude-code");
        Assert.Equal("Claude Code", agent.Product);
        Assert.Equal([new HelpAccount("work", "out")], agent.Accounts);
        // A key is an account's handle and never its value, and the room carries neither.
        // A credential's shape: `sk-` at a word's start, then its key's run (`ask-daoris` holds `sk-` inside a word and is no key).
        Assert.DoesNotMatch(@"\bsk-[A-Za-z0-9_-]{8,}", HelpRoom.Render(machine));
        Assert.DoesNotContain("/profiles/work", HelpRoom.Render(machine));
        // A checkout is named only where the helper may read it, in the reading list, by name alone, as its
        // tool takes it, and no root at all.
        var room = HelpRoom.Render(machine);
        Assert.DoesNotContain("/work/console-ui", room);
        Assert.Contains("- `console-ui` (workspace `work`)\n", room);
        var unread = HelpRoom.Describe(
            config.WithWorkspaceReadAcross("work", false), snapshot, lines, roster, asks: 1, standing);
        Assert.DoesNotContain("/work/console-ui", HelpRoom.Render(unread));
    }

    /// <summary>
    /// Each repository's standing answer is in the room, the person's words as this machine keeps them, so the
    /// helper can say what a repository's sessions are handed and propose changing it; a workspace with none says nothing.
    /// </summary>
    [Fact]
    public void The_room_carries_each_repositorys_standing_answer_in_the_persons_words()
    {
        var machine = HelpRoomFixture.Machine with
        {
            Repositories =
            [
                .. HelpRoomFixture.Machine.Repositories.Select(repository => repository.Name == "console-ui"
                    ? repository with { Standing = "dev writes allowed; prod only on a yes" }
                    : repository),
            ],
        };

        var room = HelpRoom.Render(machine);

        Assert.Contains("Standing answers in `work`, each handed to every session in its repository beneath its quest", room);
        Assert.Contains("- `console-ui`: \"dev writes allowed; prod only on a yes\"", room);
        Assert.DoesNotContain("Standing answers in `default`", room);
        Assert.DoesNotContain("Standing answers", HelpRoom.Render(HelpRoomFixture.Machine));

        var described = HelpRoom.Describe(
            DriverConfig.Empty.WithStanding("Engine", "dev only", DateTimeOffset.UnixEpoch),
            new Snapshot([], [new RepoView("engine", Adopted: true, Root: null)], []), [], [], asks: 0, []);
        Assert.Equal("dev only", Assert.Single(described.Repositories).Standing);
    }

    /// <summary>A twin (`twins.md`): the service's `SessionLedger.HelpRepository` and the page's spell it too.</summary>
    [Fact]
    public void Its_sessions_are_recorded_in_a_repository_no_folder_can_be_called()
    {
        Assert.Equal("daoris:help", HelpRoom.Repository);
    }

    /// <summary>
    /// Every section is a class in a file of its own under <c>Knowledge/Room/</c>, named after it, so a new
    /// section is a file and one registry line, and no section hides inside another's file.
    /// </summary>
    [Fact]
    public void Every_section_is_a_file_of_its_own()
    {
        // The sections' folder is the one the first section's file is in, found by name wherever the module keeps it.
        var folder = Path.GetDirectoryName(SourceFiles.Driver($"{HelpRoomSections.All[0].GetType().Name}.cs"))!;

        // Parts beside their sections: the doors' (`HelpDoor*`) and *What you may do*'s checkouts; every other file is one section's own.
        string[] parts = ["HelpRoomFullReads.cs"];
        var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
        Assert.Equal(
            HelpRoomSections.All.Select(section => $"{section.GetType().Name}.cs").Order(StringComparer.Ordinal),
            files.Where(file => File.ReadAllText(file).Contains(" : IHelpRoomSection", StringComparison.Ordinal)).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.All(
            files.Select(Path.GetFileName).Where(name => !HelpRoomSections.All.Any(section => $"{section.GetType().Name}.cs" == name) && !parts.Contains(name)),
            name => Assert.StartsWith("HelpDoor", name, StringComparison.Ordinal));
        foreach (var section in HelpRoomSections.All)
        {
            Assert.Contains($"class {section.GetType().Name} : IHelpRoomSection", File.ReadAllText(Path.Combine(folder, $"{section.GetType().Name}.cs")));
        }
    }
}
