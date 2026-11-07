using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's wired files in the shape this build reads: each receipt's <c>harness</c>, the door it ran on, is
/// <c>door</c>, and its <c>door</c>, <c>pipe</c> or <c>acp</c>, is <c>transport</c>, <c>native</c> or <c>protocol</c>. A receipt that
/// already names a transport is one this build wrote and is left as it is.
/// </summary>
public sealed class WiredFilesStep : IHomeMigrationStep
{
    public string Name => "wired-files";

    public void Run(HomeMigrationRun run)
    {
        var folder = DaorisHome.Entry(run.Home, HomeEntry.Sessions);
        if (!Directory.Exists(folder)) return;
        foreach (var path in Directory.EnumerateFiles(folder, "*" + SessionHomeFiles.HelpWiredSuffix).Order(StringComparer.Ordinal))
        {
            run.ChangeJson(path, root => root["receipts"] is JsonArray receipts && Convert(receipts));
        }
    }

    private static bool Convert(JsonArray receipts)
    {
        var changed = false;
        foreach (var receipt in receipts.OfType<JsonObject>().Where(each => !each.ContainsKey("transport")))
        {
            var door = HomeMigrationRun.Text(receipt["harness"]);
            var old = HomeMigrationRun.Text(receipt["door"]);
            receipt.Remove("harness");
            receipt.Remove("door");
            receipt["transport"] = old == "acp" ? "protocol" : "native";
            if (door is not null) receipt["door"] = door;
            changed = true;
        }

        return changed;
    }
}
