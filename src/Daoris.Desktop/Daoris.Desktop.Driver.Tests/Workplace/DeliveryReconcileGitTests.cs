using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDNODE1b3 (the land node design §3, <i>A restart settles by phase</i>; §8 row 5): a delivery stopped at each git boundary and taken up
/// after a restart, settled by phase before any new start: a recorded outcome goes on from the next phase, an entry written ahead without
/// its outcome is read from the ref and the landing record, and anything else is unknown and held for the person. Each ends in one branch,
/// never two, and never none where the effect happened; a press and a pass at once land once. Real git, as <see cref="LandDeliveryGitTests"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class DeliveryReconcileGitTests : IDisposable
{
    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    private const string Named = "feature/ENG-12-tidy";

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "delivery-reconcile", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _home;

    public DeliveryReconcileGitTests()
    {
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// Two starters of one delivery, a press and the runtime's pass, land at once: the tree's hold lets one land it, and the other, reading the
    /// delivery again under the hold, finds it delivered and lands nothing more. One branch, one write of each phase.
    /// </summary>
    [Fact]
    public async Task A_press_and_a_pass_at_once_land_their_one_delivery_once()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);
        var again = Store("run-a").Start(Intent(tip, DeliveryMethods.Branch), Accepted());
        Assert.False(again.Started);
        Assert.Equal(claim, again.Claim);

        var both = await Task.WhenAll(
            Task.Run(() => trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim))),
            Task.Run(() => new SessionTrees(_home).LandAsync(tree.Path, Subject, inUse: NoneRunning, acceptedBy: AcceptedBy.Auto,
                delivery: new TreeDelivery(Store("run-a"), again.Claim!))));

        Assert.Single(both, each => each.Landed);
        var other = Assert.Single(both, each => !each.Landed);
        Assert.Equal(AutoLandingCode.Already, other.Refusal);
        Assert.Contains("delivered", other.Message);
        Assert.Equal([Named], await BranchesAsync(root));
        Assert.Equal(tip, await RevAsync(root, $"refs/heads/{Named}"));
        Assert.Equal([DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded], Store("run-a").Phases(claim.Key).Select(each => each.Phase));
        Assert.Equal(DeliverySettlements.Delivered, Store("run-a").Delivery(claim.Key)?.Settled);
    }

    // ——— The fixture.

    private static readonly LandingSubject Subject = new("s1", "0fda18", "Tidy the readme");

    private sealed class Crash : Exception;

    private static Func<StoreRun, bool> DeadAre(params string[] runs) => run => !runs.Contains(run.Run);

    private DeliveryStore Store(string run, Func<StoreRun, bool>? alive = null) => new(_home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private static DeliveryIntent Intent(string tip, string method) => new(
        "unit-1", "engine", tip, method, "main", "main", method == DeliveryMethods.Merge ? null : Named, method == DeliveryMethods.Merge ? null : "ENG-12",
        "Tidy the readme (ENG-12)", method is DeliveryMethods.Push or DeliveryMethods.PullRequest ? "forge" : null, "epoch-7");

    private static DeliveryAuthorization Accepted() => DeliveryAuthorization.Person("accept-3", "r3", "graph-digest", """{"method":"branch"}""", "[]");

    private (DeliveryStore Store, DeliveryClaim Claim) Start(string tip, string method)
    {
        var store = Store("run-a");
        var start = store.Start(Intent(tip, method), Accepted());
        Assert.True(start.Started, start.Refused ?? start.Held);
        return (store, start.Claim!);
    }

    private async Task<(string Root, SessionTrees Trees, TreeOpened Tree, string Tip)> TreeAsync()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the session's work", "the work");
        return (root, trees, tree, (await RevAsync(tree.Path, "HEAD"))!);
    }

    /// <summary>The branches under <c>feature/</c>: a landing makes one, never two.</summary>
    private static async Task<IReadOnlyList<string>> BranchesAsync(string root) =>
        (await GitFixture.GitAsync(root, "for-each-ref", "--format=%(refname:short)", "refs/heads/feature/"))
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static async Task<string?> RevAsync(string cwd, string what)
    {
        var run = await GitFixture.RunAsync(cwd, "rev-parse", "--verify", "--quiet", what);
        return run.ExitCode == 0 ? run.Stdout.Trim() : null;
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitFixture.GitAsync(tree, "add", "-A");
        await GitFixture.GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }
}
