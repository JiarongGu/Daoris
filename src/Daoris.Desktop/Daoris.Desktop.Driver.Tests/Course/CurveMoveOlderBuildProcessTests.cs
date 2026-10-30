using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-iii part b (D212; the foundation design §3.4 rule 4, §13.4 step 5, §19.1, §19.3, row f-iii's proof): the cases where an older
/// build acts on a line beside the curve's move, as <c>LimitMoveOlderBuildProcessTests</c> proves them beside the live limit move's: an older
/// build opening a successor of the move's destination before it moved the destination's words; Codex's delayed older move, delivering its own
/// copy after a newer move ended; an older shell's resume of the old part beside the newer open, in both orders; and the reading part a
/// took, a word the person kept on the old part after its end, which the keyed open moves and the new part's own go-on carries, once.
/// </summary>
/// <remarks>
/// The older build's own statements are made at the ledger, as the build before the store made them: an unkeyed open of a successor, its
/// state moves, its takes. Words land at the real protocol stub where a go-on carries them, so what is heard there is what an agent was sent.
/// The curve's move moves the word and never writes it (its brief is its one message), so what the stub hears of a word is a go-on's.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class CurveMoveOlderBuildProcessTests : IAsyncLifetime
{
    private readonly LiveLimitHarness _h = new("curve-move-older");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <summary>
    /// 🔴 A word the person kept on the old part after its end (§13.4 step 3, part a's reading): the keyed open moves it with the words kept
    /// before, the new part's own go-on carries it, once, and the move closes held <c>undelivered</c> naming nothing, never writing it. A second
    /// come-up of the shell sends nothing more, and the record the word left holds nothing.
    /// </summary>
    [Fact]
    public async Task A_word_kept_on_the_old_part_after_its_end_is_moved_by_the_keyed_open_and_carried_once_by_the_new_parts_go_on()
    {
        const string After = "And mention the port in the docs.";
        var kept = new List<string>();
        var shell = await _h.RunCurveShellAsync(
            cut: null,
            atTheCut: name =>
            {
                if (name != Cuts.OldEnded) return;
                // The person says it on the old part once its end is written, before the open is asked: it is on the old part, never attempted.
                _h.Ledger.For("c1").Say("k2", After);
                kept.AddRange(_h.Ledger.Said("c1"));
            },
            keep: new Dictionary<string, string>());
        await Poll.Until(() => _h.MoveFrom("c1")?.PhaseWord == "closed", _h.Seen);

        // The open moved it: it waits on the new part never attempted, and the move's record names it; the move wrote no word.
        Assert.Equal(["k2"], kept);
        var move = _h.MoveFrom("c1")!;
        Assert.Equal(("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, "c2"), (move.PhaseWord, move.HeldWhy, move.ClosedWhy, move.Destination));
        var named = Assert.Single(_h.Store("run-probe").WordsOf(move.Id));
        Assert.Equal(("k2", (string?)null), (named.Word, named.Disposition));
        Assert.Equal(1, _h.Briefed(shell));
        Assert.Equal(0, LiveLimitHarness.Wrote(shell, After));
        Assert.Empty(_h.Ledger.Said("c1"));
        Assert.Single(_h.Ledger.Said("c2"));

        // The shell dies; the one that comes up carries the word on the new part, once, and again does nothing.
        _h.Kill();
        _h.Config();
        var runner = _h.Runner();
        await runner.TakeUpAsync();
        await Poll.Until(() => _h.Heard(After) >= 1, _h.Seen);
        await Poll.Until(() => _h.Waiting().Count == 0, _h.Seen);
        await runner.TakeUpAsync();
        await _h.Runner().TakeUpAsync();
        await Task.Delay(1500);

        Assert.Equal(1, _h.Heard(After));
        Assert.Equal(1, _h.HeardBy(After, "account-2"));
        Assert.Equal(1, _h.Briefed(shell));
        Assert.Empty(_h.Waiting());
        Assert.Equal(("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved), (_h.MoveFrom("c1")!.PhaseWord, _h.MoveFrom("c1")!.HeldWhy, _h.MoveFrom("c1")!.ClosedWhy));
    }

    /// <summary>
    /// 🔴 An older build opened a successor of the destination and crashed before it moved the destination's words; this build, back, delivers
    /// nothing to the destination and holds the move <c>superseded</c> naming the person's word that waits there. The person's take-back and
    /// their word said at the head, each once, leave nothing it names waiting, and the next look closes the hold <c>resolved</c>.
    /// </summary>
    [Fact]
    public async Task A_successor_an_older_build_opened_before_it_moved_the_destinations_words_holds_the_move_and_the_words_are_said_once_by_the_person()
    {
        var shell = await _h.RunCurveShellAsync(Cuts.Opened);
        _h.Ledger.Chat("c3", "working", "acp-stub", _h.Root, from: new ContinuedFrom("c2", HandOverWhy.Curve, HandOverBy.Daoris));
        _h.Kill();
        _h.Config();

        var runner = _h.Runner();
        await runner.TakeUpAsync();
        await Task.Delay(1500);

        var move = _h.MoveFrom("c1")!;
        Assert.Equal(("held", MoveHoldWhy.Superseded, "c2"), (move.PhaseWord, move.HeldWhy, move.Destination));
        var word = Assert.Single(_h.Store("run-probe").WordsOf(move.Id));
        Assert.Equal(("k1", MoveWordDispositions.Held), (word.Word, word.Disposition));

        // Nothing was delivered to the destination, by the move or by its go-on, which a record that went on elsewhere never restarts.
        Assert.Equal((0, 0, 0), (_h.Briefed(shell), LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word), _h.Heard(LiveLimitHarness.Word)));
        Assert.Equal(["c2:k1"], _h.Waiting());

        // The person's presses: the word taken back from the destination, and said at the head, each once.
        Assert.True((await _h.Client.TakeBackAsync("c2", "k1")).Done);
        await _h.Client.SayAsync("c3", LiveLimitHarness.Word);
        var look = await runner.Moves.LookAsync(CancellationToken.None);

        Assert.Single(look!.Resolved);
        var closed = _h.MoveFrom("c1")!;
        Assert.Equal(("closed", MoveCloseWhy.Resolved), (closed.PhaseWord, closed.ClosedWhy));
        Assert.Equal(1, _h.Ledger.TakenBack.Count(taken => taken == ("c2", "k1")));
        Assert.Equal(1, _h.Waiting().Count(waiting => waiting.StartsWith("c3:", StringComparison.Ordinal)));
        Assert.DoesNotContain(_h.Waiting(), waiting => waiting.StartsWith("c2:", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 Codex's delayed older move (§3.4 rule 4, the bound it states): an older build's briefed move read its words and paused; a newer move
    /// opened, briefed and ended; the older move then opened its own successor and left its copy of the word there. The newer move's word is
    /// carried once by its own part's go-on, the older copy once by its part's, and nothing a third time.
    /// </summary>
    [Fact]
    public async Task An_older_builds_delayed_move_carries_its_copy_once_after_the_newer_move_moved_its_word_and_nothing_a_third_time()
    {
        var shell = await _h.RunCurveShellAsync(cut: null);
        await Poll.Until(() => _h.MoveFrom("c1")?.PhaseWord == "closed", _h.Seen);
        Assert.Equal(1, _h.Briefed(shell));

        // The older move, late: its own successor of the old part, unkeyed, its copy of the word waiting there for its go-on.
        _h.Ledger.Chat("c3", "stopped", "acp-stub", _h.Root, from: new ContinuedFrom("c1", HandOverWhy.Curve, HandOverBy.Daoris))
            .Say("o1", LiveLimitHarness.Word);
        _h.Kill();
        _h.Config();

        var runner = _h.Runner();
        await runner.TakeUpAsync();
        await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= 2, _h.Seen);
        await Poll.Until(() => _h.Waiting().Count == 0, _h.Seen);
        await runner.TakeUpAsync();
        await Task.Delay(1500);

        Assert.Equal(2, _h.Heard(LiveLimitHarness.Word));
        Assert.Equal(1, _h.HeardBy(LiveLimitHarness.Word, "account-2"));
        Assert.Equal(("closed", MoveHoldWhy.Undelivered), (_h.MoveFrom("c1")!.PhaseWord, _h.MoveFrom("c1")!.HeldWhy));
        Assert.Empty(_h.Waiting());
    }

    /// <summary>
    /// 🔴 An older shell's resume of the old part lands first, between the move's decision and its open (§19.1): it goes on with the old
    /// part's word, once; the newer shell's open of the old part, which now runs, is refused by the service, so nothing opens, the move is held
    /// and closes without a destination, and the word is not moved.
    /// </summary>
    [Fact]
    public async Task An_older_shells_resume_of_the_old_part_before_the_open_takes_the_word_once_and_the_open_is_refused()
    {
        var resumed = 0;
        var shell = await _h.RunCurveShellAsync(cut: null, atTheCut: name =>
        {
            if (name != Cuts.OldEnded) return;
            // The older shell's go-on: the ended record moves to working with its waiting word, which it takes for its own turn.
            _h.Client.AdvanceAsync("c1", "working").GetAwaiter().GetResult();
            _h.Ledger.Edit("c1", record => record["said"] = new JsonArray());
            resumed++;
        });

        Assert.Equal(1, resumed);
        var move = _h.MoveFrom("c1")!;
        Assert.Equal(("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, (string?)null), (move.PhaseWord, move.HeldWhy, move.ClosedWhy, move.Destination));
        Assert.Single(_h.Ledger.Opened);
        Assert.Equal(1, _h.Ledger.Count);
        Assert.Equal(0, _h.Briefed(shell));
        Assert.Empty(_h.Waiting());
    }

    /// <summary>
    /// 🔴 The other order: the newer shell's open lands first and moves the word to its new part; the older shell's resume of the old part, then,
    /// finds nothing waiting there and is refused by the service's step, so the word waits on the new part for its go-on alone, which carries it once.
    /// </summary>
    [Fact]
    public async Task A_newer_shells_open_before_an_older_shells_resume_moves_the_word_and_the_resume_is_refused()
    {
        var shell = await _h.RunCurveShellAsync(cut: null);
        await Poll.Until(() => _h.MoveFrom("c1")?.PhaseWord == "closed", _h.Seen);

        await Assert.ThrowsAsync<DriverException>(() => _h.Client.AdvanceAsync("c1", "working"));

        Assert.Equal(1, _h.Briefed(shell));
        Assert.Equal("stopped", _h.Ledger.State("c1"));
        Assert.Equal(["c2:k1"], _h.Waiting());

        _h.Kill();
        _h.Config();
        await _h.Runner().TakeUpAsync();
        await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= 1, _h.Seen);
        await Task.Delay(1000);

        Assert.Equal((1, 1), (_h.Heard(LiveLimitHarness.Word), _h.HeardBy(LiveLimitHarness.Word, "account-2")));
        Assert.Empty(_h.Waiting());
    }
}
