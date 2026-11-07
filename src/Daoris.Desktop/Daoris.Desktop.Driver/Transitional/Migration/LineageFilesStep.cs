using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The sessions' lineage files in the shape this build reads: a call line's <c>harness</c>, the door that made the call, becomes <c>door</c>,
/// and a child line the protocol door announced, <c>"by":"acp"</c>, becomes <c>"by":"announced"</c>. Only a file that holds an old line is
/// read whole and written again; a line already in today's shape is left as it is.
/// </summary>
public sealed class LineageFilesStep : IHomeMigrationStep
{
    private static readonly string[] Signs = ["\"harness\"", "\"acp\""];
    private static readonly JsonSerializerOptions Spelled = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string Name => "lineage-files";

    public void Run(HomeMigrationRun run)
    {
        var folder = DaorisHome.Entry(run.Home, HomeEntry.Sessions);
        if (!Directory.Exists(folder)) return;
        foreach (var session in Directory.EnumerateDirectories(folder).Order(StringComparer.Ordinal))
        {
            var path = Path.Combine(session, ChildLineage.FileName);
            if (!File.Exists(path)) continue;
            var whole = AtomicFile.ReadText(path);
            if (!Signs.Any(sign => whole.Contains(sign, StringComparison.Ordinal))) continue;
            var text = new StringBuilder();
            var changed = false;
            using var lines = new StringReader(whole);
            for (var line = lines.ReadLine(); line is not null; line = lines.ReadLine())
            {
                var next = Signs.Any(sign => line.Contains(sign, StringComparison.Ordinal)) ? Converted(line) : line;
                changed |= !ReferenceEquals(next, line);
                text.Append(next).Append('\n');
            }

            if (changed) run.Write(path, text.ToString());
        }
    }

    /// <summary>The line as this build writes it, or the same line where nothing in it is old.</summary>
    internal static string Converted(string line)
    {
        JsonObject root;
        try
        {
            if (JsonNode.Parse(line) is not JsonObject read) return line;
            root = read;
        }
        catch (JsonException)
        {
            return line;
        }

        var changed = false;
        if (root.ContainsKey("harness"))
        {
            var door = root["harness"];
            root.Remove("harness");
            if (!root.ContainsKey("door") && door is not null) root["door"] = door.DeepClone();
            changed = true;
        }

        if (HomeMigrationRun.Text(root["by"]) == "acp")
        {
            root["by"] = "announced";
            changed = true;
        }

        return changed ? root.ToJsonString(Spelled) : line;
    }
}
