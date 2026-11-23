namespace Daoris.Driver;

/// <summary>A <c>quest.holds</c>' reading: reached once nothing of its quest's own holds the work, else none.</summary>
internal static class QuestHoldsReading
{
    /// <summary>Whether a done held for the person's yes holds the work.</summary>
    public const string Yes = "yes";

    /// <summary>Whether a done held for its evidence's verdict holds the work.</summary>
    public const string Evidence = "evidence";

    /// <summary>Its options, in the order an editor lists them; each is <c>true</c> or <c>false</c>, and true where it is not written.</summary>
    public static readonly IReadOnlyList<string> Options = [Yes, Evidence];

    private const string Yours = "its done is held for your yes";
    private const string Roles = "its done is held for the yes of the role";
    private const string Policy = "its done is held for the risk policy's yes";

    // The Yours and Roles words are read for records written before the policy's yes: they end when no record holds them (history's 30-day clear).
    public static bool YesLifts(string? holds) =>
        holds is not null && (new[] { Yours, Roles, Policy }.Any(lead => holds.Contains(lead, StringComparison.Ordinal)) || UncheckedDones.Says(holds));

    /// <summary>Why the quest's own holds hold the work, or null where they let it go; a departure, or evidence read and not found, waits for the yes <paramref name="judge"/> names.</summary>
    public static string? Held(QuestView? quest, bool yes = true, bool evidence = true, Func<RiskAct, RiskVerdict?>? judge = null) => quest switch
    {
        null => "its quest was not read",
        { Status: "Declined" } => "its quest was declined",
        _ when !QuestResults.Judged(quest) => "its quest's done is not in, or was taken back",
        { Held: true, Hold: null or EvidenceCodes.Departed or EvidenceCodes.MissingHold } when yes && (quest.Hold is not EvidenceCodes.MissingHold || judge is not null) => QuestDepartures.Verdict(quest, judge) switch
        {
            { Outcome: RiskVerdict.Pass } verdict => $"{Policy}, which Daoris gives at its next look: {verdict.Why}",
            { Outcome: RiskVerdict.Gate } verdict => $"{Roles} `{verdict.By}`, not yours: {verdict.Why}",
            { } verdict => $"{Yours}: {verdict.Why}",
            null => Yours,
        },
        { Held: true, Hold: { } code } when evidence && code is not (EvidenceCodes.Unreviewed or EvidenceCodes.Departed) =>
            "its done is held for its evidence's verdict",
        _ when yes => UncheckedDones.Of(quest),
        _ => null,
    };

    public static NodeObservation Observe(NodeReading reading)
    {
        if (HoldReading.Settled(reading) is { } settled) return settled;
        if (BranchOnLine.Observe(reading) is { } onLine) return onLine;
        var (unit, node) = (reading.Unit, reading.Node);
        if (unit.Kind != WorkUnitKind.Quest) return HoldReading.Seen(reading, null, "its door judges the quest's holds where it lands");
        var risk = reading.Git?.Risk;
        var why = Held(unit.Records.Quest, HoldReading.On(node, Yes), HoldReading.On(node, Evidence), risk is null ? null : act => RiskStepGates.Verdict(risk, act.Kind, node));
        if (UncheckedDones.Says(why) && RiskStepGates.Passed(reading, PersonWaits.Pass) is { } passed) return passed;

        return why is not null
            ? HoldReading.Seen(reading, null, why)
            : HoldReading.Reached(reading, WorkRecord.Quests, reading.Binding.SubjectRevision, "nothing of its quest's own holds it");
    }
}
