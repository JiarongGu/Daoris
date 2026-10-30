using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-iv part b, sitting 2 (D212; the foundation design §3.4 rule 4, §13.4 step 5, §19.1, §19.3, row f-iv's proof): the cases where an
/// older build acts on a quest's line beside the driven carry-on's move, as <c>CurveMoveOlderBuildProcessTests</c> proves them beside the curve's:
/// an older build opening a successor of the destination before it moved the destination's words; Codex's delayed older move; and an older
/// shell's resume of the old part beside the newer open, in both orders.
/// </summary>
/// <remarks>
/// The older build's own statements are made at the service by hand, as the build before the store made them (an unkeyed open, a state move, a
/// take), over the stand-in service the driven harness runs; the loop that comes up is the real watch.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class DrivenMoveOlderBuildProcessTests : IAsyncLifetime
{
    private readonly DrivenMoveHarness _h = new("driven-move-older");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <summary>
    /// 🔴 An older build opened a successor of the destination and crashed before it moved the destination's words; this build, back, takes the
    /// move up, delivers nothing to the destination and closes the move held <c>undelivered</c>, its record naming the person's word that waits
    /// there never attempted. No agent hears the word, nothing is said lost, and a second come-up changes nothing.
    /// </summary>
    /// <remarks>
    /// The line's head is read for a conversation (<c>WordsNever.ContinuedIn</c> reads chat records), so a successor of a driven record is not
    /// read as the head and the move is not held <c>superseded</c> as the live move's is; the word is named by the move's record and waits.
    /// </remarks>
    [Fact]
    public async Task A_successor_an_older_build_opened_before_it_moved_the_destinations_words_delivers_nothing_to_the_destination_and_names_the_word()
    {
        await _h.SeedAsync(word: true);
        await _h.KillAtAsync(Cuts.Opened);
        await _h.OlderOpenAsync();
        await _h.KillAsync();

        _h.ComeUp();
        await _h.SettleAsync(ready: () => _h.Shapes("s1").SequenceEqual(["closed/undelivered/resolved/"]));
        await _h.StopAsync();

        var move = _h.MoveFrom("s1")!;
        Assert.Equal(("closed", MoveHoldWhy.Undelivered, "s2"), (move.PhaseWord, move.HeldWhy, move.Destination));
        var word = Assert.Single(_h.Store("run-probe").WordsOf(move.Id));
        Assert.Equal(("w1", (string?)null), (word.Word, word.Disposition));
        Assert.Equal(["s2:w1"], _h.Waiting());
        Assert.Empty(_h.Prompts().Where(prompt => prompt.Text.Contains(DrivenMoveHarness.Word, StringComparison.Ordinal)));
        Assert.Empty(_h.Service.Taken);
        for (var at = 1; at <= _h.Service.SessionCount; at++)
        {
            Assert.DoesNotContain(_h.Events.After($"s{at}", 0).Events, e => e.Code == SessionEventCodes.Lost);
        }

        var shape = _h.SnapshotOf();
        _h.ComeUp();
        await _h.SettleAsync();
        await _h.StopAsync();
        Assert.Equal(shape, _h.SnapshotOf());
    }

    /// <summary>
    /// 🔴 Codex's delayed older move (§3.4 rule 4, the bound it states): a newer move opened, briefed and delivered; the older move then opened its
    /// own successor and left its copy of the word there. The newer move's word reached its agent once, the older copy waits on its own part
    /// and is sent to no agent by this build, and nothing is sent a third time, however often a loop comes up.
    /// </summary>
    /// <remarks>
    /// The quest is closed by then (its new part's agent closed it), so the older part's own go-on is not asked for: what is proved is that
    /// this build sends the copy nowhere and moves nothing.
    /// </remarks>
    [Fact]
    public async Task An_older_builds_delayed_move_leaves_its_copy_waiting_after_the_newer_move_delivered_its_word_and_nothing_is_sent_a_third_time()
    {
        await _h.SeedAsync(word: true);
        await _h.KillAtAsync(cut: null);
        Assert.Equal("delivered", _h.MoveFrom("s1")!.PhaseWord);

        var older = await _h.OlderOpenAsync();
        Assert.Equal(200, await _h.AdvanceAsync(older, "stopped", interrupted: true));
        Assert.Equal("w2", await _h.Service.SayAsync(older, DrivenMoveHarness.Word));
        await _h.KillAsync();

        _h.ComeUp();
        await _h.SettleAsync();
        await _h.StopAsync();

        var heard = Assert.Single(_h.Prompts(), prompt => prompt.Text.Contains(DrivenMoveHarness.Word, StringComparison.Ordinal));
        Assert.Equal("s2", heard.Session);
        Assert.Equal([$"{older}:w2"], _h.Waiting());
        Assert.Equal("delivered", _h.MoveFrom("s1")!.PhaseWord);

        var shape = _h.SnapshotOf();
        _h.ComeUp();
        await _h.SettleAsync();
        await _h.StopAsync();
        Assert.Equal(shape, _h.SnapshotOf());
    }

    /// <summary>
    /// 🔴 An older shell's resume of the old part lands first, between the move's decision and its open (§19.1): it goes on with the old part's
    /// word, taking it for its own turn; the newer loop's open of the old part, which now runs, is refused by the service, so nothing opens and
    /// nothing is written to a new part, the move closes held <c>unrecorded</c> and the word is not moved.
    /// </summary>
    [Fact]
    public async Task An_older_shells_resume_of_the_old_part_before_the_open_takes_the_word_once_and_the_open_is_refused()
    {
        await _h.SeedAsync(word: true);
        var resumed = 0;
        await _h.KillAtAsync(cut: null, atTheCut: name =>
        {
            if (name != Cuts.Decided) return;
            Assert.Equal(200, _h.AdvanceAsync("s1", "working").GetAwaiter().GetResult());
            _h.TakeAsync("s1", "w1").GetAwaiter().GetResult();
            resumed++;
        });

        Assert.Equal(1, resumed);
        var move = _h.MoveFrom("s1")!;
        Assert.Equal(("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, (string?)null), (move.PhaseWord, move.HeldWhy, move.ClosedWhy, move.Destination));
        Assert.Equal(1, _h.Service.SessionCount);
        Assert.Single(_h.Service.Opened);
        Assert.Empty(_h.Waiting());
        Assert.Empty(_h.Prompts().Where(prompt => prompt.Text.Contains(DrivenMoveHarness.Word, StringComparison.Ordinal)));
    }

    /// <summary>
    /// 🔴 The other order: the newer loop's open lands first and moves the word to its new part, which delivers it; the older shell's resume of
    /// the old part, then, finds nothing waiting there and is refused by the service's step, and the word reached an agent once.
    /// </summary>
    [Fact]
    public async Task A_newer_loops_open_before_an_older_shells_resume_moves_the_word_and_the_resume_is_refused()
    {
        await _h.SeedAsync(word: true);
        await _h.KillAtAsync(cut: null);
        Assert.Equal("delivered", _h.MoveFrom("s1")!.PhaseWord);

        Assert.Equal(409, await _h.AdvanceAsync("s1", "working"));

        Assert.Equal("failed", _h.Service.State("s1"));
        Assert.Empty(_h.Waiting());
        var heard = Assert.Single(_h.Prompts(), prompt => prompt.Text.Contains(DrivenMoveHarness.Word, StringComparison.Ordinal));
        Assert.Equal("s2", heard.Session);
    }
}
