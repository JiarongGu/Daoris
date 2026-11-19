namespace Daoris.Driver;

/// <summary>
/// The second check of Ask Daoris's acts as the roles hold it: the seeded supervisor that reads them, the choices a home is first given for
/// it with their budget, and whether the check is on: the machine's supervisor deciding the kind held by an agent or a terminal.
/// </summary>
public static class SecondCheck
{
    /// <summary>The seeded role, <i>Second check</i>.</summary>
    public const string Role = "second-check";

    /// <summary>The starts a day each first choice may take, which the person changes or removes in the editor.</summary>
    public const long StartsADay = 40;

    /// <summary>Another maker's agent first, at its account's default, then Claude Code's strongest reading.</summary>
    public static IReadOnlyList<ActorChoice> Choices { get; } = [new(CodexAccounts.Agent), new(ActorChoices.DefaultAgent, "opus", "high")];

    /// <summary>The policy that bounds <paramref name="choices"/>: in order, each with the day's start budget.</summary>
    public static StaffPolicy Policy(IReadOnlyList<ActorChoice> choices) =>
        new(StaffRules.Order, [.. choices.Select(choice => new StaffChoice(StaffKey.Of(choice), Budget: new StaffBudget(Starts: StartsADay)))],
            new Dictionary<string, StaffMove>(StringComparer.Ordinal));

    /// <summary>
    /// A home's seed, beside its rules' (<see cref="HomeSeed"/>): <see cref="Choices"/> written as the role's staffing row and its policy in
    /// one transaction, so turning the check on names who reads. Nothing is written where the role already has a row or a policy, or where
    /// a role of the person's is the check or decides Ask Daoris's kind; a store that does not take them now leaves the role with no
    /// choices, which the person gives it in the editor.
    /// </summary>
    /// <returns>Whether the choices were written.</returns>
    public static bool SeedFresh(string home, Func<StoreSettled>? settled = null, TimeProvider? clock = null)
    {
        var staffing = new RoleStaffing(home, settled);
        if (staffing.ImportFile().How is StaffingImported.Unread or StaffingImported.Held) return false;
        var written = false;
        staffing.Publish([], write =>
        {
            if (write.Query(Own, row => row.GetString(0), ("$role", Role)).Count > 0) return;
            if (write.Query(RoleRows, row => (row.GetString(0), row.GetString(1))).Any(role => role.Item1 == Role || DecidesTheKind(role.Item2))) return;
            RoleStaffing.AddedRows(write, [(Role, Choices)]);
            StaffPolicies.Save(write, Role, Policy(Choices), Choices, 0, Moments.Write((clock ?? TimeProvider.System).GetUtcNow()));
            write.Moved(StaffPolicies.Family);
            written = true;
        });
        return written;
    }

    private const string Own = "SELECT role FROM role_staffing WHERE role = $role UNION ALL SELECT role FROM role_policy WHERE role = $role";

    private const string RoleRows = "SELECT id, COALESCE(record, '') FROM roles";

    private static bool DecidesTheKind(string record)
    {
        try
        {
            return System.Text.Json.Nodes.JsonNode.Parse(record) is System.Text.Json.Nodes.JsonObject root
                && root["decides"] is System.Text.Json.Nodes.JsonArray decides
                && decides.Any(kind => kind is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var named)
                    && named == CheckedKind);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    /// <summary>Whether a supervisor reads Ask Daoris's acts: held by the person's rules alone, the machine's reads nothing.</summary>
    public static bool On(IReadOnlyList<RoleSlot> slots) =>
        SupervisorResolution.Find(slots, CheckedKind, new RolePlace()).Role?.Hold?.Holder is RoleHolders.Agent or RoleHolders.Terminal;

    /// <summary>The kind the check reads: Ask Daoris's acts.</summary>
    private const string CheckedKind = SupervisorKinds.AskDaoris;
}
