namespace Daoris.Driver;

/// <summary>
/// The pass's look at its runs' holds once its rounds are done: the judge's shadow of each hold, and each vacancy said once or at the owner's
/// final exit past its bound, its notice handed up (<paramref name="noticed"/>), the latter only by the pass holding the lease.
/// </summary>
internal static class PassHolds
{
    public static void Look(
        RiskShadow? risk, NodeRecordFiles records, IReadOnlyCollection<GovernedUnit> governed, DriverConfig config, DateTimeOffset now, bool leased, List<string> said,
        List<LookLine> noticed, Func<string?, string?, bool>? live = null)
    {
        if (risk is not null) RiskStepHolds.Shadow(risk, governed, records, config);
        if (!leased) return;
        PolicyPasses.Look(records, governed, config, now, said);
        StepVacancies.Look(records, new StepVacancyFiles(records.Home), governed, config, now, said, noticed, live);
    }
}
