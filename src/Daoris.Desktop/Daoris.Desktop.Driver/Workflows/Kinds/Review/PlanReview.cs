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

    /// <summary>
    /// The plan <paramref name="record"/> names, by its record, number and digest, so what is applied is what was read; else why there is none
    /// to apply: no record, work that kept no plan, a plan that did not read, one no longer kept or not the one read.
    /// </summary>
    public static (SystemChangeSet? Plan, string? None) Named(string home, ReviewRecord? record)
    {
        if (record is null) return (null, "no record of its work reads");
        if (record.Kind != ReviewKinds.Plan) return (null, "its work kept no plan: it answered in its handback alone");
        var reference = record.Reference;
        if (reference is not { PlanRecord: { } kept, Plan: { } number }) return (null, $"the plan its session `{reference.PlanRecord}` kept does not read");
        SystemChangeSet? plan;
        try
        {
            plan = new SystemPlans(home).Of(kept, number);
        }
        catch (JsonException)
        {
            plan = null;
        }

        if (plan is null) return (null, $"plan {number} of its session `{kept}` does not read");
        return DigestOf(plan) == reference.PlanDigest ? (plan, null) : (null, $"plan {number} of its session `{kept}` is not the plan its review read");
    }

    private static string DigestOf(SystemChangeSet plan) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(plan.Operations, Json))));
}
