using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1d (the review-flow design §4, <i>The words</i>, <i>The record</i>, <i>It refuses</i>): the press judges its refusals
/// under the lock order, pins every tip, records the operation at <i>asked</i> and says the fixed words to the head through the say
/// door under <c>together:&lt;operation&gt;</c>; a refusal, or words that reach nobody, bind nothing.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TogetherPressTests : LandedFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly List<(string Session, string Text, string Key)> _said = [];

    private bool _reaches = true;

    private string _base = "";

    private Task<SayAnswer?> SayAsync(string session, string text, string key, CancellationToken ct)
    {
        if (_said.All(each => each.Key != key)) _said.Add((session, text, key));
        return Task.FromResult<SayAnswer?>(new SayAnswer(_reaches, _reaches ? "kept" : "the head takes no words now"));
    }

    private TogetherPress Press() => new(Home, SayAsync, () => At);

    private static async Task<string> TipAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    private async Task<(string Root, TreeOpened Head, TreeOpened Beside, TreeOpened Other)> ThreeAsync()
    {
        var root = await RepositoryAsync("engine");
        _base = await TipAsync(root);
        var trees = new SessionTrees(Home);
        var head = await trees.OpenAsync(root, "engine", "aurora");
        var beside = await trees.OpenAsync(root, "engine", "aurora");
        var other = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(head.Path, "head.txt", "head\n", "the head's work");
        await CommitAsync(beside.Path, "beside.txt", "beside\n", "the work beside it");
        await CommitAsync(other.Path, "other.txt", "other\n", "another session's work");
        return (root, head, beside, other);
    }

    private static TogetherHeadCandidate Head(TreeOpened tree) => new("s-line", "s-head", "engine", "aurora", tree.Path, tree.Branch);

    private TogetherCandidate Member(string session, TreeOpened tree) => new(session, "engine", tree.Path, tree.Branch, _base);

    [Fact]
    public async Task A_press_pins_every_tip_records_asked_and_says_the_fixed_words_once_under_its_key()
    {
        var (_, head, beside, other) = await ThreeAsync();

        var pressed = await Press().PressAsync(Head(head), [Member("s-beside", beside), Member("s-other", other)], CancellationToken.None);

        Assert.Null(pressed.Refused);
        var operation = new TogetherOperations(Home).Of(pressed.Operation!.Id)!;
        Assert.Equal(TogetherPhase.Asked, operation.Phase);
        Assert.Equal(await TipAsync(head.Path), operation.Head.PressTip);
        Assert.Equal([await TipAsync(beside.Path), await TipAsync(other.Path)], operation.Members.Select(each => each.Tip));
        var (session, text, key) = Assert.Single(_said);
        Assert.Equal(("s-head", $"together:{operation.Id}"), (session, key));
        foreach (var member in operation.Members)
        {
            Assert.Contains($"replay `{member.Branch}`'s commits from `{member.Base}` to `{member.Tip}` onto your branch, oldest first", text);
        }

        Assert.Contains("`Supersedes: <commit>` trailer", text);
        Assert.Contains("make no merge commit", text);
    }

    [Fact]
    public async Task A_member_with_uncommitted_work_a_merge_commit_or_a_lost_tree_is_refused_and_nothing_is_bound()
    {
        var (root, head, beside, other) = await ThreeAsync();
        File.WriteAllText(Path.Combine(beside.Path, "loose.txt"), "not committed\n");

        var dirty = await Press().PressAsync(Head(head), [Member("s-beside", beside)], CancellationToken.None);
        Assert.Contains("uncommitted", dirty.Refused);

        await GitAsync(other.Path, "-c", "user.email=f@example.test", "-c", "user.name=F", "merge", "--no-ff", "-q", "-m", "a merge", beside.Branch);
        var merged = await Press().PressAsync(Head(head), [Member("s-other", other)], CancellationToken.None);
        Assert.Contains("merge commit", merged.Refused);

        var lost = await Press().PressAsync(Head(head), [Member("s-gone", other with { Path = Path.Combine(root, "..", "gone") })], CancellationToken.None);
        Assert.Contains("s-gone", lost.Refused);

        Assert.Empty(new TogetherOperations(Home).All());
        Assert.Empty(_said);
    }

    [Fact]
    public async Task Another_repositorys_or_a_teammates_session_or_one_with_a_live_part_or_a_waiting_start_is_refused()
    {
        var (_, head, beside, _) = await ThreeAsync();
        var member = Member("s-beside", beside);

        foreach (var (refused, why) in new[]
        {
            (member with { Repository = "elsewhere" }, "another repository"),
            (member with { Teammate = true }, "teammate"),
            (member with { Live = true }, "running"),
            (member with { StartWaiting = true }, "start waiting"),
            (member with { StepsOn = true }, "workflow"),
        })
        {
            var pressed = await Press().PressAsync(Head(head), [refused], CancellationToken.None);
            Assert.Contains(why, pressed.Refused);
        }

        Assert.Empty(new TogetherOperations(Home).All());
    }

    [Fact]
    public async Task A_head_that_takes_no_words_or_runs_elsewhere_is_refused()
    {
        var (_, head, beside, _) = await ThreeAsync();

        var deaf = await Press().PressAsync(Head(head) with { TakesWords = false }, [Member("s-beside", beside)], CancellationToken.None);
        var away = await Press().PressAsync(Head(head) with { RunsElsewhere = true }, [Member("s-beside", beside)], CancellationToken.None);

        Assert.Contains("takes no words", deaf.Refused);
        Assert.Contains("runs elsewhere", away.Refused);
        Assert.Empty(new TogetherOperations(Home).All());
    }

    [Fact]
    public async Task Words_that_reach_nobody_let_the_operation_go_so_nothing_stays_bound()
    {
        var (_, head, beside, _) = await ThreeAsync();
        _reaches = false;

        var pressed = await Press().PressAsync(Head(head), [Member("s-beside", beside)], CancellationToken.None);

        Assert.Contains("takes no words now", pressed.Refused);
        Assert.Equal(TogetherPhase.LetGo, Assert.Single(new TogetherOperations(Home).All()).Phase);
        Assert.Null(new TogetherOperations(Home).Binding(beside.Path));
    }

    [Fact]
    public async Task A_member_already_brought_together_elsewhere_is_refused()
    {
        var (_, head, beside, other) = await ThreeAsync();
        Assert.Null((await Press().PressAsync(Head(head), [Member("s-beside", beside)], CancellationToken.None)).Refused);

        var again = await Press().PressAsync(Head(other) with { Record = "s-other" }, [Member("s-beside", beside)], CancellationToken.None);

        Assert.Contains("brought together already", again.Refused);
    }
}
