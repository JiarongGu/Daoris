namespace Daoris.Knowledge;

/// <summary>
/// The quests and asks of a cleared unit whose kept files the disk would not let go of: they stay, named by nothing, and
/// the driver's next clear of a workspace takes them as left over.
/// </summary>
public sealed record HistoryFailed(IReadOnlyList<string> Quests, IReadOnlyList<string> Asks)
{
    public static HistoryFailed None { get; } = new([], []);

    /// <summary>Whether any removal failed.</summary>
    public bool Any => Quests.Count + Asks.Count > 0;
}
