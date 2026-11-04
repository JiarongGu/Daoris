namespace Daoris.Knowledge;

/// <summary>
/// Where an operation stands (§3): <c>staged</c> by its session; released at the session's commit point to <c>ready</c>, an end or a
/// replacement to <c>awaiting</c> its approver; <c>applying</c> while the service calls the engine; then <c>applied</c>, <c>stale</c> where its guard no
/// longer held, or <c>held</c> where nothing says whether it ran. <c>dropped</c> and <c>declined</c> never apply. A proposal (2e) is
/// <c>awaiting</c> its owner while its quest is open, then <c>declined</c>, <c>answered</c> where the quest closed done, <c>stale</c> where
/// its target moved first, or <c>dropped</c> where its quest is gone; none applies.
/// </summary>
public static class MemoryOpState
{
    public const string Answered = "answered";
    public const string Staged = "staged";
    public const string Awaiting = "awaiting";
    public const string Ready = "ready";
    public const string Applying = "applying";
    public const string Applied = "applied";
    public const string Dropped = "dropped";
    public const string Declined = "declined";
    public const string Stale = "stale";
    public const string Held = "held";
}
