namespace Daoris.Driver;

/// <summary>
/// The role door's hold on a supervisor's record until the home's supervisors are in this build's shape, which the migration's
/// <c>supervisor-kinds</c> step records, so nothing of the new shape stands in a home an older build may still be rolled back onto.
/// The hold and the step leave together, in the next release that changes a stored shape, so the record's migration is not asked.
/// </summary>
public static class SupervisorConversion
{
    public const string Step = "supervisor-kinds";

    public const string NotConverted = "not-converted";

    public const string NotConvertedSaid = "This home is converted once the update confirms; set the supervisor then.";

    public static bool Converted(string home) =>
        MigrationRecord.Read(DaorisHome.Entry(home, HomeEntry.Migration)) is { } record && record.Steps.Contains(Step, StringComparer.Ordinal);
}
