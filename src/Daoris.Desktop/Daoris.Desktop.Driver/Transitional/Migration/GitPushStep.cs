using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>no-push</c>'s switch as git access (ONEWAY1q; the one-way design §5 G2): a home whose <c>permissions.json</c> lists <c>no-push</c> in
/// <c>defaultsOff</c> lists <c>no-publish</c> there instead, since that switch also turned off the publishing sentence; and where this
/// machine's Write is unset, <c>driver.json</c>'s <c>machineGitAccess.write</c> becomes <c>allow</c>, so its sessions push as they did. A
/// Write the person set is left as it is: the switch counted only where Write was unset.
/// </summary>
public sealed class GitPushStep : IHomeMigrationStep
{
    private const string NoPush = PermissionRules.NoPushId;

    public string Name => "git-push";

    public void Run(HomeMigrationRun run)
    {
        var permissions = DaorisHome.Entry(run.Home, HomeEntry.Permissions);
        if (!SwitchedOff(permissions)) return;

        var config = DaorisHome.Entry(run.Home, HomeEntry.DriverConfig);
        if (File.Exists(config)) run.ChangeJson(config, AllowWrite);
        else
        {
            var root = new JsonObject();
            AllowWrite(root);
            Directory.CreateDirectory(Path.GetDirectoryName(config)!);
            run.Write(config, JsonFile.Serialize(root));
        }

        run.ChangeJson(permissions, root =>
        {
            if (root["defaultsOff"] is not JsonArray off) return false;
            var ids = off.Select(HomeMigrationRun.Text).Where(id => id is not null && id != NoPush).Cast<string>().ToList();
            if (!ids.Contains(PermissionRules.NoPublishId, StringComparer.Ordinal)) ids.Add(PermissionRules.NoPublishId);
            root["defaultsOff"] = new JsonArray([.. ids.Order(StringComparer.Ordinal).Select(id => (JsonNode)id)]);
            return true;
        });
    }

    /// <summary>Whether the file lists <c>no-push</c> among the defaults switched off.</summary>
    private static bool SwitchedOff(string permissions)
    {
        if (!File.Exists(permissions)) return false;
        try
        {
            return JsonNode.Parse(AtomicFile.ReadText(permissions)) is JsonObject root && root["defaultsOff"] is JsonArray off
                && off.Any(each => HomeMigrationRun.Text(each) == NoPush);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    /// <summary>The machine's Write as <c>allow</c> where the file sets none.</summary>
    private static bool AllowWrite(JsonObject root)
    {
        if (GitAccessKeys.Of(root).Machine.ContainsKey(GitAccessKeys.Write)) return false;
        if (root[GitAccessKeys.MachineKey] is not JsonObject machine) root[GitAccessKeys.MachineKey] = machine = new JsonObject();
        machine[GitAccessKeys.Write] = GitAccessKeys.Allow;
        return true;
    }
}
