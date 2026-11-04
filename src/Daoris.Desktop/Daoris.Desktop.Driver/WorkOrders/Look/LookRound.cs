namespace Daoris.Driver;

/// <summary>
/// What one look hands its passes: which part of the look it runs, the lines its report carries, and the <see cref="LookState"/> each
/// pass reads from the passes before it and writes for the ones after.
/// </summary>
/// <param name="events">The look's lines. A pass adds under a lock where work beside it may add at the same time.</param>
internal sealed class LookRound(LookPart part, List<string> events, LookState state)
{
    public LookPart Part { get; } = part;

    public List<string> Events { get; } = events;

    public LookState State { get; } = state;
}
