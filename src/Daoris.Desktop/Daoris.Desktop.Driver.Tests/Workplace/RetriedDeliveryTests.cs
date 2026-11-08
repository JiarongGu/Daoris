using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landing retried once its delivery settled withdrawn at a restart: the retry is a delivery of its own, the next generation of the same
/// unit, tip, method and target, so its phases are written ahead as any delivery's are, and the withdrawn one keeps its settlement for every
/// reader. A delivered one is still the answer its key gives.
/// </summary>
public sealed class RetriedDeliveryTests : IDisposable
{
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "retried-delivery", Guid.NewGuid().ToString("N")[..8]);

    public RetriedDeliveryTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static readonly BoundLand Bound = new("merge", "main", false, null, null, "{}", "digest", "workflow:w@1:r");

    private Task<(TreeDelivery? Delivery, TreeLanding? Refusal)> Start() =>
        CurrentDeliveries.StartAsync(
            _home, Landing.Merge, "repo", "ws", "unused-root", new LandingSubject("s1", null, "t"), Tip, "main", "feature/x", null, AcceptedBy.Person, false,
            () => null, CancellationToken.None, Bound);

    private static void Reach(TreeDelivery delivery)
    {
        foreach (var phase in new[] { DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded })
        {
            delivery.Store.Begin(delivery.Claim, phase, phase);
            delivery.Store.End(delivery.Claim, phase, DeliveryOutcomes.Done, phase, phase);
        }
    }

    [Fact]
    public async Task A_retry_after_a_delivery_settled_withdrawn_at_a_restart_writes_its_phases_ahead_in_a_delivery_of_its_own()
    {
        var (first, _) = await Start();
        var cut = first!.Store;
        cut.Begin(first.Claim, DeliveryPhases.Prepared, "preparing");
        cut.End(first.Claim, DeliveryPhases.Prepared, DeliveryOutcomes.Withdrawn, null, "Nothing was landed: the replay met a conflict.");
        var restart = new DeliveryReconciler(new DeliveryStore(_home), _home, _ => null);
        Assert.Equal(DeliveryStandings.Settled, Assert.Single(await restart.StartAsync(CancellationToken.None)).Standing);
        Assert.Equal(DeliverySettlements.Withdrawn, cut.Delivery(first.Claim.Key)!.Settled);

        var (retry, refusal) = await Start();

        Assert.Null(refusal);
        Assert.NotNull(retry);
        Assert.NotEqual(first.Claim.Key, retry!.Claim.Key);
        Reach(retry);
        Assert.Equal(DeliverySettlements.Withdrawn, cut.Delivery(first.Claim.Key)!.Settled);
        Assert.Equal(2L, cut.Delivery(retry.Claim.Key)!.Generation);
        Assert.Equal(1L, cut.Delivery(first.Claim.Key)!.Generation);
        Assert.Equal([retry.Claim.Key, first.Claim.Key], cut.OfUnit("s1").Select(each => each.Key));
    }

    [Fact]
    public async Task A_second_retry_after_a_second_withdrawal_is_the_third_generation()
    {
        var (first, _) = await Start();
        first!.Store.Settle(first.Claim, DeliverySettlements.Withdrawn, "refused");
        var (second, _) = await Start();
        second!.Store.Settle(second.Claim, DeliverySettlements.Withdrawn, "refused again");

        var (third, _) = await Start();

        Assert.Equal([3L, 2L, 1L], first.Store.OfUnit("s1").Select(each => each.Generation));
        Assert.Equal(3L, first.Store.Delivery(third!.Claim.Key)!.Generation);
        Assert.Equal(3, new[] { first.Claim.Key, second.Claim.Key, third.Claim.Key }.Distinct().Count());
    }

    [Fact]
    public async Task A_delivered_or_a_stopped_delivery_is_still_the_answer_its_key_gives_and_a_retry_starts_none()
    {
        var (first, _) = await Start();
        Reach(first!);
        first.Store.Settle(first.Claim, DeliverySettlements.Delivered, "delivered");

        var (again, refusal) = await Start();

        Assert.Null(refusal);
        Assert.Null(again);
        Assert.Single(first.Store.OfUnit("s1"));
    }

    [Fact]
    public async Task A_delivery_of_the_same_key_still_going_on_is_the_one_the_retry_goes_on_with()
    {
        var (first, _) = await Start();

        var (again, _) = await Start();

        Assert.Equal(first!.Claim, again!.Claim);
        Assert.Single(first.Store.OfUnit("s1"));
    }
}
