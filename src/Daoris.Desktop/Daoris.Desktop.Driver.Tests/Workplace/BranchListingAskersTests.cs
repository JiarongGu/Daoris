using Daoris.Driver;
using Daoris.Ports.Processes;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The branch listing a repository's walks share (<see cref="BranchesByDate"/>), over a real repository whose listing a stood-in git
/// answers, so its waits are the test's to say: each asker's wait for the listing's
/// place is its own stage's queue, apart from its hold; one asker leaving leaves the listing to the rest; the last leaving ends git's run;
/// an asker cancelled before it asks starts nothing.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class BranchListingAskersTests : IAsyncLifetime
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "branch-listing-askers", Guid.NewGuid().ToString("N")[..8]);
    private readonly TaskCompletionSource _placed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _listed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<CancellationToken> _runs = [];
    private long _now;

    private string Checkout => Path.Combine(_scratch, "reports");

    public async Task InitializeAsync()
    {
        await GitSeed.PlainAsync(Checkout, "reports");
        BranchesByDate.Forget();
    }

    public Task DisposeAsync()
    {
        BranchesByDate.Forget();
        GitSeed.Discard(_scratch);
        return Task.CompletedTask;
    }

    /// <summary>A git that waits for a place until the test places it, then lists until the test lets it answer, or until it is ended.</summary>
    private IDisposable Git() => GitFlow.Run((arguments, _, ct) =>
    {
        lock (_runs) _runs.Add(ct);
        return ListedAsync(ct);
    });

    private async Task<ChildRun> ListedAsync(CancellationToken ct)
    {
        using (ProcessStarts.Queued()) await _placed.Task.WaitAsync(ct);
        await _listed.Task.WaitAsync(ct);
        return new ChildRun(Started: true, ExitCode: 0, Stdout: new string('a', 40) + " refs/heads/main\n", Stderr: "", TimedOut: false);
    }

    private void Pass(long ms) => Interlocked.Add(ref _now, ms);

    private static async Task Until(Func<bool> done)
    {
        for (var tries = 0; !done() && tries < 1_000_000; tries++) await Task.Yield();
        Assert.True(done());
    }

    [Fact]
    public async Task Two_stages_sharing_the_listing_each_say_its_wait_for_a_place_as_their_queue()
    {
        var stages = new LookStages(clock: () => Volatile.Read(ref _now));
        using var git = Git();
        var tidy = stages.Begin("tidy");
        var opinions = stages.Begin("opinions");
        var first = tidy.RunAsync(() => BranchesByDate.ReadAsync(Checkout, CancellationToken.None));
        await Until(() => _runs.Count == 1);
        var second = opinions.RunAsync(() => BranchesByDate.ReadAsync(Checkout, CancellationToken.None));

        // The clock moves a millisecond a try until both stages wait for the listing's place, then three seconds, then stops while it is placed.
        await Until(() => Waiting(tidy) && Waiting(opinions));
        Pass(3000);
        _placed.SetResult();
        await Until(() => !Waiting(tidy) && !Waiting(opinions));
        Pass(400);
        _listed.SetResult();
        await Task.WhenAll(first, second);
        tidy.Dispose();
        opinions.Dispose();

        Assert.Single(_runs);
        var said = stages.Take()!.Split(' ').Select(entry => entry.Split('=')).ToDictionary(pair => pair[0], pair => long.Parse(pair[1].Split('x', '@')[0]));
        foreach (var stage in new[] { "tidy", "opinions" })
        {
            // The tries' milliseconds land on either side; a wait said as held instead would read 0 queued and 3,400 held.
            Assert.InRange(said[$"{stage}.queue"], 3000, 4000);
            Assert.InRange(said[stage], 400, 2000);
        }
    }

    /// <summary>Whether <paramref name="span"/> waits for a place now: its queue grows as the clock moves.</summary>
    private bool Waiting(LookStages.StageSpan span)
    {
        var before = span.QueuedSoFar();
        Pass(1);
        return span.QueuedSoFar() > before;
    }

    [Fact]
    public async Task One_asker_leaving_leaves_the_listing_to_the_one_still_waiting()
    {
        using var git = Git();
        using var leaving = new CancellationTokenSource();
        var left = BranchesByDate.ReadAsync(Checkout, leaving.Token);
        await Until(() => _runs.Count == 1);
        var staying = BranchesByDate.ReadAsync(Checkout, CancellationToken.None);

        leaving.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => left);
        Assert.False(_runs[0].IsCancellationRequested);
        _placed.SetResult();
        _listed.SetResult();

        Assert.Equal(0, (await staying).Code);
        Assert.Single(_runs);
    }

    [Fact]
    public async Task The_last_asker_leaving_ends_gits_run_and_the_next_ask_lists_again()
    {
        using var git = Git();
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();
        var one = BranchesByDate.ReadAsync(Checkout, first.Token);
        await Until(() => _runs.Count == 1);
        var two = BranchesByDate.ReadAsync(Checkout, second.Token);

        first.Cancel();
        second.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => one);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => two);
        Assert.True(_runs[0].IsCancellationRequested);

        _placed.SetResult();
        _listed.SetResult();
        Assert.Equal(0, (await BranchesByDate.ReadAsync(Checkout, CancellationToken.None)).Code);
        Assert.Equal(2, _runs.Count);
    }

    [Fact]
    public async Task An_asker_cancelled_before_it_asks_starts_no_git()
    {
        using var git = Git();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BranchesByDate.ReadAsync(Checkout, new CancellationToken(canceled: true)));
        Assert.Empty(_runs);
    }
}
