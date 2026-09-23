using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

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
    public sealed record JoinedRepository(
        string Repository, string? Summary, IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts,
        IReadOnlyList<string> Packs, bool SharesKnowledge, string Root);

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
            if (!string.Equals(
                RemoteTarget.Workspace(Text(repo, "workspace")),
                RemoteTarget.Workspace(workspace),
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            joined.Add(new JoinedRepository(
                Text(repo, "repository") ?? "",
                Text(repo, "summary"),
                Strings(repo, "owns"),
                Strings(repo, "accepts"),
                Strings(repo, "packs"),
                repo.TryGetProperty("sharesKnowledge", out var s) && s.ValueKind == JsonValueKind.True,
                root));
        }

        return joined;
    }

    /// <summary>
    /// One joined repository's registration, as the remote hears it: the declaration, no root.
    /// </summary>
    /// <param name="defaultBranch">
    /// The repository's canonical line, read from this checkout (D48 §6) — the deployment cannot ask
    /// git, so the machine holding the tree tells it. Omitted when git could not say, and omission
    /// PRESERVES whatever was declared before: an unstated field must never erase one.
    /// </param>
    public static string Registration(JoinedRepository repo, string? defaultBranch = null) => Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("repository", repo.Repository);
        if (!string.IsNullOrWhiteSpace(defaultBranch)) writer.WriteString("defaultBranch", defaultBranch);
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
        writer.WriteEndObject();
        writer.WriteBoolean("join", true);
        writer.WriteBoolean("shareKnowledge", repo.SharesKnowledge);
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

    /// <summary>Which commit the deployment holds this repository's knowledge and code map at; null where nothing has fed.</summary>
    public static (string? Knowledge, string? CodeMap) Held(string heldJson)
    {
        using var document = JsonDocument.Parse(heldJson);
        var root = document.RootElement;
        return root.ValueKind == JsonValueKind.Object ? (Text(root, "knowledge"), Text(root, "codeMap")) : (null, null);
    }

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
        return relation switch
        {
            TreeRelation.Descends => new FeedPlan(true, held, null),
            // Two lines from a common past: git cannot order them, so the deployment falls back to
            // commit time — the one question left that it can answer on its own.
            TreeRelation.Diverged => new FeedPlan(true, null, null),
            TreeRelation.Behind => new FeedPlan(false, null,
                $"`{repository}`'s {what} is held at `{shortHeld}`, which is ahead of this checkout "
                + $"(`{here.ShortCommit}`) — nothing fed. A pull catches this checkout up; nothing is wrong."),
            _ => new FeedPlan(false, null,
                $"`{repository}`'s {what} is held at `{shortHeld}`, a commit this checkout does not have — "
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

    /// <summary>Where this machine's claim on a quest stands, as its host answered: none, held, unconfirmed or lost.</summary>
    public static string Claim(string claimJson)
    {
        using var document = JsonDocument.Parse(claimJson);
        return document.RootElement.ValueKind == JsonValueKind.Object ? Text(document.RootElement, "claim") ?? "none" : "none";
    }

    private static string Clip(string text) => text.Length <= 120 ? text : text[..120] + "…";

    /// <summary>
    /// The remote's registry as this machine should hear of it: FOREIGN rows only, filed in the
    /// workspace whose deployment answered.
    /// </summary>
    /// <remarks>
    /// <para>A repository this machine already has keeps its own registration — and its root — because
    /// the machine that holds the checkout is the authority on it; re-posting the remote's stripped
    /// copy would overwrite the one field spawning needs. What arrives makes teammates' repositories
    /// addressable here (D47 §5): their quests home at the remote, and the relay carries the verbs.</para>
    ///
    /// <para>The workspace is stated rather than left silent, and that is this MACHINE's wiring
    /// speaking, not the feed: a row that came from this workspace's deployment belongs to this
    /// workspace by construction (D48 §5). Nothing in the remote's answer is consulted for it — a feed
    /// that could name its own circle could file itself into one nobody joined.</para>
    /// </remarks>
    public static IReadOnlyList<(string Repository, string Json)> ForeignRegistrations(
        string remoteRegistryJson, IReadOnlySet<string> localNames, string workspace)
    {
        using var document = JsonDocument.Parse(remoteRegistryJson);
        var foreign = new List<(string, string)>();
        foreach (var repo in document.RootElement.EnumerateArray())
        {
            var name = Text(repo, "repository") ?? "";
            if (name.Length == 0 || localNames.Contains(name)) continue;

            var payload = Write(writer =>
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
                writer.WriteEndObject();
                writer.WriteBoolean("join", repo.TryGetProperty("joined", out var j) && j.ValueKind == JsonValueKind.True);
                writer.WriteBoolean("shareKnowledge",
                    repo.TryGetProperty("sharesKnowledge", out var s) && s.ValueKind == JsonValueKind.True);
                writer.WriteString("workspace", RemoteTarget.Workspace(workspace));
                writer.WriteEndObject();
            });
            foreign.Add((name, payload));
        }

        return foreign;
    }

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
