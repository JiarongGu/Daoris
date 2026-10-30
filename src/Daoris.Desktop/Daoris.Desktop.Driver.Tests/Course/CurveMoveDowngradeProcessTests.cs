using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-iii part b (D212; the foundation design §13.5's downgrade table, §19.4, row f-iii's proof): the curve's move across a downgrade,
/// as <c>LimitMoveDowngradeProcessTests</c> proves the live limit move's. A shell is killed at each boundary of the move (<see cref="Cuts"/>),
/// then an older build comes up on the home, then this build returns. The person's word the open moved is carried once, by whichever build's
/// go-on reaches it first, and never again; this build's return acts once.
/// </summary>
/// <remarks>
/// <para>An older build's come-up is the real chat runner whose move holder reads every run as alive and so takes nothing up, as a build before
/// the store does (<see cref="LiveLimitHarness.Runner"/>): what it does is the go-on's, which row f-iii did not change. The curve's brief is
/// written by the killed shell's door at <c>written</c> and after, never again by either build.</para>
/// <para>A phase added to <see cref="Cuts.All"/> and not given a row fails the census, as beside the crash theory.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class CurveMoveDowngradeProcessTests : IAsyncLifetime
{
    private readonly LiveLimitHarness _h = new("curve-move-downgrade");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <param name="Phase">The move's phase after this build's return.</param>
    /// <param name="Held">The reason it was held, where it was.</param>
    /// <param name="Closed">The reason it closed, where it did.</param>
    /// <param name="Word">The disposition the return gave the moved word, where it read it.</param>
    /// <param name="Briefed">How many times the killed shell's door wrote the brief to the new part.</param>
    private sealed record After(string Phase, string? Held, string? Closed, string? Word, int Briefed);

    /// <summary>The row of a boundary, where the person's word was kept on the old part (<paramref name="word"/>) or none was.</summary>
    private static After Row(string cut, bool word) => (cut, word) switch
    {
        // Before the open: the old part as it was. With a word kept on it, an older build's go-on carries it on a record that continues the old
        // part, which no key of the move's made, so this build's return closes the move `superseded` (§13.5's first row, the head read); with
        // none the old part is as it was and the return falls back.
        (Cuts.Decided or Cuts.OldEnded, true) => new("closed", null, MoveCloseWhy.Superseded, null, 0),
        (Cuts.Decided or Cuts.OldEnded, false) => new("fell-back", null, null, null, 0),
        (Cuts.Opening, _) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, 0),
        (Cuts.Answered, _) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, 0),

        // The destination's word carried by the older build's go-on to a successor of the destination: at the return the line's head is past the
        // destination and the word is gone without `sent`, so it is read left and the move closes held `superseded`; with no word moved, the older
        // build has nothing to carry and the return reads what this build's own come-up reads.
        (Cuts.Opened, true) => new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, MoveWordDispositions.Left, 0),
        (Cuts.Marked, true) => new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, MoveWordDispositions.Left, 0),
        (Cuts.Written, true) => new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, MoveWordDispositions.Left, 1),
        (Cuts.Sent or Cuts.Taken, true) => new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, MoveWordDispositions.Left, 1),
        (Cuts.Opened, false) => new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, null, 0),
        (Cuts.Marked, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, 0),
        (Cuts.Written, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, null, 1),
        (Cuts.Sent or Cuts.Taken, false) => new("delivered", null, null, null, 1),
        _ => throw new InvalidOperationException($"a boundary with no row: {cut}"),
    };

    public static TheoryData<string, bool> Boundaries
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var cut in Cuts.All)
            {
                data.Add(cut, true);
                data.Add(cut, false);
            }

            return data;
        }
    }

    /// <summary>🔴 The census beside the theory: every boundary has a row here too, with a word kept and with none.</summary>
    [Fact]
    public void Every_boundary_the_executor_passes_has_a_downgrade_row()
    {
        foreach (var cut in Cuts.All)
        {
            Assert.NotNull(Row(cut, word: true));
            Assert.NotNull(Row(cut, word: false));
        }
    }

    /// <summary>
    /// 🔴 A shell killed at each boundary, an older build's come-up, then this build's return, which acts once: the person's word is carried once
    /// by a go-on and the brief written once by the dead shell or never, whichever build acts, and the older build's come-up writes no
    /// <c>lost</c> note.
    /// </summary>
    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task A_downgrade_at_each_boundary_carries_the_word_once_and_the_returning_build_acts_once(string cut, bool word)
    {
        var expect = Row(cut, word);
        var shell = await _h.RunCurveShellAsync(cut, keep: word ? null : new Dictionary<string, string>());
        var left = _h.MoveFrom("c1")!;
        _h.Kill();
        _h.Config();

        // The older build comes up: it takes no move up, and goes on as any ended record's words do.
        await _h.Runner(older: true).TakeUpAsync();
        if (word) await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= 1, _h.Seen);
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

        // Once: the word by a go-on, the brief by the dead shell or never; the move writes neither again.
        Assert.Equal((expect.Briefed, word ? 1 : 0), (_h.Briefed(shell), _h.Heard(LiveLimitHarness.Word)));
        Assert.Equal(0, LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word));
        Assert.Empty(_h.Waiting());
        Assert.Equal(0, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));

        // A second come-up of this build finds nothing to do: the return acted once.
        var shape = Shape(move);
        await _h.Runner().TakeUpAsync();
        await Task.Delay(1000);
        Assert.Equal(shape, Shape(_h.MoveFrom("c1")!));
        Assert.Equal((expect.Briefed, word ? 1 : 0), (_h.Briefed(shell), _h.Heard(LiveLimitHarness.Word)));
    }

    private MoveStore Store() => _h.Store("run-probe");

    private static string Shape(MoveRecord move) =>
        $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.ClaimRun}/{move.ClaimGeneration}";
}
