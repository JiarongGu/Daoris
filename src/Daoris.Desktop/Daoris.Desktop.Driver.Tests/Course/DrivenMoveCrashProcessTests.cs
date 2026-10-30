using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-iv part b, sitting 2 (D212; the foundation design §3.3, §13.4 steps 1 to 5, §19.4, row f-iv's proof): the driven carry-on's move
/// record holds through a loop killed at every boundary the move passes. A real driver look carries a failed quest's record <c>s1</c> on to a
/// new part through the keyed open, is stopped at each boundary (<see cref="Cuts"/>) as a killed process never leaves it, and a loop that comes
/// up (the real watch's first look) sweeps its records, takes its move up and ends it by its phase; the person's word the open moved is then
/// delivered once, held and carried once by the part it waits on, or left waiting with nothing sent again, and the move's key is never asked twice.
/// </summary>
/// <remarks>
/// <para><b>Which boundaries the driven move passes.</b> Eight of the nine: every one of <see cref="Cuts.All"/> but <see cref="Cuts.OldEnded"/>.
/// The session a carry-on goes on from has ended already, so the decision is written <i>at</i> <c>old-ended</c> and the old part's end is no
/// write of its own (the census pins both: the record read at <c>decided</c> stands at <c>old-ended</c>, and the boundary named
/// <c>old-ended</c> is never passed).</para>
/// <para><b>Each boundary is run with the person's word kept on the failed record and with none</b>, since a move that moved nothing is read by
/// its prompt's outcome alone. <b>What each closes as</b>, the same with a word and without but where the word is: <c>decided</c> falls back
/// <c>not-opened</c> and the line's next carry-on delivers; <c>opening</c> closes held <c>unrecorded</c>, the open never made, and the next
/// carry-on delivers; <c>answered</c> and <c>marked</c> close held <c>unrecorded</c> and <c>opened</c> held <c>undelivered</c> naming nothing,
/// the open made and the new part's own record carried on by the loop that comes up, which moves and delivers the word once; <c>written</c>
/// closes held <c>unrecorded</c> (the write returned, its outcome unkept: the dead part's agent heard the word once, it is never taken and
/// never sent again, and it is left waiting on that part); <c>sent</c> and <c>taken</c> close <c>delivered</c>, the take-up taking the word
/// off at <c>sent</c> and the dead loop having taken it at <c>taken</c>.</para>
/// <para>The killed loop's agent is a real process, which finishes its turn and closes the quest while its loop stands blocked, so a part whose
/// prompt was written is not carried on again by this proof; the records the loop left live are ended <c>stopped</c> and interrupted as the
/// orphan sweep ends them (<see cref="DrivenMoveHarness.KillAsync"/>).</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class DrivenMoveCrashProcessTests : IAsyncLifetime
{
    private readonly DrivenMoveHarness _h = new("driven-move-crash");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    private const string Unrecorded = "closed/unrecorded/resolved/";

    private const string Undelivered = "closed/undelivered/resolved/";

    private const string Delivered = "delivered///";

    /// <summary>What a killed loop's carry-on comes to once a loop comes up.</summary>
    /// <param name="First">The shapes of the moves of <c>s1</c>, in order: phase, hold, close, fall-back code.</param>
    /// <param name="Second">The shapes of the moves of <c>s2</c>, the destination, which the loop that came up carried on where it was left.</param>
    /// <param name="Hears">The session whose first prompt the word reached: the dead loop's part, or the part the loop that came up opened.</param>
    /// <param name="Waits">Where the word is left waiting, none where it was taken.</param>
    internal sealed record Ends(string[] First, string[] Second, string Hears, string? Waits = null);

    internal static Ends Row(string cut) => cut switch
    {
        // Before the open: the line stays on its old part, and its next carry-on moves and delivers the word on a part of its own.
        Cuts.Decided => new(["fell-back///not-opened", Delivered], [], "s2"),
        Cuts.Opening => new([Unrecorded, Delivered], [], "s2"),

        // The open made and the move not delivered: the destination, ended by the loop's death, is carried on, and the word moved with it.
        Cuts.Answered => new([Unrecorded], [Delivered], "s3"),
        Cuts.Opened => new([Undelivered], [Delivered], "s3"),
        Cuts.Marked => new([Unrecorded], [Delivered], "s3"),

        // The prompt written and its outcome not kept: the dead part's agent heard the word once, and nothing sends it again.
        Cuts.Written => new([Unrecorded], [], "s2", Waits: "s2"),

        // The outcome kept: delivered, the word taken off by the take-up (sent) or by the dead loop (taken).
        Cuts.Sent or Cuts.Taken => new([Delivered], [], "s2"),
        _ => throw new InvalidOperationException($"a boundary with no row: {cut}"),
    };

    public static TheoryData<string, bool> Boundaries
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var cut in Cuts.All.Where(cut => cut != Cuts.OldEnded))
            {
                data.Add(cut, true);
                data.Add(cut, false);
            }

            return data;
        }
    }

    /// <summary>
    /// 🔴 The census of the boundaries: a driven carry-on run to its end passes every boundary of <see cref="Cuts.All"/> but <c>old-ended</c>,
    /// once each and in the executor's order, its record standing at <c>old-ended</c> when the decision is told; so a boundary the course adds is
    /// reached by this proof or fails here, and each boundary passed has a row.
    /// </summary>
    [Fact]
    public async Task A_driven_carry_on_run_to_its_end_passes_every_boundary_but_old_ended_once_in_order_and_each_has_a_row()
    {
        await _h.SeedAsync(word: true);
        string? atDecision = null;
        await _h.KillAtAsync(cut: null, atTheCut: name =>
        {
            if (name == Cuts.Decided) atDecision = _h.MoveFrom("s1")?.PhaseWord;
        });

        Assert.Equal(Cuts.All.Where(cut => cut != Cuts.OldEnded), _h.Passed);
        Assert.Equal("old-ended", atDecision);
        Assert.Equal(("delivered", "driven", "retry"), (_h.MoveFrom("s1")!.PhaseWord, _h.MoveFrom("s1")!.Executor, _h.MoveFrom("s1")!.Why));
        Assert.Equal(1, _h.Carried("s2", DrivenMoveHarness.Word));
        foreach (var cut in _h.Passed) Assert.NotNull(Row(cut));
    }

    /// <summary>
    /// 🔴 A loop killed at each boundary, then one coming up: the move ends by its phase (§13.4), the word the open moved reaches an agent once
    /// where it is delivered and is never sent again where it is not, nothing is left standing, the move's key is never asked a second time, and
    /// the dead loop, let go, finds its claim gone and changes nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task A_loop_killed_at_a_boundary_comes_up_with_the_word_delivered_once_or_left_never_sent_twice(string cut, bool word)
    {
        var expect = Row(cut);
        await _h.SeedAsync(word);
        await _h.KillAtAsync(cut);
        var left = _h.MoveFrom("s1")!;
        await _h.KillAsync();

        _h.ComeUp();
        await _h.SettleAsync(ready: () => expect.First.SequenceEqual(_h.Shapes("s1")) && expect.Second.SequenceEqual(_h.Shapes("s2")));
        await _h.StopAsync();

        var seen = _h.Seen();
        Assert.True(expect.First.SequenceEqual(_h.Shapes("s1")), $"s1's moves {string.Join(" | ", _h.Shapes("s1"))}: {seen}");
        Assert.True(expect.Second.SequenceEqual(_h.Shapes("s2")), $"s2's moves {string.Join(" | ", _h.Shapes("s2"))}: {seen}");
        // Why: `retry` where the failed turn left words to carry, and `carry-on` where it left none: the record's own link either way.
        Assert.Equal(("driven", word ? HandOverWhy.Retry : HandOverWhy.CarryOn, "daoris"), (left.Executor, left.Why, left.By));
        Assert.All(_h.Store("run-probe").Of("s1"), move => Assert.False(move.Stands, seen));
        var taken = _h.Store("run-probe").Of("s1")[0];
        Assert.True(taken.ClaimGeneration > left.ClaimGeneration, "the come-up's take-up is a claim of its own");
        if (cut == Cuts.Decided) Assert.Equal(ConversationMoves.NotOpened, _h.Store("run-probe").Of("s1")[0].FellBackCode);

        // The word reaches an agent once, on the part that was written or carried on; nowhere else, and a part never hears it twice.
        var hearing = _h.Prompts().Where(prompt => prompt.Text.Contains(DrivenMoveHarness.Word, StringComparison.Ordinal)).ToList();
        if (word)
        {
            var heard = Assert.Single(hearing);
            Assert.Equal(expect.Hears, heard.Session);
            Assert.Equal(expect.Waits is null ? [] : [$"{expect.Waits}:w1"], _h.Waiting());
            Assert.Equal(expect.Waits is null ? 1 : 0, _h.Service.Taken.Count(taken => taken.Ids == "w1"));
        }
        else
        {
            Assert.Empty(hearing);
            Assert.Empty(_h.Waiting());
        }

        // The open is never asked again under the move's key, and none of the line's records is told the word was lost.
        var keys = _h.Service.Opened.Select(opened => opened.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        if (left.Opens is { } key) Assert.Equal(cut == Cuts.Opening ? 0 : 1, keys.Count(each => each == key));
        for (var at = 1; at <= _h.Service.SessionCount; at++)
        {
            Assert.DoesNotContain(_h.Events.After($"s{at}", 0).Events, e => e.Code == SessionEventCodes.Lost);
        }

        // The killed loop, let go, finds its claim gone and changes no move and writes no prompt.
        var snapshot = _h.SnapshotOf();
        await _h.ReleaseAsync();
        Assert.Equal(snapshot, _h.SnapshotOf());
    }

    /// <summary>
    /// 🔴 DRIVENATTEMPT1 (§13.5's <c>delivering</c> row, §19.4): a loop killed at <c>written</c> while the destination's quest is still open. The
    /// word was marked attempted at the hand, before the door's write, so the come-up reads it attempted and settles it uncertain, taking it
    /// back: the quest's own go-on, which carries a record's waiting words, never finds it to send to a part a second time, and no part hears it
    /// twice. Unmarked, the word waited on the destination for that go-on, which sent it again.
    /// </summary>
    [Fact]
    public async Task A_loop_killed_at_written_with_the_destinations_quest_open_marks_the_word_attempted_and_never_sends_it_again()
    {
        _h.Stays = true;
        await _h.SeedAsync(word: true);
        await _h.KillAtAsync(Cuts.Written);
        Assert.Contains("\"attempt\"", _h.Service.Record("s2"));
        await _h.KillAsync();
        Assert.Equal("Taken", _h.Service.Status("q1"));

        _h.Stays = false;
        _h.ComeUp();
        // Ready once the word waits nowhere: taken back where it was settled uncertain, or, where it was not marked, taken by the next part.
        await _h.SettleAsync(ready: () => _h.Waiting().Count == 0 && !_h.Store("run-probe").Of("s1").Any(move => move.Stands));
        await _h.StopAsync();

        var seen = _h.Seen();
        // The word is handed as the person's own to one part's prompt only: a later part's prompt names it in Daoris's record of the line, a
        // past exchange it is told not to act on, and never in the person's words it is handed.
        var handed = _h.Prompts().Where(prompt => prompt.Text.Contains("They added, after a session", StringComparison.Ordinal)).Select(prompt => prompt.Session).ToList();
        Assert.True(handed.SequenceEqual(["s2"]), $"the word was handed to {string.Join(", ", handed)}: {seen}");
        Assert.True(_h.Waiting().Count == 0, $"still waiting {string.Join(",", _h.Waiting())}: {seen}");
        Assert.All(_h.Store("run-probe").Of("s1"), move => Assert.False(move.Stands, seen));
        var move =_h.Store("run-probe").Of("s1").Single();
        Assert.Equal((MoveRecordPhase.Closed, MoveHoldWhy.Undelivered), (move.Phase, move.HeldWhy));
        Assert.Equal(MoveWordDispositions.Uncertain, Assert.Single(_h.Store("run-probe").WordsOf(move.Id)).Disposition);
        Assert.Contains(("s2", "w1"), _h.Service.TakenBack);

        // Said once, on the part the word was written to: the move's take-up writes the `lost` note for a moved word it settles uncertain
        // (O7-F4; the note an older come-up leaves out is §13.5's downgrade row, not this one).
        List<string> said = [];
        for (var at = 1; at <= _h.Service.SessionCount; at++)
        {
            said.AddRange(_h.Events.After($"s{at}", 0).Events.Where(e => e.Code == SessionEventCodes.Lost).Select(e => $"s{at}:{string.Join(",", e.Words ?? [])}"));
        }

        Assert.True(said.SequenceEqual(["s2:w1"]), $"lost notes {string.Join(" | ", said)}: {seen}");
    }

}
