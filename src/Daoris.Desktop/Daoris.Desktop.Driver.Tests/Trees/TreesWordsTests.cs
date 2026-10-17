using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RPC1b3 (the terminal RPC design §7.3, D171's RPC1b note): each <c>trees</c> press's route answers the verb's own words beside the
/// page's fields (<c>said</c>), and the verb that becomes a client prints them. Per verb, the text a stand-in's answer is printed
/// as equals the alone path's, case for case, refusals included: the alone path prints through <see cref="TreesWords"/> too, which
/// <c>TreesConsoleUsesTheWords</c> holds by reading the console's source.
/// </summary>
/// <remarks>Windows only, as the pipe is: each test serves its own stand-in in process, on its own home's name.</remarks>
public sealed class TreesWordsTests : IAsyncLifetime
{
    /// <summary>A press the census opens, as the trees verbs' will be once RPC1c confirms them in the window.</summary>
    private static readonly InstanceRoute Open = new(TreesRoutes.DriverModule, "TREES_PRESS", RouteClass.Open);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-trees-words-tests", Guid.NewGuid().ToString("N")[..8]);
    private InstanceStandIn? _standIn;
    private string _home = "";
    private string _pipe = "";

    public Task InitializeAsync()
    {
        _home = Path.Combine(_root, "home");
        Directory.CreateDirectory(_home);
        var id = HomeIdentity.Ensure(_home)!;
        _pipe = PipeName.For(id, Random.Shared.Next(100_000, 999_999));
        _standIn = new InstanceStandIn(_pipe, id);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_standIn is not null) await _standIn.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>The alone path's text: what the console prints from the record its core returned.</summary>
    private static (int Code, string Out, string Error) Alone(TreesSaid said)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        said.Print(output, error);
        return (said.Code, output.ToString(), error.ToString());
    }

    /// <summary>The client path's text: the route's answer, over the stand-in's pipe, printed as the verb prints it.</summary>
    private async Task<(int Code, string Out, string Error)> ViaAnswerAsync(object answer)
    {
        _standIn!.Data = answer;
        var (output, error) = (new StringWriter(), new StringWriter());
        var world = new InstanceWorld
        {
            Home = _home,
            Environment = name => name == InstanceDoor.Variable ? _pipe : null,
            Out = output,
            Error = error,
            Verb = "trees",
            Answer = TimeSpan.FromSeconds(2),
            Wait = TimeSpan.FromSeconds(10),
        };
        var code = await InstanceDoor.PressAsync(Open, new { }, () => Task.FromResult(99), TreesSaid.Words(output, error), world);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>A route's answer: the page's own fields as it sent them before, the verb's words beside them.</summary>
    private static object Answer(TreesSaid said) => new { session = "s1a2b3c4", done = said.Code == 0, message = "the page's own", said = said.ToWire() };

    private async Task AssertSameAsync(TreesSaid said)
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Equal(Alone(said), await ViaAnswerAsync(Answer(said)));
    }

    // trees remove <path>: DISCARD_SESSION_TREE.
    [Fact]
    public Task A_removed_tree_is_said_as_the_terminal_says_it() =>
        AssertSameAsync(TreesWords.Remove(new TreeRemoval(true, "Removed `tree`, and its branch.")));

    [Fact]
    public Task A_refusal_that_preserves_work_is_exit_1_in_the_same_words() =>
        AssertSameAsync(TreesWords.Remove(new TreeRemoval(false, "Not removed: it holds 2 commit(s) no branch of yours holds.")));

    // trees land <session>: LAND_SESSION_TREE, with the plugin's lines beside the loop's output.
    [Fact]
    public Task A_landing_is_its_sentence() =>
        AssertSameAsync(TreesWords.Land(new TreeLanding(true, "Landed on `main`."), []));

    [Fact]
    public async Task A_landing_through_a_plugin_says_the_plugins_lines_first_and_a_failed_push_is_exit_1()
    {
        var lines = new[] { TreesWords.PluginLine("example.lands", "pushing"), TreesWords.PluginLine("example.lands", "opened a pull request") };
        var pushed = new PluginLanding("example.lands", true, "#4", "pushed");
        var failed = new PluginLanding("example.lands", false, null, "no", Failed: true);

        var ok = TreesWords.Land(new TreeLanding(true, "Landed `feature/x`.", "feature/x", pushed), lines);
        Assert.Equal(["  plugin:example.lands  pushing", "  plugin:example.lands  opened a pull request", "trees: Landed `feature/x`."], ok.Out);
        Assert.Equal(0, ok.Code);
        await AssertSameAsync(ok);
        var bad = TreesWords.Land(new TreeLanding(true, "Landed `feature/x`.", "feature/x", failed), lines);
        Assert.Equal(1, bad.Code);
        await AssertSameAsync(bad);
    }

    [Fact]
    public Task A_landing_refused_is_its_sentence_and_exit_1() =>
        AssertSameAsync(TreesWords.Land(new TreeLanding(false, "Nothing was landed: its tree holds uncommitted work."), []));

    // trees clean --yes: SWEEP.
    private static SweepResult Swept(string kind, bool removed, string message, int commits = 0, string? where = "main") =>
        new(new SweepItem("engine", "aurora", "daoris/s-x", "tree", kind, commits, where, "detail"), removed, message);

    [Fact]
    public Task A_clean_up_says_each_branch_removed_or_kept_the_folders_left_and_the_count() =>
        AssertSameAsync(TreesWords.Clean(new SweepDone(
            [Swept(SweepKind.Landed, true, "removed"), Swept(SweepKind.Dirty, false, "kept"), Swept(SweepKind.Unlanded, false, "A tree holds it.", 3)],
            [new LandedResult(new LandedItem("engine", "aurora", "feature/x", LandedKind.OnLine, "main", [], null, null, 2), true, "removed")],
            ["folder still held"])));

    [Fact]
    public async Task A_clean_up_with_nothing_removed_says_so()
    {
        var said = TreesWords.Clean(new SweepDone([], []));
        Assert.Equal(["trees: removed 0 of 0."], said.Out);
        await AssertSameAsync(said);
    }

    // trees sync --yes: TREES_SYNC, with what was left apart and what the registry followed.
    private static SyncDone Synced(bool conflict) => new(
        [new PullResult(new LinePull("engine", "aurora", "main", PullKind.FastForward, "aaaa", "bbbb", 2, "no route to host", null), !conflict, conflict ? "diverged since" : "moved to `bbbb`")],
        [new RebaseResult(new RebaseItem("engine", "aurora", "feature/x", false, RebaseKind.Replay, "main", null, null, null, 1, null), true, "replayed")],
        [new LandedResult(new LandedItem("engine", "aurora", "feature/y", LandedKind.OnLine, "main", [], null, null, 1), true, "removed")])
    {
        Apart = [new SyncRepository("game", "aurora", false)],
    };

    [Fact]
    public Task A_sync_says_what_was_not_fetched_each_row_what_was_left_apart_and_the_count() =>
        AssertSameAsync(TreesWords.Sync(Synced(conflict: false), new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task A_sync_where_something_did_not_happen_is_exit_1()
    {
        var said = TreesWords.Sync(Synced(conflict: true), new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero));
        Assert.Equal(1, said.Code);
        Assert.EndsWith("1 did not happen, each said above.", said.Out[^1]);
        await AssertSameAsync(said);
    }

    [Fact]
    public async Task A_sync_says_the_lines_the_registry_followed_before_the_count_and_a_failed_following_on_the_error()
    {
        var now = new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);
        var followed = TreesWords.Sync(Synced(false), now, new FollowedSaid(["  registry  engine: registered"], []));
        Assert.Equal("  registry  engine: registered", followed.Out[^2]);
        await AssertSameAsync(followed);
        var failed = TreesWords.Sync(Synced(false), now, new FollowedSaid([], ["trees: the registry did not follow the lines this moved (x); the driver follows them at its next look."]));
        Assert.Single(failed.Error);
        await AssertSameAsync(failed);
    }

    [Fact]
    public Task A_sync_that_took_no_checkout_is_a_sentence_on_the_error() =>
        AssertSameAsync(TreesWords.Refused(TreesCommand.NothingToSync(new TreesAsk(TreesLook.Sync) { Repository = "game" })));

    // trees hand: HANDOFF, named by a branch and a repository.
    [Fact]
    public async Task A_hand_off_says_the_plugins_lines_then_its_sentence()
    {
        var handed = new TreeHand(true, "Handed `feature/x` to `example.lands`.", "feature/x", new PluginLanding("example.lands", true, "#4", "ok"));
        await AssertSameAsync(TreesWords.Hand(handed, [TreesWords.PluginLine("example.lands", "pushing")]));
        await AssertSameAsync(TreesWords.Hand(new TreeHand(false, "Not handed: no plugin is named."), []));
    }

    [Fact]
    public async Task A_hand_off_refused_before_any_plugin_is_spoken_to_is_a_sentence_on_the_error()
    {
        await AssertSameAsync(TreesWords.HandRefused(TreeHand.NotLanded("feature/none")));
        await AssertSameAsync(TreesWords.HandRefused(TreesWords.NamesSeveral("feature/x", ["engine", "game"])));
        await AssertSameAsync(TreesWords.HandRefused(TreesWords.HandNoCheckout("engine", "feature/x")));
        Assert.Contains("say which with `--repository <name>`", TreesWords.HandRefused(TreesWords.NamesSeveral("feature/x", ["engine", "game"])).Error[0]);
    }

    // An instance from before the words: its answer is the page's fields only.
    [Fact]
    public async Task An_answer_with_no_words_is_said_so_and_nothing_is_done_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (code, output, error) = await ViaAnswerAsync(new { session = "s1a2b3c4", done = true, message = "done" });

        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.Contains("carries no words for this press", error);
    }

    /// <summary>The alone path prints through the words the routes answer, so the two cannot drift (RPC1b3).</summary>
    [Theory]
    [InlineData("TreesWords.Remove(")]
    [InlineData("TreesWords.Land(")]
    [InlineData("TreesWords.Clean(")]
    [InlineData("TreesWords.Sync(")]
    [InlineData("TreesWords.Hand(")]
    [InlineData("TreesWords.FollowMovedAsync(")]
    public void TreesConsoleUsesTheWords(string call)
    {
        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "TreesConsole.cs"));

        Assert.Contains(call, console);
    }

    private static string SourceRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver.Host"))) folder = folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("the desktop's source folder was not found");
    }
}
