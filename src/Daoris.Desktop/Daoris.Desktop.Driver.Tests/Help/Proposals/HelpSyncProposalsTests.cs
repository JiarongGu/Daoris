using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>sync</c> proposal (HELP10, D109): Settings → Workspace → Session branches → *Bring up to date*, as a
/// card with the screen's two presses — the look, which is the person's and so the only thing that fetches, then the
/// Apply, which acts on the rows that look listed and on nothing else.
/// </summary>
public sealed class HelpSyncProposalsTests : HelpProposalsFixture
{
    private static HelpProposal Sync(string? repository = null) =>
        new("p11", "sync", "sync", repository, null, null, null, "the person's pull request merged", "h1", "proposed");

    /// <summary>The owner's case after a squash merge (D109): the line pulls, a session branch replays, a landed branch goes.</summary>
    private static readonly SyncPlan Merged = new(
        [
            new LinePull("engine", "work", "main", PullKind.FastForward, "aaaaaaaa", "bbbbbbbb", 1, null, null),
            new LinePull("game", "work", null, PullKind.NoLine, null, null, 0, null, null),
        ],
        [
            new RebaseItem("engine", "work", "daoris/s-step", false, RebaseKind.Replay, "main", "cccccccc", CutBy.Record, "feature/q2-first", 2, null),
            new RebaseItem("engine", "work", "daoris/s-busy", false, RebaseKind.InUse, "main", null, null, null, 0, null),
        ],
        [
            new LandedItem("engine", "work", "feature/q2-first", LandedKind.OnLine, "main", [], null, "https://example.test/pr/2", 3),
            new LandedItem("engine", "work", "feature/q3-other", LandedKind.Differs, "main", ["src/a.cs"], null, null, 1),
        ]);

    private static readonly SyncPlan Quiet = new(
        [new LinePull("engine", "work", "main", PullKind.UpToDate, "aaaaaaaa", "aaaaaaaa", 0, null, null)], [], []);

    [Fact]
    public void Before_the_person_looks_the_card_says_looking_fetches_and_nothing_moves_until_Apply()
    {
        var plan = HelpProposals.Plan(Sync("engine"), DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver trees sync --repository engine", plan.Terminal);
        Assert.Contains("Look for updates first", plan.Describe);
        Assert.Contains("fetches each line from `origin`, as you", plan.Describe);
        Assert.False(plan.Sync!.Looked);
        Assert.Empty(plan.Sync.Rows);
    }

    [Fact]
    public void A_repository_this_machine_does_not_have_is_refused()
    {
        Assert.Contains("`nowhere` is not registered", HelpProposals.Plan(Sync("nowhere"), DriverConfig.Empty, Facts).Refusal);
        Assert.Equal("daoris-driver trees sync", HelpProposals.Plan(Sync(), DriverConfig.Empty, Facts).Terminal);
    }

    /// <summary>
    /// The first Apply is the look (D109: looking reaches the network as the person, so it waits for their press): the
    /// list is <c>TREES_SYNC_PLAN</c>'s own, kept in the proposal's file, and the card stays for the second press.
    /// </summary>
    [Fact]
    public async Task The_first_press_looks_and_keeps_every_row_the_list_says_on_the_card()
    {
        var doors = new HelpStandInDoors { Listed = Merged };

        var (looked, _, later) = await ApplyAsync(Sync("engine"), doors);

        Assert.False(looked.Applied);
        // LEFT3 c: the look settles nothing, and says so, so the page logs no settlement for it.
        Assert.True(looked.Stands);
        Assert.Equal(["TREES_SYNC_PLAN engine"], doors.Calls);
        Assert.Contains("3 thing(s) would change", looked.Told);
        Assert.Equal([looked.Told], later);
        var kept = HelpProposals.Find(_home, "p11")!;
        Assert.Equal("proposed", kept.State);
        Assert.Equal(
            [
                ("engine:main", "line", true), ("game:", "line", false),
                ("engine:daoris/s-step", "replay", true), ("engine:daoris/s-busy", "replay", false),
                ("engine:feature/q2-first", "delete", true), ("engine:feature/q3-other", "delete", false),
            ],
            kept.Listed!.Select(row => (row.Key, row.Step, row.Moves)));
        Assert.Contains("fast-forwards 1 commit(s) to `origin/main`", kept.Listed![0].Says);

        var plan = HelpProposals.Plan(kept, DriverConfig.Empty, Facts);
        Assert.Null(plan.Refusal);
        Assert.True(plan.Sync!.Looked);
        Assert.Equal(6, plan.Sync.Rows.Count);
        Assert.Equal("daoris-driver trees sync --repository engine --yes", plan.Terminal);
        Assert.Contains("3 thing(s) change", plan.Describe);
    }

    [Fact]
    public async Task A_look_that_finds_nothing_to_do_says_so_and_settles_the_card()
    {
        var (looked, _, _) = await ApplyAsync(Sync(), new HelpStandInDoors { Listed = Quiet });

        Assert.False(looked.Applied);
        Assert.False(looked.Stands);
        Assert.Contains("nothing to bring up to date — engine  main  up to date with `origin/main`", looked.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p11")!.State);
    }

    /// <summary>
    /// WSR7: the card's look says what its rows do not — what was not fetched, once, since a row carries only a mark, and
    /// the repositories it left apart, holding no branch of Daoris's (D112), which a proposal naming one looks at.
    /// </summary>
    [Fact]
    public async Task A_look_says_once_what_was_not_fetched_and_names_the_repositories_it_left_apart()
    {
        var offline = new SyncPlan(
            [new LinePull("engine", "work", "main", PullKind.FastForward, "aaaaaaaa", "bbbbbbbb", 1, "fatal: Could not read from remote repository.", null)
                { Reach = "ssh" }],
            [], [])
        {
            Apart = [new SyncRepository("game", "work", Holds: false), new SyncRepository("tools", "work", Holds: false)],
        };

        var (looked, _, _) = await ApplyAsync(Sync(), new HelpStandInDoors { Listed = offline });

        Assert.Contains("fast-forwards 1 commit(s) to `origin/main` (not fetched)", looked.Told);
        Assert.Contains(
            "1 of 1 repositories were not fetched, so each is judged against what origin said when it was last fetched here: "
            + "fatal: Could not read from remote repository. (1): engine (never fetched)", looked.Told);
        Assert.Contains("`core.sshCommand`", looked.Told);
        Assert.Contains("Not looked at, since they hold no branch of Daoris's (2): game, tools; a proposal naming one looks at it.", looked.Told);
        Assert.DoesNotContain("trees:", looked.Told);
    }

    /// <summary>
    /// LEFT3 b: the card itself says what its rows do not, not only the look's words: each repository not fetched with
    /// git's reason, when it last heard from origin and how origin is reached, and the repositories the look left apart
    /// (D112). They are kept in the proposal's file beside the rows, so a card drawn again still says them.
    /// </summary>
    [Fact]
    public async Task A_look_keeps_what_was_not_fetched_and_the_repositories_left_apart_for_the_card()
    {
        var then = DateTimeOffset.Parse("2026-09-29T08:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture);
        var offline = new SyncPlan(
            [
                new LinePull("engine", "work", "main", PullKind.FastForward, "aaaaaaaa", "bbbbbbbb", 1, "fatal: Could not read from remote repository.", null)
                    { Reach = "ssh", LastFetch = then },
                new LinePull("game", "work", "main", PullKind.UpToDate, "cccccccc", "cccccccc", 0, "fatal: unable to access origin", null)
                    { Reach = "https" },
                new LinePull("tools", "work", "main", PullKind.UpToDate, "dddddddd", "dddddddd", 0, null, null),
            ],
            [], [])
        {
            Apart = [new SyncRepository("docs", "work", Holds: false), new SyncRepository("site", "work", Holds: false)],
        };

        await ApplyAsync(Sync(), new HelpStandInDoors { Listed = offline });
        var kept = HelpProposals.Find(_home, "p11")!;
        var besides = HelpProposals.Plan(kept, DriverConfig.Empty, Facts).Sync!.Besides;

        Assert.Equal(
            [
                new HelpSyncUnfetched("engine", "fatal: Could not read from remote repository.", then, "ssh"),
                new HelpSyncUnfetched("game", "fatal: unable to access origin", null, "https"),
            ],
            besides.NotFetched);
        Assert.Equal(["docs", "site"], besides.Apart);
    }

    /// <summary>Before a look, and in a file a look kept before LEFT3, the card has nothing besides its rows to say.</summary>
    [Fact]
    public void A_card_not_looked_at_or_kept_before_LEFT3_says_nothing_besides_its_rows()
    {
        Assert.Equal(HelpSyncBesides.None, HelpProposals.Plan(Sync(), DriverConfig.Empty, Facts).Sync!.Besides);

        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "p12.json"), """
            { "id": "p12", "proposed": "2026-09-30T10:00:00Z", "by": { "session": "h1" }, "kind": "sync", "door": "sync",
              "target": null, "why": "merged", "state": "proposed",
              "listed": [ { "key": "engine:main", "step": "line", "moves": true, "says": "engine  main  fast-forwards 1 commit(s)" } ] }
            """);
        var plan = HelpProposals.Plan(HelpProposals.Find(_home, "p12")!, DriverConfig.Empty, Facts);

        Assert.True(plan.Sync!.Looked);
        Assert.Empty(plan.Sync.Besides.NotFetched);
        Assert.Empty(plan.Sync.Besides.Apart);
    }

    /// <summary>A look that takes no repository, since none holds a branch of Daoris's (D112), says so rather than "no checkout".</summary>
    [Fact]
    public async Task A_look_where_no_repository_holds_Daoris_branches_says_so()
    {
        var none = new SyncPlan([], [], []) { Apart = [new SyncRepository("game", "work", Holds: false)] };

        var (looked, _, _) = await ApplyAsync(Sync(), new HelpStandInDoors { Listed = none });

        Assert.Contains("nothing to bring up to date — no repository with a checkout here holds a branch of Daoris's.", looked.Told);
        Assert.Contains("(1): game;", looked.Told);
    }

    [Fact]
    public async Task A_look_at_a_repository_with_no_checkout_here_says_so()
    {
        var (looked, _, _) = await ApplyAsync(Sync("game"), new HelpStandInDoors());

        Assert.Contains("nothing to bring up to date — `game` has no checkout here.", looked.Told);
    }

    [Fact]
    public async Task A_look_the_door_refuses_is_settled_refused_in_its_words()
    {
        var (looked, _, _) = await ApplyAsync(Sync(), new HelpStandInDoors { SyncRefusal = "the driver is still coming up." });

        Assert.False(looked.Applied);
        Assert.Contains("the driver is still coming up.", looked.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p11")!.State);
    }

    /// <summary>The second Apply is <c>TREES_SYNC</c>'s own press: the rows the look listed that move, and none other.</summary>
    [Fact]
    public async Task The_second_press_acts_only_on_the_rows_the_look_listed()
    {
        var doors = new HelpStandInDoors { Listed = Merged };
        await ApplyAsync(Sync("engine"), doors);
        var looked = HelpProposals.Find(_home, "p11")!;

        var applied = await HelpProposals.ApplyAsync(
            _home, looked, HelpProposals.Plan(looked, DriverConfig.Empty, Facts), doors, _ => { }, CancellationToken.None);

        Assert.True(applied.Applied);
        Assert.False(applied.Stands);
        Assert.Equal("TREES_SYNC engine engine:daoris/s-step engine:feature/q2-first engine:main", doors.Calls[^1]);
        Assert.Contains("1 line(s) moved, 1 branch(es) replayed, 1 removed", applied.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p11")!.State);
    }

    /// <summary>A listed row the press found moved since, or a conflict, is said, as the terminal says it.</summary>
    [Fact]
    public async Task A_listed_row_that_did_not_happen_is_said()
    {
        var doors = new HelpStandInDoors { Listed = Merged, Conflict = "engine  daoris/s-step  stopped at a conflict in src/a.cs" };
        await ApplyAsync(Sync("engine"), doors);
        var looked = HelpProposals.Find(_home, "p11")!;

        var applied = await HelpProposals.ApplyAsync(
            _home, looked, HelpProposals.Plan(looked, DriverConfig.Empty, Facts), doors, _ => { }, CancellationToken.None);

        Assert.True(applied.Applied);
        Assert.Contains("1 did not happen: engine  daoris/s-step  stopped at a conflict in src/a.cs", applied.Told);
    }
}

public sealed partial class HelpStandInDoors
{
    /// <summary>What the look lists, as <c>TREES_SYNC_PLAN</c> would.</summary>
    public SyncPlan Listed { get; init; } = new([], [], []);

    /// <summary>What the look's door refuses with; null to list.</summary>
    public string? SyncRefusal { get; init; }

    /// <summary>A replay the press stops at a conflict, in the tree layer's words; null for none.</summary>
    public string? Conflict { get; init; }

    public Task<SyncPlan> SyncPlanAsync(string? repository, CancellationToken ct)
    {
        if (SyncRefusal is { } refused) throw new DriverException(refused);
        Calls.Add($"TREES_SYNC_PLAN {repository}".TrimEnd());
        return Task.FromResult(Listed);
    }

    public Task<SyncDone> SyncAsync(string? repository, IReadOnlySet<string> only, CancellationToken ct)
    {
        Calls.Add($"TREES_SYNC {repository} {string.Join(' ', only.Order(StringComparer.Ordinal))}");
        return Task.FromResult(new SyncDone(
            [.. Listed.Lines.Where(pull => only.Contains($"{pull.Repository}:{pull.Line}")).Select(pull => new PullResult(pull, pull.Moves, "moved"))],
            [.. Listed.Rebases.Where(item => only.Contains($"{item.Repository}:{item.Branch}"))
                .Select(item => Conflict is { } conflict ? new RebaseResult(item, false, conflict) : new RebaseResult(item, item.Replays, "replayed"))],
            [.. Listed.Deletes.Where(item => only.Contains($"{item.Repository}:{item.Branch}")).Select(item => new LandedResult(item, item.Removable, "removed"))]));
    }
}
