using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>The rows of what waits on the person in a unit of work or on this machine: a unit's clean-up, a held attempt, a record that does not read, a terminal's press, a volume, a git remote.</summary>
internal static class AttentionMachineRows
{
    /// <summary>The rows, in the order <see cref="NeedsAPerson.Rows"/> judges them; <paramref name="unitScope"/> is the workspace of a unit whose owner is in hand.</summary>
    public static IEnumerable<JsonObject> Rows(AttentionFacts facts, Func<string?, string?> unitScope)
    {
        var rows = new List<JsonObject>();
        // A unit only the person's press settles: one whose owner nothing records, or a tree its session
        // recorded that no branch record follows, each waiting on the clean-up's press; its blocked entries with the code the page words
        // each by. A guard the tidy keeps a branch by is said on its session's page and its branch's line; a wait that ends by itself is no row.
        foreach (var unit in facts.Works)
        {
            var waits = WaitsOnPerson(unit);
            if (waits.Count == 0) continue;
            var end = unit["end"] as JsonObject;
            var unknown = Text(end, "state") == "unknown";
            // A unit no record owns carries none, deliberately: nothing says whose it is, so every window shows it.
            // A stale landing is named by its own branch, and stands since its day passed, where no end says since when.
            var stale = waits.Where(entry => Text(entry, "code") == InventoryCode.LandingStale).Select(entry => entry["facts"] as JsonObject).FirstOrDefault();
            var row = Row("work", Text(unit, "session") ?? Text(unit, "unit"), Text(end, "at") ?? Text(stale, "since"), unitScope(Text(unit, "unit")));
            row["where"] = Text(unit, "repository");
            row["session"] = Text(unit, "session");
            // Its branch, else its tree's folder by name: never the unit's id, which a tree's path makes.
            row["title"] = Text(stale, "branch") ?? Text(unit, "branch") ?? TreeName(waits.Concat(Objects(unit["entries"]))) ?? Text(unit, "unit");
            row["blocked"] = new JsonObject
            {
                ["unknown"] = unknown,
                ["entries"] = new JsonArray([.. waits.Where(entry => !unknown || Text(entry, "disposition") == "blocked").Select(Blocked)]),
            };
            rows.Add(row);
        }

        // A plugin step's attempt held for the person, a held close among them: the attempt whole, since
        // its recorded word where there is one, else since its hold.
        foreach (var attempt in facts.Held)
        {
            var row = Row(
                "work", $"{Part(attempt["unit"])}#{Part(attempt["node"])}#{Part(attempt["attempt"])}", Text(attempt, "resolvedAt") ?? Text(attempt, "at"),
                unitScope(Text(attempt, "unit")));
            row["where"] = Text(attempt, "unit");
            row["held"] = attempt.DeepClone();
            rows.Add(row);
        }

        // A node record that does not read: nothing on its unit is judged or started until the person mends it, and
        // the row's press opens the folder it is kept in. By its file, since its unit may be unknown; since its last write.
        foreach (var record in facts.Unreadable)
        {
            var row = Row("work", $"record#{Part(record["file"])}", Text(record, "at"), unitScope(Text(record, "unit")));
            row["where"] = Text(record, "unit");
            row["unreadable"] = record.DeepClone();
            rows.Add(row);
        }

        // A closure graph closed only in part: while its close node's stop is offered, or the person's stop waits for
        // the next look, since its finding. An override is the person's own, and said on the work's foot alone.
        foreach (var end in facts.Ends.Where(end => Text(end, "kind") == "partially-closed"
                     && (Text(end, "resolution") is not null || (end["words"] as JsonArray)?.Any(word => word?.GetValue<string>() == "stop") == true)))
        {
            var row = Row("work", $"{Part(end["unit"])}#{Part(end["node"])}#{Part(end["quest"])}", Text(end, "at"), unitScope(Text(end, "unit")));
            row["where"] = Text(end, "unit");
            row["ended"] = end.DeepClone();
            rows.Add(row);
        }

        // A press a terminal sent that waits for the person's Confirm in the window: its terminal holds still until they
        // answer on its card, or its time ends, which refuses it. By its route and the terminal as the system names it, since its ask;
        // never a value it carries, which the card alone shows beside its Confirm. Every window shows it: the pipe is this machine's.
        foreach (var waiting in facts.Confirmations)
        {
            // No workspace, deliberately: a terminal's press is the machine's, whichever workspace its route touches.
            var row = Row("confirmation", Text(waiting, "id"), Text(waiting, "asked"));
            var asker = waiting["asker"] as JsonObject;
            row["confirmation"] = new JsonObject
            {
                ["module"] = Text(waiting, "module"),
                ["route"] = Text(waiting, "route"),
                ["asker"] = new JsonObject { ["process"] = Text(asker, "process"), ["pid"] = asker?["pid"]?.DeepClone() },
                ["expires"] = Text(waiting, "expires"),
                ["shownAt"] = Text(waiting, "shownAt"),
            };
            rows.Add(row);
        }

        // A volume still below its free-space floor once the floor freed what Daoris can make again: the
        // rest is the person's to free, by the press its row opens, since its saying; it goes once the volume is above its floor by the
        // margin. Every window shows it: the disk is the machine's.
        foreach (var low in facts.Space)
        {
            var row = Row("space", Text(low, "volume"), Text(low, "said"));
            row["space"] = new JsonObject
            {
                ["root"] = Text(low, "root"),
                ["free"] = low["free"]?.DeepClone(),
                ["total"] = low["total"]?.DeepClone(),
                ["floor"] = low["floor"]?.DeepClone(),
                ["freed"] = low["freed"]?.DeepClone(),
                ["could"] = low["could"]?.DeepClone(),
            };
            rows.Add(row);
        }

        // A git remote that cannot authenticate, held: one row per held group, its host,
        // class, what it holds and the identity that serves it in a workspace, the repositories in it named on the row. Said once here; a
        // session's and a press's words name this row rather than repeat it. Its acts are its class's, Try again among them.
        // The move: every remote nothing serves in a workspace is one row, whatever its host, its repositories counted
        // and those whose own configuration names authentication listed apart. Either holds work where a session of one of its
        // repositories runs or waits on the person.
        var moved = facts.Git.Where(held => Text(held, "class") == "no-identity").ToList();
        foreach (var group in moved.GroupBy(held => Text(held, "workspace") ?? "default").OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var held = group.OrderBy(each => Text(each, "since"), StringComparer.Ordinal).ToList();
            var row = Row("git-held", $"{group.Key}|no-identity", Text(held[0], "since"), group.Key);
            var own = facts.Git.Where(each => Text(each, "class") == "own-input" && (Text(each, "workspace") ?? "default") == group.Key).ToList();
            row["git"] = new JsonObject
            {
                ["host"] = Text(held[0], "host"),
                ["hosts"] = Names(held.Select(each => Text(each, "host"))),
                ["class"] = "no-identity",
                ["holds"] = GitHolds.All,
                ["remote"] = Text(held[0], "remote"),
                ["repositories"] = Names(held.Select(each => Text(each, "repository"))),
                ["ownInput"] = Names(own.Select(each => Text(each, "repository"))),
                ["words"] = "",
            };
            if (GitWorkWaits(facts, group.Key, held)) row["stands"] = true;
            rows.Add(row);
        }

        foreach (var group in facts.Git.Except(moved)
            .GroupBy(held => (Workspace: Text(held, "workspace") ?? "default", Host: Text(held, "host") ?? "", Class: Text(held, "class") ?? "",
                Holds: Text(held, "holds") ?? "", Identity: Text(held, "identityId")))
            .OrderBy(group => group.Key.Workspace, StringComparer.Ordinal).ThenBy(group => group.Key.Host, StringComparer.Ordinal))
        {
            var held = group.OrderBy(each => Text(each, "since"), StringComparer.Ordinal).ToList();
            var key = group.Key;
            var id = $"{key.Workspace}|{key.Host}|{key.Class}|{key.Holds}" + (key.Identity is null ? "" : $"|{key.Identity}");
            var row = Row("git-held", id, Text(held[0], "since"), key.Workspace);
            var git = new JsonObject
            {
                ["host"] = key.Host,
                ["class"] = key.Class,
                ["holds"] = key.Holds,
                ["remote"] = Text(held[0], "remote"),
                ["repositories"] = Names(held.Select(each => Text(each, "repository"))),
                ["words"] = Text(held[0], "words"),
            };
            foreach (var field in (string[])["way", "served", "identity", "identityId", "keyPage"])
            {
                if (Text(held[0], field) is { } value) git[field] = value;
            }

            row["git"] = git;
            if (GitWorkWaits(facts, key.Workspace, held)) row["stands"] = true;
            rows.Add(row);
        }

        // A git go-ahead: a door holds a remote act whose class is ask until the person allows
        // or refuses it, or its bound passes; its session's where its marker named one, else its repository's. Scoped as its repository's
        // workspace; one with no workspace known is the machine's, every window's.
        foreach (var ask in facts.GitAsks)
        {
            var row = Row("git-ask", Text(ask, "id"), Text(ask, "at"), Text(ask, "workspace"));
            row["where"] = Text(ask, "repository") ?? Text(ask, "host");
            if (Text(ask, "session") is { } session) row["session"] = session;
            row["gitAsk"] = new JsonObject
            {
                ["repository"] = Text(ask, "repository"),
                ["remote"] = Text(ask, "remote"),
                ["host"] = Text(ask, "host"),
                ["class"] = Text(ask, "class"),
                ["session"] = Text(ask, "session"),
                ["gone"] = ask["gone"] is JsonValue gone && gone.TryGetValue<bool>(out var stopped) && stopped,
            };
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// The entries a unit's clean-up waits on the person for, as the work inventory's row-level wait: the blocked
    /// entries its clean-up is blocked on, else the unit itself by its first entry; where its owner is known, the trees its session recorded
    /// and no branch record follows, which only the clean-up's press takes. A guard the tidy keeps a branch by has no act of the person's
    /// here, and a pending wait ends by itself.
    /// </summary>
    private static List<JsonObject> WaitsOnPerson(JsonObject unit)
    {
        var blocked = Objects((unit["cleanup"] as JsonObject)?["waits"]).Where(entry => Text(entry, "disposition") == "blocked").ToList();
        // And a landing's branch whose quest is closed and which the line has not held for a day, which the person withdraws.
        if (Text(unit["end"] as JsonObject, "state") != "unknown")
        {
            return [.. blocked.Where(entry => Text(entry, "code") is InventoryCode.Unfollowed or InventoryCode.LandingStale)];
        }
        return blocked.Count > 0 ? blocked : [.. Objects(unit["entries"]).Take(1)];
    }

    /// <summary>A blocked entry as the row carries it: what it is, the driver's sentence, and the code the page words it by, with its facts.</summary>
    private static JsonNode Blocked(JsonObject entry)
    {
        var said = new JsonObject { ["kind"] = Text(entry, "kind"), ["resource"] = Text(entry, "resource"), ["detail"] = Text(entry, "detail") };
        if (Truthy(entry, "code") is { } code) said["code"] = code;
        if (entry["facts"] is JsonObject facts) said["facts"] = facts.DeepClone();
        return said;
    }

    /// <summary>The first tree entry's folder by its name: what a unit with no branch is called, never its path.</summary>
    private static string? TreeName(IEnumerable<JsonObject> entries) =>
        entries.Where(entry => Text(entry, "kind") == InventoryKind.Tree).Select(entry => Text(entry, "resource")).OfType<string>()
            .Select(path => path.Replace('\\', '/').TrimEnd('/').Split('/')[^1]).FirstOrDefault(name => name.Length > 0);

    /// <summary>
    /// Whether a held git group holds work: a session of one of its repositories in its workspace
    /// runs a turn or waits on the person, so its next remote act waits on this row.
    /// </summary>
    private static bool GitWorkWaits(AttentionFacts facts, string workspace, IReadOnlyList<JsonObject> held)
    {
        var repositories = held.Select(each => Text(each, "repository")).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return facts.Sessions.Any(session =>
            Text(session, "repository") is { } repository && repositories.Contains(repository)
            && (Text(session, "workspace") ?? "default") == workspace
            && (AttentionSessionRows.TurnRuns.Contains(Text(session, "state") ?? "") || SessionStates.IsParked(Text(session, "state"))));
    }
}
