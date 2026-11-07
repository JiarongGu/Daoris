using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The usage file in the shape this build reads (D232; AGENTPROVIDER1t): each entry's <c>harness</c>, the door the session ran on, is
/// <c>door</c>. An entry that already says <c>door</c> is one this build wrote and is left as it is.
/// </summary>
public sealed class UsageFileStep : IHomeMigrationStep
{
    public string Name => "usage-file";

    public void Run(HomeMigrationRun run)
    {
        var path = DaorisHome.Entry(run.Home, HomeEntry.Usage);
        if (!File.Exists(path)) return;
        JsonArray entries;
        try
        {
            if (JsonNode.Parse(AtomicFile.ReadText(path)) is not JsonArray read) return;
            entries = read;
        }
        catch (JsonException)
        {
            return;
        }

        var changed = false;
        foreach (var entry in entries.OfType<JsonObject>().Where(each => each.ContainsKey("harness")))
        {
            var door = HomeMigrationRun.Text(entry["harness"]);
            entry.Remove("harness");
            if (door is not null && !entry.ContainsKey("door")) entry["door"] = door;
            changed = true;
        }

        if (changed) run.Write(path, JsonFile.Serialize(entries));
    }
}
