using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-v part b (D212; the foundation design §13.5's downgrade table, §19.4, row f-v's proof): a conversation's hand-over across a
/// downgrade, as <c>EndedLimitMoveDowngradeProcessTests</c> proves the ended limit move's. A shell is killed at each boundary of the hand-over
/// (<see cref="Cuts"/>, and <c>quiescing</c> while the old part's turn runs), then an older build comes up on the home and leaves its own
/// <c>&lt;s&gt;.hand-over.json</c> (the person's press, in the build that keeps a box), then this build returns. Its first come-up ends the
/// killed move by its phase with the box waiting behind it; the next imports the box once, as a record of its own, and sets it aside; a third
/// does nothing. The person's word is delivered once, whichever build acts, and the older build's come-up writes no <c>lost</c> note.
/// </summary>
/// <remarks>
/// An older build's come-up is the real chat runner whose move holder reads every run as alive and so takes nothing up, as a build before the store
/// does (<see cref="LiveLimitHarness.Runner"/>); what it does is the go-on's. A word the older build's go-on carries on the old part's record is
/// carried by a briefed part that continues it, so the return closes the move that stood <c>superseded</c> (§13.5's first row, the head read).
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HandOverMoveDowngradeProcessTests : IAsyncLifetime
{
    private readonly LiveLimitHarness _h = new("hand-over-move-downgrade");

    private const string Quiescing = "quiescing";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <param name="Phase">The killed move's phase after this build's first come-up.</param>
    /// <param name="Held">The reason it was held, where it was.</param>
    /// <param name="Closed">The reason it closed, where it did.</param>
    /// <param name="Older">Whether the older build's go-on carried the person's word on the old part, before the return.</param>
    /// <param name="Boxed">The box's own record after the next come-up: phase, held and closed.</param>
    private sealed record After(string Phase, string? Held, string? Closed, bool Older, string[] Boxed);

    private static readonly string[] Delivered = ["delivered", "", ""];

    /// <summary>The box's hand-over where the line went on past the old part: judged again as it is taken and refused, the move closed by its phase.</summary>
    private static readonly string[] Superseded = ["closed", "", MoveCloseWhy.Superseded];

    /// <summary>The row of a boundary, where the person's word was kept on the old part (<paramref name="word"/>) or none was.</summary>
    private static After Row(string cut, bool word) => (cut, word) switch
    {
        // Decided and waiting for a turn: the hand-over holds its record from the go-on, so the older build carries nothing and the return takes it.
        (Cuts.Decided or Quiescing, true) => new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, false, Superseded),
        (Cuts.Decided or Quiescing, false) => new("delivered", null, null, false, Superseded),

        // Old-ended: the older build's go-on carries the word on a part continuing the old one, which supersedes the move at the return.
        (Cuts.OldEnded, true) => new("closed", null, MoveCloseWhy.Superseded, true, Superseded),
        // Nothing opened and no word on a part continuing the old one: the old part is still the line's head, so the box's hand-over is taken as asked.
        (Cuts.OldEnded, false) => new("fell-back", null, null, false, Delivered),

        (Cuts.Opening, true) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, true, Superseded),
        (Cuts.Opening, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, false, Delivered),
        (Cuts.Answered, _) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, word, Superseded),
        (Cuts.Opened, true) => new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, true, Superseded),
        (Cuts.Marked, true) => new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, true, Superseded),
        (Cuts.Written, true) => new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, true, Superseded),
        (Cuts.Sent or Cuts.Taken, true) => new("closed", MoveHoldWhy.Superseded, MoveCloseWhy.Resolved, true, Superseded),
        (Cuts.Opened, false) => new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, false, Superseded),
        (Cuts.Marked, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, false, Superseded),
        (Cuts.Written, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, false, Superseded),
        (Cuts.Sent or Cuts.Taken, false) => new("delivered", null, null, false, Superseded),
        _ => throw new InvalidOperationException($"a boundary with no row: {cut}"),
    };

    public static TheoryData<string, bool> Boundaries
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var cut in Cuts.All.Append(Quiescing))
            {
                data.Add(cut, true);
                data.Add(cut, false);
            }

            return data;
        }
    }

    /// <summary>🔴 The census beside the theory: every boundary has a downgrade row, with a word kept and with none.</summary>
    [Fact]
    public void Every_boundary_the_hand_over_passes_has_a_downgrade_row()
    {
        foreach (var cut in Cuts.All.Append(Quiescing))
        {
            Assert.NotNull(Row(cut, word: true));
            Assert.NotNull(Row(cut, word: false));
        }
    }

    private string BoxPath => new HandOverBox(_h.Home).PathOf("c1");

    private MoveStore Store() => _h.Store("run-probe");

    private static string[] Ended(MoveRecord move) => [move.PhaseWord, move.HeldWhy ?? "", move.ClosedWhy ?? ""];

    /// <summary>
    /// 🔴 A shell killed at each boundary, an older build's come-up that takes nothing up and leaves its own box, then this build's return: the
    /// first come-up ends the killed move by its phase with the box still in place behind it, the second imports the box once, as a record of its
    /// own ended by its phase, and sets it aside, and a third does nothing; the person's word is delivered once and nothing is written <c>lost</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task A_downgrade_at_each_boundary_delivers_once_and_the_older_builds_box_is_imported_once_at_the_return(string cut, bool word)
    {
        var expect = Row(cut, word);
        var shell = await _h.RunHandOverShellAsync(
            cut == Quiescing ? null : cut, keep: word ? null : new Dictionary<string, string>(), turn: cut == Quiescing);
        var left = _h.MoveFrom("c1")!;
        _h.Kill();
        _h.Config();

        // The older build comes up: it takes no move up, and goes on as any ended record's words do; then it leaves its own box.
        await _h.Runner(older: true).TakeUpAsync();
        if (expect.Older) await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= 1, _h.Seen);
        await Task.Delay(1000);
        var standing = _h.MoveFrom("c1")!;
        Assert.True(standing.Stands, $"an older build's come-up leaves the move standing under its dead run: {_h.Seen()}");
        Assert.Equal((left.PhaseWord, left.ClaimRun), (standing.PhaseWord, standing.ClaimRun));
        Assert.Equal(0, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));
        new HandOverBox(_h.Home).Write("c1", new HandOverRequest("acp-stub", "account-2", HandOverWhy.Press, HandOverBy.Person, DateTimeOffset.UtcNow));

        // This build returns: its come-up ends the killed move by its phase, the box waiting behind it, still in place.
        var runner = _h.Runner();
        await runner.TakeUpAsync();
        await Task.Delay(1500);
        var move = Store().Of("c1").First();
        var seen = _h.Seen();
        Assert.True(expect.Phase == move.PhaseWord, $"the move is {move.PhaseWord}, not {expect.Phase}: {seen}");
        Assert.Equal((expect.Held, expect.Closed), (move.HeldWhy, move.ClosedWhy));
        if (expect.Phase == "fell-back") Assert.Equal(ConversationMoves.NotOpened, move.FellBackCode);
        Assert.True(File.Exists(BoxPath), "the box waits behind the move that stood");

        // The next come-up imports the box once, as the record it keeps, and sets it aside, never deleted.
        await runner.TakeUpAsync();
        await Task.Delay(1500);
        var moves = Store().Of("c1");
        Assert.True(expect.Boxed.SequenceEqual(Ended(moves[^1])), $"the box's move is {string.Join("/", Ended(moves[^1]))}, {moves.Count} moves: {_h.Seen()}");
        Assert.Equal(Ended(move), Ended(moves[0]));
        Assert.Equal((HandOverWhy.Press, HandOverBy.Person, StoreProcess.Run), (moves[^1].Why, moves[^1].By, moves[^1].ClaimRun));
        Assert.False(File.Exists(BoxPath));
        Assert.True(File.Exists(BoxPath + ".imported"));
        Assert.Equal(2, moves.Count);

        // A part this shell holds live keeps the word waiting for its next go-on: the shell closes it and comes up again, which carries it once.
        if (_h.Waiting().SingleOrDefault() is { } waiting && waiting.Split(':')[0] is var holder && _h.Ledger.State(holder) == "working")
        {
            Assert.True(runner.Finish(holder), _h.Seen());
            await Poll.Until(() => _h.Ledger.State(holder) == "completed", _h.Seen);
            await runner.TakeUpAsync();
        }

        if (word) await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= 1, _h.Seen);
        await Poll.Until(() => _h.Waiting().Count == 0, _h.Seen);
        await Task.Delay(1000);
        Assert.Equal(word ? 1 : 0, _h.Heard(LiveLimitHarness.Word));
        Assert.Equal(0, LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word));
        Assert.Equal(0, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));

        // A third come-up finds nothing to do: the box was imported once, and the return acted once.
        var shape = string.Join(" | ", Store().Of("c1").Select(Shape));
        var heard = (_h.Heard(LiveLimitHarness.Word), _h.Ledger.Opened.Count);
        await _h.Runner().TakeUpAsync();
        await Task.Delay(1000);
        Assert.Equal(shape, string.Join(" | ", Store().Of("c1").Select(Shape)));
        Assert.Equal(heard, (_h.Heard(LiveLimitHarness.Word), _h.Ledger.Opened.Count));
    }

    private static string Shape(MoveRecord move) =>
        $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.ClaimRun}/{move.ClaimGeneration}";
}
