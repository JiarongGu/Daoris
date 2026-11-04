using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The supervisor's rules a test's home decides by, as a fresh home's first start seeds them (<see cref="SupervisorRuleBook.SeedFresh"/>): answers
/// alone 2 in a row, the failed sessions at <c>strikes</c>, five transient tries; and a role's policy moves as rules at the machine, as the
/// migration writes them (<see cref="SupervisorRuleBook.FromPolicy"/>). A home whose rules were never written is not reached, and nothing there
/// decides: no count is watched and no policy move is made.
/// </summary>
internal static class SeededRules
{
    /// <summary>Write the machine's rules into <paramref name="home"/>'s store, after any policy the role door saved there.</summary>
    public static void In(string home, int strikes = 3, StaffPolicy? policy = null, string role = RoleWork.Build)
    {
        var store = new SupervisorRuleStore(home, () => StoreSettled.Yes);
        var world = SupervisorRuleDoor.World(home) with { ReadReached = false, Reached = true };
        IReadOnlyList<SupervisorRule> rules = policy is null
            ? SupervisorRuleBook.Seeded(strikes)
            : [.. SupervisorRuleBook.Seeded(strikes), .. SupervisorRuleBook.FromPolicy(policy, role, SupervisorRules.Machine)];
        Assert.True(store.Write(SupervisorRules.Machine, store.Of(SupervisorRules.Machine).Revision, rules, SupervisorRuleBook.SeedDoor, world).Stored);
    }
}
