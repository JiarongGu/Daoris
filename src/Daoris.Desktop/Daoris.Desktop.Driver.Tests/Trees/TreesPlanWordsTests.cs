using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RPC1b2 (the terminal RPC design §7.3, RPCOPEN1): each <c>trees</c> read's words (<see cref="TreesWords"/>'s plan functions, which
/// the alone path prints and the routes answer as <c>said</c>), and the verb as the running Daoris's client: the text its stand-in's
/// answer is printed as equals the alone path's, case for case, and a read the instance does not answer in words is read alone.
/// </summary>
/// <remarks>Windows only, as the pipe is: each test serves its own stand-in in process, on its own home's name.</remarks>
public sealed class TreesPlanWordsTests
{
    private static readonly TreesAsk Clean = new(TreesLook.Clean);
    private static readonly TreesAsk CleanWorkspace = new(TreesLook.Clean) { Workspace = "team" };
    private static readonly TreesAsk Sync = new(TreesLook.Sync);

    private static SweepItem Branch(string kind, string name = "daoris/s-1", int commits = 0, string? tree = null) =>
        new("game", "default", name, tree, kind, commits, "main", null);

    private static LandedItem Landed(string kind = LandedKind.OnLine) =>
        new("game", "default", "daoris/landed", kind, "main", [], null, null, 0);

    private static LinePull Pull(string kind = PullKind.FastForward) =>
        new("game", "default", "main", kind, "0123456789abcdef", "fedcba9876543210", 2, null, null);

    [Fact]
    public void A_clean_up_with_nothing_to_list_says_so_in_the_verbs_sentence()
    {
        var said = TreesWords.CleanPlan(new SweepPlan([], []), [], Clean);

        Assert.Equal([TreesCommand.NothingToClean(Clean)], said.Out);
        Assert.Equal(0, said.Code);
    }

    [Fact]
    public void A_clean_up_lists_each_branch_the_landed_group_and_what_the_press_would_remove()
    {
        var plan = new SweepPlan([Branch(SweepKind.Empty), Branch(SweepKind.Dirty, "daoris/s-2")], [Landed()]);

        var said = TreesWords.CleanPlan(plan, [], CleanWorkspace);

        Assert.Equal(0, said.Code);
        Assert.Equal(
            [
                "  goes  game  daoris/s-1  nothing beyond `main`",
                "  kept  game  daoris/s-2  its tree has uncommitted work ()",
                "  landed branches — each goes once its work reads on the line:",
                $"  goes  {LandedWords.Describe(Landed())}",
                $"trees: 2 branch(es) would go, a session's with its tree. `{TreesCommand.Press(CleanWorkspace)}` removes them.",
            ],
            said.Out);
        Assert.Contains("--workspace team", said.Out[^1]);
    }

    [Fact]
    public void A_clean_up_that_removes_nothing_says_every_branch_holds_something()
    {
        var said = TreesWords.CleanPlan(new SweepPlan([Branch(SweepKind.InUse)], []), [], Clean);

        Assert.Equal("trees: nothing to clean — every branch listed holds something of its own.", said.Out[^1]);
    }

    [Fact]
    public void A_sync_list_says_each_row_and_how_many_things_the_press_would_change()
    {
        var plan = new SyncPlan([Pull(), Pull(PullKind.UpToDate)], [], [Landed()])
        {
            Looked = [new SyncRepository("game", "default", true)],
            Apart = [new SyncRepository("engine", "default", false)],
        };

        var said = TreesWords.SyncPlan(plan, Sync, DateTimeOffset.UtcNow);

        Assert.Equal(0, said.Code);
        Assert.Equal($"  moves  {SyncWords.Describe(Pull())}", said.Out[0]);
        Assert.Equal($"  stays  {SyncWords.Describe(Pull(PullKind.UpToDate))}", said.Out[1]);
        Assert.Contains("  landed branches whose work reached the line:", said.Out);
        Assert.Equal($"trees: 2 thing(s) would change. `{TreesCommand.Press(Sync)}` does them — Daoris fetches, and never pushes.", said.Out[^1]);
    }

    [Fact]
    public void A_sync_list_that_took_no_repository_says_none_held_a_branch_and_one_up_to_date_says_so()
    {
        Assert.Equal(TreesCommand.NoneHeld(Sync), TreesWords.SyncPlan(new SyncPlan([], [], []), Sync, DateTimeOffset.UtcNow).Out[^1]);

        var upToDate = new SyncPlan([Pull(PullKind.UpToDate)], [], []) { Looked = [new SyncRepository("game", "default", true)] };
        Assert.Equal("trees: everything here is up to date.", TreesWords.SyncPlan(upToDate, Sync, DateTimeOffset.UtcNow).Out[^1]);
    }

    [Fact]
    public void A_landing_plan_is_one_sentence_with_the_plugin_and_what_would_refuse_it()
    {
        var plan = new LandingPlan(LandingForm.Branch, "daoris/work", "s1a2b3c4", Plugin: "pushes", Problem: "the plugin is not ready.");

        Assert.Equal(
            "trees: accepting `s1a2b3c4` would put its work on the branch `daoris/work` (s1a2b3c4), then plugin `pushes` would push it and open "
            + "the pull request. It would be refused now: the plugin is not ready.",
            TreesWords.Planned("s1a2b3c4", plan));
    }

    [Fact]
    public void A_hand_plan_says_the_plugin_its_commits_and_what_would_refuse_it_after_the_plugins_own_lines()
    {
        var plan = new HandPlan("game", "daoris/landed", "pushes", "its checkout is gone", "#12", 3);

        var said = TreesWords.HandPlan(plan, ["  plugin:pushes  asked"]);

        Assert.Equal(
            [
                "  plugin:pushes  asked",
                "trees: handing on `daoris/landed` (game) would give plugin `pushes` its 3 commit(s) to push and open the pull request for. "
                + "Its last pull request: #12 It would be refused now: its checkout is gone",
            ],
            said.Out);
    }

    [Fact]
    public void An_entry_with_no_part_to_land_says_so()
    {
        var entry = new SessionEntry("k", "ask:a1", "game", SessionGroup.Ended, "s1", []);
        var lines = TreesWords.EntryPlanned(entry, new EntryPlan(LandingForm.Branch, "", []), new Dictionary<string, string>()).ToList();

        Assert.Equal(["trees: `k` (game) has no part with work to land here."], lines);
    }

    // The verb as the running Daoris's client: each read's answer carries `said`, and the text the stand-in's answer is printed as
    // equals the alone path's.

    private static (int Code, string Out, string Error) Alone(TreesSaid said)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        said.Print(output, error);
        return (said.Code, output.ToString(), error.ToString());
    }

    private static async Task<(int Code, string Out, string Error)> ViaAsync(InstanceTestHome home, InstanceRoute route, object answer)
    {
        home.StandIn!.Data = answer;
        var (output, error) = (new StringWriter(), new StringWriter());
        var code = await InstanceDoor.ReadAsync(route, new { said = true }, () => Task.FromResult(99), TreesSaid.ReadWords(output, error), home.World("trees"));
        return (code, output.ToString(), error.ToString());
    }

    public static TheoryData<string> Reads => new()
    {
        nameof(OpenRoutes.SweepPlan), nameof(OpenRoutes.TreesSyncPlan), nameof(OpenRoutes.Landing), nameof(OpenRoutes.EntryLanding),
        nameof(OpenRoutes.AskLanding), nameof(OpenRoutes.HandoffPlan),
    };

    [Theory]
    [MemberData(nameof(Reads))]
    public async Task Each_read_through_the_instance_prints_what_the_alone_path_prints(string read)
    {
        if (!OperatingSystem.IsWindows()) return;
        var route = (InstanceRoute)typeof(OpenRoutes).GetProperty(read)!.GetValue(null)!;
        var said = read switch
        {
            nameof(OpenRoutes.SweepPlan) => TreesWords.CleanPlan(new SweepPlan([Branch(SweepKind.Empty)], [Landed()]), [], Clean),
            nameof(OpenRoutes.TreesSyncPlan) => TreesWords.SyncPlan(
                new SyncPlan([Pull()], [], []) { Looked = [new SyncRepository("game", "default", true)] }, Sync, DateTimeOffset.UtcNow),
            nameof(OpenRoutes.HandoffPlan) => TreesWords.HandPlan(new HandPlan("game", "b", "pushes", null, null, 1), ["  plugin:pushes  asked"]),
            _ => TreesWords.EntryPlan(["trees: accepting `k` (game) would merge its work into `main`, 1 tree(s), oldest first.", "  s1  is held: review"]),
        };
        await using var home = new InstanceTestHome();
        home.Serve();

        var routed = await ViaAsync(home, route, new { session = "s1", said = said.ToWire() });

        Assert.Equal(Alone(said), routed);
        Assert.Equal(0, routed.Code);
    }

    [Fact]
    public async Task A_read_whose_answer_carries_no_words_is_read_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        home.Serve().Data = new { branches = Array.Empty<object>(), landed = Array.Empty<object>() };

        var code = await InstanceDoor.ReadAsync(
            OpenRoutes.SweepPlan, new { said = true }, () => Task.FromResult(7), TreesSaid.ReadWords(home.Out, home.Error), home.World("trees"));

        Assert.Equal(7, code);
        Assert.Equal("", home.Out.ToString());
    }

    [Fact]
    public async Task A_refused_plan_is_exit_1_in_its_words_as_the_alone_path_says_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var refusal = TreesWords.Refused("trees: no checkout here.");
        await using var home = new InstanceTestHome();
        home.Serve();

        Assert.Equal(Alone(refusal), await ViaAsync(home, OpenRoutes.SweepPlan, new { said = refusal.ToWire() }));
    }
}
