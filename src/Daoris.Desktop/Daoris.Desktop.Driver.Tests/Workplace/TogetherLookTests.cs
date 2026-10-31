using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1d (the review-flow design §4, <i>The head's line</i>, <i>Phases</i>): the look moves an operation by what it reads of
/// the head and its members: the head's turn, its tip and its line, each member's tip. A head reset after <i>checked</i> goes back to
/// <i>checking</i>; a member moving before it ends it stale; the line carried to a successor its own word opened, stale on another.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TogetherLookTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "together-look", Guid.NewGuid().ToString("N")[..8]);

    private string Root => Path.Combine(_scratch, "main");

    private string Home => Path.Combine(_scratch, "home");

    private string _base = "";

    public void Dispose() => GitSeed.Discard(_scratch);

    private static async Task<string> TipAsync(string tree) => (await GitFixture.GitAsync(tree, "rev-parse", "HEAD")).Trim();

    private async Task<string> TreeAsync(string name)
    {
        if (_base.Length == 0)
        {
            Directory.CreateDirectory(Root);
            await GitFixture.GitAsync(Root, "init", "-q", "-b", "main");
            await GitFixture.GitAsync(Root, "config", "user.email", "t@example.test");
            await GitFixture.GitAsync(Root, "config", "user.name", "t");
            File.WriteAllText(Path.Combine(Root, "a.txt"), "0\n");
            await GitFixture.GitAsync(Root, "add", "-A");
            await GitFixture.GitAsync(Root, "commit", "-q", "-m", "base");
            _base = await TipAsync(Root);
        }

        var tree = Path.Combine(_scratch, name);
        await GitFixture.GitAsync(Root, "worktree", "add", "-q", "-b", name, tree, _base);
        return tree;
    }

    private static async Task<string> CommitAsync(string tree, string file, string text)
    {
        File.WriteAllText(Path.Combine(tree, file), text);
        await GitFixture.GitAsync(tree, "add", "-A");
        await GitFixture.GitAsync(tree, "commit", "-q", "-m", file);
        return await TipAsync(tree);
    }

    private async Task<(TogetherOperation Operation, string Head, string Member, string MemberTip)> BoundAsync()
    {
        var head = await TreeAsync("head");
        var member = await TreeAsync("member");
        var memberTip = await CommitAsync(member, "b.txt", "member\n");
        var press = await CommitAsync(head, "c.txt", "head\n");
        var (opened, _) = new TogetherOperations(Home).Open(
            "engine", new TogetherHead("s-line", "s-head", head, "head", press),
            [new TogetherMember("s-member", member, "member", _base, memberTip)], At);
        return (opened!, head, member, memberTip);
    }

    private Task<TogetherOperation?> StepAsync(TogetherOperation operation, bool running, string record = "s-head", bool byOwnWord = false) =>
        new TogetherLook(Home, () => At).StepAsync(Root, operation.Id, new TogetherFacts(record, byOwnWord, null, running, "main"), null, CancellationToken.None);

    [Fact]
    public async Task The_heads_turn_replays_then_its_end_is_checked_and_ready_at_the_heads_tip()
    {
        var (operation, head, member, _) = await BoundAsync();

        Assert.Equal(TogetherPhase.Replaying, (await StepAsync(operation, running: true))!.Phase);
        await GitFixture.GitAsync(head, "cherry-pick", "member");
        var ready = (await StepAsync(operation, running: false))!;

        Assert.Equal(TogetherPhase.Ready, ready.Phase);
        Assert.Equal(await TipAsync(head), ready.Checked);
        Assert.Equal(TogetherAccounts.Held, Assert.Single(ready.Account).How);
        _ = member;
    }

    [Fact]
    public async Task A_head_reset_after_checked_goes_back_to_checking_and_is_checked_again()
    {
        var (operation, head, _, _) = await BoundAsync();
        await StepAsync(operation, running: true);
        await GitFixture.GitAsync(head, "cherry-pick", "member");
        var ready = (await StepAsync(operation, running: false))!;

        await GitFixture.GitAsync(head, "reset", "-q", "--hard", "HEAD~1");
        var back = (await StepAsync(operation, running: false))!;

        Assert.Equal(TogetherPhase.Checking, back.Phase);
        Assert.NotEqual(ready.Checked, back.Tried);
        Assert.Equal(TogetherAccounts.Missing, back.Why);
    }

    [Fact]
    public async Task A_member_moving_before_checked_ends_it_stale()
    {
        var (operation, _, member, _) = await BoundAsync();
        await StepAsync(operation, running: true);

        await CommitAsync(member, "d.txt", "more\n");
        var stale = (await StepAsync(operation, running: true))!;

        Assert.Equal(TogetherPhase.Stale, stale.Phase);
        Assert.Equal(TogetherWhy.MemberMoved, stale.Why);
    }

    [Fact]
    public async Task Carried_to_the_successor_its_own_word_opened_and_stale_on_another_word()
    {
        var (operation, _, _, _) = await BoundAsync();

        var carried = (await StepAsync(operation, running: true, record: "s-next", byOwnWord: true))!;
        Assert.Equal(("s-next", TogetherPhase.Replaying), (carried.Head.Record, carried.Phase));

        var stale = (await StepAsync(operation, running: true, record: "s-after"))!;
        Assert.Equal((TogetherPhase.Stale, TogetherWhy.AnotherWord), (stale.Phase, stale.Why));
    }

    [Fact]
    public async Task A_failed_check_is_not_tried_again_at_the_same_tip()
    {
        var (operation, _, _, _) = await BoundAsync();
        await StepAsync(operation, running: true);
        var failed = (await StepAsync(operation, running: false))!;
        Assert.Equal((TogetherPhase.Checking, TogetherAccounts.Missing), (failed.Phase, failed.Why));

        var written = File.GetLastWriteTimeUtc(new TogetherOperations(Home).FilePath);
        var again = (await StepAsync(operation, running: false))!;

        Assert.Equal(failed.Tried, again.Tried);
        Assert.Equal(written, File.GetLastWriteTimeUtc(new TogetherOperations(Home).FilePath));
    }
}
