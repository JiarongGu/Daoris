using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git (<c>work.ended</c>): the end keeping's one writer says each
/// entry it writes ended through <see cref="WorkEnds"/>, with the session it names and its quest, whichever caller asked; a fact that still
/// holds is written and said once; a cleared fact is not said; a process without the home's lease writes and says nothing; and a landing
/// whose branch is gone, retired with its end, is said as it retires.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class WorkEndsGitTests : LandedFixture
{
    private const string Session = "4b62a9fe";

    private const string Quest = "48f8ed9121ec";

    private readonly List<WorkEnd> _heard = [];

    private static Task<int> KeepAsync(SessionTrees trees, string root) =>
        trees.Ends.KeepAsync(root, "engine", "aurora", "main", CancellationToken.None);

    /// <summary>A landing branch with one commit off the line, recorded for <see cref="Session"/> and <see cref="Quest"/>: its tip.</summary>
    private async Task<(LandedBranches Landed, LandedBranch Entry)> LandedAsync(string root, string branch = "feature/q1-work")
    {
        await GitAsync(root, "checkout", "--quiet", "-b", branch);
        await CommitAsync(root, "a.txt", "a\n", "the landed work");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "checkout", "--quiet", "main");
        var entry = new LandedBranch("engine", "aurora", branch, "main", tip, Session, Quest, "Work", DateTimeOffset.UtcNow);
        var landed = new LandedBranches(Home);
        landed.Record(entry);
        return (landed, entry);
    }

    [Fact]
    public async Task A_landed_branch_the_line_now_holds_is_said_once_with_its_session_and_quest()
    {
        var root = await RepositoryAsync("engine");
        var (_, entry) = await LandedAsync(root);
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the landing", entry.Branch);
        using var listening = WorkEnds.Listen(Home, _heard.Add);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        Assert.Equal(1, await KeepAsync(new SessionTrees(Home), root));
        // A fact that still holds is not written again, so nothing is said twice.
        Assert.Equal(0, await KeepAsync(new SessionTrees(Home), root));

        var heard = Assert.Single(_heard);
        Assert.Equal((Session, Quest), (heard.Session, heard.Quest));
        Assert.Equal(Path.GetFullPath(Home), heard.Home);
    }

    /// <summary>🔴 Canary 9's half for the wake: a process that does not hold the home's lease writes nothing, so it says nothing.</summary>
    [Fact]
    public async Task A_process_without_the_lease_writes_and_says_nothing()
    {
        var root = await RepositoryAsync("engine");
        var (_, entry) = await LandedAsync(root);
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the landing", entry.Branch);
        using var listening = WorkEnds.Listen(Home, _heard.Add);

        Assert.Equal(0, await KeepAsync(new SessionTrees(Home), root));

        Assert.Empty(_heard);
    }

    /// <summary>
    /// The kind says what is reached: a fact cleared, the line rewound so it no longer holds the work, is written and not said; and a
    /// listener of another home hears nothing of this one.
    /// </summary>
    [Fact]
    public async Task A_cleared_fact_is_not_said_and_another_homes_listener_hears_nothing()
    {
        var root = await RepositoryAsync("engine");
        var (_, entry) = await LandedAsync(root);
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the landing", entry.Branch);
        var elsewhere = new List<WorkEnd>();
        using var other = WorkEnds.Listen(Path.Combine(Home, "another-home"), elsewhere.Add);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        Assert.Equal(1, await KeepAsync(new SessionTrees(Home), root));

        using var listening = WorkEnds.Listen(Home, _heard.Add);
        await GitAsync(root, "reset", "--hard", "--quiet", "HEAD~1");
        await CommitAsync(root, "b.txt", "b\n", "the line moves elsewhere");

        Assert.Equal(1, await KeepAsync(new SessionTrees(Home), root));
        Assert.Null(new LandedBranches(Home).All().Single(each => each.Branch == entry.Branch).Held);
        Assert.Empty(_heard);
        Assert.Empty(elsewhere);
    }

    /// <summary>A landing whose branch git says is missing, retired with the end the line proves, is said as it retires.</summary>
    [Fact]
    public async Task A_gone_landing_retired_with_its_end_is_said()
    {
        var root = await RepositoryAsync("engine");
        var (_, entry) = await LandedAsync(root);
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the landing", entry.Branch);
        await GitAsync(root, "branch", "-D", "--quiet", entry.Branch);
        using var listening = WorkEnds.Listen(Home, _heard.Add);
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        Assert.True(await new SessionTrees(Home).Ends.KeepGoneAsync(root, "engine", entry, "main", CancellationToken.None));

        Assert.Equal((Session, Quest), (Assert.Single(_heard).Session, _heard[0].Quest));
    }
}
