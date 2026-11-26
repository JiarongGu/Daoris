using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An ask across two repositories of a family over real git: the producer's ask branch lands on its line and keeps the commit it
/// landed as; the consumer's ask branch then takes its pin of the producer as a commit of its own, from objects alone; and the consumer's
/// landing carries that pin, which names the commit the producer's part landed. The members are <c>engine</c> and <c>game</c>, as the
/// examples are, and nothing in the code reads what either is.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AskPinsGitTests : LandedFixture
{
    private const string Ask = "a1";

    private static string Branch => TrainBranch.Of(Ask);

    private async Task<(string Engine, string Game, IReadOnlyList<RepoView> Registry)> FamilyAsync()
    {
        var engine = await RepositoryAsync("engine");
        var game = await RepositoryAsync("game");
        await File.WriteAllTextAsync(Path.Combine(Scratch, "daoris.family.json"), """{ "layers": [["engine"], ["game"]] }""");
        await CommitAsync(engine, "daoris.json", """{ "domain": { "uses": [] } }""", "the manifest");
        await CommitAsync(game, "daoris.json", """{ "domain": { "uses": ["engine"] } }""", "the manifest");
        DriverConfig.Empty.WithLine("engine", "main").WithLine("game", "main").Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        GitFolders.Remember(Home, [(engine, "engine", "default"), (game, "game", "default")]);
        foreach (var repository in new[] { "engine", "game" })
        {
            Assert.True(WorkflowRunBindings.Bind(Home, new WorkflowRunBinding(
                AskRunKey.Of(Ask, repository)!, repository, "default", "ask-delivery", WorkflowLevels.Task, DateTimeOffset.UnixEpoch) { Ask = Ask }));
        }

        return (engine, game, [new RepoView("engine", true, engine), new RepoView("game", true, game)]);
    }

    /// <summary>The ask's branch of a repository, one commit past its line.</summary>
    private async Task<string> AskBranchAsync(string root, string file)
    {
        await GitAsync(root, "checkout", "-b", Branch);
        await CommitAsync(root, file, "a part\n", "a part of the ask");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "checkout", "main");
        return tip;
    }

    private static LandStart Start(string repository, string tip) =>
        new(RunUnits.IdOf(AskRunKey.Of(Ask, repository)!), AskRunKey.Of(Ask, repository)!, "landing", AskRunKey.BranchUnit(AskRunKey.Of(Ask, repository)!), [],
            repository, "default", tip, "gph-1", "review")
        {
            Authority = StepAuthority.OfDoor(RunUnits.IdOf(AskRunKey.Of(Ask, repository)!), "landing", 1),
        };

    private async Task<string> HeadAsync(string root, string rev) => (await GitAsync(root, "rev-parse", rev)).Trim();

    private static AskPinDoors Doors => new();

    [Fact]
    public async Task A_producers_ask_landing_keeps_the_commit_it_landed_as_and_none_before()
    {
        var (engine, _, registry) = await FamilyAsync();
        var tip = await AskBranchAsync(engine, "part.txt");
        var row = registry[0];
        Assert.Null(await Doors.LandedAsync(row, Ask, default));

        var said = await new AskLanding(Home, () => DateTimeOffset.UnixEpoch).PerformAsync(Start("engine", tip));

        Assert.Contains("merged", said);
        Assert.Equal(await HeadAsync(engine, "main"), await Doors.LandedAsync(row, Ask, default));
        Assert.Equal(0, (await WorkingTree.GitAsync(engine, ["merge-base", "--is-ancestor", tip, await Doors.LandedAsync(row, Ask, default) ?? ""], default)).Code);
    }

    [Fact]
    public async Task A_pin_is_a_commit_of_its_own_on_the_consumers_ask_branch_made_from_objects_alone()
    {
        var (_, game, _) = await FamilyAsync();
        var before = await AskBranchAsync(game, "part.txt");
        var commit = new string('c', 40);

        var written = await TrainPins.WriteAsync(game, Ask, "engine", commit, default);

        Assert.Equal(TrainPinKinds.Written, written.Kind);
        var after = await HeadAsync(game, Branch);
        Assert.Equal(before, await HeadAsync(game, $"{Branch}^"));
        Assert.Equal(["daoris.family.lock"], (await GitAsync(game, "diff", "--name-only", before, after)).Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(commit, FamilyLock.Read((await GitAsync(game, "show", $"{after}:daoris.family.lock")))!["engine"]);
        Assert.Contains("engine", await GitAsync(game, "log", "-1", "--format=%s", after));
        // The checkout is the line's, untouched: no lock in its tree, and its branch has not moved.
        Assert.False(File.Exists(Path.Combine(game, "daoris.family.lock")));
        Assert.Equal("main", (await GitAsync(game, "branch", "--show-current")).Trim());
    }

    [Fact]
    public async Task A_pin_already_there_makes_no_commit_a_newer_one_replaces_it_and_another_producers_pin_stays()
    {
        var (_, game, _) = await FamilyAsync();
        await AskBranchAsync(game, "part.txt");
        var (first, second) = (new string('1', 40), new string('2', 40));

        await TrainPins.WriteAsync(game, Ask, "tools", first, default);
        await TrainPins.WriteAsync(game, Ask, "engine", first, default);
        var tip = await HeadAsync(game, Branch);
        Assert.Equal(TrainPinKinds.Current, (await TrainPins.WriteAsync(game, Ask, "engine", first, default)).Kind);
        Assert.Equal(tip, await HeadAsync(game, Branch));

        Assert.Equal(TrainPinKinds.Written, (await TrainPins.WriteAsync(game, Ask, "engine", second, default)).Kind);
        var pins = FamilyLock.Read(await GitAsync(game, "show", $"{Branch}:daoris.family.lock"))!;
        Assert.Equal((first, second), (pins["tools"], pins["engine"]));
    }

    [Fact]
    public async Task A_lock_that_is_no_lock_and_a_branch_that_is_not_there_are_left_alone()
    {
        var (_, game, _) = await FamilyAsync();
        Assert.Equal(TrainPinKinds.Unread, (await TrainPins.WriteAsync(game, Ask, "engine", new string('c', 40), default)).Kind);

        await GitAsync(game, "checkout", "-b", Branch);
        await CommitAsync(game, "daoris.family.lock", "not a lock\n", "a lock written by hand");
        await GitAsync(game, "checkout", "main");
        var tip = await HeadAsync(game, Branch);

        Assert.Equal(TrainPinKinds.Refused, (await TrainPins.WriteAsync(game, Ask, "engine", new string('c', 40), default)).Kind);
        Assert.Equal(tip, await HeadAsync(game, Branch));
    }

    [Fact]
    public async Task A_two_part_ask_lands_both_and_the_consumers_pin_names_the_producers_landed_commit()
    {
        var (engine, game, registry) = await FamilyAsync();
        var engineTip = await AskBranchAsync(engine, "engine-part.txt");
        await AskBranchAsync(game, "game-part.txt");
        var landing = new AskLanding(Home, () => DateTimeOffset.UnixEpoch);
        var report = new Dictionary<string, PartsReport>
        {
            [Ask] = new(Ask, 1, ["p1", "p2"], new HashSet<string> { "p1", "p2" })
            {
                Repositories = new Dictionary<string, string> { ["p1"] = "engine", ["p2"] = "game" },
            },
        };
        var runs = new[] { new AskRunOf(Ask, "engine"), new AskRunOf(Ask, "game") };

        // Before the producer's part lands, the consumer's pin waits and nothing is written.
        Assert.Empty(await AskPinning.RunAsync(registry, report, runs, Doors, default));
        Assert.Equal(AskPinStates.Waiting, Assert.Single(await AskPins.ReadAsync(registry, "game", Ask, ["engine", "game"], Doors, default)).State);

        await landing.PerformAsync(Start("engine", engineTip));
        var landed = await HeadAsync(engine, "main");
        Assert.Single(await AskPinning.RunAsync(registry, report, runs, Doors, default));
        Assert.Empty(await AskPinning.RunAsync(registry, report, runs, Doors, default));
        Assert.Equal(AskPinStates.Pinned, Assert.Single(await AskPins.ReadAsync(registry, "game", Ask, ["engine", "game"], Doors, default)).State);

        var gameTip = await HeadAsync(game, Branch);
        await landing.PerformAsync(Start("game", gameTip));

        Assert.Equal(landed, FamilyLock.Read(await GitAsync(game, "show", "main:daoris.family.lock"))!["engine"]);
        Assert.Equal(0, (await WorkingTree.GitAsync(engine, ["merge-base", "--is-ancestor", landed, "main"], default)).Code);
        Assert.Equal("a part\n", await GitAsync(game, "show", "main:game-part.txt"));
    }
}
