using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// How long a landing's branch has stood, said to the person (LANDRETIRE1 part 2, amending D102's note on the clean-up of landing
/// branches): a branch whose quest is closed and which the line has not held for <see cref="Grace"/> is the person's to withdraw
/// (<see cref="LandedWithdrawals"/>), so the list names its age and its closed quest with the door beside it, and <i>What needs you</i> names
/// it once (<see cref="InventoryCode.LandingStale"/>). The clean-up never removes it on this: an age says nothing of whether the person wants
/// the work (CLEANUP1). Whether a quest is closed is the service's, so the one read of it is a seam the callers hand in
/// (<see cref="ClosedAsync"/>), and everything below it is pure.
/// </summary>
public static class LandedStanding
{
    /// <summary>
    /// How long the line may go without holding a landing's branch, its quest closed, before the person is told. A day: the six branches
    /// that raised this stood 14 to 29 hours, past the hours a landing's pull request or a second look takes, and well short of the weeks a
    /// silence took to notice.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromDays(1);

    /// <summary>
    /// Whether a quest is closed: its done is in and nothing holds it for the person's yes, or it was declined. A quest still open or
    /// taken, or one whose done is held, may yet land its work, so it is not closed.
    /// </summary>
    public static bool IsClosed(QuestView quest) => quest.Status == "Declined" || (quest.Status == "Done" && !quest.Held);

    /// <summary>
    /// Whether the entry is one the question is asked of: standing, the line not holding it, and naming a quest. Another needs no
    /// answer, so the service is never read for it.
    /// </summary>
    public static bool Asks(LandedBranch entry) => entry.GoneAt is null && entry.Held is null && entry.Quest is { Length: > 0 };

    /// <summary>
    /// A pull request may still carry it (<see cref="LandedWithdrawals"/> refuses it too): a plugin pushed the branch or it names one, unless
    /// the platform said the pull request was abandoned.
    /// </summary>
    public static bool Carried(LandedBranch entry) =>
        (entry.Pushed || entry.PullRequest is not null) && entry.PullRequestState?.State != PullRequestStates.Abandoned;

    /// <summary>When the entry became one to name: its landing and a <see cref="Grace"/> after.</summary>
    public static DateTimeOffset Since(LandedBranch entry) => entry.LandedAt + Grace;

    /// <summary>
    /// Whether the entry is named to the person: it is asked of, its quest is among <paramref name="closed"/>, a pull request does not
    /// carry it, and <see cref="Grace"/> has passed since it landed.
    /// </summary>
    public static bool Stale(LandedBranch entry, IReadOnlySet<string> closed, DateTimeOffset now) =>
        Asks(entry) && closed.Contains(entry.Quest!) && !Carried(entry) && now >= Since(entry);

    /// <summary>
    /// The quests among the entries' that the service says are closed, read once and only where an entry asks. A service that does not
    /// answer closes nothing: the age is still said, and a quest is never taken for closed for want of an answer (D57).
    /// </summary>
    /// <param name="quests">The service's every quest, closed ones included (<c>ServiceClient.EveryQuestAsync</c>).</param>
    public static async Task<IReadOnlySet<string>> ClosedAsync(
        IEnumerable<LandedBranch> entries, Func<CancellationToken, Task<IReadOnlyList<QuestView>>> quests, CancellationToken ct)
    {
        var asked = entries.Where(Asks).Select(entry => entry.Quest!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (asked.Count == 0) return new HashSet<string>();
        try
        {
            return (await quests(ct).ConfigureAwait(false)).Where(quest => asked.Contains(quest.Id) && IsClosed(quest))
                .Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
            || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return new HashSet<string>();
        }
    }

    /// <summary>How long ago, in the words the list and the page share: hours up to two days, then days.</summary>
    public static string Age(TimeSpan age)
    {
        if (age < TimeSpan.FromHours(1)) return "less than an hour";
        if (age < TimeSpan.FromHours(48)) return Plural((int)age.TotalHours, "hour");
        return Plural((int)age.TotalDays, "day");
    }

    private static string Plural(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";
}
