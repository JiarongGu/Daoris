namespace Daoris.Driver;

/// <summary>A <c>quest.holds</c>' reading (LANDNODE1k): reached once nothing of its quest's own holds the work, else none.</summary>
internal static class QuestHoldsReading
{
    /// <summary>Whether a done held for the person's yes (D133) holds the work.</summary>
    public const string Yes = "yes";

    /// <summary>Whether a done held for its evidence's verdict (D144) holds the work.</summary>
    public const string Evidence = "evidence";

    /// <summary>Its options, in the order an editor lists them; each is <c>true</c> or <c>false</c>, and true where it is not written.</summary>
    public static readonly IReadOnlyList<string> Options = [Yes, Evidence];

    /// <summary>Why the quest's own holds hold the work, or null where they let it go (the look's own <c>unreviewed</c> is the look's).</summary>
    public static string? Held(QuestView? quest, bool yes = true, bool evidence = true) => quest switch
    {
        null => "its quest was not read",
        { Status: "Declined" } => "its quest was declined",
        _ when !QuestResults.Judged(quest) => "its quest's done is not in, or was taken back",
        { Held: true, Hold: null } when yes => "its done is held for your yes",
        { Held: true, Hold: { } code } when evidence && code != EvidenceCodes.Unreviewed => "its done is held for its evidence's verdict",
        _ => null,
    };

    public static NodeObservation Observe(NodeReading reading)
    {
        if (HoldReading.Settled(reading) is { } settled) return settled;
        var (unit, node) = (reading.Unit, reading.Node);
        if (unit.Kind != WorkUnitKind.Quest) return HoldReading.Seen(reading, null, "its door judges the quest's holds where it lands");
        return Held(unit.Records.Quest, HoldReading.On(node, Yes), HoldReading.On(node, Evidence)) is { } why
            ? HoldReading.Seen(reading, null, why)
            : HoldReading.Reached(reading, WorkRecord.Quests, reading.Binding.SubjectRevision, "nothing of its quest's own holds it");
    }
}
