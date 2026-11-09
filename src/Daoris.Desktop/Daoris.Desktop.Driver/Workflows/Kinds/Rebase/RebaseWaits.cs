namespace Daoris.Driver;

/// <summary>
/// When a <c>rebase</c> may start (§2.2, §14): once its input binds and its work is still. No session runs a turn in its tree; and the work is
/// held by no operation bringing sessions together, but as the head of one <c>ready</c> at its tip (<see cref="TogetherBinding"/>). Until then it
/// is <c>ready</c>, not active, its wait said; no wait sits inside the step.
/// </summary>
internal static class RebaseWaits
{
    /// <summary>Why it waits, said; null where it may start.</summary>
    /// <param name="turning">Whether a session runs a turn in the work's tree.</param>
    /// <param name="hold">The operations' hold on the work (<see cref="TogetherBinding.Of(IReadOnlyList{TogetherOperation}, string, string, string, string)"/>).</param>
    public static string? Of(bool turning, TogetherHold? hold)
    {
        if (hold is { } held && TogetherHolds.Waits(held.How)) return held.Said + ".";
        return turning ? "Waits for its session's turn to end." : null;
    }
}
