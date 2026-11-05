using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP2f over real git and a local bare origin (D204; the git-writes design §9 <i>Fetch</i>, §12 row f): a fetch confined to
/// <c>refs/remotes/origin/*</c> whatever the repository's configuration says of origin, its prune's removals named, <c>FETCH_HEAD</c> kept by a
/// failed and a successful one, refused where the configuration widens it, not offered where Daoris's git doors are not installed, and journaled
/// and reconciled as an act. Nothing here reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitFetchTests : TreeSyncFixture
{
    private ActJournal Journal(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private ActRuns Runs(string run = "run-a") => new(Journal(run)) { ExitBound = TimeSpan.FromSeconds(2) };

    private static FetchAsk Ask(string root) => new("engine", root);

    private void LayDoors()
    {
        var bin = Path.Combine(Home, "app", "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, GitWiring.SshDoor), "");
        File.WriteAllText(Path.Combine(bin, GitWiring.RemoteHelper), "");
        File.WriteAllText(Path.Combine(bin, GitCredentialDoor.Exe), "");
    }

    private async Task<(string Root, string Origin)> OfferedAsync()
    {
        LayDoors();
        return await RepositoryWithOriginAsync("engine");
    }

    private static async Task<string?> RefAsync(string root, string name)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", name], CancellationToken.None);
        return code == 0 ? said.Trim() : null;
    }

    /// <summary>Every ref outside <c>refs/remotes/origin/</c>, with its value, as one text.</summary>
    private static async Task<string> OutsideAsync(string root) =>
        string.Join('\n', (await GitAsync(root, "for-each-ref", "--format=%(refname) %(objectname)")).Split('\n')
            .Where(row => row.Length > 0 && !row.StartsWith("refs/remotes/origin/", StringComparison.Ordinal)));

    private static string FetchHead(string root) => Path.Combine(root, ".git", "FETCH_HEAD");

    private void KillRun(string run)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DesktopStore.LocationIn(Home)};Pooling=false");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE store_runs SET pid = 2147483000, started = 1 WHERE run = $run";
        command.Parameters.AddWithValue("$run", run);
        Assert.Equal(1, command.ExecuteNonQuery());
    }

    [Fact]
    public async Task Where_the_git_doors_are_not_installed_a_fetch_is_not_offered_and_nothing_runs()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await CommitOnOriginAsync(origin, "ahead", ("a.txt", "a"));
        var before = await RefAsync(root, "refs/remotes/origin/main");

        var offered = await GitFetchActs.OfferedAsync(Home, root, CancellationToken.None);
        var run = await GitFetchActs.FetchAsync(Runs(), Ask(root), "window", "window", CancellationToken.None);

        Assert.False(offered.Offered);
        Assert.Contains("doors", offered.Why);
        Assert.True(run.Refused);
        Assert.Null(run.Act);
        Assert.Contains("doors", run.Said);
        Assert.Equal(before, await RefAsync(root, "refs/remotes/origin/main"));
    }

    [Fact]
    public async Task A_fetch_moves_origin_s_tracking_refs_alone_and_is_journaled_closed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await OfferedAsync();
        var ahead = await CommitOnOriginAsync(origin, "ahead", ("a.txt", "a"));
        await GitAsync(origin, "branch", "feature", ahead);
        var outside = await OutsideAsync(root);

        var run = await GitFetchActs.FetchAsync(Runs(), Ask(root), "window", "window", CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal(ahead, await RefAsync(root, "refs/remotes/origin/main"));
        Assert.Equal(ahead, await RefAsync(root, "refs/remotes/origin/feature"));
        Assert.Equal(outside, await OutsideAsync(root));
        Assert.Contains("origin/feature", run.Notes);
        var act = Journal("run-a").Act(run.Act!)!;
        Assert.Equal((GitFetchActs.Kind, "window", false), (act.Kind, act.Press, act.Open));
        Assert.Equal([ActStepKinds.Fetch], Journal("run-a").Steps(act.Id).Select(step => step.Kind));
    }

    [Fact]
    public async Task Prune_s_removals_are_named_and_a_local_branch_of_the_same_name_is_left()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await OfferedAsync();
        await GitAsync(origin, "branch", "gone", "main");
        await GitAsync(root, "fetch", "--quiet", "origin");
        await GitAsync(root, "branch", "gone", "main");
        Assert.NotNull(await RefAsync(root, "refs/remotes/origin/gone"));
        await GitAsync(origin, "branch", "-D", "gone");

        var run = await GitFetchActs.FetchAsync(Runs(), Ask(root), "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Null(await RefAsync(root, "refs/remotes/origin/gone"));
        Assert.NotNull(await RefAsync(root, "refs/heads/gone"));
        Assert.Contains("origin/gone", run.Notes);
        Assert.Contains("Removed `origin/gone`", run.Notes);
    }

    [Theory]
    [InlineData("remote.origin.fetch", "+refs/heads/*:refs/heads/*")]
    [InlineData("remote.origin.fetch", "+refs/*:refs/*")]
    [InlineData("remote.origin.tagOpt", "--tags")]
    [InlineData("remote.origin.pruneTags", "true")]
    [InlineData("fetch.pruneTags", "true")]
    [InlineData("fetch.prune", "true")]
    [InlineData("fetch.recurseSubmodules", "true")]
    [InlineData("fetch.writeCommitGraph", "true")]
    public async Task Hostile_configuration_moves_nothing_outside_origin_s_tracking_refs(string key, string value)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await OfferedAsync();
        var ahead = await CommitOnOriginAsync(origin, "ahead", ("a.txt", "a"));
        await GitAsync(origin, "tag", "v1", ahead);
        await GitAsync(origin, "branch", "feature", ahead);
        await GitAsync(root, "tag", "mine", "main");
        await GitAsync(root, "config", key, value);
        var outside = await OutsideAsync(root);

        var run = await GitFetchActs.FetchAsync(Runs(), Ask(root), "window", "window", CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal(outside, await OutsideAsync(root));
        Assert.Null(await RefAsync(root, "refs/tags/v1"));
        Assert.NotNull(await RefAsync(root, "refs/tags/mine"));
        Assert.Equal(ahead, await RefAsync(root, "refs/remotes/origin/feature"));
    }

    [Theory]
    [InlineData("remote.origin.uploadpack", "git-upload-pack-elsewhere")]
    [InlineData("remote.origin.receivepack", "git-receive-pack-elsewhere")]
    [InlineData("remote.origin.mirror", "true")]
    public async Task A_remote_configured_to_run_another_program_or_mirror_is_refused_before_anything_runs(string key, string value)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await OfferedAsync();
        await CommitOnOriginAsync(origin, "ahead", ("a.txt", "a"));
        await GitAsync(root, "config", key, value);
        var before = await RefAsync(root, "refs/remotes/origin/main");

        var offered = await GitFetchActs.OfferedAsync(Home, root, CancellationToken.None);
        var run = await GitFetchActs.FetchAsync(Runs(), Ask(root), "window", "window", CancellationToken.None);

        Assert.False(offered.Offered);
        Assert.True(run.Refused);
        Assert.Null(run.Act);
        Assert.Contains(key, run.Said, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await RefAsync(root, "refs/remotes/origin/main"));
        Assert.Empty(Journal("run-a").Unclosed());
    }

    [Fact]
    public async Task FETCH_HEAD_is_kept_by_a_successful_fetch_and_by_a_failed_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await OfferedAsync();
        const string Kept = "0123456789012345678901234567890123456789\t\tbranch 'main' of the person's own last fetch\n";
        await File.WriteAllTextAsync(FetchHead(root), Kept);
        await CommitOnOriginAsync(origin, "ahead", ("a.txt", "a"));

        var done = await GitFetchActs.FetchAsync(Runs(), Ask(root), "window", "window", CancellationToken.None);
        Assert.Equal(ActOutcomes.Done, done.Outcome);
        Assert.Equal(Kept, await File.ReadAllTextAsync(FetchHead(root)));

        await GitAsync(root, "remote", "set-url", "origin", Path.Combine(Scratch, "nothing-here.git"));
        var failed = await GitFetchActs.FetchAsync(Runs(), Ask(root), "window", "window", CancellationToken.None);

        Assert.False(failed.Refused, failed.Said);
        Assert.Equal(ActOutcomes.NotDone, failed.Outcome);
        Assert.Equal(Kept, await File.ReadAllTextAsync(FetchHead(root)));
    }

    [Fact]
    public async Task A_fetch_whose_run_died_before_it_started_is_reconciled_not_done_and_never_run_again()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await OfferedAsync();
        await CommitOnOriginAsync(origin, "ahead", ("a.txt", "a"));
        var before = await RefAsync(root, "refs/remotes/origin/main");
        var runs = Runs();
        runs.Seam = (point, _) => point == ActSeams.BeforeLaunch ? throw new InvalidOperationException("killed") : Task.CompletedTask;
        await Assert.ThrowsAsync<InvalidOperationException>(() => GitFetchActs.FetchAsync(runs, Ask(root), "window", "window", CancellationToken.None));
        var act = Journal("run-a").Unclosed().Single();
        KillRun("run-a");

        var hold = await new ActReconciler(Journal("run-b", run => run.Run != "run-a")) { ExitBound = TimeSpan.FromSeconds(2) }.UnderLockAsync(root, CancellationToken.None);

        Assert.Null(hold);
        Assert.Equal((ActPhases.Closed, ActOutcomes.NotDone), (Journal("run-b").Act(act.Id)!.Phase, Journal("run-b").Act(act.Id)!.Outcome));
        Assert.Equal(before, await RefAsync(root, "refs/remotes/origin/main"));
    }

    [Fact]
    public async Task A_fetch_that_moved_refs_before_its_run_died_is_reconciled_unknown_not_done()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await OfferedAsync();
        await CommitOnOriginAsync(origin, "ahead", ("a.txt", "a"));
        var runs = Runs();
        runs.Seam = (point, _) => point == ActSeams.AfterLaunch ? throw new InvalidOperationException("killed") : Task.CompletedTask;
        await Assert.ThrowsAsync<InvalidOperationException>(() => GitFetchActs.FetchAsync(runs, Ask(root), "window", "window", CancellationToken.None));
        // git, let go, finishes without its run; the tracking ref then moved, or the cut came first and it did not.
        await Task.Delay(1500);
        KillRun("run-a");
        var act = Journal("run-b").Unclosed().Single();

        await new ActReconciler(Journal("run-b", run => run.Run != "run-a")) { ExitBound = TimeSpan.FromSeconds(2) }.UnderLockAsync(root, CancellationToken.None);

        var closed = Journal("run-b").Act(act.Id)!;
        Assert.Contains(closed.Outcome, new[] { ActOutcomes.Unknown, ActOutcomes.NotDone });
        Assert.Equal(closed.Outcome == ActOutcomes.Unknown, await RefAsync(root, "refs/remotes/origin/main") != await RefAsync(root, "main"));
    }
}
