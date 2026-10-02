using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris reaches every door (HELP9, D110): each <c>daoris driver</c> verb and each control a Settings domain
/// holds is a door a kind of proposal takes, a door owed with what it waits on, or exempt with its reason. A verb
/// or a control on none of the three fails here, so the next door cannot be built without being answered for.
/// </summary>
/// <remarks>
/// <para><b>Read as text, the way a reviewer would</b> (<c>.claude/knowledge/twins.md</c>): the CLI, the page and the
/// driver share no code. The verbs are the CLI's command table (<c>src/Daoris.Cli/src/cli/driver.ts</c>). The
/// controls are each Settings domain's component and every file it imports, the page's bridge hooks among them that
/// change something, and each action such a hook's payload names.</para>
///
/// <para><b>Three answers.</b> A door names the kind and its door, held against <see cref="IHelpProposalKind.Doors"/>.
/// An exemption is a principle: one of D89's own presses (a sign-in, a key, a discard), something that changes
/// nothing, a viewer's own look, or what an agent may do. A door owed is a door Ask Daoris should have, and says
/// what it waits on.</para>
/// </remarks>
public sealed partial class HelpCoverageTests
{
    private abstract record Answer;

    /// <summary>A door a kind takes: Ask Daoris proposes it, and the person applies it.</summary>
    private sealed record Door(string Kind, string Name) : Answer;

    /// <summary>Not Ask Daoris's to propose, for the reason given.</summary>
    private sealed record Exempt(string Reason) : Answer;

    /// <summary>A door Ask Daoris should have, not built yet, and what it waits on.</summary>
    private sealed record Owed(string Reason) : Answer;

    private const string SignIn = "a sign-in is the person's own press, where it already is (D89).";

    private const string Key = "a key is the person's own, typed where it already is (D89).";

    /// <summary>Settings → Tools' *System* and *Use this version* (TOOLS7): the `tool` kind's `use` door, TOOLS8's.</summary>
    private const string ToolUseOwed =
        "it narrows what runs to the machine's own, or to a version the lists name, so Ask Daoris should propose it, judged "
        + "against the lists as last fetched; that is the `tool` kind's `use` door, which TOOLS8 builds (D121 §4.3), and "
        + "until then `daoris tool use` is the terminal's door.";

    /// <summary>The default cool-off (TOOL4g, D125 §6): the `setting` kind's `cooloff`, whose service half is another lane's.</summary>
    private const string CoolOffOwed =
        "the cool-off a limit naming no time takes is a setting that undoes itself, so Ask Daoris should propose it; that is "
        + "the `setting` kind's `cooloff` door (D125 §6), which waits on the service's setting writer listing it beside this "
        + "side's doors, and until then `daoris driver cooloff` and Settings → Driver are its doors.";

    /// <summary>
    /// How an agent's accounts are used, Settings → Agents' controls (TOOL4g; D125 §6, D130 §9): each a door of the `agent`
    /// kind that the service's `agent_propose` does not write yet, which another lane's row holds.
    /// </summary>
    private static Owed AccountDoorOwed(string door, string what, string terminal) => new(
        $"{what}, so Ask Daoris should propose it as a card the person applies (D89); that is the `agent` kind's `{door}` "
        + $"door (D125 §6, D130 §9), which waits on the service's `agent_propose` writing it, and until then `{terminal}` is the "
        + "terminal's door.");

    private const string Rules =
        "what an agent may do is never proposed by an agent: the room is not handed `permission_propose` (HELP1c), "
        + "since a rule narrowing is applied with nobody's press (PERM2).";

    /// <summary>
    /// The driver lock's <c>--share</c> (DRV8, D104), decided: a flag on the start of a headless loop, not a setting.
    /// </summary>
    private static readonly Exempt Share = new(
        "`daoris-driver drive --share` starts a headless loop beside a live one; it is a process a person runs at a "
        + "terminal, not a setting: nothing is stored to apply, Ask Daoris starts no loop, and the desktop's own loop "
        + "waits on the lock instead (D104).");

    /// <summary>
    /// LAYOUT7's <c>daoris-driver setup</c> (D117 §6.1), a verb of the headless host rather than the CLI's table: a door
    /// Ask Daoris owes, until LAYOUT8 builds the <c>setup</c> kind it is to take.
    /// </summary>
    private static readonly Owed SetupDoor = new(
        "setting a repository up publishes a quest to it and adds a rule its session needs, a widening the person applies "
        + "as a card, so Ask Daoris should propose it; that waits on LAYOUT8's `setup` kind: the service's box and tool, the "
        + "driver's judge against the same facts, and the card (D110).");

    /// <summary>
    /// WSSETUP5's <c>daoris-driver register</c> (D124 §3.1), a verb of the headless host: a door Ask Daoris owes, until WSSETUP7
    /// gives LAYOUT8's <c>setup</c> kind its <c>register</c> door (§4.5).
    /// </summary>
    private static readonly Owed RegisterDoor = new(
        "registering a repository from its line changes only what the line already says, so Ask Daoris should propose it and "
        + "the person apply it; that waits on WSSETUP7's `register` door of LAYOUT8's `setup` kind, judged against the same "
        + "line (D124 §4.5).");

    /// <summary>
    /// WSSETUP6's <c>daoris-driver setup --workspace</c> (D124 §4.5), a verb of the headless host: the press, and its pause,
    /// resume and stop, doors Ask Daoris owes until WSSETUP7 gives LAYOUT8's <c>setup</c> kind its <c>workspace</c>,
    /// <c>pause</c>, <c>resume</c> and <c>stop</c> doors, and the screen its route.
    /// </summary>
    private static readonly Owed WorkspaceDoor = new(
        "setting a workspace up writes a plan that publishes a quest to each repository in turn and adds a rule at the "
        + "workspace's scope, a widening the person applies as a card, and pausing, resuming or stopping it changes only what "
        + "the person already said, so Ask Daoris should propose each; that waits on WSSETUP7's `workspace`, `pause`, `resume` "
        + "and `stop` doors of LAYOUT8's `setup` kind, judged against the same plan (D124 §4.5).");

    /// <summary>
    /// SESSUX1a's <c>useArchiveSessions</c> (D126 §7.3), a bridge hook no Settings domain presses and no screen yet: a door
    /// Ask Daoris owes, until SESSUX1h builds the <c>session</c> kind's <c>archive</c> and <c>unarchive</c> doors.
    /// </summary>
    private static readonly Owed SessionArchiveDoor = new(
        "archiving takes ended sessions out of this machine's list and unarchiving brings them back, a tidy that undoes "
        + "itself, so Ask Daoris should propose it; that is the `session` kind's `archive` and `unarchive` doors, which "
        + "SESSUX1h builds once Sessions' screen presses them (D126 §7.3).");

    /// <summary>Each <c>daoris driver</c> verb, and <c>across</c> by its two forms, as the CLI's command table spells them.</summary>
    private static readonly (string Verb, Answer Answer)[] Verbs =
    [
        ("list", new Exempt("it reads `driver.json` and changes nothing; the room already carries what it lists, from the same file.")),
        ("drive", new Door("setting", "drive")),
        ("undrive", new Door("setting", "undrive")),
        ("hold", new Door("setting", "hold")),
        ("resume", new Door("setting", "resume")),
        ("trees", new Door("setting", "trees")),
        ("line", new Door("setting", "line")),
        ("landing", new Door("setting", "landing")),
        ("across read", new Door("setting", "across")),
        ("across write-to", new Door("setting", "across")),
        ("notify", new Door("setting", "notify")),
        ("intake", new Door("setting", "intake")),
        ("helper", new Door("setting", "helper")),
        ("strikes", new Door("setting", "strikes")),
        ("retry", new Door("setting", "retry")),
        ("timeout", new Door("setting", "timeout")),
        // TOOL4e: the default cool-off's terminal door (D125 §6); TOOL4g built Settings → Driver's control, and its Ask Daoris
        // door waits on the service.
        ("cooloff", new Owed(CoolOffOwed)),
        ("cap", new Door("setting", "cap")),
        ("adapter", new Door("setting", "adapter")),
    ];

    /// <summary>
    /// Each control a Settings domain holds that changes something: the page's bridge hook it presses, and the
    /// action where the hook carries several.
    /// </summary>
    private static readonly (string Domain, string Hook, string? Action, Answer Answer)[] Controls =
    [
        ("ai", "useSetIntake", null, new Door("setting", "intake")),
        ("ai", "useSetHelper", null, new Door("setting", "helper")),

        ("workspace", "useSetLine", null, new Door("setting", "line")),
        ("workspace", "useSetLanding", null, new Door("setting", "landing")),
        ("workspace", "useTreesSync", null, new Door("sync", "sync")),
        ("workspace", "useSweep", null, new Exempt(
            "it removes session trees and branches whose work landed, a discard, which stays the person's own press "
            + "(D89); the room names Settings → Workspace → Session branches.")),
        ("workspace", "useWireRemote", null, new Exempt("wiring a workspace to a remote takes that remote's key; " + Key)),
        ("workspace", "useUnwireRemote", null, new Exempt(
            "unwiring drops the remote's key from this machine, which only the person can give back (D89).")),

        ("driver", "useSetNotify", null, new Door("setting", "notify")),
        ("driver", "useSetStrikes", null, new Door("setting", "strikes")),
        ("driver", "useSetCoolOff", null, new Owed(CoolOffOwed)),

        // TOOL4g: how an agent's accounts are used. The `use` door is judged and applied on this side already
        // (`HelpAgentProposals`); every one waits on the service's writer.
        ("agents", "useAccountUse", "order", AccountDoorOwed(
            "order", "adding an account to a list widens what Daoris may spend", "daoris agent profile order")),
        ("agents", "useAccountUse", "use", AccountDoorOwed(
            "use", "how a list is used changes which account a start spends, and undoes itself", "daoris agent profile use")),
        ("agents", "useAccountUse", "ready", AccountDoorOwed(
            "ready", "*Try now* spends an account sooner than its cool-off said", "daoris agent profile ready")),
        ("agents", "useAccountUse", "inherit", AccountDoorOwed(
            "order", "returning a workspace to this machine's accounts may widen what its work spends",
            "daoris agent profile order <agent> --clear --workspace <name>")),

        ("agents", "useHarnessAction", "install", new Exempt(
            "an install fetches a maker's release and runs its installer on this machine, the person's own press (HELP6).")),
        ("agents", "useHarnessAction", "update", new Door("agent", "update")),
        ("agents", "useHarnessAction", "pin", new Door("agent", "pin")),
        ("agents", "useHarnessAction", "unpin", new Exempt(
            "unpinning hands the agent back to whatever the PATH finds, a binary nobody chose here, the person's own press (HELP6).")),
        ("agents", "useHarnessAction", "login", new Exempt(SignIn)),
        ("agents", "useHarnessAction", "login-new", new Exempt(SignIn)),
        ("agents", "useHarnessAction", "key-add", new Exempt(Key)),
        ("agents", "useHarnessAction", "profile-add", new Exempt("an account is made by its own sign-in; " + SignIn)),
        ("agents", "useHarnessAction", "profile-remove", new Exempt(
            "it deletes an account with its sign-in, which only the person can make again (D89).")),
        ("agents", "useHarnessAction", "profile-default", new Door("agent", "default")),
        ("agents", "useSetAgentSettings", null, new Door("account", "settings")),
        ("agents", "useRefreshHarnesses", null, new Exempt("it reads the roster again and changes nothing.")),

        // TOOLS7: Settings → Tools, answered as D121 §4.3 decides, its doors owed until TOOLS8 builds the `tool` kind.
        ("tools", "useToolUse", "system", new Owed(ToolUseOwed)),
        ("tools", "useToolUse", "managed", new Owed(ToolUseOwed)),
        ("tools", "useToolUse", "file", new Exempt(
            "a program the person names runs as them on every call, so naming one stays their own press (D121 §4.3): a "
            + "helper that can invent a path must not be one press away from running it.")),
        ("tools", "useToolDownload", null, new Exempt(
            "a download changes nothing about what Daoris runs; the use that would is a door (D121 §4.3).")),
        ("tools", "useToolStop", null, new Exempt(
            "it stops a download the person started, theirs to stop, and nothing switches either way.")),
        ("tools", "useToolDelete", null, new Exempt(
            "deleting a downloaded version is a discard, which stays the person's own press (D89); the one in use is never deleted.")),
        ("tools", "useToolsLook", null, new Exempt(
            "a look reads the resource lists and changes nothing about what runs (D121 §4.3).")),
        ("tools", "useToolLocation", "add", new Exempt(
            "a location is a source of programs with hashes of its own choosing, so naming one is the person's own trust and "
            + "their own press (D121 §4.3).")),
        ("tools", "useToolLocation", "remove", new Owed(
            "removing a location only narrows what is offered, so Ask Daoris should propose it, its card listing the versions "
            + "it stops offering; that is the `tool` kind's `location` door, which TOOLS8 builds (D121 §4.3), and until then "
            + "`daoris tool locations remove` is the terminal's door.")),
        ("tools", "useToolPick", null, new Exempt("it is the system's file picker, for the person's own choice.")),
        ("agents", "useHarnessInput", null, new Exempt("it types into a running sign-in, the person's own words to it; " + SignIn)),
        ("agents", "useHarnessCancel", null, new Exempt("it stops a sign-in the person started, theirs to stop; " + SignIn)),

        ("permissions", "useRuleAction", "add", new Exempt(Rules)),
        ("permissions", "useRuleAction", "remove", new Exempt(Rules)),
        ("permissions", "useRuleAction", "default", new Exempt(Rules)),
        ("permissions", "useRuleProposal", null, new Exempt(
            "accepting or declining a rule another agent proposed is the person's review of it (PERM2, D74); a helper "
            + "answering it would be one agent approving another.")),
        ("permissions", "useSetReadAcross", null, new Door("setting", "across")),
        ("permissions", "useSetWriteAcross", null, new Door("setting", "across")),

        ("plugins", "usePluginAction", "enable", new Door("plugin", "enable")),
        ("plugins", "usePluginAction", "disable", new Door("plugin", "disable")),
        ("plugins", "usePluginAction", "remove", new Exempt(
            "a removal takes an installed plugin away, which only a fresh install brings back, so like a discard it stays "
            + "the person's own press (D89); Ask Daoris proposes switching it off, which undoes itself.")),
        ("plugins", "usePluginUpdate", null, new Door("plugin", "update")),
        ("plugins", "usePluginInstall", null, new Door("plugin", "add")),
        ("plugins", "usePluginNew", null, new Exempt(
            "a plugin is made as an ask at the repository that holds plugins, which `ask_propose` proposes (PLUG9); the "
            + "kit's first files are the person's own start, in a folder they chose.")),
        ("plugins", "usePluginTry", null, new Exempt(
            "a trial runs the plugin's code as the person to show what it answers (PLUG8), and changes nothing to propose.")),
        ("plugins", "usePickFolder", null, new Exempt("it is the system's folder picker, for the person's own choice.")),

        ("browser", "useSetBrowser", null, new Door("browser", "use")),
        ("browser", "useSetLinks", null, new Door("browser", "links")),
        ("browser", "useSetExtensions", null, new Door("browser", "extensions")),
        ("browser", "useAddFavorite", null, new Door("browser", "favorite")),
        ("browser", "useRemoveFavorite", null, new Door("browser", "favorite")),

        ("logs", "useOpenLogFolder", null, new Exempt(
            "it opens the log's folder in the system's file browser and changes nothing; a go reaches Settings → Logs.")),
    ];

    /// <summary>
    /// A domain's controls that press no bridge hook: each named by what its source must still say, so a row outlives
    /// its control by no more than the next run.
    /// </summary>
    private static readonly (string Domain, string Control, string Mentions, Answer Answer)[] Local =
    [
        ("start", "a step's door", "onGo", new Door("go", "go")),
        ("appearance", "theme", "useThemeChoice", new Exempt(
            "a viewer's own look, kept by this window and never the machine's (D66); Ask Daoris proposes what the machine does.")),
        ("appearance", "language", "changeLanguage", new Exempt(
            "a viewer's own language, kept by this window and never the machine's (D66); Ask Daoris proposes what the machine does.")),
    ];

    /// <summary>
    /// A form of a control answered for above that its door does not take yet: the same hook and action, another use
    /// of it, owed with what it waits on, and the terminal's spelling of it, which the room names meanwhile.
    /// </summary>
    private static readonly (string Control, string Form, string Terminal, Answer Answer)[] Forms =
    [
        // LEFT3: the terminal's `--clear` made this the screen's and the terminal's both (D50), and Ask Daoris's next.
        ("agents: useHarnessAction profile-default", "clearing an account's default back to the tool's own home",
            "daoris agent profile default <agent> <profile>|--clear", new Owed(
                "a cleared default is a setting that undoes itself, so Ask Daoris should propose it; the `agent` kind's "
                + "`default` door takes an account and no clear yet, which waits on the service's `agent_propose` taking "
                + "one and the driver's judge applying it through `IHelpDoors.SetDefaultAccountAsync` with none.")),
    ];

    private static string Page => Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Web", "src");

    [Fact]
    public void Every_driver_verb_is_a_door_owed_or_exempt_with_its_reason()
    {
        var table = File.ReadAllText(Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Cli", "src", "cli", "driver.ts"));

        Same("`daoris driver` verbs", DriverVerbs(table), Verbs.Select(row => row.Verb));
    }

    [Fact]
    public void Every_settings_control_is_a_door_owed_or_exempt_with_its_reason()
    {
        var hooks = BridgeHooks();
        var found = new List<string>();
        foreach (var (domain, files) in Domains())
        {
            var used = files.SelectMany(file => HookUse().Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value)).ToHashSet();
            foreach (var hook in used.Where(hooks.ContainsKey).Where(hook => hooks[hook].Changes))
            {
                found.AddRange(hooks[hook].Actions.Count == 0
                    ? [Control(domain, hook, null)]
                    : hooks[hook].Actions.Select(action => Control(domain, hook, action)));
            }
        }

        Same("Settings controls", found, Controls.Select(row => Control(row.Domain, row.Hook, row.Action)));
    }

    /// <summary>A domain with no control that changes anything is still answered for, and a local row still names what its source says.</summary>
    [Fact]
    public void Every_settings_domain_is_answered_for()
    {
        var domains = Domains();

        Same("Settings domains", domains.Keys, Controls.Select(row => row.Domain).Concat(Local.Select(row => row.Domain)).Distinct());
        foreach (var (domain, control, mentions, _) in Local)
        {
            Assert.True(
                domains[domain].Any(file => File.ReadAllText(file).Contains(mentions, StringComparison.Ordinal)),
                $"Settings → {domain}'s {control} is answered for by `{mentions}`, which its source no longer says.");
        }
    }

    [Fact]
    public void Every_door_named_is_one_its_kind_takes()
    {
        var doors = Verbs.Select(row => row.Answer).Concat(Controls.Select(row => row.Answer)).Concat(Local.Select(row => row.Answer)).OfType<Door>();

        foreach (var door in doors.Distinct())
        {
            var kind = HelpProposalKinds.Find(door.Kind);
            Assert.True(kind is not null, $"`{door.Kind}` is not a kind Ask Daoris proposes.");
            Assert.True(kind!.Doors.Contains(door.Name), $"the `{door.Kind}` kind takes no door `{door.Name}` — one of {string.Join(", ", kind.Doors)}.");
        }
    }

    [Fact]
    public void Every_exemption_and_every_door_owed_says_why()
    {
        var reasons = Verbs.Select(row => row.Answer).Concat(Controls.Select(row => row.Answer)).Concat(Local.Select(row => row.Answer))
            .Concat(Forms.Select(row => row.Answer))
            .Append(Share)
            .Append(SetupDoor)
            .Append(RegisterDoor)
            .Append(WorkspaceDoor)
            .Append(SessionArchiveDoor)
            .Select(answer => answer switch { Exempt exempt => exempt.Reason, Owed owed => owed.Reason, _ => null })
            .OfType<string>();

        foreach (var reason in reasons)
        {
            Assert.True(reason.Length >= 40 && reason.EndsWith('.'), $"a reason is a sentence that says why: `{reason}`.");
        }
    }

    /// <summary>
    /// HELP10: WSR6's <c>daoris-driver trees sync</c> (D109), a verb of the headless host rather than the CLI's table, is
    /// the <c>sync</c> kind's door while the host's usage still spells it (with WSR7's `--all`, D112).
    /// </summary>
    [Fact]
    public void The_headless_hosts_trees_sync_is_the_sync_kinds_door()
    {
        Assert.Contains("sync [--repository <name>] [--all] [--yes]", DriverCommand.Usage);
        Assert.Contains("sync", HelpProposalKinds.Find("sync")!.Doors);
    }

    /// <summary>
    /// LAYOUT7: the headless host's <c>setup</c> is a door owed to LAYOUT8's <c>setup</c> kind (D110), while the host's usage
    /// spells it and no kind of that name is built; the room names it meanwhile, so the helper can point the person at it.
    /// When LAYOUT8 lands the kind, this owed door becomes its door.
    /// </summary>
    [Fact]
    public void The_headless_hosts_setup_is_a_door_owed_to_the_setup_kind()
    {
        Assert.Contains("setup <repository> [--plan]", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("setup"));
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver setup <repository> [--plan]`", StringComparison.Ordinal));
        Assert.Contains("LAYOUT8", SetupDoor.Reason);
    }

    /// <summary>
    /// WSSETUP5: the headless host's <c>register</c> is a door owed to the <c>setup</c> kind's <c>register</c> (D124 §4.5), while
    /// the host's usage spells it and no kind of that name is built; the room names it meanwhile.
    /// </summary>
    [Fact]
    public void The_headless_hosts_register_is_a_door_owed_to_the_setup_kind()
    {
        Assert.Contains("register [--repository <name>]", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("setup"));
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver register [--repository <name>]`", StringComparison.Ordinal));
        Assert.Contains("WSSETUP7", RegisterDoor.Reason);
    }

    /// <summary>
    /// WSSETUP6: the headless host's <c>setup --workspace</c>, its press and its pause, resume and stop, are doors owed to the
    /// <c>setup</c> kind (D124 §4.5) while the host's usage spells them and no kind of that name is built; the room names both
    /// forms meanwhile, so the helper can point the person at them. The screen's route is WSSETUP7's too.
    /// </summary>
    [Fact]
    public void The_headless_hosts_workspace_plan_is_a_door_owed_to_the_setup_kind()
    {
        Assert.Contains("setup --workspace <name> [--plan]", DriverCommand.Usage);
        Assert.Contains("setup --workspace <name> --pause | --resume | --stop", DriverCommand.Usage);
        Assert.Null(HelpProposalKinds.Find("setup"));
        Assert.Contains(HelpRoomDoors.Doors, door => door.Terminal.Contains("`daoris-driver setup --workspace <name> [--plan]", StringComparison.Ordinal)
            && door.Terminal.Contains("--pause|--resume|--stop", StringComparison.Ordinal));
        Assert.Contains("WSSETUP7", WorkspaceDoor.Reason);
    }

    /// <summary>
    /// SESSUX1a: the archive's bridge hook changes something and no screen presses it yet, so it is a door owed to
    /// SESSUX1h's <c>session</c> kind (D126 §7.3) while no kind of that name is built. When the kind lands, this owed door
    /// becomes its door, and Sessions' acts are read as Settings' domains are.
    /// </summary>
    [Fact]
    public void The_session_archive_is_a_door_owed_to_the_session_kind()
    {
        var hooks = BridgeHooks();

        Assert.True(hooks.TryGetValue("useArchiveSessions", out var archive) && archive.Changes);
        Assert.Null(HelpProposalKinds.Find("session"));
        Assert.Contains("SESSUX1h", SessionArchiveDoor.Reason);
    }

    /// <summary>
    /// LEFT3: a form of a control is owed only while its control is answered for, as a door its kind takes, the CLI's
    /// usage still spells its terminal door, and the room names that door so the helper can point at it meanwhile.
    /// </summary>
    [Fact]
    public void Every_form_owed_is_of_a_door_answered_for_and_named_in_the_room()
    {
        var usage = File.ReadAllText(Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Cli", "src", "cli", "agent.ts"));
        var room = string.Join("\n", HelpRoomDoors.Doors.Select(door => door.Terminal));

        foreach (var (control, form, terminal, answer) in Forms)
        {
            Assert.IsType<Owed>(answer);
            Assert.IsType<Door>(Controls.Single(row => Control(row.Domain, row.Hook, row.Action) == control).Answer);
            var spelled = terminal.Replace("daoris agent ", "", StringComparison.Ordinal);
            Assert.True(usage.Contains(spelled, StringComparison.Ordinal), $"the CLI no longer spells `{terminal}` ({form}).");
            Assert.True(room.Contains(terminal, StringComparison.Ordinal), $"the room's doors do not name `{terminal}` ({form}).");
        }
    }

    /// <summary>DRV8's <c>--share</c>, decided (D110): exempt, while the headless loop's usage still names it.</summary>
    [Fact]
    public void The_driver_locks_share_flag_is_decided()
    {
        Assert.Contains("[--share]", DriverCommand.Usage);
        Assert.Contains("not a setting", Share.Reason);
    }

    /// <summary>
    /// Every verb that changes something is named in the room's doors, a door owed included, so the helper can point
    /// the person at the screen and the command even where it cannot propose.
    /// </summary>
    [Fact]
    public void Every_driver_verb_that_changes_something_is_named_in_the_rooms_doors()
    {
        var named = HelpRoomDoors.Doors
            .SelectMany(door => RoomVerb().Matches(door.Terminal).SelectMany(match => match.Groups[1].Value.Split('|')))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (verb, _) in Verbs.Where(row => row.Answer is not Exempt))
        {
            var head = verb.Split(' ')[0];
            Assert.True(named.Contains(head), $"the room's doors name no `daoris driver {head}`.");
        }
    }

    private static string Control(string domain, string hook, string? action) =>
        action is null ? $"{domain}: {hook}" : $"{domain}: {hook} {action}";

    /// <summary>Two sets the same, with what one lacks named — a control found and answered for nowhere, or a row whose control is gone.</summary>
    private static void Same(string what, IEnumerable<string> found, IEnumerable<string> answered)
    {
        var have = found.ToHashSet(StringComparer.Ordinal);
        var rows = answered.ToList();
        var unanswered = have.Except(rows, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var stale = rows.Except(have, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.True(unanswered.Count == 0,
            $"{what} with no Ask Daoris door and no reason: {string.Join("; ", unanswered)}. Build the door, or say why not here.");
        Assert.True(stale.Count == 0, $"{what} answered for that no longer exist: {string.Join("; ", stale)}.");
        Assert.Equal(rows.Count, rows.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The verbs the CLI's command table spells, from its usage: each line at the verbs' column, its alternatives
    /// (<c>drive|undrive</c>), the verbs a <c>·</c> puts on one line, and a verb's second word where one follows its
    /// operand (<c>across &lt;repo&gt; read</c>).
    /// </summary>
    private static IReadOnlyList<string> DriverVerbs(string table)
    {
        var verbs = new List<string>();
        foreach (Match line in UsageVerbLine().Matches(table))
        {
            foreach (var spelled in line.Groups[1].Value.Split(" · "))
            {
                var words = spelled.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var second = words.Skip(1).FirstOrDefault(word => PlainWord().IsMatch(word));
                verbs.AddRange(words[0].Split('|').Select(verb => second is null ? verb : $"{verb} {second}"));
            }
        }

        Assert.NotEmpty(verbs);
        return verbs;
    }

    /// <summary>Each exported hook of the page's bridge: whether it changes something, and the actions its payload names.</summary>
    private static Dictionary<string, (bool Changes, IReadOnlyList<string> Actions)> BridgeHooks()
    {
        var hooks = new Dictionary<string, (bool, IReadOnlyList<string>)>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(Page, "bridge"), "*.ts").Where(file => !file.Contains(".test.", StringComparison.Ordinal)))
        {
            foreach (var part in TopLevel().Split(File.ReadAllText(file).Replace("\r\n", "\n")))
            {
                if (ExportedHook().Match(part) is not { Success: true } exported) continue;
                var actions = ActionUnion().Matches(part)
                    .SelectMany(union => Quoted().Matches(union.Groups[1].Value).Select(action => action.Groups[1].Value))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                hooks[exported.Groups[1].Value] = (Changes().IsMatch(part), actions);
            }
        }

        Assert.Contains("useSetReadAcross", hooks.Keys);
        return hooks;
    }

    /// <summary>
    /// Each Settings domain, by its id, with its component's file and every file that file imports, and so on: not
    /// the bridge, whose hooks are read by name, and not a type-only import, which presses nothing.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<string>> Domains()
    {
        var settings = Path.Combine(Page, "settings");
        var registry = File.ReadAllText(Path.Combine(settings, "domains.ts"));
        var imported = DomainImport().Matches(registry).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
        var domains = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (Match row in DomainRow().Matches(registry))
        {
            var seen = new List<string>();
            var next = new Stack<string>([Path.GetFullPath(Path.Combine(settings, imported[row.Groups[2].Value]))]);
            while (next.TryPop(out var named))
            {
                var file = new[] { named + ".tsx", named + ".ts", named }.FirstOrDefault(File.Exists);
                if (file is null || seen.Contains(file)) continue;
                seen.Add(file);
                var name = Path.GetFileName(file);
                if (file.Contains($"{Path.DirectorySeparatorChar}bridge{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || name is "shell.ts" or "queries.ts")
                {
                    continue;
                }

                foreach (Match import in ValueImport().Matches(File.ReadAllText(file)))
                {
                    next.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, import.Groups[1].Value)));
                }
            }

            domains[row.Groups[1].Value] = seen;
        }

        Assert.Contains("permissions", domains.Keys);
        return domains;
    }

    // A usage line at the verbs' column: 25 spaces, then the verb's spelling up to the gap before its help.
    [GeneratedRegex(@"^\s*' {25}([a-z][^' ]*(?: [^ ']+)*?)(?: {2,}[^']*)?',?\s*$", RegexOptions.Multiline)]
    private static partial Regex UsageVerbLine();

    [GeneratedRegex("^[a-z][a-z-]*$")]
    private static partial Regex PlainWord();

    [GeneratedRegex(@"daoris driver ([a-z|-]+)")]
    private static partial Regex RoomVerb();

    [GeneratedRegex(@"\n(?=export |const |function |type |/\*\*|//)")]
    private static partial Regex TopLevel();

    [GeneratedRegex(@"^export const (use[A-Z]\w*)\s*=")]
    private static partial Regex ExportedHook();

    [GeneratedRegex(@"useMutation\(|\buse[A-Z]\w*Change\b")]
    private static partial Regex Changes();

    [GeneratedRegex(@"\baction\s*:\s*('[a-z-]+'(?:\s*\|\s*'[a-z-]+')*)")]
    private static partial Regex ActionUnion();

    [GeneratedRegex("'([a-z-]+)'")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"\b(use[A-Z]\w*)\b")]
    private static partial Regex HookUse();

    [GeneratedRegex(@"^import \{ (\w+) \} from '([^']+)';", RegexOptions.Multiline)]
    private static partial Regex DomainImport();

    [GeneratedRegex(@"\{ id: '(\w+)'[^}]*?component: (\w+) \}")]
    private static partial Regex DomainRow();

    [GeneratedRegex(@"^import (?!type )[^;]*?from '(\.{1,2}/[^']+)'", RegexOptions.Multiline)]
    private static partial Regex ValueImport();
}
