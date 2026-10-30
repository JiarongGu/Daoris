using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-vi part b (D212; the foundation design §13.5's downgrade table, §19.4, row f-vi's proof): the ended limit move across a
/// downgrade, as <c>LimitMoveDowngradeProcessTests</c> proves the live move's, and the box an older build writes imported once at the return. A
/// shell is killed at each boundary of the move, an older build comes up on the home and writes its own box, then this build returns: its
/// come-up ends the killed move by its phase, a later one imports the box into a record of its own and sets it aside, and a third finds nothing
/// to do. Each word is delivered once, uncertain and said, or carried by a go-on, never twice, whichever build acts.
/// </summary>
/// <remarks>
/// <para>An older build's come-up is the real chat runner whose move holder reads every run as alive and so takes nothing up, as a build before
/// the store does (<see cref="LiveLimitHarness.Runner"/>): what it does is the go-on's, which row f-vi did not change. The box it writes is
/// the ended move's own, <see cref="LimitBoxes.Write"/> left by the test as the older build wrote it: with no destination yet in the ledger it
/// names none, as before its open; with one it names it, where the older build's open moved the word (<c>moved</c>) or, with none kept, moved
/// nothing.</para>
/// <para><b>Why the import is the second come-up.</b> The killed move still stands under its dead run when the return comes up, and a box waits
/// behind a standing move (there are never two records of one move, §3.3), so the first come-up ends the move and the next imports the box.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class EndedLimitMoveDowngradeProcessTests : IAsyncLifetime
{
    private readonly LiveLimitHarness _h = new("ended-limit-move-downgrade");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <param name="Phase">The killed move's phase after this build's return.</param>
    /// <param name="Held">The reason it was held, where it was.</param>
    /// <param name="Closed">The reason it closed, where it did.</param>
    /// <param name="Word">The disposition the return gave the moved word, where it read it.</param>
    /// <param name="Boxed">The phase, hold and close of the record the older build's box was imported as.</param>
    /// <param name="OnOldPart">Whether the word still waits on the old record: the open never moved it.</param>
    /// <param name="Wrote">How many messages the killed shell's door wrote to the new part.</param>
    /// <param name="Carried">How many times an older build's go-on carried the word to the stub.</param>
    private sealed record After(string Phase, string? Held, string? Closed, string? Word, string[] Boxed, bool OnOldPart, int Wrote, int Carried);

    private static readonly string[] BoxedFellBack = ["fell-back", "", ""];

    private static readonly string[] BoxedUndelivered = ["closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved];

    private static readonly string[] BoxedSuperseded = ["closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved];

    /// <summary>The row of a boundary, where the person's word waited on the record (<paramref name="word"/>) or none did.</summary>
    private static After Row(string cut, bool word) => (cut, word) switch
    {
        // Before the open: the old part as it was, an older build opening nothing here. The box names no destination, so it is imported at
        // `old-ended` and falls back, `not-opened`, as the killed move does.
        (Cuts.Decided, _) => new("fell-back", null, null, null, BoxedFellBack, OnOldPart: word, 0, 0),
        (Cuts.Opening, _) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, BoxedFellBack, OnOldPart: word, 0, 0),

        // The open made, its answer lost: the older build's go-on carries a moved word, once, to a successor of the destination; the box that
        // names the destination is imported at `opened`, the word gone from it, so it is superseded, or with no word undelivered.
        (Cuts.Answered, true) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, BoxedSuperseded, OnOldPart: false, 0, 1),
        (Cuts.Answered, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, BoxedUndelivered, OnOldPart: false, 0, 0),

        // The destination's word carried by the older build's go-on to a successor of the destination: at the return the line's head is past
        // the destination and the word is gone without `sent`, so it is read left and the move closes held `superseded`.
        (Cuts.Opened or Cuts.Marked, true) => new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, MoveWordDispositions.Left, BoxedSuperseded, OnOldPart: false, 0, 1),
        (Cuts.Opened, false) => new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, null, BoxedUndelivered, OnOldPart: false, 0, 0),
        (Cuts.Marked, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, BoxedUndelivered, OnOldPart: false, 0, 0),

        // The write made and `sent` not written: the older build's come-up settles an attempted word uncertain with no note of its own; at the
        // return it is read left and the move held for it, never delivered again. With no word, an effect never recorded.
        (Cuts.Written, true) => new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, MoveWordDispositions.Left, BoxedUndelivered, OnOldPart: false, 1, 0),
        (Cuts.Written, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, BoxedUndelivered, OnOldPart: false, 1, 0),

        // `sent` written: the move is delivered by it, whether its take was made or not.
        (Cuts.Sent or Cuts.Taken, true) => new("delivered", null, null, MoveWordDispositions.Sent, BoxedUndelivered, OnOldPart: false, 1, 0),
        (Cuts.Sent or Cuts.Taken, false) => new("delivered", null, null, null, BoxedUndelivered, OnOldPart: false, 1, 0),
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

    /// <summary>🔴 The census beside the theory: each of the eight boundaries the ended move passes has a downgrade row, with a word and with none.</summary>
    [Fact]
    public void Every_boundary_the_ended_move_passes_has_a_downgrade_row()
    {
        foreach (var cut in Cuts.All.Where(cut => cut != Cuts.OldEnded))
        {
            Assert.NotNull(Row(cut, word: true));
            Assert.NotNull(Row(cut, word: false));
        }
    }

    private LimitMoveBox OlderBox(bool destination, bool word) => destination
        ? new LimitMoveBox(HandOverWhy.Limit, Before, _h.Events.After("c1", 0).Events.Count, word ? ["k1"] : [], "c2", Moved: word)
        : new LimitMoveBox(HandOverWhy.Limit, Before, _h.Events.After("c1", 0).Events.Count, word ? ["k1"] : []);

    /// <summary>How many of the record's events stand before the first refused turn's words.</summary>
    private const int Before = 2;

    private string BoxPath => new LimitBoxes(_h.Home).PathOf("c1");

    private MoveStore Store() => _h.Store("run-probe");

    private static string[] Ended(MoveRecord move) => [move.PhaseWord, move.HeldWhy ?? "", move.ClosedWhy ?? ""];

    /// <summary>
    /// 🔴 A shell killed at each boundary, an older build's come-up that takes nothing up and writes its own box, then this build's return: the
    /// first come-up ends the killed move by its phase with the box still in place behind it, the second imports the box once, as a record of
    /// its own ended by its phase, and sets it aside, and a third does nothing; each word is delivered once, whichever build acts, and the
    /// older build's come-up writes no <c>lost</c> note.
    /// </summary>
    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task A_downgrade_at_each_boundary_delivers_once_and_the_older_builds_box_is_imported_once_at_the_return(string cut, bool word)
    {
        var expect = Row(cut, word);
        var shell = await _h.RunEndedShellAsync(cut, keep: word ? null : new Dictionary<string, string>());
        var left = _h.MoveFrom("c1")!;
        _h.Kill();
        _h.Config();

        // The older build comes up: it takes no move up, and goes on as any ended record's words do; then it leaves its own box.
        await _h.Runner(older: true).TakeUpAsync();
        if (expect.Carried > 0) await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= expect.Carried, _h.Seen);
        await Task.Delay(1000);
        var standing = _h.MoveFrom("c1")!;
        Assert.True(standing.Stands, $"an older build's come-up leaves the move standing under its dead run: {_h.Seen()}");
        Assert.Equal((left.PhaseWord, left.ClaimRun), (standing.PhaseWord, standing.ClaimRun));
        Assert.Equal(0, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));
        new LimitBoxes(_h.Home).Write("c1", OlderBox(destination: _h.Ledger.Count >= 2, word));

        // This build returns: its come-up ends the killed move by its phase, the box waiting behind it, still in place.
        await _h.Runner().TakeUpAsync();
        await Task.Delay(1500);
        var move = Store().Of("c1").Single();
        var seen = _h.Seen();
        Assert.True(expect.Phase == move.PhaseWord, $"the move is {move.PhaseWord}, not {expect.Phase}: {seen}");
        Assert.Equal((expect.Held, expect.Closed), (move.HeldWhy, move.ClosedWhy));
        if (expect.Phase == "fell-back") Assert.Equal(ConversationMoves.NotOpened, move.FellBackCode);
        Assert.Equal(expect.Word, Store().WordsOf(move.Id).SingleOrDefault()?.Disposition);
        Assert.True(File.Exists(BoxPath), "the box waits behind the move that stood");

        // The next come-up imports the box once, as the record it keeps, ended by its phase, and sets it aside, never deleted.
        await _h.Runner().TakeUpAsync();
        await Task.Delay(1500);
        var moves = Store().Of("c1");
        Assert.Equal(2, moves.Count);
        Assert.Equal(expect.Boxed, Ended(moves[1]));
        Assert.Equal(Ended(move), Ended(moves[0]));
        Assert.Equal(("limit", "daoris", StoreProcess.Run), (moves[1].Why, moves[1].By, moves[1].ClaimRun));
        if (expect.Boxed[0] == "fell-back") Assert.Equal(ConversationMoves.NotOpened, moves[1].FellBackCode);
        Assert.False(File.Exists(BoxPath));
        Assert.True(File.Exists(BoxPath + ".imported"));

        // Once: the word by the dead shell's write or by the older build's go-on, never both, never again, and no `lost` note.
        Assert.Equal(expect.Wrote, _h.Briefed(shell));
        Assert.Equal(word ? (expect.Wrote, expect.Carried) : (0, 0), (LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word), _h.Heard(LiveLimitHarness.Word)));
        Assert.True(LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word) + _h.Heard(LiveLimitHarness.Word) <= 1, "a word is delivered at most once");
        string[] waiting = expect.OnOldPart ? ["c1:k1"] : [];
        Assert.Equal(waiting, _h.Waiting());
        Assert.Equal(0, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));

        // A third come-up finds nothing to do: the box was imported once, and the return acted once.
        var shape = string.Join(" | ", Store().Of("c1").Select(Shape));
        await _h.Runner().TakeUpAsync();
        await Task.Delay(1000);
        Assert.Equal(shape, string.Join(" | ", Store().Of("c1").Select(Shape)));
        Assert.Equal((expect.Wrote, word ? expect.Wrote + expect.Carried : 0), (_h.Briefed(shell), _h.Heard(LiveLimitHarness.Word) + LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word)));
    }

    /// <summary>
    /// 🔴 The box an older build wrote at each phase of its own move, imported at this build's return and nothing else on the home: with no
    /// destination it is imported at <c>old-ended</c> and falls back, the word where it was; naming one whose open moved the word, at
    /// <c>opened</c>, the word never attempted left on the new part for its own go-on, which carries it once, or attempted without a
    /// <c>sent</c>, settled uncertain with the <c>lost</c> note and taken back, never sent again; naming one whose open moved nothing, with no word.
    /// Each is set aside once and nothing is delivered from the box.
    /// </summary>
    [Theory]
    [InlineData("old-ended")]
    [InlineData("moved-never-attempted")]
    [InlineData("moved-attempted")]
    [InlineData("moved-nothing")]
    public async Task An_older_builds_box_at_each_phase_is_imported_once_at_the_return_and_set_aside(string phase)
    {
        _h.LeaveEnded(phase == "moved-nothing" ? new Dictionary<string, string>() : null);
        if (phase != "old-ended")
        {
            // The older build's own open, which moved the word to its new part: it waits there, in the order the older build left it.
            var next = _h.Ledger.Chat("c2", "stopped", "acp-stub", _h.Root, profile: "account-2", from: new ContinuedFrom("c1", HandOverWhy.Limit, HandOverBy.Daoris));
            if (phase != "moved-nothing") next.Say("k1", LiveLimitHarness.Word);
            _h.Ledger.Edit("c1", record => record["said"] = new JsonArray());
            if (phase == "moved-attempted") _h.Ledger.Edit("c2", record => { foreach (var word in record["said"]!.AsArray()) word!["attempt"] = "2026-10-03T09:59:30Z"; });
        }

        var box = phase switch
        {
            "old-ended" => new LimitMoveBox(HandOverWhy.Limit, Before, _h.Events.After("c1", 0).Events.Count, ["k1"]),
            "moved-nothing" => new LimitMoveBox(HandOverWhy.Limit, Before, _h.Events.After("c1", 0).Events.Count, [], "c2"),
            _ => new LimitMoveBox(HandOverWhy.Limit, Before, _h.Events.After("c1", 0).Events.Count, ["k1"], "c2", Moved: true),
        };
        new LimitBoxes(_h.Home).Write("c1", box);

        await _h.Runner().TakeUpAsync();
        await Task.Delay(1500);
        var seen = _h.Seen();
        var move = Assert.Single(Store().Of("c1"));
        var (ends, carried, lost) = phase switch
        {
            // Never attempted: left on the new part, whose own go-on carries it, once.
            "moved-never-attempted" => (BoxedUndelivered, 1, 0),
            // Attempted and never sent: uncertain, said and taken back, never carried again.
            "moved-attempted" => (BoxedUndelivered, 0, 1),
            "moved-nothing" => (BoxedUndelivered, 0, 0),
            _ => (BoxedFellBack, 0, 0),
        };
        Assert.True(ends.SequenceEqual(Ended(move)), $"the imported move is {string.Join("/", Ended(move))}: {seen}");
        Assert.Equal(("limit", "daoris", StoreProcess.Run), (move.Why, move.By, move.ClaimRun));
        if (phase == "old-ended") Assert.Equal(ConversationMoves.NotOpened, move.FellBackCode);
        Assert.False(File.Exists(BoxPath));
        Assert.True(File.Exists(BoxPath + ".imported"));
        Assert.Equal(carried, _h.Heard(LiveLimitHarness.Word));
        Assert.Equal(lost, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));
        Assert.Equal(phase == "old-ended" ? ["c1:k1"] : [], _h.Waiting());

        // Once: a second come-up of this build imports nothing and delivers nothing more.
        var shape = Shape(move);
        await _h.Runner().TakeUpAsync();
        await Task.Delay(1000);
        Assert.Equal(shape, Shape(Assert.Single(Store().Of("c1"))));
        Assert.Equal(carried, _h.Heard(LiveLimitHarness.Word));
    }

    private static string Shape(MoveRecord move) =>
        $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.ClaimRun}/{move.ClaimGeneration}";
}
