using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// In a repository's own checkout the writer's lease says who holds it: a writer of Daoris's waits for a turn running there or a line
/// parked with uncommitted paths of its own, and the person's act is refused only while a turn runs, so the person may commit, stash or
/// discard a parked line's work. A session's own tree is held by every session there, as before.
/// </summary>
public sealed class CheckoutBesideLeaseTests
{
    private const string Root = "/places/reports";

    private static Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>> Beside(params TreeHolder[] holders) =>
        _ => Task.FromResult<IReadOnlyList<TreeHolder>>(holders);

    private static readonly TreeHolder Running = new(Root, "s-run") { Leases = true, Runs = true };
    private static readonly TreeHolder Keeping = new(Root, "s-keep") { Leases = true };
    private static readonly TreeHolder Parked = new(Root, "s-park");

    [Fact]
    public async Task A_writer_of_the_checkout_waits_for_the_lease_s_holder_alone()
    {
        Assert.Null(await CheckoutBeside.WaitsAsync(Beside(Parked), Root, "reports", "the pull", CancellationToken.None, place: true));
        Assert.Contains("`s-keep`", await CheckoutBeside.WaitsAsync(Beside(Parked, Keeping), Root, "reports", "the pull", CancellationToken.None, place: true));
        Assert.Contains("`s-park`", await CheckoutBeside.WaitsAsync(Beside(Parked), Root, "reports", "the pull", CancellationToken.None));
    }

    [Fact]
    public async Task The_person_s_act_is_refused_only_while_a_turn_runs_in_the_checkout()
    {
        Assert.Null(await CheckoutBeside.RefusesAsync(Beside(Parked, Keeping), Root, "reports", "this act", "is refused", CancellationToken.None, place: true));
        Assert.Contains("`s-run`", await CheckoutBeside.RefusesAsync(Beside(Keeping, Running), Root, "reports", "this act", "is refused", CancellationToken.None, place: true));
        Assert.Contains("`s-keep`", await CheckoutBeside.RefusesAsync(Beside(Keeping), Root, "reports", "this act", "is refused", CancellationToken.None));
    }
}
