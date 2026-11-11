using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A system's review record: the newest plan the run's sessions kept, by its record and number, its system and its content's digest. A plan
/// is never rewritten (<see cref="SystemPlans"/>), so the number names exactly what the review read; a plan that does not read is said, never
/// taken for no plan.
/// </summary>
internal static class PlanReview
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The newest plan of <paramref name="sessions"/>, or null where none of them planned.</summary>
    public static ReviewReference? Reference(string home, IReadOnlyList<string> sessions)
    {
        var plans = new SystemPlans(home);
        (string Record, SystemChangeSet? Plan, bool Unread)? newest = null;
        foreach (var session in sessions.Where(session => Directory.Exists(DaorisHome.Entry(home, HomeEntry.Plans, session))))
        {
            (string, SystemChangeSet?, bool) read;
            try
            {
                read = (session, plans.Latest(session), false);
            }
            catch (JsonException)
            {
                read = (session, null, true);
            }

            if (newest is null || (read.Item2?.At ?? DateTimeOffset.MinValue) > (newest.Value.Plan?.At ?? DateTimeOffset.MinValue)) newest = read;
        }

        if (newest is not { } found || (found.Plan is null && !found.Unread)) return null;
        return found.Plan is { } plan
            ? new() { PlanRecord = found.Record, Plan = plan.Plan, System = plan.Place, PlanDigest = DigestOf(plan) }
            : new() { PlanRecord = found.Record };
    }

    /// <summary>A plan named but not read is incomplete: the review waits with the handbacks, and nothing is carried.</summary>
    public static IReadOnlyList<ReviewGap> Gaps(string home, ReviewReference reference) =>
        reference.Plan is null ? [new ReviewGap(ReviewGaps.NotRead, null, $"The plan its session `{reference.PlanRecord}` kept does not read.")] : [];

    private static string DigestOf(SystemChangeSet plan) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(plan.Operations, Json))));
}
