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
        ("workspace", "useSweep", null, new Exempt(
            "it removes session trees and branches whose work landed, a discard, which stays the person's own press "
            + "(D89); the room names Settings → Workspace → Session branches.")),
        ("workspace", "useWireRemote", null, new Exempt("wiring a workspace to a remote takes that remote's key; " + Key)),
        ("workspace", "useUnwireRemote", null, new Exempt(
            "unwiring drops the remote's key from this machine, which only the person can give back (D89).")),

        ("driver", "useSetNotify", null, new Door("setting", "notify")),
        ("driver", "useSetStrikes", null, new Door("setting", "strikes")),

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
            .Append(Share)
            .Select(answer => answer switch { Exempt exempt => exempt.Reason, Owed owed => owed.Reason, _ => null })
            .OfType<string>();

        foreach (var reason in reasons)
        {
            Assert.True(reason.Length >= 40 && reason.EndsWith('.'), $"a reason is a sentence that says why: `{reason}`.");
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
