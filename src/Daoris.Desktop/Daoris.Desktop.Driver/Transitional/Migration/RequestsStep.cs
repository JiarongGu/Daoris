using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The waiting request files in the shape this build reads: a say that waits with <c>now: true</c> and no <c>level</c>
/// written with <c>level: now</c>, the old field taken out.
/// </summary>
public sealed class RequestsStep : IHomeMigrationStep
{
    public string Name => "requests";

    public void Run(HomeMigrationRun run)
    {
        var folder = DaorisHome.Entry(run.Home, HomeEntry.Requests);
        if (!Directory.Exists(folder)) return;
        foreach (var path in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            run.ChangeJson(path, root =>
            {
                if (!root.ContainsKey("now")) return false;
                var now = root["now"] is JsonValue flag && flag.TryGetValue<bool>(out var on) && on;
                root.Remove("now");
                if (now && root["level"] is null) root["level"] = MessageLevels.Now;
                return true;
            });
        }
    }
}
