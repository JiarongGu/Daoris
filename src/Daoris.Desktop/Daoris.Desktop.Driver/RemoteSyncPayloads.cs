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
    /// The session records worth feeding: joined repositories only, and never the transcript — the
    /// field is dropped here, at parse, so no later step could forward it. Null when nothing qualifies.
    /// </summary>
    public static (string Json, int Count)? Sessions(string sessionsJson, IReadOnlySet<string> joined)
    {
        using var document = JsonDocument.Parse(sessionsJson);
        var count = 0;
        var json = Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("records");
            foreach (var session in document.RootElement.EnumerateArray())
            {
                var repository = Text(session, "repository") ?? "";
                var id = Text(session, "id") ?? "";
                // A record already carrying an origin is somebody else's, mirrored here — feeding it
                // back would launder another machine's record through this machine's identity.
                if (!joined.Contains(repository) || id.Contains('/')) continue;

                count++;
                writer.WriteStartObject();
                writer.WriteString("id", id);
                writer.WriteString("quest", Text(session, "quest"));
                writer.WriteString("repository", repository);
                writer.WriteString("adapter", Text(session, "adapter"));
                writer.WriteString("state", Text(session, "state"));
                Copy(writer, session, "note");
                Copy(writer, session, "evidence");
                writer.WriteString("created", Text(session, "created"));
                writer.WriteString("updated", Text(session, "updated"));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        return count == 0 ? null : (json, count);
    }

    /// <summary>
    /// One sharing repository's content for the remote's ingest, stamped with the point in its history
    /// that it came from (D48 §6).
    /// </summary>
    /// <remarks>
    /// An empty list still feeds: the remote's copy is a replacement, and a repository that deleted its
    /// knowledge means the deletion. That is only safe because the provenance travels with it — a
    /// replacement the receiver cannot order against what it holds is how two machines flap.
    /// </remarks>
    public static (string Json, int Count) Entries(
        string repository, string entriesJson, TreeProvenance? provenance)
    {
        using var document = JsonDocument.Parse(entriesJson);
        var count = 0;
        var json = Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("repository", repository);
            if (provenance is not null)
            {
                writer.WriteString("commit", provenance.Commit);
                writer.WriteString("committedAt", provenance.CommittedAt.ToString("O"));
                writer.WriteString("branch", provenance.Branch);
            }

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

    /// <summary>
    /// The remote quests this machine mirrors: those touching its joined repositories, as sender or
    /// receiver. Everything else on the remote is other people's business. Null when nothing qualifies.
    /// </summary>
    public static (string Json, int Count)? Quests(string remoteQuestsJson, IReadOnlySet<string> joined)
    {
        using var document = JsonDocument.Parse(remoteQuestsJson);
        var count = 0;
        var json = Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("quests");
            foreach (var quest in document.RootElement.EnumerateArray())
            {
                var from = Text(quest, "from") ?? "";
                var to = Text(quest, "to") ?? "";
                if (!joined.Contains(from) && !joined.Contains(to)) continue;

                count++;
                writer.WriteStartObject();
                writer.WriteString("id", Text(quest, "id"));
                writer.WriteString("from", from);
                writer.WriteString("to", to);
                writer.WriteString("title", Text(quest, "title"));
                writer.WriteString("body", Text(quest, "body"));
                writer.WriteString("status", Text(quest, "status"));
                Copy(writer, quest, "note");
                writer.WriteString("filed", Text(quest, "filed"));
                writer.WriteString("updated", Text(quest, "updated"));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        return count == 0 ? null : (json, count);
    }

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
