using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Where a circle stands on this machine (SYNC6a): what it has not pushed, the quests its last pass
/// could not bring level, the quests carrying a conflict, and how that pass ended.
/// </summary>
/// <param name="Synced">When a pass last reached the remote; a wall does not move it.</param>
/// <param name="Tried">When a pass last ran, reaching the remote or not.</param>
/// <param name="Problem">The wall the last pass hit; null when it reached the remote.</param>
public sealed record SyncStanding(
    string Workspace, bool Wired, int Ahead, IReadOnlyList<string> Behind, IReadOnlyList<string> Conflicts,
    DateTimeOffset? Synced, DateTimeOffset? Tried, string? Problem)
{
    /// <summary>
    /// As a terminal prints it: the circle and its counts on one line, then the quests each count
    /// names, then the wall as the last TRY, beside the last time the circle reached its remote.
    /// </summary>
    public IReadOnlyList<string> Describe()
    {
        if (!Wired) return [$"{Workspace}  no remote on this machine's host — nothing of this workspace leaves it"];

        var lines = new List<string>
        {
            $"{Workspace}  {Ahead} ahead · {Behind.Count} behind · {Conflicts.Count} in conflict · "
            + (Synced is { } synced ? $"synced {synced.UtcDateTime:yyyy-MM-dd HH:mm}Z" : "not synced yet"),
        };
        if (Conflicts.Count > 0) lines.Add($"  in conflict: {string.Join(", ", Conflicts.Select(id => $"#{id}"))}");
        if (Behind.Count > 0) lines.Add($"  behind: {string.Join(", ", Behind.Select(id => $"#{id}"))}");
        if (Problem is not null)
        {
            // Only WHEN: the wall is the host's own sentence and already says what went wrong.
            lines.Add($"  the last try{(Tried is { } tried ? $", {tried.UtcDateTime:HH:mm}Z" : "")}: {Problem}");
        }

        return lines;
    }
}

/// <summary>
/// The pure half of the sync: what leaves this machine and what comes back, built from the doors' own
/// JSON. Pure so the boundary is testable where it matters most — the payloads these functions build
/// are the disclosure boundary in practice, and none of them has a field for a machine path. A root or
/// a transcript in the input is dropped at PARSE time; there is no branch that could forward one.
/// </summary>
public static class RemoteSyncPayloads
{
    /// <param name="SharesKnowledge">Whether its knowledge content feeds too — its manifest's second
    /// declaration (D47 §4). The registration itself travels for every joined repository.</param>
    /// <param name="Root">
    /// The checkout on THIS machine — kept so the driver can ask git where the tree stands (D48 §6),
    /// and never written into any payload. The disclosure guarantee lives in the payload builders
    /// below, each of which has no field for a machine path; the tests assert on what goes on the
    /// wire, which is the only place the guarantee can be broken.
    /// </param>
    /// <param name="Uses">What it says it depends on (D91), carried with the rest of the declaration.</param>
    /// <param name="Lanes">The lanes it declares (D115 §2.2), as their words; none when the registry answered none.</param>
    public sealed record JoinedRepository(
        string Repository, string? Summary, IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts,
        IReadOnlyList<string> Packs, bool SharesKnowledge, string Root, IReadOnlyList<string>? Uses = null,
        IReadOnlyList<LaneView>? Lanes = null);

    /// <summary>Every repository a registry answer names — joined or not, adopted or not.</summary>
    public static IReadOnlySet<string> Names(string registryJson)
    {
        using var document = JsonDocument.Parse(registryJson);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in document.RootElement.EnumerateArray())
        {
            if (Text(repo, "repository") is { Length: > 0 } name) names.Add(name);
        }

        return names;
    }

    /// <summary>The retires this machine owes a circle, by name, as its host answered (SYNC5b).</summary>
    public static IReadOnlyList<string> Retired(string retiredJson)
    {
        using var document = JsonDocument.Parse(retiredJson);
        return document.RootElement.ValueKind == JsonValueKind.Object ? Strings(document.RootElement, "repositories") : [];
    }

    private static bool InCircle(JsonElement repo, string workspace) =>
        string.Equals(
            RemoteTarget.Workspace(Text(repo, "workspace")),
            RemoteTarget.Workspace(workspace),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The joined repositories in a local registry answer that belong to one WORKSPACE — the only ones
    /// that remote may hear of FROM here (D48 §5).
    /// </summary>
    /// <remarks>
    /// Joined alone is not enough, on three counts. Mirrored-down teammate rows carry the flag too, and
    /// feeding one back up would speak for a repository this machine cannot see — worse, its empty
    /// entries feed is a replacement, wiping the teammate's shared knowledge from a machine that never
    /// had it. The root is the checkout and the checkout is the authority (D47 §5), so a root is
    /// required — and the local host answers roots to this loopback caller, so a rootless row IS a
    /// foreign one. And the workspace must match: a repository joined in another circle is a
    /// declaration addressed to a different deployment, which is the whole of the boundary.
    /// </remarks>
    public static IReadOnlyList<JoinedRepository> Joined(string registryJson, string workspace)
    {
        using var document = JsonDocument.Parse(registryJson);
        var joined = new List<JoinedRepository>();
        foreach (var repo in document.RootElement.EnumerateArray())
        {
            if (!(repo.TryGetProperty("joined", out var j) && j.ValueKind == JsonValueKind.True)) continue;
            if (Text(repo, "root") is not { Length: > 0 } root) continue;
            if (!InCircle(repo, workspace)) continue;

            joined.Add(new JoinedRepository(
                Text(repo, "repository") ?? "",
                Text(repo, "summary"),
                Strings(repo, "owns"),
                Strings(repo, "accepts"),
                Strings(repo, "packs"),
                repo.TryGetProperty("sharesKnowledge", out var s) && s.ValueKind == JsonValueKind.True,
                root,
                Strings(repo, "uses"),
                Lanes(repo)));
        }

        return joined;
    }

    /// <summary>
    /// One joined repository's registration, as the remote hears it: the declaration, no root — and the
    /// commit its manifest stands on, so the deployment can order it against another checkout's (SYNC5b).
    /// </summary>
    /// <param name="defaultBranch">
    /// The repository's canonical line, read from this checkout (D48 §6) — the deployment cannot ask
    /// git, so the machine holding the tree tells it. Omitted when git could not say, and omission
    /// PRESERVES whatever was declared before: an unstated field must never erase one.
    /// </param>
    /// <param name="declaredAt">The commit the manifest was read at; null when it names none — a checkout
    /// git cannot answer for, or a manifest with changes of its own not yet committed.</param>
    /// <param name="onBase">The held declaration's commit git said this checkout descends from.</param>
    public static string Registration(
        JoinedRepository repo, string? defaultBranch = null, TreeProvenance? declaredAt = null, string? onBase = null) => Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("repository", repo.Repository);
        if (!string.IsNullOrWhiteSpace(defaultBranch)) writer.WriteString("defaultBranch", defaultBranch);
        WriteProvenance(writer, declaredAt, onBase);
        writer.WriteStartArray("packs");
        foreach (var pack in repo.Packs) writer.WriteStringValue(pack);
        writer.WriteEndArray();
        writer.WriteStartObject("domain");
        if (repo.Summary is not null) writer.WriteString("summary", repo.Summary);
        writer.WriteStartArray("owns");
        foreach (var owns in repo.Owns) writer.WriteStringValue(owns);
        writer.WriteEndArray();
        writer.WriteStartArray("accepts");
        foreach (var accepts in repo.Accepts) writer.WriteStringValue(accepts);
        writer.WriteEndArray();
        WriteUses(writer, repo.Uses ?? []);
        writer.WriteEndObject();
        writer.WriteBoolean("join", true);
        writer.WriteBoolean("shareKnowledge", repo.SharesKnowledge);
        WriteLanes(writer, repo.Lanes ?? []);
        writer.WriteEndObject();
    });

    /// <summary>
    /// One sharing repository's content for the remote's ingest, stamped with the point in its history
    /// that it came from (D48 §6).
    /// </summary>
    /// <remarks>
    /// An empty list still feeds: the remote's copy is a replacement, and a repository that deleted its
    /// knowledge means the deletion. That is only safe because the provenance travels with it — a
    /// replacement the receiver cannot order against what it holds is how two machines flap.
    /// </remarks>
    /// <param name="onBase">The held commit git said this checkout descends from (SYNC5a); null lets
    /// commit time decide at the deployment.</param>
    public static (string Json, int Count) Entries(
        string repository, string entriesJson, TreeProvenance? provenance, string? onBase = null)
    {
        using var document = JsonDocument.Parse(entriesJson);
        var count = 0;
        var json = Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("repository", repository);
            WriteProvenance(writer, provenance, onBase);

            writer.WriteStartArray("entries");
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                count++;
                writer.WriteStartObject();
                writer.WriteString("kind", Text(entry, "kind"));
                writer.WriteString("title", Text(entry, "title"));
                writer.WriteString("body", Text(entry, "body"));
                writer.WriteString("relativePath", Text(entry, "path"));
                Copy(writer, entry, "anchor");
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        return (json, count);
    }

    private static void WriteProvenance(Utf8JsonWriter writer, TreeProvenance? provenance, string? onBase)
    {
        if (provenance is null) return;

        writer.WriteString("commit", provenance.Commit);
        writer.WriteString("committedAt", provenance.CommittedAt.ToString("O"));
        writer.WriteString("branch", provenance.Branch);
        if (!string.IsNullOrWhiteSpace(onBase)) writer.WriteString("base", onBase);
    }

    // ——— What is fed from a checkout besides its knowledge, and in what order (SYNC5a, MAP3b).

    /// <param name="Json">The feed, or null when there is nothing this machine may send.</param>
    /// <param name="Problem">Why nothing goes — the reader's own sentence about this checkout's file.</param>
    public sealed record CodeMapFeed(string? Json, string? Problem);

    /// <summary>
    /// This checkout's code map as the deployment hears it (MAP3b): the file it was read from and its
    /// text in the file's own shape, rebuilt from what this machine's host judged — or no map, which is
    /// how a deletion travels.
    /// </summary>
    /// <remarks>
    /// A map the host refused feeds nothing. The deployment would refuse it whole for the same reason,
    /// and only this side has the file to fix, so the problem is said here.
    /// </remarks>
    /// <param name="codeMapJson">The host's answer from its code-map door.</param>
    public static CodeMapFeed CodeMap(
        string repository, string codeMapJson, TreeProvenance provenance, string? onBase)
    {
        using var document = JsonDocument.Parse(codeMapJson);
        var root = document.RootElement;
        if (Text(root, "problem") is { Length: > 0 } problem) return new CodeMapFeed(null, problem);

        var file = Text(root, "file");
        string? map = file is null ? null : Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteStartArray("modules");
            foreach (var module in Items(root, "modules"))
            {
                writer.WriteStartObject();
                writer.WriteString("id", Text(module, "id"));
                writer.WriteString("path", Text(module, "path"));
                writer.WriteString("summary", Text(module, "summary"));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("dependencies");
            foreach (var dependency in Items(root, "dependencies"))
            {
                writer.WriteStartObject();
                writer.WriteString("from", Text(dependency, "from"));
                writer.WriteString("to", Text(dependency, "to"));
                writer.WriteString("kind", Text(dependency, "kind"));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        return new CodeMapFeed(Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("repository", repository);
            WriteProvenance(writer, provenance, onBase);
            if (file is null) writer.WriteNull("file");
            else writer.WriteString("file", file);
            if (map is null) writer.WriteNull("map");
            else writer.WriteString("map", map);
            writer.WriteEndObject();
        }), null);
    }

    /// <summary>Which commit the deployment holds each of a repository's feeds at; null where nothing has fed.</summary>
    public sealed record HeldCommits(string? Knowledge, string? CodeMap, string? Registration)
    {
        public static readonly HeldCommits None = new(null, null, null);
    }

    /// <summary>What the deployment holds for one repository, as its held door answered.</summary>
    public static HeldCommits Held(string heldJson)
    {
        using var document = JsonDocument.Parse(heldJson);
        var root = document.RootElement;
        return root.ValueKind == JsonValueKind.Object
            ? new HeldCommits(Text(root, "knowledge"), Text(root, "codeMap"), Text(root, "registration"))
            : HeldCommits.None;
    }

    /// <summary>Several things waiting on one commit, as one phrase: "a", "a and b", "a, b and c".</summary>
    public static string Whats(IReadOnlyList<string> whats) => whats.Count switch
    {
        0 => "",
        1 => whats[0],
        _ => $"{string.Join(", ", whats.Take(whats.Count - 1))} and {whats[^1]}",
    };

    /// <param name="Feed">Whether anything goes to the deployment.</param>
    /// <param name="Base">The held commit the feed names as its base, when git said it descends from it.</param>
    /// <param name="Note">What a person should hear when nothing goes; null when the feed goes.</param>
    public sealed record FeedPlan(bool Feed, string? Base, string? Note);

    /// <summary>
    /// Whether to feed, and on what base (SYNC5a) — this machine's half of the ordering, decided from
    /// what git answered about the held commit.
    /// </summary>
    /// <param name="what">What would be fed, as the note says it: "knowledge", "code map".</param>
    /// <param name="held">The commit the deployment holds, or null where nothing has fed.</param>
    /// <param name="relation">What git said; null when there was nothing to ask.</param>
    public static FeedPlan Order(
        string repository, string what, TreeProvenance here, string? held, TreeRelation? relation)
    {
        if (held is null) return new FeedPlan(true, null, null);
        if (string.Equals(held, here.Commit, StringComparison.OrdinalIgnoreCase)) return new FeedPlan(true, held, null);

        var shortHeld = held.Length <= 8 ? held : held[..8];
        var are = what.Contains(" and ", StringComparison.Ordinal) ? "are" : "is";
        return relation switch
        {
            TreeRelation.Descends => new FeedPlan(true, held, null),
            // Two lines from a common past: git cannot order them, so the deployment falls back to
            // commit time — the one question left that it can answer on its own.
            TreeRelation.Diverged => new FeedPlan(true, null, null),
            TreeRelation.Behind => new FeedPlan(false, null,
                $"`{repository}`'s {what} {are} held at `{shortHeld}`, which is ahead of this checkout "
                + $"(`{here.ShortCommit}`) — nothing fed. A pull catches this checkout up; nothing is wrong."),
            _ => new FeedPlan(false, null,
                $"`{repository}`'s {what} {are} held at `{shortHeld}`, a commit this checkout does not have — "
                + "nothing fed until a fetch lets git say how the two relate."),
        };
    }

    // ——— The quest sync (D69). The host runs the pass — fetch, rebase, push — because a take claims by
    // push from the door it was made at, and the tick runs the same code by asking for it. What comes
    // back to the driver is what a person should hear about the pass, never an operation.

    /// <param name="Wired">Whether the HOST has a remote for this circle — false when it reads another map than the driver.</param>
    /// <param name="Notes">What a person should hear: a move of this machine's that lost, a quest the remote would not keep, a quest still behind.</param>
    /// <param name="Problem">The wall the pass hit, in the host's words; null when it reached the remote.</param>
    public sealed record SyncPass(bool Wired, IReadOnlyList<string> Notes, string? Problem);

    /// <summary>The host's answer to a quest pass, as the notes and the wall the tick reports.</summary>
    public static SyncPass Pass(string passJson)
    {
        using var document = JsonDocument.Parse(passJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new DriverException($"a host answered a sync pass with something that is not one: {Clip(document.RootElement.GetRawText())}");
        }

        var root = document.RootElement;
        var notes = new List<string>();
        if (root.TryGetProperty("conflicts", out var lost) && lost.ValueKind == JsonValueKind.Array)
        {
            foreach (var conflict in lost.EnumerateArray())
            {
                notes.Add($"quest `#{Text(conflict, "quest")}`: this machine's `{Text(conflict, "attempted")}` reached the "
                          + "remote after another machine's move, and is kept on the quest as a conflict.");
            }
        }

        if (root.TryGetProperty("refused", out var no) && no.ValueKind == JsonValueKind.Array)
        {
            foreach (var refusal in no.EnumerateArray())
            {
                notes.Add($"quest `#{Text(refusal, "quest")}` was not taken by the remote: {Text(refusal, "reason")}");
            }
        }

        if (Strings(root, "behind") is { Count: > 0 } behind)
        {
            notes.Add($"{behind.Count} quest(s) moved at the remote on every round of the pass; what is pending stays "
                      + "pending, and the next pass goes round again.");
        }

        return new SyncPass(
            root.TryGetProperty("wired", out var wired) && wired.ValueKind == JsonValueKind.True,
            notes,
            Text(root, "problem"));
    }

    /// <summary>Where a circle stands on this machine (SYNC6a), as its host answered.</summary>
    public static SyncStanding Standing(string standingJson)
    {
        using var document = JsonDocument.Parse(standingJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new DriverException($"a host answered a workspace's standing with something that is not one: {Clip(root.GetRawText())}");
        }

        DateTimeOffset? At(string name) =>
            Text(root, name) is { } text ? DateTimeOffset.Parse(text, null, System.Globalization.DateTimeStyles.RoundtripKind) : null;

        return new SyncStanding(
            Text(root, "workspace") ?? "default",
            root.TryGetProperty("wired", out var wired) && wired.ValueKind == JsonValueKind.True,
            root.TryGetProperty("ahead", out var ahead) && ahead.ValueKind == JsonValueKind.Number ? ahead.GetInt32() : 0,
            Strings(root, "behind"),
            Strings(root, "conflicts"),
            At("synced"),
            At("tried"),
            Text(root, "problem"));
    }

    /// <summary>Where this machine's claim on a quest stands, as its host answered: none, held, unconfirmed or lost.</summary>
    public static string Claim(string claimJson)
    {
        using var document = JsonDocument.Parse(claimJson);
        return document.RootElement.ValueKind == JsonValueKind.Object ? Text(document.RootElement, "claim") ?? "none" : "none";
    }

    private static string Clip(string text) => text.Length <= 120 ? text : text[..120] + "…";

    /// <param name="Write">The team's rows to write here — new, or with a declaration that changed.</param>
    /// <param name="Retire">This circle's copies the circle no longer lists, to retire here.</param>
    public sealed record RegistryMirror(IReadOnlyList<(string Repository, string Json)> Write, IReadOnlyList<string> Retire);

    /// <summary>
    /// The remote's registry as this machine should keep it (SYNC5b): the TEAM's rows, written when new
    /// or changed, and this circle's copies retired when the circle no longer lists them — filed in the
    /// workspace whose deployment answered.
    /// </summary>
    /// <remarks>
    /// <para>A repository this machine holds with a root, in ANY circle, keeps its own registration — root
    /// included — because the machine that holds the checkout is the authority on it; writing the
    /// remote's stripped copy over it would erase the one field spawning needs. A copy another circle's
    /// sync keeps is that sync's, or two remotes naming one repository would take turns re-filing it. What
    /// arrives makes teammates' repositories addressable here (D47 §5).</para>
    ///
    /// <para>The workspace is stated rather than left silent, and that is this MACHINE's wiring
    /// speaking, not the feed: a row that came from this workspace's deployment belongs to this
    /// workspace by construction (D48 §5). Nothing in the remote's answer is consulted for it — a feed
    /// that could name its own circle could file itself into one nobody joined.</para>
    /// </remarks>
    /// <param name="localRegistryJson">This machine's registry, unscoped: the guard spans every circle.</param>
    /// <param name="retiredHere">What this pass retired at the circle — still in the list it read before.</param>
    public static RegistryMirror Mirror(
        string remoteRegistryJson, string localRegistryJson, string workspace, IReadOnlyCollection<string> retiredHere)
    {
        using var local = JsonDocument.Parse(localRegistryJson);
        var copies = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var guarded = new HashSet<string>(retiredHere, StringComparer.OrdinalIgnoreCase);
        foreach (var repo in local.RootElement.EnumerateArray())
        {
            if (Text(repo, "repository") is not { Length: > 0 } name) continue;
            if (Text(repo, "root") is not { Length: > 0 } && InCircle(repo, workspace)) copies[name] = repo.Clone();
            else guarded.Add(name);
        }

        using var document = JsonDocument.Parse(remoteRegistryJson);
        var write = new List<(string, string)>();
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in document.RootElement.EnumerateArray())
        {
            var name = Text(repo, "repository") ?? "";
            if (name.Length == 0) continue;
            listed.Add(name);
            if (guarded.Contains(name)) continue;
            if (copies.TryGetValue(name, out var copy) && Declares(copy) == Declares(repo)) continue;

            write.Add((name, TeamCopy(repo, name, workspace)));
        }

        return new RegistryMirror(write, copies.Keys.Where(name => !listed.Contains(name)).Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>What a row declares, as one comparable value — the fields a copy is written from.</summary>
    private static string Declares(JsonElement repo) => string.Join(
        "\u001f",
        Text(repo, "summary") ?? "\u0000",
        string.Join("\u001e", Strings(repo, "owns")),
        string.Join("\u001e", Strings(repo, "accepts")),
        string.Join("\u001e", Strings(repo, "packs")),
        repo.TryGetProperty("joined", out var j) && j.ValueKind == JsonValueKind.True,
        repo.TryGetProperty("sharesKnowledge", out var s) && s.ValueKind == JsonValueKind.True,
        string.Join("\u001e", Strings(repo, "uses")),
        string.Join("\u001e", Lanes(repo).Select(lane => $"{lane.Id}\u001d{lane.Title}\u001d{lane.Summary}\u001d{lane.Steward}")));

    /// <summary>A row's lanes (D115 §2.2), as a registry answers them: each lane's words, in order.</summary>
    private static IReadOnlyList<LaneView> Lanes(JsonElement repo) =>
        Items(repo, "lanes")
            .Where(lane => Text(lane, "id") is { Length: > 0 })
            .Select(lane => new LaneView(
                Text(lane, "id")!, Text(lane, "title") ?? "", Text(lane, "summary") ?? "",
                lane.TryGetProperty("steward", out var steward) && steward.ValueKind == JsonValueKind.True))
            .ToList();

    /// <summary>
    /// A repository's lanes (D115 §2.2), written ALWAYS, `[]` for none: the deployment keeps a row's
    /// lanes when a registration says nothing of them, so silence here could never take one away.
    /// </summary>
    private static void WriteLanes(Utf8JsonWriter writer, IReadOnlyList<LaneView> lanes)
    {
        writer.WriteStartArray("lanes");
        foreach (var lane in lanes)
        {
            writer.WriteStartObject();
            writer.WriteString("id", lane.Id);
            writer.WriteString("title", lane.Title);
            writer.WriteString("summary", lane.Summary);
            writer.WriteBoolean("steward", lane.Steward);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// What a repository says it uses (D91), written only when it says something: a declaration of none
    /// goes on the wire exactly as it did before the field existed.
    /// </summary>
    private static void WriteUses(Utf8JsonWriter writer, IReadOnlyList<string> uses)
    {
        if (uses.Count == 0) return;
        writer.WriteStartArray("uses");
        foreach (var name in uses) writer.WriteStringValue(name);
        writer.WriteEndArray();
    }

    /// <summary>A teammate's row as this machine files it: the declaration, this circle's name, no root.</summary>
    private static string TeamCopy(JsonElement repo, string name, string workspace) => Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("repository", name);
        writer.WriteStartArray("packs");
        foreach (var pack in Strings(repo, "packs")) writer.WriteStringValue(pack);
        writer.WriteEndArray();
        writer.WriteStartObject("domain");
        if (Text(repo, "summary") is { } summary) writer.WriteString("summary", summary);
        writer.WriteStartArray("owns");
        foreach (var owns in Strings(repo, "owns")) writer.WriteStringValue(owns);
        writer.WriteEndArray();
        writer.WriteStartArray("accepts");
        foreach (var accepts in Strings(repo, "accepts")) writer.WriteStringValue(accepts);
        writer.WriteEndArray();
        WriteUses(writer, Strings(repo, "uses"));
        writer.WriteEndObject();
        writer.WriteBoolean("join", repo.TryGetProperty("joined", out var j) && j.ValueKind == JsonValueKind.True);
        writer.WriteBoolean("shareKnowledge",
            repo.TryGetProperty("sharesKnowledge", out var s) && s.ValueKind == JsonValueKind.True);
        writer.WriteString("workspace", RemoteTarget.Workspace(workspace));
        WriteLanes(writer, Lanes(repo));
        writer.WriteEndObject();
    });

    private static void Copy(Utf8JsonWriter writer, JsonElement element, string name)
    {
        if (Text(element, name) is { } value) writer.WriteString(name, value);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IEnumerable<JsonElement> Items(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).ToList()
            : [];

    private static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return [];

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text) items.Add(text);
        }

        return items;
    }

    private static string Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
