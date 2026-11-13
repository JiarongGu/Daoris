using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The roles' orders a test's configuration runs by, as the home migration's staffing step writes them from the single-agent keys
/// (<see cref="StaffingStep"/>): Build, Chat and Brainstorm on the machine's agent, Intake and Ask Daoris on theirs where named. A role
/// runs by its stored order alone.
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
        // The lead is the intake where the workspace has more than one repository, so an intake on an agent is a lead on it too.
        if (intake is { Length: > 0 }) orders["intake"] = orders["lead"] = [new ActorChoice(intake)];
        if (helper is { Length: > 0 }) orders["ask-daoris"] = [new ActorChoice(helper)];
        return orders;
    }

    /// <summary>The empty configuration on <paramref name="agent"/>: the roles' orders <see cref="On"/> writes.</summary>
    public static DriverConfig Config(string agent) => DriverConfig.Empty with { Actors = On(agent) };

    /// <summary>Intake on <paramref name="agent"/> alone, as the role door writes it: the <c>intakeAdapter</c> key no longer turns it on.</summary>
    public static DriverConfig StaffIntake(this DriverConfig config, string agent) => config.StaffIntake([new ActorChoice(agent)]);

    /// <summary>Intake and the lead, which answers an ask where the workspace has more than one repository, on one order.</summary>
    public static DriverConfig StaffIntake(this DriverConfig config, IReadOnlyList<ActorChoice> order) =>
        ActorChoices.With(ActorChoices.With(config, "intake", order), "lead", order);
}
