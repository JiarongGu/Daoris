namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="FeedRefusal.None"/> when the feed is taken.</param>
/// <param name="Message">The full answer, phrased once here so no two doors can drift on it.</param>
/// <param name="Entries">How many entries now stand for that repository, when accepted.</param>
public sealed record FeedOutcome(FeedRefusal Refusal, string Message, int Entries)
{
    /// <summary>Whether the feed is taken. Derived, never stored twice: two fields that can disagree
    /// about the same fact eventually do.</summary>
    public bool Accepted => Refusal == FeedRefusal.None;

    /// <summary>
    /// Whether this refusal is something to REPORT rather than to fix.
    /// </summary>
    /// <remarks>
    /// A stale or branch feed is the system working: two machines hold two points in one history, and
    /// the deployment kept the canonical newer one. A sync that logged those as failures would teach
    /// the person to ignore its failures, which is the one thing a report must never do.
    /// </remarks>
    public bool Information =>
        Refusal is FeedRefusal.Stale or FeedRefusal.NotDefaultBranch or FeedRefusal.Moved or FeedRefusal.ContentDiffers
            or FeedRefusal.Unordered;
}
