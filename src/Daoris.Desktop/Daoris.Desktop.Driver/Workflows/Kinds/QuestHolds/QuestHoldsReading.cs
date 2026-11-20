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

    private const string HeldForYes = "its done is held for your yes";

    public static bool PersonLifts(string? holds) =>
        holds is not null && (holds.Contains(HeldForYes, StringComparison.Ordinal) || UncheckedDones.Says(holds));

    /// <summary>Why the quest's own holds hold the work, or null where they let it go (the look's own <c>unreviewed</c> is the look's).</summary>
    public static string? Held(QuestView? quest, bool yes = true, bool evidence = true) => quest switch
    {
        null => "its quest was not read",
        { Status: "Declined" } => "its quest was declined",
        _ when !QuestResults.Judged(quest) => "its quest's done is not in, or was taken back",
        { Held: true, Hold: null or EvidenceCodes.Departed } when yes => HeldForYes,
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
        var why = Held(unit.Records.Quest, HoldReading.On(node, Yes), HoldReading.On(node, Evidence));
        if (UncheckedDones.Says(why) && UncheckedDones.Unread(node, reading.Git?.Cast))
        {
            return HoldReading.Reached(reading, WorkRecord.Quests, reading.Binding.SubjectRevision, UncheckedDones.GoesOn) with { Words = UncheckedDones.GoesOn };
        }

        return why is not null
            ? HoldReading.Seen(reading, null, why)
            : HoldReading.Reached(reading, WorkRecord.Quests, reading.Binding.SubjectRevision, "nothing of its quest's own holds it");
    }
}
