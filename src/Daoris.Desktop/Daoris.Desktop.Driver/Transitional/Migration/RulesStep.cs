using System.Globalization;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The old caps as the supervisor's rules (D232; RULESCONVERT1): on a home whose rules were never written, the machine's rules at the numbers
/// the caps held (answers alone 2 in a row, the failed sessions <c>driver.json</c>'s <c>strikes</c> said, five transient tries, three moves a
/// day), and <c>strikes</c> taken out of the file.
/// </summary>
public sealed class RulesStep : IHomeMigrationStep
{
    private const string Door = "migration";
    private const int StrikesByDefault = 3;

    public string Name => "rules";

    public void Run(HomeMigrationRun run)
    {
        var home = run.Home;
        var file = DaorisHome.Entry(home, HomeEntry.DriverConfig);
        var location = DesktopStore.LocationIn(home);
        if (!new SupervisorRuleStore(home, () => StoreSettled.Yes).Seeded())
        {
            var strikes = Strikes(file);
            run.Keep(location);
            using var store = DesktopStore.Open(location, StoreProcess.Options(() => StoreSettled.Yes));
            var world = SupervisorRuleDoor.World(home);
            store.Write(SupervisorRuleStore.Family, write =>
                SupervisorRuleStore.WriteIn(write, SupervisorRules.Machine, 0, SupervisorRuleBook.Seeded(strikes), Door, world));
            run.Said(location);
        }

        run.ChangeJson(file, root => root.Remove("strikes"));
    }

    /// <summary>The failed sessions the file's <c>strikes</c> parked a quest at, its default where it says none.</summary>
    private static int Strikes(string file)
    {
        try
        {
            if (!File.Exists(file) || JsonNode.Parse(AtomicFile.ReadText(file)) is not JsonObject root || root["strikes"] is not JsonValue value) return StrikesByDefault;
            return value.TryGetValue<int>(out var whole) ? Math.Max(0, whole)
                : value.TryGetValue<double>(out var number) ? Math.Max(0, (int)number)
                : int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var text) ? Math.Max(0, text) : StrikesByDefault;
        }
        catch (System.Text.Json.JsonException)
        {
            return StrikesByDefault;
        }
    }
}
