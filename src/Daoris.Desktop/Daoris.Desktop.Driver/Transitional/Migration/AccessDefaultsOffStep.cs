using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>no-push</c>, <c>no-publish</c> and <c>tree-guard</c> were defaults every agent met unless the person switched them off; they are now
/// opt-in. A home that never switched one off had it on, so it is now off (counted by id in the machine log, and the person turns it back on
/// on the agent rules page); a home that had switched one off keeps it off, and the id leaves <c>defaultsOff</c>, which no longer names it. It
/// ends when no home on a supported update path lists these ids in <c>defaultsOff</c>.
/// </summary>
public sealed class AccessDefaultsOffStep : IHomeMigrationStep
{
    private static readonly string[] Former = [PermissionRules.NoPushId, PermissionRules.NoPublishId, PermissionRules.TreeGuardId];

    public string Name => "access-defaults-off";

    public void Run(HomeMigrationRun run)
    {
        var path = DaorisHome.Entry(run.Home, HomeEntry.Permissions);
        var listed = new HashSet<string>(StringComparer.Ordinal);
        if (File.Exists(path))
        {
            run.ChangeJson(path, root =>
            {
                if (root["defaultsOff"] is not JsonArray off) return false;
                var ids = off.Select(HomeMigrationRun.Text).OfType<string>().ToList();
                foreach (var id in ids.Where(Former.Contains)) listed.Add(id);
                if (listed.Count == 0) return false;
                var kept = ids.Where(id => !Former.Contains(id)).ToList();
                if (kept.Count == 0) root.Remove("defaultsOff");
                else root["defaultsOff"] = new JsonArray([.. kept.Select(id => (JsonNode)id)]);
                return true;
            });
        }

        foreach (var id in Former)
        {
            run.Count(Name, listed.Contains(id) ? $"{id}.kept-off" : $"{id}.turned-off");
        }
    }
}
