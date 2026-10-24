using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The owner's census sweep (STEWARDROLE1a-ii, D209 §5.4, §10 row 1a-ii): every person's and window's route of the census has a mark
/// (<see cref="PipeTargets.Mark"/>); every proposal door Ask Daoris's Apply takes of the setting and set-up kinds declares its target;
/// every person's door of the service declares the record kind its effect is attributed on, by evidence the desktop's reader knows; and
/// the sweep fails on any of them that names a repository, a quest or a session and marks nothing. A declaration can be wrong, so each
/// is cross-checked against its handler: a route that publishes a session or the state a role's acts read names its target, a route
/// that only names one is <c>none</c> only from the reviewed exemptions below, and a mutating route declaring <c>none</c>, or listing
/// itself an exemption, fails.
/// </summary>
public sealed class OwnerSetCensusTests
{
    /// <summary>
    /// The reviewed exemptions (§5.4, finding 9): a route that names a target field and marks nothing, each with its reason. An
    /// <c>acts-on-nothing</c> one publishes none of the role's scopes and no session; a <c>machine-setting</c> one publishes no session
    /// and none of the sessions', the landing's or the lines' state (a machine's settings are read under the driver's); an
    /// <c>own-conversation</c> one publishes no session of a quest; the appointment acts on the role.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> Exemptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SESSION_OPEN_FOLDER"] = OwnerNone.ActsOnNothing, // opens the session's folder in the file manager; changes nothing
        ["NOTICE_DISMISS"] = OwnerNone.ActsOnNothing, // the owner's news dismissed, by its id or a workspace's
        ["HELP_DISMISS"] = OwnerNone.ActsOnNothing, // a proposal set aside by its id; the Apply is what acts
        ["CHANNEL_MARK"] = OwnerNone.ActsOnNothing, // a read mark on an ask's channel, which never moves back
        ["PERSON_CONFIRM"] = OwnerNone.ActsOnNothing, // a card answered by its id; the press it confirms runs through this dispatcher and is marked
        ["PERSON_CONFIRM_SHOWN"] = OwnerNone.ActsOnNothing, // a card said shown, by its id
        ["RULE_ACTION"] = OwnerNone.ActsOnNothing, // a rule of what agents may do, by the rule's id
        ["RULE_PROPOSAL"] = OwnerNone.ActsOnNothing, // an agent's proposed rule answered, by its id
        ["PLUGIN_OPEN_FOLDER"] = OwnerNone.ActsOnNothing, // a plugin's folder opened in the file manager
        ["PLUGIN_ACTION"] = OwnerNone.MachineSetting, // a plugin turned on or off, by the plugin's id
        ["PLUGIN_UPDATE"] = OwnerNone.MachineSetting, // a plugin replaced, by its id
        ["PLUGIN_NEW"] = OwnerNone.MachineSetting, // a plugin made in a folder, by its id
        ["PLUGIN_TRY"] = OwnerNone.MachineSetting, // a plugin tried from a folder, by its id
        ["ACCOUNT_USE"] = OwnerNone.MachineSetting, // an agent's account's use, by the account's ids and the workspaces it serves
        ["START_CHAT"] = OwnerNone.OwnConversation, // a conversation begun in a repository: no quest, and its record is a new conversation's
        ["START_HELP"] = OwnerNone.OwnConversation, // Ask Daoris begun in a workspace: no quest
        ["SET_APPOINTMENT"] = OwnerNone.Appointment, // appointing a role acts on the role, never on the repository it names
    };

    /// <summary>The setting doors that name no repository, workspace or quest (a machine's), each with why.</summary>
    internal static readonly IReadOnlyDictionary<string, string> MachineDoors = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["strikes"] = "the machine's strikes", ["timeout"] = "the machine's timeout", ["notify"] = "the machine's notifications",
        ["cap"] = "the machine's cap", ["cooloff"] = "the machine's cool-off", ["keep"] = "how long the machine keeps what it keeps",
        ["adapter"] = "the machine's default agent", ["intake"] = "the machine's intake", ["helper"] = "the machine's helper agent",
    };

    // The fields that name a target, as a handler reads them: what the sweep tells a naming route by.
    private static readonly string[] TargetFields = ["repository", "workspace", "quest", "session", "id", "ids", "ask", "entry", "unit"];

    private const StateScopes RoleScopes = StateScopes.Driver | StateScopes.Sessions | StateScopes.Landing | StateScopes.Lines;

    [Fact]
    public void Every_person_and_window_route_has_a_mark_and_every_mark_is_one()
    {
        var owner = OwnerRoutes().ToHashSet(StringComparer.Ordinal);

        Assert.Empty(owner.Where(each => !PipeTargets.Daoris.Marks.ContainsKey(each)).Order(StringComparer.Ordinal));
        Assert.Empty(PipeTargets.Daoris.Marks.Keys.Where(each => !owner.Contains(each)).Order(StringComparer.Ordinal));
        Assert.All(PipeTargets.Daoris.Marks, each => Assert.True(
            each.Value.ByPlan || each.Value.Fields.Count > 0 != (each.Value.None is not null),
            $"{each.Key} names fields or a reason, never both nor neither"));
        Assert.All(PipeTargets.Daoris.Marks.Values.Where(each => each.None is not null), each => Assert.Contains(each.None, OwnerNone.All));
        Assert.Equal(["HELP_APPLY"], PipeTargets.Daoris.Marks.Where(each => each.Value.ByPlan).Select(each => each.Key));
    }

    /// <summary>The sweep over the census as built: every route's mark holds against its handler.</summary>
    [Fact]
    public void Every_mark_holds_against_its_handler()
    {
        var failures = Sweep(PipeTargets.Daoris.Marks, Exemptions, Handlers());
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>A mutating route declaring none fails, as one declaring another field does, whatever its reason says.</summary>
    [Theory]
    [InlineData("STOP_SESSION", OwnerNone.NamesNothing)]
    [InlineData("SET_LANDING", OwnerNone.MachineSetting)]
    [InlineData("SESSION_QUEUE_EDIT", OwnerNone.ActsOnNothing)]
    public void A_mutating_route_declaring_none_fails(string route, string why)
    {
        var marks = new Dictionary<string, OwnerTarget>(PipeTargets.Daoris.Marks, StringComparer.Ordinal) { [route] = OwnerTarget.Nothing(why) };

        Assert.Contains(Sweep(marks, Exemptions, Handlers()), each => each.StartsWith(route + ":", StringComparison.Ordinal));
    }

    [Fact]
    public void A_session_route_naming_another_field_fails()
    {
        var marks = new Dictionary<string, OwnerTarget>(PipeTargets.Daoris.Marks, StringComparer.Ordinal)
        {
            ["SESSION_QUEUE_EDIT"] = OwnerTarget.Of(new OwnerField("id", OwnerKinds.Session)),
        };

        Assert.Contains(Sweep(marks, Exemptions, Handlers()), each => each.StartsWith("SESSION_QUEUE_EDIT:", StringComparison.Ordinal));
    }

    /// <summary>A mutating route adding itself to the exemptions fails; a naming route left off them fails.</summary>
    [Fact]
    public void A_mutating_route_listing_itself_an_exemption_fails_and_a_naming_one_off_the_list_fails()
    {
        var marks = new Dictionary<string, OwnerTarget>(PipeTargets.Daoris.Marks, StringComparer.Ordinal)
        {
            ["DISCARD_SESSION_TREE"] = OwnerTarget.Nothing(OwnerNone.ActsOnNothing),
        };
        var exemptions = new Dictionary<string, string>(Exemptions, StringComparer.Ordinal) { ["DISCARD_SESSION_TREE"] = OwnerNone.ActsOnNothing };
        Assert.Contains(Sweep(marks, exemptions, Handlers()), each => each.StartsWith("DISCARD_SESSION_TREE:", StringComparison.Ordinal));

        var unlisted = new Dictionary<string, string>(Exemptions, StringComparer.Ordinal);
        unlisted.Remove("SESSION_OPEN_FOLDER");
        Assert.Contains(Sweep(PipeTargets.Daoris.Marks, unlisted, Handlers()), each => each.StartsWith("SESSION_OPEN_FOLDER:", StringComparison.Ordinal));
    }

    /// <summary>Finding 9: a route that only names its target, as <c>SESSION_OPEN_FOLDER</c> requires its <c>id</c>, passes as a reviewed exemption.</summary>
    [Fact]
    public void A_naming_route_passes_as_a_reviewed_exemption()
    {
        var handlers = Handlers();
        Assert.Contains("id", handlers["SESSION_OPEN_FOLDER"].Fields);
        Assert.Equal(OwnerNone.ActsOnNothing, PipeTargets.Daoris.Mark(DriverModule.Name, "SESSION_OPEN_FOLDER")!.None);
        Assert.DoesNotContain(Sweep(PipeTargets.Daoris.Marks, Exemptions, handlers), each => each.StartsWith("SESSION_OPEN_FOLDER:", StringComparison.Ordinal));
    }

    /// <summary>Every door of the setting and set-up kinds declares its target, but the machine's, each said why above.</summary>
    [Theory]
    [InlineData("setting")]
    [InlineData("setup")]
    public void Every_apply_door_declares_its_target(string kind)
    {
        foreach (var door in HelpProposals.DoorsOf(kind))
        {
            var target = door == "retry" ? "#q1" : "engine";
            var owned = HelpProposals.Owned(new HelpProposal("p1", kind, door, target, null, null, null, "", null, "proposed"));
            if (MachineDoors.ContainsKey(door) && kind == "setting") continue;
            Assert.True(owned.Repositories.Count + owned.Quests.Count > 0, $"{kind}.{door} declares no target");
            if (door == "retry") Assert.Equal(["q1"], owned.Quests);
            else Assert.Equal(["engine"], owned.Repositories);
        }

        Assert.Empty(MachineDoors.Keys.Where(door => !HelpProposals.DoorsOf("setting").Contains(door)));
        var workspace = HelpProposals.Owned(new HelpProposal("p2", "setting", "landing", null, "games", "merge", null, "", null, "proposed"));
        Assert.Equal(["games"], workspace.Workspaces);
    }

    /// <summary>
    /// Every person's door of the service's <c>PersonDoors.cs</c> declares the record kind its effect is attributed on, and each
    /// evidence it names is one <see cref="OwnerRecords"/> reads, or an instance's route whose mark names what it acts on.
    /// </summary>
    [Fact]
    public void Every_person_door_of_the_service_declares_its_record_kind_and_evidence_the_reader_knows()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Http", "PersonDoors.cs"));
        var persons = Regex.Matches(source, @"(?:new|DoorForm)\((?<head>[^\n]*?)DoorClass\.Person,[^\n]*").ToList();
        Assert.True(persons.Count >= 30, $"the source names {persons.Count} person doors");
        var marks = Regex.Matches(source, @"Marks = OwnerMark\.(?<how>Of|None)\((?:OwnerMarkKinds\.(?<kind>\w+), )?""(?<evidence>[^""]+)""\)").ToList();
        Assert.Equal(persons.Count, marks.Count);
        foreach (var mark in marks.Where(each => each.Groups["how"].Value == "Of"))
        {
            var evidence = mark.Groups["evidence"].Value;
            Assert.Contains(mark.Groups["kind"].Value switch
            {
                "SessionMove" => OwnerRecordKinds.SessionMove, "SessionWord" => OwnerRecordKinds.SessionWord,
                "HandOver" => OwnerRecordKinds.HandOver, "Status" => OwnerRecordKinds.Status, var other => other,
            }, OwnerRecordKinds.All);
            Assert.Contains(evidence, OwnerRecords.Evidence);
            if (evidence.StartsWith("instance:", StringComparison.Ordinal))
            {
                Assert.True(PipeTargets.Daoris.Mark(DriverModule.Name, evidence["instance:".Length..]) is { Fields.Count: > 0 }, evidence);
            }
        }
    }

    // ——— The sweep.

    /// <summary>What the sweep reads of a handler: what its route declares a successful answer changed, and the target fields its body reads.</summary>
    internal sealed record HandlerFacts(StateScopes Changes, string? Session, IReadOnlyList<string> Fields, string Body, string File);

    /// <summary>
    /// Every failure of <paramref name="marks"/> against the handlers: a route that publishes a session names that field as a session; a
    /// route that changes the state a role's acts read and reads a target field names one; a <c>none</c> whose handler reads a target
    /// field is a reviewed exemption, cross-checked against its effects; each field a row names is one its handler reads.
    /// </summary>
    internal static List<string> Sweep(
        IReadOnlyDictionary<string, OwnerTarget> marks, IReadOnlyDictionary<string, string> exemptions, IReadOnlyDictionary<string, HandlerFacts> handlers)
    {
        var failures = new List<string>();
        foreach (var (route, mark) in marks)
        {
            if (mark.ByPlan || !handlers.TryGetValue(route, out var handler)) continue;
            var mutating = (handler.Changes & RoleScopes) != 0 || handler.Session is not null;
            var names = handler.Fields.Count > 0;
            // A handler that hands its payload to a helper of its file (the lines' scoped choices, the work's plans) reads it there.
            foreach (var field in mark.Fields.Where(each => !handler.Body.Contains($"\"{each.Field}\"", StringComparison.Ordinal)
                                                             && !handler.File.Contains($"\"{each.Field}\"", StringComparison.Ordinal)))
            {
                failures.Add($"{route}: names `{field.Field}`, which its handler does not read");
            }

            if (handler.Session is { } session && !mark.Fields.Any(each => each.Field == session && each.Kind == OwnerKinds.Session))
            {
                failures.Add($"{route}: publishes the session `{session}` and does not mark it");
            }

            if (mark.None is null)
            {
                if (exemptions.ContainsKey(route)) failures.Add($"{route}: listed an exemption, and marks fields");
                continue;
            }

            if (exemptions.TryGetValue(route, out var reason))
            {
                if (reason != mark.None) failures.Add($"{route}: exempt as {reason}, declared {mark.None}");
                var effects = reason switch
                {
                    OwnerNone.ActsOnNothing => mutating,
                    OwnerNone.MachineSetting => handler.Session is not null || (handler.Changes & (RoleScopes & ~StateScopes.Driver)) != 0,
                    OwnerNone.OwnConversation => handler.Session is not null,
                    OwnerNone.Appointment => route != "SET_APPOINTMENT",
                    _ => true,
                };
                if (effects) failures.Add($"{route}: exempt as {reason}, and its handler changes what a role acts on");
                continue;
            }

            if (mutating && names) failures.Add($"{route}: changes what a role acts on, names {string.Join(",", handler.Fields)}, and marks nothing");
            else if (names) failures.Add($"{route}: names {string.Join(",", handler.Fields)}, marks nothing, and is no reviewed exemption");
            if (mark.None is OwnerNone.ActsOnNothing or OwnerNone.OwnConversation or OwnerNone.Appointment && names)
            {
                failures.Add($"{route}: {mark.None} is taken only by a reviewed exemption");
            }
        }

        return failures;
    }

    /// <summary>Every route of the driver the census makes the person's or a window's, its handler's facts, read off its own source.</summary>
    internal static IReadOnlyDictionary<string, HandlerFacts> Handlers()
    {
        var root = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Modules");
        var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(each => !each.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText).ToList();
        var facts = new Dictionary<string, HandlerFacts>(StringComparer.Ordinal);
        foreach (var route in DriverModule.Routes)
        {
            var file = sources.FirstOrDefault(source => Body(source, route) is not null) ?? "";
            var body = Body(file, route) ?? "";
            var declared = DriverModule.Declared(new IpcRequest { Module = DriverModule.Name, Type = route });
            var fields = TargetFields.Where(field => body.Contains($"\"{field}\"", StringComparison.Ordinal)).ToList();
            facts[route] = new HandlerFacts(declared?.Scopes ?? StateScopes.None, declared?.Session, fields, body, file);
        }

        return facts;
    }

    /// <summary>The handler's own text: its marks, then its method to the brace that closes it, or to its expression's end.</summary>
    private static string? Body(string source, string route)
    {
        var at = source.IndexOf($"[DriverRoute(\"{route}\")]", StringComparison.Ordinal);
        if (at < 0) return null;
        var open = source.IndexOf('{', at);
        var arrow = source.IndexOf("=>", at, StringComparison.Ordinal);
        if (arrow >= 0 && (open < 0 || arrow < open))
        {
            var end = source.IndexOf(";\n", arrow, StringComparison.Ordinal);
            return source[at..(end < 0 ? source.Length : end)];
        }

        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[at..(i + 1)];
        }

        return source[at..];
    }

    /// <summary>Every route the census makes a person's or a window's, in any form, as <see cref="PipeTargets.Marks"/> keys it.</summary>
    private static IEnumerable<string> OwnerRoutes() =>
        PipeCensus.Daoris.Routes
            .Where(each => each.Class is PipeClass.Person or PipeClass.Window || each.When?.Class is PipeClass.Person or PipeClass.Window)
            .Select(each => each.Route is null ? each.Module.ToUpperInvariant() : PipeTargets.Key(each.Module, each.Route));

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("the census reads the checkout's sources.");
    }
}
