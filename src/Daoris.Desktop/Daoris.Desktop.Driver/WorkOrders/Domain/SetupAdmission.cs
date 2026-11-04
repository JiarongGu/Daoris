namespace Daoris.Driver;

/// <summary>A set-up admitted alone (<see cref="DriverConfig.Admitted"/>), read against the quest the planner considers.</summary>
public static class SetupAdmission
{
    /// <summary>Whether this quest is admitted alone: written against the repository it is to, and still open or taken.</summary>
    public static bool Admits(this DriverConfig config, QuestView quest) =>
        quest.Status is "Open" or "Taken" && string.Equals(config.AdmittedIn(quest.Id), quest.To, StringComparison.OrdinalIgnoreCase);
}
