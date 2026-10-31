using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1d (the review-flow design §4, <i>Checking</i>): every member change accounted from the head's tip, never the agent's
/// claim nor ancestry. Commits the tip holds are held; each other one is tried alone past a conflict, so a conflict cannot hide a later
/// clean change; a conflict is superseded only by a head commit since the press naming it; the backlog is settled by rule; a merge
/// commit in the replayed range fails.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TogetherChecksTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "together-checks", Guid.NewGuid().ToString("N")[..8]);

    private string _base = "";

    public void Dispose() => GitSeed.Discard(_root);

    private Task<string> GitAsync(params string[] args) => GitFixture.GitAsync(_root, args);

    private async Task<string> TipAsync() => (await GitAsync("rev-parse", "HEAD")).Trim();

    private async Task SeedAsync()
    {
        Directory.CreateDirectory(_root);
        await GitAsync("init", "-q", "-b", "main");
        await GitAsync("config", "user.email", "t@example.test");
        await GitAsync("config", "user.name", "t");
        File.WriteAllText(Path.Combine(_root, "a.txt"), "0\n");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "0\n");
        File.WriteAllText(Path.Combine(_root, "TASKS.md"), "# Tasks\n\n- [ ] base row\n");
        await GitAsync("add", "-A");
        await GitAsync("commit", "-q", "-m", "base");
        _base = await TipAsync();
    }

    private async Task<string> CommitAsync(string branch, string file, string text, string message = "change")
    {
        await GitAsync("checkout", "-q", branch);
        File.WriteAllText(Path.Combine(_root, file), text);
        await GitAsync("add", "-A");
        await GitAsync("commit", "-q", "-m", message);
        return await TipAsync();
    }

    private async Task BranchAsync(string name) => await GitAsync("branch", name, _base);

    private TogetherOperation Operation(string pressTip, string memberTip) =>
        new("tg1", "engine", new TogetherHead("s-line", "s-head", _root, "head", pressTip),
            [new TogetherMember("s-beside", _root, "member", _base, memberTip)], TogetherPhase.Checking, At);

    private async Task<TogetherCheck> CheckAsync(TogetherOperation operation) =>
        await TogetherChecks.AccountAsync(_root, operation, await RevAsync("head"), await RevAsync("main"), null, CancellationToken.None);

    private async Task<string> RevAsync(string name) => (await GitAsync("rev-parse", name)).Trim();

    private static string How(TogetherCheck check, string commit, string path) =>
        Assert.Single(check.Paths, each => each.Commit == commit && each.Path == path).How;

    [Fact]
    public async Task A_member_the_head_replayed_cleanly_is_held_and_checked()
    {
        await SeedAsync();
        await BranchAsync("member");
        await BranchAsync("head");
        var one = await CommitAsync("member", "a.txt", "member\n");
        var press = await CommitAsync("head", "b.txt", "head\n");
        await GitAsync("cherry-pick", one);

        var check = await CheckAsync(Operation(press, one));

        Assert.Null(check.Failed);
        Assert.Equal(TogetherAccounts.Held, How(check, one, ""));
    }

    [Fact]
    public async Task A_change_the_head_made_by_its_own_commits_is_contained()
    {
        await SeedAsync();
        await BranchAsync("member");
        await BranchAsync("head");
        var first = await CommitAsync("member", "a.txt", "member\n");
        var later = await CommitAsync("member", "b.txt", "both\n");
        var press = await CommitAsync("head", "a.txt", "head\n");
        await CommitAsync("head", "b.txt", "half\n");
        await CommitAsync("head", "b.txt", "both\n", $"settle\n\nSupersedes: {first}");
        await CommitAsync("head", "a.txt", "head and member\n", $"settle a\n\nSupersedes: {first}");

        var check = await CheckAsync(Operation(press, later));

        Assert.Equal(TogetherAccounts.Superseded, How(check, first, "a.txt"));
        Assert.Equal(TogetherAccounts.Contained, How(check, later, "b.txt"));
        Assert.Null(check.Failed);
    }

    [Fact]
    public async Task A_conflict_settled_in_a_commit_naming_it_is_superseded_and_one_unnamed_is_unproven()
    {
        await SeedAsync();
        await BranchAsync("member");
        await BranchAsync("head");
        var one = await CommitAsync("member", "a.txt", "member\n");
        var press = await CommitAsync("head", "a.txt", "head\n");

        var unnamed = await CheckAsync(Operation(press, one));
        Assert.Equal(TogetherAccounts.Unproven, How(unnamed, one, "a.txt"));
        Assert.Equal(TogetherAccounts.Unproven, unnamed.Failed);

        await CommitAsync("head", "a.txt", "head and member\n", $"settle a\n\nSupersedes: {one[..10]}");
        var named = await CheckAsync(Operation(press, one));

        Assert.Equal(TogetherAccounts.Superseded, How(named, one, "a.txt"));
        Assert.Null(named.Failed);
    }

    [Fact]
    public async Task A_supersedes_trailer_on_a_commit_from_before_the_press_counts_for_nothing()
    {
        await SeedAsync();
        await BranchAsync("member");
        await BranchAsync("head");
        var one = await CommitAsync("member", "a.txt", "member\n");
        var press = await CommitAsync("head", "a.txt", "head\n", $"before\n\nSupersedes: {one}");

        var check = await CheckAsync(Operation(press, one));

        Assert.Equal(TogetherAccounts.Unproven, How(check, one, "a.txt"));
    }

    [Fact]
    public async Task Two_sessions_changing_the_backlog_are_settled_by_rule()
    {
        await SeedAsync();
        await BranchAsync("member");
        await BranchAsync("head");
        var one = await CommitAsync("member", "TASKS.md", "# Tasks\n\n- [ ] base row\n- [ ] member row\n");
        var press = await CommitAsync("head", "TASKS.md", "# Tasks\n\n- [ ] base row\n- [ ] head row\n");

        var check = await CheckAsync(Operation(press, one));

        Assert.Equal(TogetherAccounts.Settled, How(check, one, "TASKS.md"));
        Assert.Null(check.Failed);
    }

    [Fact]
    public async Task A_conflicting_first_commit_hides_no_later_clean_one_the_head_omitted()
    {
        await SeedAsync();
        await BranchAsync("member");
        await BranchAsync("head");
        var first = await CommitAsync("member", "a.txt", "member\n");
        var later = await CommitAsync("member", "b.txt", "member later\n");
        var press = await CommitAsync("head", "a.txt", "head\n");
        await CommitAsync("head", "a.txt", "settled\n", $"settle a\n\nSupersedes: {first}");

        var check = await CheckAsync(Operation(press, later));

        Assert.Equal(TogetherAccounts.Superseded, How(check, first, "a.txt"));
        Assert.Equal(TogetherAccounts.Missing, How(check, later, "b.txt"));
        Assert.Equal(TogetherAccounts.Missing, check.Failed);
    }

    [Fact]
    public async Task A_merge_commit_in_the_range_a_landing_replays_fails_the_check()
    {
        await SeedAsync();
        await BranchAsync("member");
        await BranchAsync("head");
        var one = await CommitAsync("member", "a.txt", "member\n");
        var press = await CommitAsync("head", "b.txt", "head\n");
        await GitAsync("merge", "-q", "--no-ff", "-m", "merged", "member");

        var check = await CheckAsync(Operation(press, one));

        Assert.Single(check.Merges);
        Assert.Equal(TogetherChecks.Merge, check.Failed);
    }
}
