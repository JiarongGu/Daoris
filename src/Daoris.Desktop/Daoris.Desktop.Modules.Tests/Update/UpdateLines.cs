using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>The `update.state` lines an updater said: each transition, read back from the machine log of a scratch home.</summary>
internal static class UpdateLines
{
    /// <summary>Every `update.state` line's data, oldest first.</summary>
    public static List<JsonObject> States(string dataHome) => Of(dataHome, "update.state");

    public static List<JsonObject> Of(string dataHome, string name)
    {
        var folder = DaorisHome.Entry(dataHome, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .Where(line => line["event"]!.GetValue<string>() == name)
            .Select(line => line["data"]!.AsObject())
            .ToList();
    }

    /// <summary>The transitions as `from>to:why`, in order.</summary>
    public static List<string> Said(string dataHome) =>
        [.. States(dataHome).Select(line => $"{line["from"]}>{line["to"]}:{line["why"]}")];

    public static List<JsonObject> To(string dataHome, string to) =>
        [.. States(dataHome).Where(line => line["to"]!.GetValue<string>() == to)];
}
