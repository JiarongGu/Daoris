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

    /// <summary>Whether a supervisor reads Ask Daoris's acts: held by the person's rules alone, the machine's reads nothing.</summary>
    public static bool On(IReadOnlyList<RoleSlot> slots) =>
        SupervisorResolution.Find(slots, SupervisorKinds.AskDaoris, new RolePlace()).Role?.Hold?.Holder is RoleHolders.Agent or RoleHolders.Terminal;
}
