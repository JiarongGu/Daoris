using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One transitional read the driver keeps (D228; the oneway design §1.3): an older way kept for an old record or an older build,
/// with the condition that ends it and the row that deletes it. Its kept sites carry <c>// transitional: &lt;Id&gt;</c>, and
/// <c>tools/transitional-markers.mjs</c> holds the markers, the entries and the rows together.
/// </summary>
public sealed record TransitionalEntry
{
    /// <summary>The name the markers carry.</summary>
    public required string Id { get; init; }

    /// <summary>The code it keeps, file:line against the driver's folder.</summary>
    public required string Kept { get; init; }

    /// <summary>What reaches it: the records or the builds that still need the old way.</summary>
    public required string Reaches { get; init; }

    /// <summary>The condition that ends it, in words.</summary>
    public required string Ends { get; init; }

    /// <summary>The row that deletes the read, its marker and this entry; the gate fails once the row is closed.</summary>
    public required string Row { get; init; }

    /// <summary>Whether the condition holds on <c>home</c>: a pure read of the home's files, false where one does not read.</summary>
    public required Func<string, bool> Ended { get; init; }

    /// <summary>For a read kept for an older build: the build whose writes it waits out, which the home's floor passes (ONEWAY1ad-ii).</summary>
    public OlderBuild? Waits { get; init; }

    /// <summary>
    /// For a read that ends with a conversion part's census (WORKFLOWCLEAN1 §4.7): the part, whose census the list reads, since the census reads
    /// the service and <see cref="Ended"/> reads the home alone.
    /// </summary>
    public string? Part { get; init; }
}

/// <summary>The copy's end handed to the staffing below, which names nothing of the register's, as the driver loads.</summary>
internal static class StaffingCopyWiring
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Install() => RoleStaffing.CopyEnded = TransitionalRegister.StaffingCopyEnded;
}

/// <summary>Every transitional read the driver keeps, in one plain list (oneway design §1.3), and This machine's list of them.</summary>
public static class TransitionalRegister
{
    private static readonly string[] OldNames = ["early", "near", "prefer", "parallel"];
    private static readonly string[] PooledSections = ["defaults", "workspaces", "workspaceVersions", "workspaceRotation", "workspaceRotationUse"];

    /// <summary>The builds before COMMS1b's, which wrote no answers mark and no keyed attempt. No release names COMMS1b's yet.</summary>
    private static readonly OlderBuild BeforeComms1b = new("the builds before COMMS1b's, which wrote no answers mark and no keyed attempt");

    /// <summary>The builds before COMMS1h's, which wrote a turn's end as the receipt `answered`. No release names COMMS1h's yet.</summary>
    private static readonly OlderBuild BeforeComms1h = new("the builds before COMMS1h's, which wrote a turn's end as the receipt `answered`");

    /// <summary>The builds before ONEWAY1l's, which wrote a say's `now` alone. No release names ONEWAY1l's yet.</summary>
    private static readonly OlderBuild BeforeOneway1l = new("the builds before ONEWAY1l's, which wrote a say's `now` alone");

    /// <summary>The builds before COMMS1e's, whose host sends no `presses` mark. No release names COMMS1e's yet.</summary>
    private static readonly OlderBuild BeforeComms1e = new("the builds before COMMS1e's, whose host sends no `presses` mark");

    /// <summary>The builds before the staffing part's w-s, which read the staffing keys from driver.json: older than any build that stamps a receipt.</summary>
    private static readonly OlderBuild BeforeStaffingStore = new("the builds before the staffing part's w-s, which read the staffing keys from driver.json") { BeforeStamps = true };

    /// <summary>
    /// Whether no older build reads <paramref name="home"/>'s staffing from driver.json any more (ONEWAY1c-ii): the floor passes the w-s build,
    /// which a standing conversion's receipt makes so; a conversion not undone took the keys out; or the home was seeded fresh and its file
    /// never held the staffing. The copy's writer and its found check skip such a home.
    /// </summary>
    public static bool StaffingCopyEnded(string home) =>
        HomeFloor.Of(home).Passes(BeforeStaffingStore) || StaffingPart.KeysTakenOut(home) || new RoleStaffing(home).Seeded();

    /// <summary>The builds before CONTEXTONE1c's, which handed the map, the look, the indexes and the standing answer as sections and told a chat its routing. No release names CONTEXTONE1c's yet.</summary>
    private static readonly OlderBuild BeforeContextOne1c = new("the builds before CONTEXTONE1c's, which handed the map, the look, the indexes and the standing answer as sections of their own and told a chat its routing in a note");

    /// <summary>
    /// This machine's list (ONEWAY1ad-ii; the oneway design §1.3): each conversion part's census first, as last read (WORKFLOWCLEAN1's
    /// <i>Workflows: nothing old here</i> its first line), then each kept read with its condition's state; a read that ends with a part's census
    /// reads that census. What This machine's page and Ask Daoris's room read alike.
    /// </summary>
    /// <param name="parts">Each part's census as last read; null or empty where none was read yet.</param>
    /// <param name="entries">The register read; <see cref="Entries"/> by default.</param>
    public static IReadOnlyList<KeptLine> List(string home, IReadOnlyList<PartCensus>? parts, IReadOnlyList<TransitionalEntry>? entries = null)
    {
        var lines = new List<KeptLine>();
        foreach (var part in parts ?? []) lines.Add(new(KeptLine.Part, part.Part, part.Ended) { Count = part.Count });
        foreach (var entry in entries ?? Entries)
        {
            var ended = entry.Part is { } of ? parts?.FirstOrDefault(each => each.Part == of)?.Ended == true : entry.Ended(home);
            lines.Add(new(KeptLine.Read, entry.Id, ended) { Reaches = entry.Reaches, Ends = entry.Ends, Row = entry.Row, Waits = entry.Waits?.Said });
        }

        return lines;
    }

    /// <summary>
    /// The first build that deleted a read the floor ended (a ONEWAY1af step, or FOUNDATION1 row v), from which the conversion's Undo is refused:
    /// Undo lifts the rollback refusal that holds the floor. Null while no such read is deleted; the row that deletes one names its build here.
    /// </summary>
    public static string? FloorNeeded => null;

    public static IReadOnlyList<TransitionalEntry> Entries { get; } =
    [
        new()
        {
            Id = "actor-key-before",
            Kept = "Settings/ActorKey.cs:32-49",
            Reaches = "A role of the five with an order of its own and no stored staffing, on a home no conversion has reached: it starts as its single-agent key says, shown as before.",
            Ends = "The staffing part's census has ended: each of the five holds a written order in the store, which the staffing part's conversion writes.",
            Row = "ONEWAY1af",
            Part = StaffingPart.Part,
            Ended = StaffingPart.EveryOrderWritten,
        },
        new()
        {
            Id = "git-moved-key",
            Kept = "Workplace/GitMoved.cs:18, Workplace/GitMove.cs:18",
            Reaches = "The `gitMoved` key in driver.json, which an older build finds missing and then takes the ambient credential path.",
            Ends = "The home's driver.json holds no `gitMoved` key, which the git part's plan removes once the floor passes ONEWAY1p's build.",
            Row = "ONEWAY1af",
            Ended = home => Read(home, HomeEntry.DriverConfig) is { } root && !root.ContainsKey(GitMoved.Key),
        },
        new()
        {
            Id = "harness-old-names",
            Kept = "Agents/HarnessSettings.cs:1151-1161",
            Reaches = "harnesses.json written by an older build: `early` and `near` read as switch and curve, `prefer` and `parallel` as nothing.",
            Ends = "No entry of harnesses.json spells an old name, once the accounts part's census ends.",
            Row = "ONEWAY1af",
            Ended = home => Read(home, HomeEntry.Harnesses) is { } root && !Spells(root, OldNames),
        },
        new()
        {
            Id = "account-pool-fold",
            Kept = "Agents/AccountSet.cs:29, Agents/HarnessSettings.cs:241",
            Reaches = "harnesses.json that still holds per-workspace lists and defaults, folded to one set per agent in memory by every reader.",
            Ends = "harnesses.json holds one set per agent: no defaults and no workspace sections.",
            Row = "ONEWAY1af",
            Ended = home => Read(home, HomeEntry.Harnesses) is { } root && !PooledSections.Any(root.ContainsKey),
        },
        new()
        {
            Id = "say-now-field",
            Kept = "Course/Requests/SessionRequests.cs:626",
            Reaches = "A say request file written by an older build that waits with `now: true` and no `level`, read as `level: now`.",
            Ends = "No waiting request file carries `now` without a `level`, once the floor passes the last build that wrote `now` alone.",
            Row = "ONEWAY1af",
            Waits = BeforeOneway1l,
            Ended = home => Read(home, HomeEntry.DriverConfig) is not null && !WaitingNowAlone(home) && HomeFloor.Of(home).Passes(BeforeOneway1l),
        },
        new()
        {
            Id = "solicited-older-host",
            Kept = "Loop/DriverHelpers.cs:758, Loop/DriverPartners.cs:484, Loop/OpinionDeliverer.cs:182",
            Reaches = "A host with no `answers` mark, before COMMS1b: the helper's sixteen-link walk and read-back, and the partner's and the opinion's pre-check of the record named.",
            Ends = "The floor passes COMMS1b's build: the host and the driver ship together, so no host without the mark is reached.",
            Row = "ONEWAY1af",
            Waits = BeforeComms1b,
            Ended = home => HomeFloor.Of(home).Passes(BeforeComms1b),
        },
        new()
        {
            Id = "solicited-old-files",
            Kept = "Loop/DriverHelpers.cs:806, Loop/DriverPartners.cs:464",
            Reaches = "A helper state or a partner receipt whose attempt an older build wrote, with no `keyed` mark: read back along the line, else uncertain.",
            Ends = "No open helper or partner ask holds an attempt without `keyed`, and the floor passes COMMS1b's build. The open asks are the line's, so it never reads as ended here and the row deletes it.",
            Row = "ONEWAY1af",
            Waits = BeforeComms1b,
            Ended = Never,
        },
        new()
        {
            Id = "session-answer-column",
            Kept = "../../Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:1858",
            Reaches = "A session row from before `said` that holds an `answer` and no `said`: the service's store reads it as the first said word, id `answer`, and the next word written empties the column.",
            Ends = "No session row in the service's store holds an `answer` and no `said` (a census over the store; finished ones go with their history's clear). The condition lives in the service's store, so it never reads as ended here and the row deletes it.",
            Row = "ONEWAY1af",
            Ended = Never,
        },
        new()
        {
            Id = "solicited-old-guards",
            Kept = "Loop/DriverHelpers.cs:835, Loop/DriverPartners.cs:496",
            Reaches = "A driver rolled back before COMMS1b, or a host that drops its mark mid-hand: the helper state's and the partner receipt's attempt, still written.",
            Ends = "The floor passes COMMS1b's build.",
            Row = "ONEWAY1af",
            Waits = BeforeComms1b,
            Ended = home => HomeFloor.Of(home).Passes(BeforeComms1b),
        },
        new()
        {
            Id = "opinion-handed-guard",
            Kept = "Loop/OpinionDeliverer.cs:95",
            Reaches = "An opinion handed before the one door, whose findings word has no key: its `handed` is the only guard that hands it once.",
            Ends = "No opinion waits with an unkeyed findings word (census), and the floor passes COMMS1b's build. The waiting opinions are the service's, so it never reads as ended here and the row deletes it.",
            Row = "ONEWAY1af",
            Waits = BeforeComms1b,
            Ended = Never,
        },
        new()
        {
            Id = "receipt-answered",
            Kept = "../../Daoris.Web/src/work/conversation/conversation.ts:752",
            Reaches = "An event log an older build wrote, holding the receipt `answered` for the turn that took a word, which COMMS1h's build writes `ended`: the page draws it as it was.",
            Ends = "No event log on the home holds an `answered` receipt (the logs are history, cleared by their store's remover), and the floor passes COMMS1h's build. The logs are every session's, so it never reads as ended here and the row deletes it.",
            Row = "ONEWAY1af",
            Waits = BeforeComms1h,
            Ended = Never,
        },
        new()
        {
            Id = "handed-before-block",
            Kept = "Events/Domain/HandedCodes.cs:26, Events/Domain/HandedCodes.cs:44, Events/Domain/HandedCodes.cs:53, Events/Domain/HandedCodes.cs:57",
            Reaches = "A session's account of what it was handed, kept on its record by an older build, that names a `map`, `look`, `indexes` or "
                + "`standing` section: the codes stay declared, and the page's `work.handed.section.*` rows word them as they were.",
            Ends = "No session record holds an account naming one of the four (finished ones go with their history's clear), and the floor passes "
                + "CONTEXTONE1c's build, the last that wrote them. The records are the service's, so it never reads as ended here and the row deletes it.",
            Row = "ONEWAY1af",
            Waits = BeforeContextOne1c,
            Ended = Never,
        },
        new()
        {
            Id = "chat-told-routing",
            Kept = "Events/NoteCodes.cs:775, ../../Daoris.Web/src/work/noteLines.ts:329",
            Reaches = "An event log an older build wrote, holding the note `chat.told-routing` a chat's start was told: the code stays declared, "
                + "and the page's `note.chat.told-routing` row words it as it was.",
            Ends = "No event log on the home holds a `chat.told-routing` note (the logs are history, cleared by their store's remover), and the floor "
                + "passes CONTEXTONE1c's build. The logs are every session's, so it never reads as ended here and the row deletes it.",
            Row = "ONEWAY1af",
            Waits = BeforeContextOne1c,
            Ended = Never,
        },
        new()
        {
            Id = "answers-alone-events",
            Kept = "Course/Domain/AnswersAlone.cs:49, Course/ContinuationAdmissions.cs:301, Course/GoOn/AnswersAloneHold.cs:97, Course/GoOn/AnswersAloneHold.cs:130",
            Reaches = "A line no admission keeps yet, seeded once from its events as the count was read before; and a store short of step 20, or records that did not read, counted from the events as before.",
            Ends = "No event log on the home was begun before its store kept the admissions, so no line holds history an admission did not read.",
            Row = "ONEWAY1af",
            Ended = ContinuationAdmissions.Begun,
        },
        new()
        {
            Id = "presses-older-host",
            Kept = "Service/ServiceClient.cs:447",
            Reaches = "A host with no `presses` mark, before COMMS1e: a person's press is said with no question, the modules' own check before the say standing alone.",
            Ends = "The floor passes COMMS1e's build: the host and the driver ship together, so no host without the mark is reached.",
            Row = "ONEWAY1af",
            Waits = BeforeComms1e,
            Ended = home => HomeFloor.Of(home).Passes(BeforeComms1e),
        },
        new()
        {
            Id = "held-press-unasked",
            Kept = "../Daoris.Desktop.Modules/Loop/SessionWords.cs:1219",
            Reaches = "A press of a park's question held at a door before COMMS1e, which names its choice and no question: forwarded with no question for the head to check.",
            Ends = "No held word names a choice without its question, once the census of the held words finds none.",
            Row = "ONEWAY1af",
            Ended = home => Read(home, HomeEntry.DriverConfig) is not null && !HeldPressUnasked(home),
        },
        new()
        {
            Id = "staffing-copy",
            Kept = "Roles/RoleStaffing.cs:664, Roles/StaffingVerbs.cs:14, ../Daoris.Desktop.Modules/Bridge/Routes/DriverRoutes.cs:334",
            Reaches = "A home an older build may still open (D226): each staffing write is copied onto driver.json's staffing keys, the found check compares the file with what the store wrote, and a found change waits for Take it or Dismiss.",
            Ends = "The floor passes the w-s build on the home, which the conversion's first Apply makes so, or the home was seeded fresh: the writer and the check skip such a home (ONEWAY1c-ii), and the staffing part clears its found changes.",
            Row = "ONEWAY1af",
            Waits = BeforeStaffingStore,
            Ended = StaffingCopyEnded,
        },
        new()
        {
            Id = "conversation-moves-refused",
            Kept = "Roles/StaffPolicyDoor.cs:41",
            Reaches = "A role that takes a conversation's work, whose policy names a limit's wait or ask, or `most`: refused at the door, since a conversation's limit moves by its own order only (D226).",
            Ends = "A conversation's moves are the supervisor's rules (CONTEXTDRIVE1n, which took STAFFPOLICY1e3's conversation half up; D229).",
            Row = "CONTEXTDRIVE1n",
            Ended = Unbuilt,
        },
        new()
        {
            Id = "answers-alone-bound",
            Kept = "Course/GoOn/AnswersAloneHold.cs:34",
            Reaches = "A home whose supervisor rules are not reached: the answers-alone hold, a conversation's course and the line's counts read Daoris's own two in a row.",
            Ends = "The home's rules are reached, no policy moves and driver.json holds no `strikes`: the line's answers-alone rule decides everywhere.",
            Row = "CONTEXTDRIVE1l",
            Ended = SupervisorRuleCensus.Ended,
        },
        new()
        {
            Id = "strikes-key",
            Kept = "WorkOrders/Domain/Planner.cs:1365",
            Reaches = "A home whose rules are not reached: `strikes` parks a quest at the planner and the staffing course, the line's counts say it, and the save keeps the key as the file held it.",
            Ends = "The conversion's rules part removed the key and wrote the failures rule; no door writes it again.",
            Row = "CONTEXTDRIVE1l",
            Ended = SupervisorRuleCensus.Ended,
        },
        new()
        {
            Id = "policy-moves",
            Kept = "WorkOrders/StaffCourse.cs:34",
            Reaches = "A home whose rules are not reached: a role's applied policy's moves and `most` move and bound its lines, and the next start's walk passes by them.",
            Ends = "No policy holds moves or `most`: the line's effective rules are the staffing course's moves and bound.",
            Row = "CONTEXTDRIVE1l",
            Ended = SupervisorRuleCensus.Ended,
        },
        new()
        {
            Id = "transient-bound",
            Kept = "WorkOrders/StaffCourse.cs:19",
            Reaches = "A home whose rules are not reached: a driven run's transient episode tries five times, or its policy's `after`, before the hold.",
            Ends = "The home's rules are reached: the lowest effective tries rule is the episode's limit, and its action the end's.",
            Row = "CONTEXTDRIVE1l",
            Ended = SupervisorRuleCensus.Ended,
        },
        // WORKFLOWCLEAN1 §4.7's kept reads (ONEWAY1ad-ii): the old workflow engine's, each ending with the workflow part's census, which reads
        // the service's runs as well as the home's files.
        new()
        {
            Id = "auto-lander",
            Kept = "Workplace/AutoLander.cs:57",
            Reaches = "Work landed by the old doors (the auto lander's choosing, the presses' landing in place, the tidy) for a run on Current, steps or `handed`.",
            Ends = "The workflow part's census has ended, as the three reads above end.",
            Row = "WORKFLOWCLEAN1z",
            Part = ConversionParts.Workflows,
            Ended = Never,
        },
        new()
        {
            Id = "roles-mode-setup",
            Kept = "../../Daoris.Web/src/agents/modes.ts:31",
            Reaches = "A window whose browser storage still holds the retired Roles mode: it opens on Setup, which replaced it.",
            Ends = "One release after SETUPUX1y. The storage is the window's, so it never reads as ended here and the row deletes it.",
            Row = "WORKFLOWCLEAN1z",
            Ended = Never,
        },
        new()
        {
            Id = "old-command-names",
            Kept = "Platform/CommandWords.cs:93, Platform/CommandWords.cs:214, ../Daoris.Desktop.Driver.Host/Program.cs:43",
            Reaches = "A session an older build briefed with `daoris-driver …` or a nested family (`daoris driver …`), carried on over the update: "
                + "each old form is refused naming its new one, and the install's `app/bin/daoris-driver` launcher, kept one release, runs nothing "
                + "and says the same.",
            Ends = "No open session was started before this install took the command tree's build (ONEWAY1ai), so none was briefed with an old name. "
                + "The sessions' starts and the install's builds live in two stores, so it never reads as ended here and the row deletes it.",
            Row = "ONEWAY1af",
            Ended = Never,
        },
    ];

    /// <summary>A condition no file of the home answers (the floor of a build no release names, a store elsewhere, the owner's word): it never reads as ended, and its row deletes it.</summary>
    private static bool Never(string _) => false;

    /// <summary>Whether <paramref name="key"/> holds an object with an entry.</summary>
    private static bool Holds(JsonObject root, string key) => root[key] is JsonObject map && map.Count > 0;

    /// <summary>An entry that ends when its row is built is no file of the home either: it never reads as ended, and that row deletes it.</summary>
    private static bool Unbuilt(string _) => false;

    /// <summary>Whether the home's held words hold a press that names its choice and no question; true where the file does not read.</summary>
    private static bool HeldPressUnasked(string home)
    {
        try
        {
            var path = HeldWordsFile.PathOf(home);
            if (!File.Exists(path)) return false;
            if (JsonNode.Parse(AtomicFile.ReadText(path)) is not JsonObject root || root["held"] is not JsonArray held) return false;
            return held.OfType<JsonObject>().Any(word =>
                word["choice"] is JsonValue choice && choice.TryGetValue<string>(out var label) && label.Length > 0
                && (word["question"] is not JsonValue question || !question.TryGetValue<string>(out var asked) || asked.Length == 0));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException or InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>Whether a request file waiting in the home's requests folder says <c>now: true</c> with no <c>level</c>.</summary>
    private static bool WaitingNowAlone(string home)
    {
        try
        {
            var folder = DaorisHome.Entry(home, HomeEntry.Requests);
            if (!Directory.Exists(folder)) return false;
            foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
            {
                if (JsonNode.Parse(AtomicFile.ReadText(path)) is JsonObject root && root["now"] is JsonValue now && now.TryGetValue<bool>(out var on) && on && root["level"] is null) return true;
            }

            return false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException or InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>The home's entry as a JSON object; an absent file is an empty one, and one that does not read is null.</summary>
    private static JsonObject? Read(string home, HomeEntry entry)
    {
        try
        {
            var path = DaorisHome.Entry(home, entry);
            if (!File.Exists(path)) return new JsonObject();
            return JsonNode.Parse(AtomicFile.ReadText(path)) as JsonObject;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
            return null;
        }
    }

    private static bool Spells(JsonNode? node, string[] names) => node switch
    {
        JsonObject obj => obj.Any(pair => names.Contains(pair.Key, StringComparer.Ordinal) || Spells(pair.Value, names)),
        JsonArray array => array.Any(item => Spells(item, names)),
        _ => false,
    };
}
