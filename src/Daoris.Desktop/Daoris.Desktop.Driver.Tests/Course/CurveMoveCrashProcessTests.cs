using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-iii part b (D212; the foundation design §3.3, §13.4 steps 1 to 5, §19.4, row f-iii's proof): the curve's move record holds
/// through a crash at every boundary. A shell is killed at each place the executor passes (<see cref="Cuts"/>), and a shell that comes up
/// takes its move up and ends it by its phase; the person's word the keyed open moved is then carried once by its destination's go-on, and the
/// brief is written once by the dead shell or never, never twice; the move is not asked again under its key.
/// </summary>
/// <remarks>
/// <para><b>Which boundaries the curve passes.</b> All nine, which the census pins by running a move to its end and listing them in order. Two
/// differences from the live limit move's proof (<c>LimitMoveCrashProcessTests</c>): the curve's one message is the brief, which carries no
/// word's id, so its <c>sent</c> is the prompt's outcome written as the door's write returns (<c>Wire([])</c>) and <c>written</c> is the brief's
/// write made with that outcome not yet kept; and a word the open moves waits on the new part never attempted, which no write of the move's
/// carries, so its destination's own go-on carries it, once, in every row from <c>answered</c> on.</para>
/// <para>Each boundary is run with the person's word kept on the old part and with none, since a move that moved nothing is read by its prompt's
/// outcome alone, and closes <c>delivered</c> on it where one that moved a word closes held <c>undelivered</c> naming nothing (the delivery's
/// one reading, <see cref="ConversationMoves.ReadAsync"/>).</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class CurveMoveCrashProcessTests : IAsyncLifetime
{
    private readonly LiveLimitHarness _h = new("curve-move-crash");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <summary>What a killed shell's curve move comes to once a shell comes up.</summary>
    /// <param name="Phase">The move's phase after the come-up.</param>
    /// <param name="Held">The reason it was held, where it was.</param>
    /// <param name="Closed">The reason it closed, where it did.</param>
    /// <param name="Account">The account of the part whose go-on carries the word: the old part's before the open was made, the new part's after.</param>
    /// <param name="Briefed">How many times the dead shell's door wrote the brief to the new part.</param>
    private sealed record Ends(string Phase, string? Held, string? Closed, string Account, int Briefed);

    /// <summary>The row of a boundary, where the person's word was kept on the old part (<paramref name="word"/>) or none was.</summary>
    private static Ends Row(string cut, bool word) => cut switch
    {
        // Before the open was asked: the line stays on its old part, the word with it, and the old part's own come-up carries it (§13.5's first row).
        Cuts.Decided or Cuts.OldEnded => new("fell-back", null, null, "account-1", 0),

        // The open asked and never made: held `unrecorded` and closed, nothing opened, the word carried by the old part's go-on.
        Cuts.Opening => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, "account-1", 0),

        // The open made and its answer lost: closed, and the word the open moved is carried by the destination's go-on, never asked again.
        Cuts.Answered => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, "account-2", 0),

        // The answer kept and nothing handed: the move closes held `undelivered` naming nothing, its word carried by the destination's go-on.
        Cuts.Opened => new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, "account-2", 0),

        // The prompt marked, or written with its outcome not kept: an effect never recorded, held `unrecorded`, never made again by itself.
        Cuts.Marked => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, "account-2", 0),
        Cuts.Written => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, "account-2", 1),

        // The prompt's outcome kept: a move that moved nothing is delivered by it; one that moved a word, whose `sent` no write of the move's
        // carries, closes held `undelivered` naming nothing, and the destination's go-on carries the word.
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
            foreach (var cut in Cuts.All)
            {
                data.Add(cut, true);
                data.Add(cut, false);
            }

            return data;
        }
    }

    /// <summary>
    /// 🔴 The census of the boundaries: a curve move run to its end passes every boundary of <see cref="Cuts.All"/>, once each and in the
    /// executor's order, so a boundary the course adds is reached by the curve's own proof or fails here; each has a row in the theory beside.
    /// </summary>
    [Fact]
    public async Task A_curve_move_run_to_its_end_passes_every_boundary_once_in_order_and_each_has_a_row()
    {
        var passed = new List<string>();
        var shell = await _h.RunCurveShellAsync(cut: null, atTheCut: name => { lock (passed) passed.Add(name); });
        await Poll.Until(() => { lock (passed) return passed.Count >= Cuts.All.Count; }, () => $"passed [{string.Join(",", passed)}], {_h.Seen()}");
        await Task.Delay(300);

        lock (passed) Assert.Equal(Cuts.All, passed);
        Assert.Single(shell.Runner.Opened!.Prompts);
        foreach (var cut in Cuts.All) Assert.NotNull(Row(cut, word: true));
    }

    /// <summary>
    /// 🔴 A shell killed at each boundary, then one coming up: the move ends by its phase (§13.4), the person's word the open moved is carried by
    /// its destination's go-on once, the brief is written once or never, and the move's key is never asked again.
    /// </summary>
    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task A_shell_killed_at_a_boundary_comes_up_with_the_word_carried_once_and_the_brief_written_once_or_never(string cut, bool word)
    {
        var expect = Row(cut, word);
        var shell = await _h.RunCurveShellAsync(cut, keep: word ? null : new Dictionary<string, string>());
        var left = _h.MoveFrom("c1")!;
        _h.Kill();
        _h.Config();

        var runner = _h.Runner();
        await runner.TakeUpAsync();

        // The word is carried once, by the go-on of the part it waits on: the new part's where the open moved it, else the old part's own.
        var carried = word ? 1 : 0;
        if (word) await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= 1, _h.Seen);
        await Poll.Until(() => _h.Waiting().Count == 0, _h.Seen);

        // Nothing more is sent after the come-up settles: a second delivery would be heard here.
        await Task.Delay(1500);
        var move = _h.MoveFrom("c1")!;
        var seen = _h.Seen();
        Assert.True(expect.Phase == move.PhaseWord, $"the move is {move.PhaseWord}, not {expect.Phase}: {seen}");
        Assert.Equal((expect.Held, expect.Closed), (move.HeldWhy, move.ClosedWhy));
        if (expect.Phase == "fell-back") Assert.Equal(ConversationMoves.NotOpened, move.FellBackCode);
        Assert.Equal(("curve", "daoris", StoreProcess.Run), (move.Why, move.By, move.ClaimRun));
        Assert.True(move.ClaimGeneration > left.ClaimGeneration, "the come-up's take-up is a claim of its own");

        // The word once, by the destination's go-on; the brief by the dead shell or not at all; none by the move again.
        Assert.Equal((expect.Briefed, carried), (_h.Briefed(shell), _h.Heard(LiveLimitHarness.Word)));
        Assert.Equal(0, LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word));
        Assert.Equal(carried, _h.HeardBy(LiveLimitHarness.Word, expect.Account));
        Assert.Empty(_h.Waiting());
        Assert.Equal(0, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));

        // The open made is never asked again under the move's key, a lost answer included.
        if (left.Opens is { } key) Assert.Equal(cut == Cuts.Opening ? 0 : 1, _h.Ledger.Opened.Count(each => each.Key == key));

        // The killed shell, let go, finds its claim gone and changes nothing.
        var snapshot = Shape(move);
        await _h.ReleaseAsync();
        Assert.Equal(snapshot, Shape(_h.MoveFrom("c1")!));
        Assert.Equal(carried, _h.Heard(LiveLimitHarness.Word));
        Assert.Equal(expect.Briefed, _h.Briefed(shell));
    }

    private static string Shape(MoveRecord move) =>
        $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.ClaimRun}/{move.ClaimGeneration}";
}
