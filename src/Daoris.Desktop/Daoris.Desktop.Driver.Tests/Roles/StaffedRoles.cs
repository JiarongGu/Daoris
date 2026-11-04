using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The roles' orders a test's configuration runs by, as the home migration's staffing step writes them from the single-agent keys
/// (<see cref="StaffingStep"/>): Build, Chat and Brainstorm on the machine's agent, Intake and Ask Daoris on theirs where named. A role
/// runs by its stored order alone, so a configuration naming only <c>Adapter</c> leaves Build with no choice that can run.
/// </summary>
internal static class StaffedRoles
{
    public static IReadOnlyDictionary<string, IReadOnlyList<ActorChoice>> On(string agent, string? intake = null, string? helper = null)
    {
        var orders = new Dictionary<string, IReadOnlyList<ActorChoice>>(StringComparer.Ordinal)
        {
            ["build"] = [new ActorChoice(agent)],
            ["chat"] = [new ActorChoice(agent)],
            ["brainstorm"] = [new ActorChoice(agent)],
        };
        if (intake is { Length: > 0 }) orders["intake"] = [new ActorChoice(intake)];
        if (helper is { Length: > 0 }) orders["ask-daoris"] = [new ActorChoice(helper)];
        return orders;
    }

    /// <summary>Intake on <paramref name="agent"/> alone, as the role door writes it: the <c>intakeAdapter</c> key no longer turns it on.</summary>
    public static DriverConfig StaffIntake(this DriverConfig config, string agent) => ActorChoices.With(config, "intake", [new ActorChoice(agent)]);
}
