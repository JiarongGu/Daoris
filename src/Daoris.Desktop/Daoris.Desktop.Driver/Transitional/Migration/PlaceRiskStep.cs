using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The place risk record (<see cref="PlaceRiskBook"/>) written once into <c>driver.json</c> with today's implicit person-holds made explicit, so
/// no live routing moves: the machine's level and each supervisor's level held by the person's rules or by nobody gain the owner's
/// <c>line</c> mark there, every place left low. Nothing is written where the record already stands.
/// </summary>
/// <remarks>
/// The seed's machine supervisor reads as held by rules until the store holds a record of it, so a home with none is marked at the machine.
/// It ends with the record's older reading (<see cref="PlaceRiskBook.Written"/>): once no home on a supported update path lacks the record.
/// </remarks>
public sealed class PlaceRiskStep : IHomeMigrationStep
{
    public string Name => "place-risk";

    private const string Records = "SELECT id, COALESCE(record, '') FROM roles";

    public void Run(HomeMigrationRun run)
    {
        var path = DaorisHome.Entry(run.Home, HomeEntry.DriverConfig);
        if (!File.Exists(path)) return;
        var held = PersonHeld(run.Home);
        run.ChangeJson(path, root =>
        {
            if (root.ContainsKey(PlaceRiskBook.Key)) return false;
            var places = new JsonObject();
            foreach (var key in held.Order(StringComparer.Ordinal))
            {
                places[key] = new JsonObject { ["risk"] = PlaceRisks.Low, ["marks"] = new JsonArray(PlaceMarks.Line) };
            }

            root[PlaceRiskBook.Key] = places;
            return true;
        });
    }

    /// <summary>The places whose supervisor level the person holds today: each supervisor's held by rules or nobody, and the machine's seed.</summary>
    internal static IReadOnlySet<string> PersonHeld(string home)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var staffing = new RoleStaffing(home, () => StoreSettled.Yes);
        var seedOwn = false;
        using (var read = DesktopStoreReader.Open(staffing.Location))
        {
            var rows = read is null ? [] : read.Query(Records, row => (row.GetString(0), row.GetString(1)));
            foreach (var (id, record) in rows)
            {
                if (id == AppointedRoles.Supervisor) seedOwn = true;
                if (Supervisor(record) is { } key) keys.Add(key);
            }
        }

        if (!seedOwn) keys.Add(PlaceRiskBook.MachineKey);
        return keys;
    }

    /// <summary>The place key of a supervisor's record held by rules or by nobody; none for any other record, or one that does not read.</summary>
    private static string? Supervisor(string record)
    {
        try
        {
            if (JsonNode.Parse(record) is not JsonObject root || HomeMigrationRun.Text(root["kind"]) != AppointedRoles.Supervisor) return null;
            if (HomeMigrationRun.Text(root["holder"]) is RoleHolders.Agent or RoleHolders.Terminal) return null;
            var scope = root["scope"] as JsonObject;
            return PlaceRiskBook.KeyOf(new RoleScope(HomeMigrationRun.Text(scope?["kind"]) ?? RoleScope.Machine, HomeMigrationRun.Text(scope?["id"])));
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
