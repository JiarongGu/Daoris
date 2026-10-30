using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-v part b (D212; the foundation design §3.4 rule 4, §13.4 step 5, §19.1, §19.3, row f-v's proof): the cases where an older build acts
/// on a line beside a conversation's hand-over, as <c>EndedLimitMoveOlderBuildProcessTests</c> proves them beside the ended limit move's: an older
/// build opening a successor of the hand-over's destination before the words it moved were carried; an older build's own hand-over of the old
/// part, opened unkeyed beside the newer move that stood decided; an older shell's resume of the old record beside the newer open, in both orders.
/// </summary>
/// <remarks>
/// The older build's own statements are made at the ledger, as the build before the store made them: an unkeyed open of a successor, its state
/// moves, its takes. The hand-over's old part has an end boundary of its own, which the harness writes as the old part's watch does, so an older
/// shell's resume lands after it, between the open's ask and the open itself, at <see cref="Cuts.Opening"/>.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HandOverMoveOlderBuildProcessTests : IAsyncLifetime
{
    private readonly LiveLimitHarness _h = new("hand-over-move-older");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <summary>
    /// 🔴 An older build opened a successor of the destination and crashed before the words the open moved were carried; this build, back,
    /// delivers nothing to the destination and holds the move <c>superseded</c> naming the person's word that waits there. The person's take-back
    /// and their word said at the head, each once, leave nothing it names waiting, and the next look closes the hold <c>resolved</c>.
    /// </summary>
    [Fact]
    public async Task A_successor_an_older_build_opened_before_the_moved_word_was_carried_holds_the_move_and_the_word_is_said_once_by_the_person()
    {
        var shell = await _h.RunHandOverShellAsync(Cuts.Opened);
        _h.Ledger.Chat("c3", "working", "acp-stub", _h.Root, from: new ContinuedFrom("c2", HandOverWhy.Limit, HandOverBy.Daoris));
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
        Assert.Equal((0, 0, 0), (LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word), _h.Heard(LiveLimitHarness.Word), _h.Briefed(shell)));
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
    /// 🔴 An older build took its own hand-over of the old part before this build's move, decided by the person's press, was taken: its unkeyed
    /// open made a part continuing the old record. This build, back, finds the head gone on past the old part: the move that stood decided is closed
    /// <c>superseded</c> and opens nothing, and the word the older build left on the old record is never sent, by the move or by a go-on.
    /// </summary>
    [Fact]
    public async Task An_older_builds_hand_over_of_the_old_part_before_the_newer_move_was_taken_supersedes_it_and_nothing_is_opened_or_sent()
    {
        var shell = await _h.RunHandOverShellAsync(Cuts.Decided, atTheCut: null);
        _h.Ledger.Chat("c2", "stopped", "acp-stub", _h.Root, profile: "account-2", from: new ContinuedFrom("c1", HandOverWhy.Press, HandOverBy.Person));
        _h.Kill();
        _h.Config();

        await _h.Runner().TakeUpAsync();
        await Task.Delay(1500);

        var move = _h.MoveFrom("c1")!;
        Assert.Equal(("closed", MoveCloseWhy.Superseded), (move.PhaseWord, move.ClosedWhy));
        Assert.Empty(_h.Ledger.Opened);
        Assert.Equal(0, _h.Briefed(shell));
        Assert.Equal(0, _h.Heard(LiveLimitHarness.Word));
        Assert.Equal(["c1:k1"], _h.Waiting());
    }

    /// <summary>
    /// 🔴 An older shell's resume of the old record lands first, between the move's open being asked and made (§19.1): it goes on with the record's
    /// word, once; the newer shell's open of a part continuing the record, which now runs, is refused by the service, so nothing opens, the move is
    /// held <c>unrecorded</c> and closes without a destination, and the word is not delivered a second time.
    /// </summary>
    [Fact]
    public async Task An_older_shells_resume_of_the_old_record_before_the_open_delivers_the_word_once_and_the_open_is_refused()
    {
        var resumed = 0;
        var shell = await _h.RunHandOverShellAsync(cut: null, atTheCut: name =>
        {
            if (name != Cuts.Opening) return;
            // The older shell's go-on: the old record moves to working with its waiting word, which it takes for its own turn.
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
    /// 🔴 The other order: the newer shell's open lands first and moves the word to its new part; the older shell's resume of the old record, then,
    /// is refused by the service's step, so the word waits on the new part once, and is sent by neither build here.
    /// </summary>
    [Fact]
    public async Task A_newer_shells_open_before_an_older_shells_resume_moves_the_word_once_and_the_resume_is_refused()
    {
        var shell = await _h.RunHandOverShellAsync(cut: null);
        await Poll.Until(() => _h.MoveFrom("c1")?.PhaseWord is "delivered" or "closed", _h.Seen);

        await Assert.ThrowsAsync<DriverException>(() => _h.Client.AdvanceAsync("c1", "working"));

        Assert.Equal(1, _h.Briefed(shell));
        Assert.Equal("stopped", _h.Ledger.State("c1"));
        Assert.Equal(["c2:k1"], _h.Waiting());
        Assert.Equal(0, _h.Heard(LiveLimitHarness.Word));
    }
}
