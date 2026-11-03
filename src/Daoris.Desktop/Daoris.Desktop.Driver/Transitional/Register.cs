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
}

/// <summary>Every transitional read the driver keeps, in one plain list (oneway design §1.3).</summary>
public static class TransitionalRegister
{
    private static readonly string[] Actors = ["build", "chat", "brainstorm", "intake", "ask-daoris"];
    private static readonly string[] OldNames = ["early", "near", "prefer", "parallel"];
    private static readonly string[] PooledSections = ["defaults", "workspaces", "workspaceVersions", "workspaceRotation", "workspaceRotationUse"];

    public static IReadOnlyList<TransitionalEntry> Entries { get; } =
    [
        new()
        {
            Id = "actor-key-before",
            Kept = "Settings/ActorKey.cs:32-49",
            Reaches = "A role with no stored staffing on a home no conversion has reached starts as its single-agent key says, shown as before.",
            Ends = "Every role holds a stored entry, which the staffing part's conversion writes.",
            Row = "ONEWAY1af",
            Ended = home => Read(home, HomeEntry.DriverConfig) is { } root && root["actors"] is JsonObject stored && Actors.All(stored.ContainsKey),
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
            Ended = home => Read(home, HomeEntry.DriverConfig) is not null && !WaitingNowAlone(home),
        },
        new()
        {
            Id = "solicited-older-host",
            Kept = "Loop/DriverHelpers.cs:758, Loop/DriverPartners.cs:484, Loop/OpinionDeliverer.cs:182",
            Reaches = "A host with no `answers` mark, before COMMS1b: the helper's sixteen-link walk and read-back, and the partner's and the opinion's pre-check of the record named.",
            Ends = "The floor passes COMMS1b's build: the host and the driver ship together, so no host without the mark is reached.",
            Row = "ONEWAY1af",
            Ended = Floor,
        },
        new()
        {
            Id = "solicited-old-files",
            Kept = "Loop/DriverHelpers.cs:806, Loop/DriverPartners.cs:464",
            Reaches = "A helper state or a partner receipt whose attempt an older build wrote, with no `keyed` mark: read back along the line, else uncertain.",
            Ends = "No open helper or partner ask holds an attempt without `keyed`, and the floor passes COMMS1b's build.",
            Row = "ONEWAY1af",
            Ended = Floor,
        },
        new()
        {
            Id = "solicited-old-guards",
            Kept = "Loop/DriverHelpers.cs:835, Loop/DriverPartners.cs:496",
            Reaches = "A driver rolled back before COMMS1b, or a host that drops its mark mid-hand: the helper state's and the partner receipt's attempt, still written.",
            Ends = "The floor passes COMMS1b's build.",
            Row = "ONEWAY1af",
            Ended = Floor,
        },
        new()
        {
            Id = "opinion-handed-guard",
            Kept = "Loop/OpinionDeliverer.cs:95",
            Reaches = "An opinion handed before the one door, whose findings word has no key: its `handed` is the only guard that hands it once.",
            Ends = "No opinion waits with an unkeyed findings word (census), and the floor passes COMMS1b's build.",
            Row = "ONEWAY1af",
            Ended = Floor,
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
    ];

    /// <summary>The floor of the builds that reach a home is no file of the home: an entry that ends with it never reads as ended, and its row deletes it.</summary>
    private static bool Floor(string _) => false;

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
