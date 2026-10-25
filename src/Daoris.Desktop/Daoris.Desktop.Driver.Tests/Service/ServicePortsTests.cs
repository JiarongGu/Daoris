using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The two ports the service declares and the workplace answers (FOUNDATION1 mod-iv): the workplace's git read for the remote sync and its
/// lock let go when a session's record ends. The workplace installs both when the driver's assembly loads, so a sync and a client read and
/// release as they always did; a test may put another in and puts it back.
/// </summary>
public sealed class ServicePortsTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "service-ports", Guid.NewGuid().ToString("N")[..8]);

    public ServicePortsTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        if (Directory.Exists(_scratch)) Directory.Delete(_scratch, recursive: true);
    }

    [Fact]
    public async Task The_checkout_reads_in_force_are_the_workplaces_git()
    {
        var reads = CheckoutReads.Current;
        Assert.IsType<WorkingTreeReads>(reads);
        // A held commit that is no commit id is no git argument: git is never asked, and the relation is unknown.
        Assert.Equal(TreeRelation.Unknown, await reads.RelationAsync(_scratch, "not-a-commit", "also-not", CancellationToken.None));
    }

    [Fact]
    public async Task A_test_may_install_other_reads_and_put_the_workplaces_back()
    {
        var workplaces = CheckoutReads.Current;
        try
        {
            CheckoutReads.Current = new Stand();
            Assert.True(await CheckoutReads.Current.CleanAsync(_scratch, CancellationToken.None));
        }
        finally
        {
            CheckoutReads.Current = workplaces;
        }

        Assert.Same(workplaces, CheckoutReads.Current);
        Assert.IsType<WorkingTreeReads>(CheckoutReads.Current);
    }

    [Fact]
    public async Task A_record_that_ends_lets_go_through_the_hook_and_a_teammates_record_lets_go_of_nothing()
    {
        var told = new List<string>();
        var hook = SessionEnds.Ended;
        try
        {
            SessionEnds.Ended = record =>
            {
                told.Add(record.Session);
                return Task.CompletedTask;
            };
            await SessionEnds.Ended(new PriorSession("s1", null, "completed"));
        }
        finally
        {
            SessionEnds.Ended = hook;
        }

        Assert.Equal(["s1"], told);
        // The workplace's own hook: a teammate's record names no tree of this machine's, so nothing is let go and nothing throws.
        await SessionEnds.Ended(new PriorSession("origin/s2", Path.Combine(_scratch, "tree"), "completed"));
    }

    private sealed class Stand : ICheckoutReads
    {
        public Task<string?> LineAsync(string root, string repository, string? workspace, DriverConfig choices, CancellationToken ct) => Task.FromResult<string?>("main");

        public Task<TreeProvenance?> ProvenanceAsync(string root, CancellationToken ct) => Task.FromResult<TreeProvenance?>(null);

        public Task<bool> CleanAsync(string root, CancellationToken ct) => Task.FromResult(true);

        public Task<bool> UnmodifiedAsync(string root, string path, CancellationToken ct) => Task.FromResult(true);

        public Task<TreeRelation> RelationAsync(string root, string held, string head, CancellationToken ct) => Task.FromResult(TreeRelation.Descends);
    }
}
