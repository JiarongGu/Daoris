using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BGWAIT1b: the one judgement a start and a resumed run share for a turn that ends on the session's own background work
/// (BGWAIT1): wait, as a park would leave the quest, or end as before. In-process over the stand-in ledger, the real client
/// and the real rule; the resumed run itself is the full set's, since the ledger's stand-in runs do not run
/// <c>Driver.ContinueAsync</c>.
/// </summary>
public sealed class BackgroundWaitJudgementTests : IDisposable
{
    private readonly StandInLedger _ledger = new();

    public BackgroundWaitJudgementTests()
    {
        _ledger.Register("engine", "engine-root");
        _ledger.Publish("q1", "engine");
        _ledger.Park("s1", "q1", "tree");
    }

    public void Dispose()
    {
        _ledger.Dispose();
        if (_tree is not null)
        {
            try { Directory.Delete(_tree, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static QuestView Planned(string status, string? awaits = null) =>
        new("q1", "game", "engine", "The work of #q1", "Stand-in work.", status) { Awaits = awaits };

    private async Task<bool?> JudgeAsync(QuestView planned, bool resumed)
    {
        var judge = Daoris.Driver.Driver.WaitsOnBackground(_ledger.Client(), planned, "s1", resumed);
        return judge is null ? null : await judge(CancellationToken.None);
    }

    [Fact]
    public async Task A_resumed_run_whose_quest_is_still_taken_waits()
    {
        Assert.True(await JudgeAsync(Planned("Taken"), resumed: true));
    }

    [Fact]
    public async Task A_resumed_run_on_a_quest_closed_as_planned_never_waits()
    {
        Assert.Null(await JudgeAsync(Planned("Done"), resumed: true));
        Assert.Null(await JudgeAsync(Planned("Declined"), resumed: true));
    }

    [Fact]
    public async Task A_resumed_run_that_asked_another_repository_anew_does_not_wait()
    {
        _ledger.Awaiting("q1", "other");

        Assert.False(await JudgeAsync(Planned("Taken"), resumed: true));
    }

    /// <summary>
    /// DONEEND1: the judgement is read again while the session waits, each read the quest as it stands, so a quest that reached done
    /// meanwhile ends the wait.
    /// </summary>
    [Fact]
    public async Task The_same_judgement_read_again_turns_false_once_the_quest_is_done()
    {
        var judge = Daoris.Driver.Driver.WaitsOnBackground(_ledger.Client(), Planned("Taken"), "s1", resumed: true)!;

        Assert.True(await judge(CancellationToken.None));
        _ledger.Move("q1", "Done");
        Assert.False(await judge(CancellationToken.None));
    }

    /// <summary>DONEEND1: a start's judgement, read again, holds while the quest it took stays taken, and ends once it is done.</summary>
    [Fact]
    public async Task A_start_s_judgement_read_again_holds_while_taken_and_turns_false_once_done()
    {
        _ledger.Driven("s2", "q1", "tree");
        var judge = Daoris.Driver.Driver.WaitsOnBackground(_ledger.Client(), Planned("Open"), "s2", resumed: false)!;

        Assert.True(await judge(CancellationToken.None));
        Assert.True(await judge(CancellationToken.None));
        _ledger.Move("q1", "Done");
        Assert.False(await judge(CancellationToken.None));
    }

    [Fact]
    public async Task A_start_that_did_not_take_its_quest_does_not_wait()
    {
        Assert.False(await JudgeAsync(Planned("Open"), resumed: false));
    }

    // ——— LANDBACK1 part 2: the look a turn's end takes of a quest that closed with uncommitted work in its tree, one for a start and a
    // resumed run, over a real repository's tree.

    private string? _tree;

    private string Tree(bool dirty)
    {
        if (_tree is null)
        {
            _tree = Path.Combine(Path.GetTempPath(), "daoris-turn-carry-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tree);
            Git("init", "-q", "-b", "main");
            Git("config", "user.email", "turn-carry@example.com");
            Git("config", "user.name", "LANDBACK1");
            File.WriteAllText(Path.Combine(_tree, "README.md"), "# engine\n");
            Git("add", "-A");
            Git("commit", "-qm", "the starting point");
        }

        if (dirty) File.WriteAllText(Path.Combine(_tree, "fix.txt"), "the fix\n");
        return _tree;
    }

    private void Git(params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = _tree!, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    private async Task<TurnCarry?> LookAsync(QuestView planned, bool resumed, string tree, Func<bool>? stopped = null)
    {
        var look = Daoris.Driver.Driver.CarriesOn(_ledger.Client(), planned, tree, resumed, stopped ?? (() => false));
        return look is null ? null : await look(CancellationToken.None);
    }

    /// <summary>TURNWAIT1: a quest done with its fix uncommitted is owed a carry-on, in Daoris's own words and never an Accept's.</summary>
    [Fact]
    public async Task A_done_quest_with_an_uncommitted_tree_is_owed_a_carry_on_in_daoris_s_own_words()
    {
        _ledger.Move("q1", "Done");

        var look = await LookAsync(Planned("Taken"), resumed: false, Tree(dirty: true));

        Assert.NotNull(look);
        Assert.True(look.Dirty);
        Assert.Equal(1, look.Paths);
        Assert.Contains("1 uncommitted path(s)", look.Words, StringComparison.Ordinal);
        Assert.Contains("by this repository's conventions", look.Words, StringComparison.Ordinal);
        Assert.Contains("Do not create, switch or push branches", look.Words, StringComparison.Ordinal);
        Assert.Contains("never runs `git add` or `git commit`", look.Words, StringComparison.Ordinal);
        Assert.DoesNotContain("accepted", look.Words, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_done_quest_whose_tree_is_clean_owes_nothing()
    {
        _ledger.Move("q1", "Done");

        var look = await LookAsync(Planned("Taken"), resumed: false, Tree(dirty: false));

        Assert.NotNull(look);
        Assert.False(look.Dirty);
        Assert.Equal(0, look.Paths);
    }

    /// <summary>A parked quest asks the person, and an ask-and-wait waits on another repository: neither is carried on.</summary>
    [Fact]
    public async Task A_parked_quest_and_an_ask_and_wait_are_never_carried_on()
    {
        var tree = Tree(dirty: true);
        // Parked: taken, and held by its own session.
        Assert.False((await LookAsync(Planned("Taken"), resumed: false, tree))!.Eligible);
        Assert.False((await LookAsync(Planned("Taken"), resumed: true, tree))!.Eligible);

        // Asking another repository and waiting.
        _ledger.Awaiting("q1", "other");
        Assert.False((await LookAsync(Planned("Taken"), resumed: true, tree))!.Eligible);
    }

    /// <summary>A session the person stopped is never carried on: not at the look, and not at the write, where the stop is read again.</summary>
    [Fact]
    public async Task A_stopped_session_is_not_carried_on_at_the_look_or_at_the_write()
    {
        _ledger.Move("q1", "Done");
        var tree = Tree(dirty: true);
        var stopped = false;

        var look = await LookAsync(Planned("Taken"), resumed: false, tree, () => stopped);
        Assert.True(look!.Dirty);
        Assert.True(look.Holds!());

        stopped = true;
        Assert.False(look.Holds!());
        Assert.False((await LookAsync(Planned("Taken"), resumed: false, tree, () => stopped))!.Eligible);
    }

    /// <summary>
    /// LANDBACK1 part 3 (b): a resumed run on a quest already closed as planned (a went-on run, D83) is carried on once where the quest
    /// stands done and its tree is dirty, and owes nothing where the tree is clean or the quest was declined. It still never waits on
    /// background work: <see cref="Daoris.Driver.Driver.WaitsOnBackground"/> keeps BGWAIT1b's null for it.
    /// </summary>
    [Fact]
    public async Task A_resumed_run_on_a_quest_closed_as_planned_is_carried_on_once_where_its_tree_is_dirty()
    {
        _ledger.Move("q1", "Done");

        var dirty = await LookAsync(Planned("Done"), resumed: true, Tree(dirty: true));
        Assert.True(dirty!.Dirty);
        Assert.Equal(1, dirty.Limit);
        // A turn's end, resumed or not, keeps the two of part 2.
        Assert.Equal(TurnCarry.Bound, (await LookAsync(Planned("Taken"), resumed: false, Tree(dirty: true)))!.Limit);
        Assert.Null(Daoris.Driver.Driver.WaitsOnBackground(_ledger.Client(), Planned("Done"), "s1", resumed: true));
    }

    [Fact]
    public async Task A_resumed_run_on_a_closed_quest_whose_tree_is_clean_or_that_was_declined_owes_nothing()
    {
        _ledger.Move("q1", "Done");
        Assert.False((await LookAsync(Planned("Done"), resumed: true, Tree(dirty: false)))!.Dirty);

        _ledger.Move("q1", "Declined");
        Assert.False((await LookAsync(Planned("Declined"), resumed: true, Tree(dirty: true)))!.Eligible);
    }

    /// <summary>
    /// A resumed run that is an Accept's carry-on (the session's yes is kept, LANDBACK1 part 1) is carried on by that Accept's own bound,
    /// never also by the run's: one cause has one counter.
    /// </summary>
    [Fact]
    public async Task A_resumed_run_that_is_an_accepts_carry_on_is_not_also_carried_on_from_inside_the_run()
    {
        _ledger.Move("q1", "Done");
        var tree = Tree(dirty: true);

        var look = Daoris.Driver.Driver.CarriesOn(_ledger.Client(), Planned("Done"), tree, resumed: true, () => false, accepted: false)!;
        Assert.True((await look(CancellationToken.None)).Dirty);

        Assert.Null(Daoris.Driver.Driver.CarriesOn(_ledger.Client(), Planned("Done"), tree, resumed: true, () => false, accepted: true));
    }

    /// <summary>A stopped session is never carried on, a closed quest's resume included.</summary>
    [Fact]
    public async Task A_stopped_closed_quests_resume_is_not_carried_on()
    {
        _ledger.Move("q1", "Done");

        Assert.False((await LookAsync(Planned("Done"), resumed: true, Tree(dirty: true), () => true))!.Eligible);
    }

    /// <summary>A resumed run whose quest closed while it ran is judged as a start's is, each read the quest as it stands.</summary>
    [Fact]
    public async Task A_resumed_run_whose_quest_closes_while_it_runs_is_owed_a_carry_on()
    {
        var tree = Tree(dirty: true);
        var planned = Planned("Taken");
        var look = Daoris.Driver.Driver.CarriesOn(_ledger.Client(), planned, tree, resumed: true, () => false)!;

        Assert.False((await look(CancellationToken.None)).Eligible);
        _ledger.Move("q1", "Done");
        Assert.True((await look(CancellationToken.None)).Dirty);
    }

    /// <summary>A folder with no commit has no tree to read: an unread tree owes nothing.</summary>
    [Fact]
    public async Task A_tree_that_cannot_be_read_owes_nothing()
    {
        _ledger.Move("q1", "Done");
        var folder = Path.Combine(Path.GetTempPath(), "daoris-turn-carry-none-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        try
        {
            var look = await LookAsync(Planned("Taken"), resumed: false, folder);
            Assert.False(look!.Dirty);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
