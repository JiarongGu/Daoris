using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-v part b (D212; the foundation design §3.3, §13.4 steps 1 to 5, §13.5, §19.4, row f-v's proof): a conversation's hand-over keeps its
/// move record through a crash at every boundary, as <c>CurveMoveCrashProcessTests</c> proves the curve's. A shell is killed at each place the hand-over
/// passes (<see cref="Cuts"/>, and <c>quiescing</c> where the old part's turn runs), and a shell that comes up takes its move up and ends it by its
/// phase; the person's word is then carried once, by the go-on of the part it waits on, and the brief written once, never twice.
/// </summary>
/// <remarks>
/// <para><b>What differs from the curve's rows.</b> The hand-over passes the curve's nine boundaries, its one message the brief, but a hand-over that
/// stands at <c>decided</c> or <c>quiescing</c> under a dead run is <i>taken</i> at the come-up, not fallen back (§13.5's first row, part a): the
/// coming-up shell's real runner opens the new part itself, briefs it once, and the move closes by its own take. At <c>old-ended</c> it falls back
/// as the limit move's does, its word carried by the old part's go-on.</para>
/// <para>Each boundary is run with the person's word kept on the old part and with none, since a move that moved nothing is read by its prompt's
/// outcome alone and closes <c>delivered</c> where one that moved a word closes held <c>undelivered</c> naming nothing. A word carried by the go-on of
/// a part the dead shell made, or of the old part, is carried by a briefed part (BriefedMove), the stub keeping no conversation of that part, and
/// counts the one more brief and open; one the coming-up shell's own take opened is resumed, and counts none.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HandOverMoveCrashProcessTests : IAsyncLifetime
{
    private readonly LiveLimitHarness _h = new("hand-over-move-crash");

    /// <summary>The wait for the old part's turn, where the ask stood at <c>quiescing</c>: no boundary of <see cref="Cuts.All"/>.</summary>
    private const string Quiescing = "quiescing";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <summary>What a killed shell's hand-over comes to once a shell comes up.</summary>
    /// <param name="Phase">The move's phase after the come-up.</param>
    /// <param name="Held">The reason it was held, where it was.</param>
    /// <param name="Closed">The reason it closed, where it did.</param>
    /// <param name="Account">The account of the part whose go-on carries the word: the old part's, or the new part's.</param>
    /// <param name="Briefed">How many times the dead shell's door wrote the brief to the new part.</param>
    private sealed record Ends(string Phase, string? Held, string? Closed, string Account, int Briefed);

    /// <summary>Every brief the protocol stub is told begins so, whichever part's.</summary>
    private const string BriefStart = "You continue session `";

    /// <summary>Whether the coming-up shell's own take opens the new part and briefs it: a hand-over stood at <c>decided</c> or <c>quiescing</c>.</summary>
    private static bool Retaken(string cut) => cut is Cuts.Decided or Quiescing;

    /// <summary>Whether the killed shell's open was made, the new part standing in the ledger when it was killed.</summary>
    private static bool Opened(string cut) => cut is Cuts.Answered or Cuts.Opened or Cuts.Marked or Cuts.Written or Cuts.Sent or Cuts.Taken;

    /// <summary>The row of a boundary, where the person's word was kept on the old part (<paramref name="word"/>) or none was.</summary>
    private static Ends Row(string cut, bool word) => cut switch
    {
        // A hand-over decided, or waiting for a turn, under a dead run is taken at the come-up: the new shell opens and briefs the new part once.
        Cuts.Decided or Quiescing => word
            ? new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, "account-2", 0)
            : new("delivered", null, null, "account-2", 0),

        // The old part ended and no open asked: the line stays on its old part, the word with it, and the old part's own come-up carries it.
        Cuts.OldEnded => new("fell-back", null, null, "account-1", 0),

        // The open asked and never made: held `unrecorded` and closed, nothing opened.
        Cuts.Opening => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, "account-1", 0),

        // The open made and its answer lost, or kept and nothing handed: the word the open moved is carried by the destination's go-on.
        Cuts.Answered => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, "account-2", 0),
        Cuts.Opened => new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, "account-2", 0),

        // The prompt marked, or written with its outcome not kept: an effect never recorded, never made again by itself.
        Cuts.Marked => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, "account-2", 0),
        Cuts.Written => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, "account-2", 1),

        // The prompt's outcome kept: a move that moved nothing is delivered by it; one that moved a word closes held `undelivered` naming nothing.
        Cuts.Sent or Cuts.Taken => word
            ? new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, "account-2", 1)
            : new("delivered", null, null, "account-2", 1),
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

    /// <summary>
    /// 🔴 The census of the boundaries: a hand-over run to its end passes every boundary of <see cref="Cuts.All"/>, once each and in order, so a
    /// boundary the course adds is reached by this proof or fails here; each has a row in the theory beside, with a word and with none. One asked
    /// while the old part's turn runs stands at <c>quiescing</c> and passes none, and is a row of its own.
    /// </summary>
    [Fact]
    public async Task A_hand_over_run_to_its_end_passes_every_boundary_once_in_order_and_each_has_a_row()
    {
        var passed = new List<string>();
        var shell = await _h.RunHandOverShellAsync(cut: null, atTheCut: name => { lock (passed) passed.Add(name); });
        await Poll.Until(() => { lock (passed) return passed.Count >= Cuts.All.Count; }, () => $"passed [{string.Join(",", passed)}], {_h.Seen()}");
        await Task.Delay(300);

        lock (passed) Assert.Equal(Cuts.All, passed);
        Assert.Single(shell.Runner.Opened!.Prompts);
        foreach (var cut in Cuts.All.Append(Quiescing))
        {
            Assert.NotNull(Row(cut, word: true));
            Assert.NotNull(Row(cut, word: false));
        }
    }

    /// <summary>
    /// 🔴 A shell killed at each boundary, then one coming up: the move ends by its phase (§13.4, §13.5), the person's word is carried once by the
    /// go-on of the part it waits on, the brief is written once, by the dead shell or by the shell that took the move up, and never twice.
    /// </summary>
    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task A_shell_killed_at_a_boundary_comes_up_with_the_word_carried_once_and_the_brief_written_once(string cut, bool word)
    {
        var expect = Row(cut, word);
        var shell = await _h.RunHandOverShellAsync(
            cut == Quiescing ? null : cut, keep: word ? null : new Dictionary<string, string>(), turn: cut == Quiescing);
        var left = _h.MoveFrom("c1")!;
        if (cut == Quiescing) Assert.Equal("quiescing", left.PhaseWord);
        _h.Kill();
        _h.Config();

        var runner = _h.Runner();
        await runner.TakeUpAsync();

        // The word is carried once, by the go-on of the part it waits on: the new part's where the open moved it, else the old part's own.
        var carried = word ? 1 : 0;
        if (word && Retaken(cut))
        {
            // The take opened the new part, which this shell holds live: the word the open moved waits there for the loop's words to say it, which
            // no loop does here, and is not sent twice meanwhile. It goes on at the new part's next go-on, as its shell is closed and comes up again.
            await Poll.Until(() => _h.Waiting() is ["c2:k1"], _h.Seen);
            Assert.Equal(0, _h.Heard(LiveLimitHarness.Word));
            Assert.True(runner.Finish("c2"), _h.Seen());
            await Poll.Until(() => _h.Ledger.State("c2") == "completed", _h.Seen);
            await runner.TakeUpAsync();
        }

        if (word) await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= 1, _h.Seen);
        await Poll.Until(() => _h.Waiting().Count == 0, _h.Seen);

        // Nothing more is sent after the come-up settles: a second delivery would be heard here.
        await Task.Delay(1500);
        var move = _h.MoveFrom("c1")!;
        var seen = _h.Seen();
        Assert.True(expect.Phase == move.PhaseWord, $"the move is {move.PhaseWord}, not {expect.Phase}: {seen}");
        Assert.Equal((expect.Held, expect.Closed), (move.HeldWhy, move.ClosedWhy));
        if (expect.Phase == "fell-back") Assert.Equal(ConversationMoves.NotOpened, move.FellBackCode);
        Assert.Equal((HandOverWhy.Near, HandOverBy.Daoris, StoreProcess.Run), (move.Why, move.By, move.ClaimRun));
        Assert.True(move.ClaimGeneration > left.ClaimGeneration, "the come-up's take-up is a claim of its own");
        Assert.Single(_h.Store("run-probe").Of("c1"));

        // The word once, by its part's go-on; the brief by the dead shell, or by the shell that took the move up (one open and one brief each), never both.
        // A new part the take opened and this shell holds is resumed by its go-on, which opens nothing; one the stub kept no conversation for is briefed.
        var briefs = Retaken(cut) ? 1 : carried;
        var opens = Retaken(cut) ? 1 : (Opened(cut) ? 1 : 0) + carried;
        var heard = (_h.Briefed(shell), _h.Heard(BriefStart), _h.Heard(LiveLimitHarness.Word), _h.Ledger.Opened.Count);
        Assert.True((expect.Briefed, briefs, carried, opens) == heard, $"briefed, told, carried, opens: expected {(expect.Briefed, briefs, carried, opens)}, got {heard}: {seen}");
        Assert.Equal(0, LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word));
        Assert.Equal(carried, _h.HeardBy(LiveLimitHarness.Word, expect.Account));
        Assert.Empty(_h.Waiting());
        Assert.Equal(0, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));

        // The killed shell, let go, finds its claim gone and changes nothing.
        var snapshot = Shape(move);
        await _h.ReleaseAsync();
        Assert.Equal(snapshot, Shape(_h.MoveFrom("c1")!));
        Assert.Equal(heard, (_h.Briefed(shell), _h.Heard(BriefStart), _h.Heard(LiveLimitHarness.Word), _h.Ledger.Opened.Count));
    }

    private static string Shape(MoveRecord move) =>
        $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.ClaimRun}/{move.ClaimGeneration}";
}
