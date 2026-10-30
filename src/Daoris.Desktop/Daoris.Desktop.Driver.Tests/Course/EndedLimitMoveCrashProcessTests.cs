using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-vi part b (D212; the foundation design §3.3, §13.4 steps 1 to 5, §19.4, row f-vi's proof): the ended limit move's record holds
/// through a crash at every boundary it passes, as <c>LimitMoveCrashProcessTests</c> proves the live move's. A shell is killed at each place
/// the ended move's executor passes, and a shell that comes up takes its move up and ends it by its phase; the word the open moved is then
/// delivered once by the dead shell's write, held uncertain and said, or carried once by its destination's go-on, and never twice; the move is
/// not asked again under its key.
/// </summary>
/// <remarks>
/// <para><b>Which boundaries the ended move passes.</b> Eight of <see cref="Cuts.All"/>, never <see cref="Cuts.OldEnded"/>: an ended record has
/// no part to end, so its decision is written at <c>old-ended</c> and a kill at <see cref="Cuts.Decided"/> leaves it standing there. The census
/// pins the eight by running a move to its end.</para>
/// <para>Each boundary is run with the person's word kept on the record and with none: a move that moved no word is read by its prompt's
/// outcome alone and closes <c>delivered</c> on it where one that moved a word closes by the word's <c>sent</c>. Its one message is the refused
/// turn's word, the moved word and the brief, written once by the dead shell or never.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class EndedLimitMoveCrashProcessTests : IAsyncLifetime
{
    private readonly LiveLimitHarness _h = new("ended-limit-move-crash");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <summary>The boundaries the ended move passes, in its order.</summary>
    private static readonly string[] Passed = [.. Cuts.All.Where(cut => cut != Cuts.OldEnded)];

    /// <summary>What a killed shell's ended move comes to once a shell comes up.</summary>
    /// <param name="Phase">The move's phase after the come-up.</param>
    /// <param name="Held">The reason it was held, where it was.</param>
    /// <param name="Closed">The reason it closed, where it did.</param>
    /// <param name="OnOldPart">Whether the word still waits on the old record: the open never moved it.</param>
    /// <param name="Wrote">How many messages the dead shell's door wrote to the new part.</param>
    /// <param name="Carried">How many times the destination's go-on carried the word to the stub.</param>
    /// <param name="Lost">How many <c>lost</c> notes the move's settlement wrote for the word: said as uncertain.</param>
    private sealed record Ends(string Phase, string? Held, string? Closed, bool OnOldPart, int Wrote, int Carried, int Lost);

    /// <summary>The row of a boundary, where the person's word waits on the record (<paramref name="word"/>) or none does.</summary>
    private static Ends Row(string cut, bool word) => (cut, word) switch
    {
        // The decision at `old-ended`, the open not asked: the line stays on its old part, the word with it (§13.5's first row).
        (Cuts.Decided, true) => new("fell-back", null, null, OnOldPart: true, 0, 0, 0),
        (Cuts.Decided, false) => new("fell-back", null, null, OnOldPart: false, 0, 0, 0),

        // The open asked and never made: held `unrecorded` and closed, nothing opened, the word where it was.
        (Cuts.Opening, true) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, OnOldPart: true, 0, 0, 0),
        (Cuts.Opening, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, OnOldPart: false, 0, 0, 0),

        // The open made, and every boundary before the write: the destination's own go-on carries the word, once, never asked again under the key.
        (Cuts.Answered or Cuts.Marked, true) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, OnOldPart: false, 0, 1, 0),
        (Cuts.Opened, true) => new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, OnOldPart: false, 0, 1, 0),
        (Cuts.Answered or Cuts.Marked, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, OnOldPart: false, 0, 0, 0),
        (Cuts.Opened, false) => new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, OnOldPart: false, 0, 0, 0),

        // The write made and `sent` not written: a moved word is uncertain, taken back and said, never carried again; with none, an effect
        // never recorded.
        (Cuts.Written, true) => new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, OnOldPart: false, 1, 0, 1),
        (Cuts.Written, false) => new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, OnOldPart: false, 1, 0, 0),

        // The prompt's outcome kept: the word is taken, and the move delivered by it, its take made or not.
        (Cuts.Sent or Cuts.Taken, _) => new("delivered", null, null, OnOldPart: false, 1, 0, 0),
        _ => throw new InvalidOperationException($"a boundary with no row: {cut}"),
    };

    public static TheoryData<string, bool> Boundaries
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var cut in Passed)
            {
                data.Add(cut, true);
                data.Add(cut, false);
            }

            return data;
        }
    }

    /// <summary>
    /// 🔴 The census of the boundaries: an ended move run to its end passes the eight of <see cref="Cuts.All"/> it passes, once each and in the
    /// executor's order, never <c>old-ended</c>, so a boundary the course adds is reached by this proof or fails here; each has a row.
    /// </summary>
    [Fact]
    public async Task An_ended_move_run_to_its_end_passes_eight_boundaries_once_in_order_never_old_ended_and_each_has_a_row()
    {
        var passed = new List<string>();
        var shell = await _h.RunEndedShellAsync(cut: null, atTheCut: name => { lock (passed) passed.Add(name); });
        await Poll.Until(() => { lock (passed) return passed.Count >= Passed.Length; }, () => $"passed [{string.Join(",", passed)}], {_h.Seen()}");
        await Task.Delay(300);

        lock (passed) Assert.Equal(Passed, passed);
        Assert.Equal(8, Passed.Length);
        Assert.DoesNotContain(Cuts.OldEnded, Passed);
        Assert.Single(shell.Runner.Opened!.Prompts);
        foreach (var cut in Passed)
        {
            Assert.NotNull(Row(cut, word: true));
            Assert.NotNull(Row(cut, word: false));
        }
    }

    /// <summary>
    /// 🔴 A shell killed at each boundary, then one coming up: the move ends by its phase (§13.4), and the word the open moved is delivered once,
    /// uncertain and said, or carried by its destination's go-on, never twice; the move's key is never asked again.
    /// </summary>
    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task A_shell_killed_at_a_boundary_comes_up_with_the_word_delivered_once_uncertain_and_said_or_carried_never_twice(string cut, bool word)
    {
        var expect = Row(cut, word);
        var shell = await _h.RunEndedShellAsync(cut, keep: word ? null : new Dictionary<string, string>());
        var left = _h.MoveFrom("c1")!;
        _h.Kill();
        _h.Config();

        var runner = _h.Runner();
        await runner.TakeUpAsync();

        if (expect.Carried > 0) await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= expect.Carried, _h.Seen);
        if (word && expect.Wrote + expect.Carried > 0) await Poll.Until(() => _h.Waiting().Count == 0, _h.Seen);

        // Nothing more is sent after the come-up settles: a second delivery would be heard here.
        await Task.Delay(1500);
        var move = _h.MoveFrom("c1")!;
        var seen = _h.Seen();
        Assert.True(expect.Phase == move.PhaseWord, $"the move is {move.PhaseWord}, not {expect.Phase}: {seen}");
        Assert.Equal((expect.Held, expect.Closed), (move.HeldWhy, move.ClosedWhy));
        if (expect.Phase == "fell-back") Assert.Equal(ConversationMoves.NotOpened, move.FellBackCode);
        Assert.Equal(("limit", "daoris", StoreProcess.Run), (move.Why, move.By, move.ClaimRun));
        Assert.True(left.Moments.ContainsKey(MoveRecordPhase.OldEnded), "the move was decided at old-ended, an ended record having no part to end");
        if (cut == Cuts.Decided) Assert.Equal("old-ended", left.PhaseWord);
        Assert.True(move.ClaimGeneration > left.ClaimGeneration, "the come-up's take-up is a claim of its own");

        // Written once by the dead shell or carried once by a go-on, or none: never twice, and never by the move again.
        var wrote = LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word);
        Assert.Equal(expect.Wrote, _h.Briefed(shell));
        Assert.Equal(word ? (expect.Wrote, expect.Carried) : (0, 0), (wrote, _h.Heard(LiveLimitHarness.Word)));
        Assert.True(wrote + _h.Heard(LiveLimitHarness.Word) <= 1, "a word is delivered at most once");
        string[] waiting = expect.OnOldPart ? ["c1:k1"] : [];
        Assert.Equal(waiting, _h.Waiting());
        Assert.Equal(expect.Lost, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));

        // The open made is never asked again under the move's key, a lost answer included.
        if (left.Opens is { } key) Assert.Equal(cut == Cuts.Opening ? 0 : 1, _h.Ledger.Opened.Count(each => each.Key == key));
        Assert.Empty(new LimitBoxes(_h.Home).Pending());

        // The killed shell, let go, finds its claim gone and changes nothing.
        var snapshot = Shape(move);
        await _h.ReleaseAsync();
        Assert.Equal(snapshot, Shape(_h.MoveFrom("c1")!));
        Assert.Equal(expect.Wrote, _h.Briefed(shell));
        Assert.Equal(word ? expect.Wrote + expect.Carried : 0, _h.Heard(LiveLimitHarness.Word) + LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word));
    }

    private static string Shape(MoveRecord move) =>
        $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.ClaimRun}/{move.ClaimGeneration}";
}
