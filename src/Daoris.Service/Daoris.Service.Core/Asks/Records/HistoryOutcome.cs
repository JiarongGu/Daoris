namespace Daoris.Knowledge;

/// <summary>What the press did to one unit: the unit as judged where it was cleared, whether it went, and the sentence.</summary>
public sealed record HistoryOutcome(HistoryUnit Unit, bool Cleared, string Message)
{
    /// <summary>What of a cleared unit's kept files stayed (HIST1j); none where every removal was made, and for a unit kept.</summary>
    public HistoryFailed Failed { get; init; } = HistoryFailed.None;
}
