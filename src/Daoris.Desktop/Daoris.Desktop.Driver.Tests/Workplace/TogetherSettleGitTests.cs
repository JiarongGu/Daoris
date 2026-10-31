using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1d (the review-flow design §4, <i>Restart</i>): a landing attempt interrupted after git made its ref and before its
/// record, or after a merge's admission and before its correlation, settled by its git effects and the request at the replayed tip;
/// what is not proved stays <i>uncertain</i>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TogetherSettleGitTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "together-settle", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_root);

    private async Task<(string Line, string Head)> RepositoryAsync()
    {
        Directory.CreateDirectory(_root);
        await GitFixture.GitAsync(_root, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(_root, "config", "user.email", "t@example.test");
        await GitFixture.GitAsync(_root, "config", "user.name", "t");
        File.WriteAllText(Path.Combine(_root, "a.txt"), "0\n");
        await GitFixture.GitAsync(_root, "add", "-A");
        await GitFixture.GitAsync(_root, "commit", "-q", "-m", "base");
        var line = (await GitFixture.GitAsync(_root, "rev-parse", "HEAD")).Trim();
        await GitFixture.GitAsync(_root, "checkout", "-q", "-b", "daoris/head");
        File.WriteAllText(Path.Combine(_root, "a.txt"), "head\n");
        await GitFixture.GitAsync(_root, "commit", "-q", "-am", "head");
        var head = (await GitFixture.GitAsync(_root, "rev-parse", "HEAD")).Trim();
        await GitFixture.GitAsync(_root, "checkout", "-q", "main");
        return (line, head);
    }

    private static TogetherOperation Landing(TogetherAttempt attempt) =>
        new("tg1", "engine", new TogetherHead("s-line", "s-head", "trees/head", "daoris/head", attempt.Pinned), [], TogetherPhase.Landing, At)
        {
            Checked = attempt.Pinned,
            Attempt = attempt,
        };

    private static IntegrationRequest Request(string id, string tip) =>
        new(id, "engine", "default", "daoris/head", tip, "merge", "main", new IntegrationRequester("person", "accept"), new LandingIntegrate(), At);

    private Task<LandingEvidence> ReadAsync(TogetherOperation operation, IReadOnlyList<IntegrationRequest>? requests = null) =>
        TogetherSettle.ReadAsync(_root, operation, _ => false, id => requests?.FirstOrDefault(each => each.Id == id),
            (_, _) => requests ?? [], CancellationToken.None);

    [Fact]
    public async Task A_branch_made_before_its_record_is_landed_and_one_never_made_is_proved_unmade()
    {
        var (_, head) = await RepositoryAsync();
        var operation = Landing(new TogetherAttempt(TogetherForms.Branch, "daoris/landed", null, head, At));

        var before = await ReadAsync(operation);
        Assert.True(before.Target.Missing);
        Assert.Equal(TogetherSettled.Unmade, TogetherSettle.Decide(operation.Attempt!, before));

        await GitFixture.GitAsync(_root, "branch", "daoris/landed", head);
        var after = await ReadAsync(operation);
        Assert.Equal(head, after.Target.Tip);
        Assert.Equal(TogetherSettled.Landed, TogetherSettle.Decide(operation.Attempt!, after));
    }

    [Fact]
    public async Task A_target_moved_to_work_not_the_pinned_tips_stays_uncertain()
    {
        var (line, head) = await RepositoryAsync();
        await GitFixture.GitAsync(_root, "branch", "daoris/landed", line);
        var operation = Landing(new TogetherAttempt(TogetherForms.Branch, "daoris/landed", null, head, At));

        var read = await ReadAsync(operation);

        Assert.False(read.HoldsPinned);
        Assert.Equal(TogetherSettled.Uncertain, TogetherSettle.Decide(operation.Attempt!, read));
    }

    [Fact]
    public async Task A_merge_admitted_before_its_correlation_is_found_at_its_replayed_tip_and_stays_landing()
    {
        var (line, head) = await RepositoryAsync();
        var operation = Landing(new TogetherAttempt(TogetherForms.Merge, "main", line, head, At) { Replayed = head });

        var none = await ReadAsync(operation);
        Assert.Equal(TogetherSettled.Unmade, TogetherSettle.Decide(operation.Attempt!, none));

        var admitted = await ReadAsync(operation, [Request("req-old", line) , Request("req-1", head)]);
        Assert.Equal("req-1", admitted.Request!.Id);
        Assert.Equal(TogetherSettled.Open, TogetherSettle.Decide(operation.Attempt!, admitted));

        var settled = await ReadAsync(operation, [Request("req-1", head) with { ClosedAt = At, ClosedWhy = IntegrationRequestCloses.Settled }]);
        Assert.Equal(TogetherSettled.Landed, TogetherSettle.Decide(operation.Attempt!, settled));
    }

    [Fact]
    public async Task A_merge_whose_integration_store_does_not_read_stays_uncertain_with_its_line_unmoved()
    {
        var (line, head) = await RepositoryAsync();
        var operation = Landing(new TogetherAttempt(TogetherForms.Merge, "main", line, head, At) { Replayed = head });

        var read = await TogetherSettle.ReadAsync(_root, operation, _ => false, _ => null,
            (_, _) => throw new IOException("the store is held"), CancellationToken.None);

        Assert.True(read.RequestUnread);
        Assert.Equal(TogetherSettled.Uncertain, TogetherSettle.Decide(operation.Attempt!, read));
    }

    [Fact]
    public async Task The_line_moved_past_its_expected_tip_to_one_holding_the_squashed_work_is_landed()
    {
        var (line, head) = await RepositoryAsync();
        var operation = Landing(new TogetherAttempt(TogetherForms.Squash, "main", line, head, At));
        await GitFixture.GitAsync(_root, "merge", "-q", "--squash", "daoris/head");
        await GitFixture.GitAsync(_root, "commit", "-q", "-m", "squashed");

        var read = await ReadAsync(operation);

        Assert.True(read.HoldsPinned);
        Assert.Equal(TogetherSettled.Landed, TogetherSettle.Decide(operation.Attempt!, read));
    }
}
