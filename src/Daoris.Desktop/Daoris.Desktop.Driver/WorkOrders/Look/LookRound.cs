namespace Daoris.Driver;

/// <summary>
/// What one look hands its passes: which part of the look it runs, and the lines its report carries. A pass that reads more of
/// the look (its snapshot, the starts judged, the slots taken) adds it here as it moves out of the driver.
/// </summary>
/// <param name="events">The look's lines. A pass adds under a lock where work beside it may add at the same time.</param>
internal sealed class LookRound(LookPart part, List<string> events)
{
    public LookPart Part { get; } = part;

    public List<string> Events { get; } = events;
}
