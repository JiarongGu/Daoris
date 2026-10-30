using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-ii part b (D212; the foundation design §13.5's downgrade table, §19.4, row f-ii's proof): the live limit move across a downgrade.
/// A shell is killed at each boundary of the move (<see cref="Cuts"/>), then an older build comes up on the home, then this build returns.
/// Each moved word is delivered once, uncertain and said, or carried by its destination's go-on, never twice, whichever build acts, and this
/// build's return acts once.
/// </summary>
/// <remarks>
/// <para>An older build's come-up is the real chat runner whose move holder reads every run as alive and so takes nothing up, as a build before
/// the store does (<see cref="LiveLimitHarness.Runner"/>): what it does is the go-on's, which row f-ii did not change. It writes no <c>lost</c>
/// note for a moved word, since the destination's own event for the word is recorded under its id before the write (§13.5, <i>O8-F8</i>), which
/// the killed shell's door here records as the real door does. <b>What is left of the table's rows:</b> the older build's own statements are
/// this build's go-on's, so a difference an older build had in its go-on itself is not made here; the held note and the person's presses a
/// hold offers are rows of their own, and the hold is proved by what it names.</para>
/// <para>A phase added to <see cref="Cuts.All"/> and not given a row fails the census, as beside the crash theory.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LimitMoveDowngradeProcessTests : IAsyncLifetime
{
    private readonly LiveLimitHarness _h = new("limit-move-downgrade");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <param name="Phase">The move's phase after this build's return.</param>
    /// <param name="Held">The reason it was held, where it was.</param>
    /// <param name="Closed">The reason it closed, where it did.</param>
    /// <param name="Word">The disposition the return gave the word, where it read it.</param>
    /// <param name="OnOldPart">Whether the word still waits on the old part: the open never moved it.</param>
    /// <param name="Wrote">How many times the killed shell's door wrote the word to the new part.</param>
    /// <param name="Carried">How many times a destination's go-on carried it to the stub.</param>
    private sealed record After(string Phase, string? Held, string? Closed, string? Word, bool OnOldPart, int Wrote, int Carried);

    private static readonly Dictionary<string, After> Table = new(StringComparer.Ordinal)
    {
        // `decided`, `quiescing`, `old-ended`: the old part as it was; an older build opens nothing here, and this build's return goes back
        // to its old part (§13.5's first row). `opening` with no open made: the same, the move closed with nothing to name.
        [Cuts.Decided] = new("fell-back", null, null, null, OnOldPart: true, 0, 0),
        [Cuts.OldEnded] = new("fell-back", null, null, null, OnOldPart: true, 0, 0),
        [Cuts.Opening] = new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, OnOldPart: true, 0, 0),

        // The open made, its words waiting never attempted: the older build's go-on carries them once, to a successor of the destination,
        // so at the return the line's head is past the destination and the move closes without a word of its own to deliver.
        [Cuts.Answered] = new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, OnOldPart: false, 0, 1),
        [Cuts.Opened] = new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, MoveWordDispositions.Left, OnOldPart: false, 0, 1),
        [Cuts.Marked] = new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, MoveWordDispositions.Left, OnOldPart: false, 0, 1),

        // The write made and `sent` not written: the older build's come-up settles the word uncertain, taken back with no note; at the
        // return the word is gone without `sent`, so it is read left and the move is held for it, never delivered again.
        [Cuts.Written] = new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, MoveWordDispositions.Left, OnOldPart: false, 1, 0),

        // `sent` written: the older come-up's take-back changes nothing the move reads, and the return closes it delivered by `sent`.
        [Cuts.Sent] = new("delivered", null, null, MoveWordDispositions.Sent, OnOldPart: false, 1, 0),
        [Cuts.Taken] = new("delivered", null, null, MoveWordDispositions.Sent, OnOldPart: false, 1, 0),
    };

    public static TheoryData<string> Boundaries => [.. Cuts.All];

    /// <summary>🔴 The census beside the theory: every boundary has a row here too.</summary>
    [Fact]
    public void Every_boundary_the_executor_passes_has_a_downgrade_row()
    {
        Assert.Equal(Cuts.All.Order(StringComparer.Ordinal), Table.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// 🔴 A shell killed at each boundary, an older build's come-up, then this build's return, which acts once: each moved word is delivered
    /// once, or settled uncertain by the older build and read <i>left</i> or <i>taken</i> by this one, never twice, and the older build's
    /// come-up writes no <c>lost</c> note for a moved word its destination's event shows.
    /// </summary>
    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task A_downgrade_at_each_boundary_delivers_each_moved_word_once_and_the_returning_build_acts_once(string cut)
    {
        var expect = Table[cut];
        var shell = await _h.RunShellAsync(cut);
        var left = _h.MoveFrom("c1")!;
        _h.Kill();
        _h.Config();

        // The older build comes up: it takes no move up, and goes on as any ended record's words do.
        await _h.Runner(older: true).TakeUpAsync();
        if (expect.Carried > 0) await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= expect.Carried, _h.Seen);
        await Task.Delay(1000);
        var standing = _h.MoveFrom("c1")!;
        Assert.True(standing.Stands, $"an older build's come-up leaves the move standing under its dead run: {_h.Seen()}");
        Assert.Equal((left.PhaseWord, left.ClaimRun), (standing.PhaseWord, standing.ClaimRun));
        Assert.Equal(0, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));

        // This build returns: its come-up reads what the older one left, and acts once.
        await _h.Runner().TakeUpAsync();
        await Task.Delay(1500);
        var move = _h.MoveFrom("c1")!;
        var seen = _h.Seen();
        Assert.True(expect.Phase == move.PhaseWord, $"the move is {move.PhaseWord}, not {expect.Phase}: {seen}");
        Assert.Equal((expect.Held, expect.Closed), (move.HeldWhy, move.ClosedWhy));
        if (expect.Phase == "fell-back") Assert.Equal(ConversationMoves.NotOpened, move.FellBackCode);
        Assert.Equal(expect.Word, Store().WordsOf(move.Id).SingleOrDefault()?.Disposition);

        // Once: by the dead shell's write, or by the older build's go-on, never both and never again.
        Assert.Equal((expect.Wrote, expect.Carried), (LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word), _h.Heard(LiveLimitHarness.Word)));
        Assert.True(expect.Wrote + expect.Carried <= 1, "a word is delivered at most once");
        string[] waiting = expect.OnOldPart ? ["c1:k1"] : [];
        Assert.Equal(waiting, _h.Waiting());
        Assert.Equal(0, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));

        // A second come-up of this build finds nothing to do: the return acted once.
        var shape = Shape(move);
        await _h.Runner().TakeUpAsync();
        await Task.Delay(1000);
        Assert.Equal(shape, Shape(_h.MoveFrom("c1")!));
        Assert.Equal(expect.Wrote + expect.Carried, _h.Heard(LiveLimitHarness.Word) + LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word));
    }

    private MoveStore Store() => _h.Store("run-probe");

    private static string Shape(MoveRecord move) =>
        $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.ClaimRun}/{move.ClaimGeneration}";
}
