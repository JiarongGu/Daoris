using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A brainstorm's and a meeting's files in the shape this build reads (D232; AGENTPROVIDER1t-ii): the pass records, the document, the
/// room measures and the meeting's packet name the door a pass ran on <c>door</c>, where they said <c>adapter</c>. A meeting's packet is
/// named by the digest of its content, so a packet whose names changed is written again under the digest of what it now holds. A tool's
/// answers are not ours and are left as they are; a file already in today's shape is left as it is.
/// </summary>
public sealed class BrainstormFilesStep : IHomeMigrationStep
{
    public string Name => "brainstorm-files";

    public void Run(HomeMigrationRun run)
    {
        var folders = DaorisHome.Entry(run.Home, HomeEntry.Brainstorms);
        if (Directory.Exists(folders))
        {
            foreach (var file in Directory.EnumerateFiles(folders, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                if (Ours(folders, file)) Convert(run, file);
            }
        }

        var rooms = Path.GetDirectoryName(DaorisHome.Entry(run.Home, HomeEntry.BrainstormRooms, "x"))!;
        if (!Directory.Exists(rooms)) return;
        foreach (var file in Directory.EnumerateFiles(rooms, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal)) Convert(run, file);
    }

    /// <summary>A pass record, a document or a packet; never a tool's answer or a request.</summary>
    private static bool Ours(string root, string file)
    {
        var parts = Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar);
        return parts[^1] is "document.json" or "packet.json" || parts.Length > 1 && parts[^2] == "passes";
    }

    private static void Convert(HomeMigrationRun run, string file)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(AtomicFile.ReadText(file));
        }
        catch (System.Text.Json.JsonException)
        {
            return;
        }

        if (node is null || !Renamed(node)) return;
        var text = JsonFile.Serialize(node);
        if (node is JsonObject { } packet && packet.ContainsKey("meeting")
            && MeetingPackets.Read(System.Text.Encoding.UTF8.GetBytes(text), digestHeld: false) is { } meeting)
        {
            text = System.Text.Encoding.UTF8.GetString(MeetingPackets.Bytes(meeting));
        }

        run.Write(file, text);
    }

    private static bool Renamed(JsonNode node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject members:
                if (members.ContainsKey("adapter") && !members.ContainsKey("door"))
                {
                    members["door"] = members["adapter"]?.DeepClone();
                    members.Remove("adapter");
                    changed = true;
                }

                foreach (var child in members.Select(each => each.Value).OfType<JsonNode>().ToList()) changed |= Renamed(child);
                break;
            case JsonArray items:
                foreach (var child in items.OfType<JsonNode>().ToList()) changed |= Renamed(child);
                break;
        }

        return changed;
    }
}
