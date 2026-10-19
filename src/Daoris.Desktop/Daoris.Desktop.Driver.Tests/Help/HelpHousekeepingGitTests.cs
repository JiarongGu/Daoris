using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPMODE1c2a over real git (the Ask Daoris modes design §4.1): Ask Daoris's granted clean-up, asked through its request, calls
/// the cleanup's own judges. A session branch whose work landed goes with its tree; one holding work no branch of the person's
/// holds stays, said with why; the look lists both as the cleanup judges them. The judgement before git is
/// <c>HelpHousekeepingTests</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class HelpHousekeepingGitTests : LandedFixture
{
    private static SessionRequest Request(string operation, params string[] branches) =>
        new("h1", SessionMove.Housekeep, DateTimeOffset.UtcNow)
        {
            By = RequestDoor.Help, Key = "0123456789ab", Operation = operation, Repository = "engine", Branches = branches,
        };

    [Fact]
    public async Task A_cleared_branch_goes_with_its_tree_and_a_held_one_stays()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(landed.Path, "landed.txt", "on the line\n", "landed work");
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", $"merge {landed.Branch}", landed.Branch);
        var held = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(held.Path, "held.txt", "nowhere else\n", "held work");

        File.WriteAllText(ConfiguredAccess.PathOf(Home), """{ "housekeeping": ["inspect", "clean-up"] }""");
        new HelpWired(Home).Keep("h1", new HelpReceipt
        {
            At = DateTimeOffset.UtcNow, Harness = "codex-acp", Folder = HelpRoom.PathOf(Home, "aurora"),
            Housekeeping = [ConfiguredAccess.Inspect, ConfiguredAccess.CleanUp],
        });
        var ledger = new HousekeepingLedger
        {
            Records = [("h1", HelpRoom.Repository, "aurora", HelpRoom.PathOf(Home, "aurora"))],
            Registry = [("engine", "aurora", root)],
        };
        var housekeeping = new HelpHousekeeping(Home, ledger.Client);

        var look = await housekeeping.AnswerAsync(Request(ConfiguredAccess.Inspect), CancellationToken.None);
        Assert.True(look.Done, look.Said);
        Assert.Contains(landed.Branch, look.Said);
        Assert.Contains(held.Branch, look.Said);

        var cleaned = await housekeeping.AnswerAsync(Request(ConfiguredAccess.CleanUp, landed.Branch, held.Branch), CancellationToken.None);

        Assert.True(cleaned.Done, cleaned.Said);
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.DoesNotContain(landed.Branch, branches);
        Assert.False(Directory.Exists(landed.Path));
        Assert.Contains(held.Branch, branches);
        Assert.True(Directory.Exists(held.Path));
        Assert.Contains($"removed engine  {landed.Branch}", cleaned.Said);
        Assert.Contains($"kept    engine  {held.Branch}", cleaned.Said);
    }
}
