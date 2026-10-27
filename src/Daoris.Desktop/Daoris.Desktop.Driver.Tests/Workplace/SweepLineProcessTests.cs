using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The clean-up's list and its press ask git for a checkout's line once each (UIFREEZE1), over a real repository: the list
/// once asked it from the pull requests' asks, the session branches' list and the landed branches' judgement, three times a
/// repository, and a frozen window's stack held seventeen such asks waiting on .NET's one process-start lock.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class SweepLineProcessTests : LandedFixture
{
    [Fact]
    public async Task The_clean_ups_list_and_its_press_each_ask_git_for_the_checkouts_line_once()
    {
        var root = await RepositoryAsync("engine");
        var asked = 0;
        var trees = new SessionTrees(Home)
        {
            CheckoutLine = (at, ct) =>
            {
                Interlocked.Increment(ref asked);
                return WorkingTree.DefaultBranchAsync(at, ct);
            },
        };
        // A landing recorded, so the landed half is judged as well as the session branches'.
        var landed = await LandAsync(trees, root, "a.txt", "a\n", new LandingSubject("s1", "q1", "First"));
        Interlocked.Exchange(ref asked, 0);

        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Contains(plan.Landed, item => item.Branch == landed.Branch);
        Assert.Equal(1, Interlocked.Exchange(ref asked, 0));

        await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal(1, Interlocked.Exchange(ref asked, 0));

        // The next look asks again: the checkout's line may have moved between them.
        await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.Equal(1, asked);
    }
}
