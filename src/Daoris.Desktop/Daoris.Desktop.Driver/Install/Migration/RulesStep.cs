using System.Globalization;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The old caps as the supervisor's rules (D232; RULESCONVERT1): on a home whose rules were never written, the machine's rules at the numbers
/// the caps held (answers alone 2 in a row, the failed sessions <c>driver.json</c>'s <c>strikes</c> said, five transient tries, three moves a
/// day), each role's policy moves and bound as rules at its place, those moves taken out of the policy, and <c>strikes</c> taken out of the file.
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
        if (!new SupervisorRuleStore(home, () => StoreSettled.Yes).Reached())
        {
            var strikes = Strikes(file);
            var catalogue = RoleCatalog.ReadHome(home);
            var moving = SupervisorRuleCensus.PoliciesMoving(home)
                .Select(each => (each.Role, Place: RuledPolicy.PlaceOf(catalogue.Slots.First(slot => slot.Id == each.Role).Role!.Scope), Policy: each.Read.Policy!))
                .ToList();
            run.Keep(location);
            using var store = DesktopStore.Open(location, StoreProcess.Options(() => StoreSettled.Yes));
            var world = SupervisorRuleDoor.World(home) with { ReadReached = false, Reached = true };
            store.Write(SupervisorRuleStore.Family, write =>
            {
                SupervisorRuleStore.WriteIn(write, SupervisorRules.Machine, 0, [.. SupervisorRuleBook.Seeded(strikes), Moves()], Door, world);
                foreach (var (role, place, policy) in moving)
                {
                    try
                    {
                        if (SupervisorRuleBook.FromPolicy(policy, role, place) is { Count: > 0 } rules) SupervisorRuleStore.MergeIn(write, place, rules, Door, world);
                    }
                    catch (RuleRefusedException)
                    {
                        // The judge refuses before it writes: the policy keeps its moves, for the person to set as rules on its role's page.
                        continue;
                    }

                    write.Execute(
                        "UPDATE role_policy SET policy = $policy WHERE role = $role",
                        ("$role", role), ("$policy", (policy with { Moves = new Dictionary<string, StaffMove>(), Most = null }).Write()));
                    write.Moved(StaffPolicies.Family);
                }
            });
            run.Said(location);
        }

        run.ChangeJson(file, root => root.Remove("strikes"));
    }

    /// <summary>A role's moves bounded by day on every line, as a policy's moves were by its bound.</summary>
    private static SupervisorRule Moves() => new(
        "moves", SupervisorRules.Moves, SupervisorRules.Windows[SupervisorRules.Moves], StaffPolicy.MostByDefault, SupervisorRules.Ask, null, SupervisorRules.Machine, null);

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
