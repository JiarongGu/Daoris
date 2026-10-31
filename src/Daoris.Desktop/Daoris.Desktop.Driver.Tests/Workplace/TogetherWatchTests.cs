using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1d (the review-flow design §4): the look's pass over the operations. What the records say of the head moves it: its
/// line carried to a successor its own word opened, stale on a hand-over, its turn running. An operation whose sessions were deleted is
/// dropped, and one at <i>landing</i> after a restart is settled by its git effects, never while a landing of the head's tree is under way.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TogetherWatchTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "together-watch", Guid.NewGuid().ToString("N")[..8]);

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

    private async Task<(TogetherOperation Operation, string Head)> BoundAsync()
    {
        var head = await TreeAsync("head");
        var member = await TreeAsync("member");
        var memberTip = await CommitAsync(member, "b.txt", "member\n");
        var press = await CommitAsync(head, "c.txt", "head\n");
        var (opened, _) = new TogetherOperations(Home).Open(
            "engine", new TogetherHead("s-head", "s-head", head, "head", press),
            [new TogetherMember("s-member", member, "member", _base, memberTip)], At);
        return (opened!, head);
    }

    private static SessionRecord Record(string id, string state = "completed", string? head = null, string? from = null, string? why = null) =>
        new(id, "engine", state) { Head = head, From = from, FromWhy = why };

    private static Dictionary<string, SessionRecord> Records(params SessionRecord[] records) =>
        records.ToDictionary(each => each.Id, StringComparer.OrdinalIgnoreCase);

    private Task<IReadOnlyList<TogetherOperation>> LookAsync(params SessionRecord[] records) =>
        new TogetherWatch(Home, () => At).LookAsync(records, (_, _, _) => Task.FromResult<string?>("main"), CancellationToken.None);

    [Fact]
    public async Task A_successor_its_own_word_opened_while_asked_carries_it_and_a_hand_over_ends_it_stale()
    {
        var (operation, _) = await BoundAsync();

        var carried = TogetherWatch.FactsOf(operation, Records(Record("s-head", head: "s-next"), Record("s-next", "running", from: "s-head")), "main")!;
        Assert.Equal(("s-next", true, null, true), (carried.HeadRecord, carried.OpenedByOwnWord, carried.MoveWhy, carried.HeadRunning));

        var handed = TogetherWatch.FactsOf(operation, Records(Record("s-head", head: "s-next"), Record("s-next", from: "s-head", why: HandOverWhy.Press)), "main")!;
        Assert.Equal((false, TogetherWhy.HandedOver), (handed.OpenedByOwnWord, handed.MoveWhy));

        var replaying = operation with { Phase = TogetherPhase.Replaying };
        Assert.False(TogetherWatch.FactsOf(replaying, Records(Record("s-head", head: "s-next"), Record("s-next", from: "s-head")), "main")!.OpenedByOwnWord);
        Assert.Null(TogetherWatch.FactsOf(operation, Records(Record("s-other")), "main"));
    }

    [Fact]
    public async Task The_look_steps_an_operation_by_its_heads_record_and_drops_one_whose_member_was_deleted()
    {
        var (operation, _) = await BoundAsync();

        var looked = Assert.Single(await LookAsync(Record("s-head", "running"), Record("s-member")));
        Assert.Equal(TogetherPhase.Replaying, looked.Phase);

        Assert.Empty(await LookAsync(Record("s-head")));
        Assert.Null(new TogetherOperations(Home).Of(operation.Id));
    }

    [Fact]
    public async Task A_landing_after_a_restart_is_settled_by_its_branch_and_waits_while_a_landing_of_the_head_holds_its_tree()
    {
        var (operation, head) = await BoundAsync();
        var store = new TogetherOperations(Home);
        var tip = await TipAsync(head);
        store.TurnEnded(operation.Id, At);
        store.Checked(operation.Id, tip, [], null, At);
        Assert.NotNull(store.Landing(operation.Id, new TogetherAttempt(TogetherForms.Branch, "daoris/landed", null, tip, At)));

        Assert.Equal(TogetherPhase.Ready, Assert.Single(await LookAsync(Record("s-head"), Record("s-member"))).Phase);

        Assert.NotNull(store.Landing(operation.Id, new TogetherAttempt(TogetherForms.Branch, "daoris/landed", null, tip, At)));
        await GitFixture.GitAsync(Root, "branch", "daoris/landed", tip);
        using (await LandingHolds.TakeAsync(Home, head))
        {
            Assert.Equal(TogetherPhase.Landing, Assert.Single(await LookAsync(Record("s-head"), Record("s-member"))).Phase);
        }

        Assert.Equal(TogetherPhase.Landed, Assert.Single(await LookAsync(Record("s-head"), Record("s-member"))).Phase);
    }
}
