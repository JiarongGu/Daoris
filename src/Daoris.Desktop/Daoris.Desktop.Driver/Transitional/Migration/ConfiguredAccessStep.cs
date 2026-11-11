using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Configured access as an older build kept it: housekeeping's operations and the chosen settings as lists of their own, each a
/// connector tool. Both are families of acts now (<see cref="ActFamilies"/>), so a clean-up granted becomes <c>clean-up</c>, a discard
/// <c>discard</c>, any chosen setting <c>settings</c>, and both lists leave the record; a look grants nothing, since it is a read.
/// It ends when the owner's install finds neither list; it is then deleted.
/// </summary>
public sealed class ConfiguredAccessStep : IHomeMigrationStep
{
    private const string Housekeeping = "housekeeping";

    private const string Settings = "settings";

    public string Name => "configured-access";

    public void Run(HomeMigrationRun run) => run.ChangeJson(ConfiguredAccess.PathOf(run.Home), file =>
    {
        if (!file.ContainsKey(Housekeeping) && !file.ContainsKey(Settings)) return false;
        var granted = Listed(file, ConfiguredAccess.ActsGrant);
        var housekeeping = Listed(file, Housekeeping);
        if (housekeeping.Contains("clean-up")) granted.Add(ActFamilies.CleanUp);
        if (housekeeping.Contains("discard-branch")) granted.Add(ActFamilies.Discard);
        if (Listed(file, Settings).Count > 0) granted.Add(ActFamilies.Settings);
        file.Remove(Housekeeping);
        file.Remove(Settings);
        var ordered = ActFamilies.Built.Where(granted.Contains).Concat(granted.Where(each => !ActFamilies.Built.Contains(each))).Distinct(StringComparer.Ordinal);
        file[ConfiguredAccess.ActsGrant] = new JsonArray([.. ordered.Select(each => (JsonNode)JsonValue.Create(each))]);
        return true;
    });

    private static List<string> Listed(JsonObject file, string key) =>
        file[key] is JsonArray array ? [.. array.Select(HomeMigrationRun.Text).OfType<string>()] : [];
}
