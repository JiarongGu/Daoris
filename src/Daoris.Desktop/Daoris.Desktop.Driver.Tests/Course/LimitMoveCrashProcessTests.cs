using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-ii part b (D212; the foundation design §3.3, §13.4 steps 1 to 5, §19.4, row f-ii's proof): the live limit move's record holds
/// through a crash at every boundary. A shell is killed at each place the executor passes (<see cref="Cuts"/>: after the decision, after
/// <c>old-ended</c>, after <c>opening</c> with and without the open made, after <c>opened</c>, after the mark and before the write, after the
/// write and before <c>sent</c>, after <c>sent</c> and before the take, after the take and before <c>delivered</c>), and a shell that comes up
/// takes its move up and ends it by its phase. The word the open moved is then delivered once by the dead shell's write, held uncertain and
/// said, or carried once by its destination's go-on in the real protocol stub, and never twice; the move is not asked again under its key.
/// </summary>
/// <remarks>
/// <para>The killed shell and the coming-up shell are two runs over one home and one ledger, one OS process (<see cref="LiveLimitHarness"/>).
/// A phase added to <see cref="Cuts.All"/> and not given a row here fails the census beside the theory, so no boundary goes unproved.</para>
/// <para><b>The reading taken for a move found at <c>opening</c>:</b> the take-up holds it <c>unrecorded</c> and closes it, and asks no open
/// again under its stored key. The destination the open made, where it made one, is a record of its own with the moved words waiting on it
/// never attempted, which its go-on carries as it carries any ended record's words; a second open would only learn what the line shows.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LimitMoveCrashProcessTests : IAsyncLifetime
{
    private readonly LiveLimitHarness _h = new("limit-move-crash");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    /// <summary>What a killed shell's move comes to once a shell comes up: its end, whether its word was written by the dead shell or carried, and where it waits.</summary>
    /// <param name="Phase">The move's phase after the come-up.</param>
    /// <param name="Held">The reason it was held, where it was.</param>
    /// <param name="Closed">The reason it closed, where it did.</param>
    /// <param name="OnOldPart">Whether the word still waits on the old part: the open never moved it.</param>
    /// <param name="Wrote">How many times the dead shell's door wrote the word to the new part.</param>
    /// <param name="Carried">How many times the destination's go-on carried it to the stub.</param>
    /// <param name="Lost">How many <c>lost</c> notes the move's settlement wrote for it: said as uncertain.</param>
    private sealed record Ends(string Phase, string? Held, string? Closed, bool OnOldPart, int Wrote, int Carried, int Lost);

    private static readonly Dictionary<string, Ends> Table = new(StringComparer.Ordinal)
    {
        // Before the open was asked: the line stays on its old part, the word with it, for the old part's own come-up (§13.5's first row).
        [Cuts.Decided] = new("fell-back", null, null, OnOldPart: true, 0, 0, 0),
        [Cuts.OldEnded] = new("fell-back", null, null, OnOldPart: true, 0, 0, 0),

        // The open asked and never made: held `unrecorded` and closed, nothing opened, the word where it was.
        [Cuts.Opening] = new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, OnOldPart: true, 0, 0, 0),

        // The open made and its answer lost, and every later boundary before the write: the move is closed and the destination's own go-on
        // carries the word, once, never asked again under the move's key.
        [Cuts.Answered] = new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, OnOldPart: false, 0, 1, 0),
        [Cuts.Opened] = new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, OnOldPart: false, 0, 1, 0),
        [Cuts.Marked] = new("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved, OnOldPart: false, 0, 1, 0),

        // The write made and `sent` not written: uncertain, taken back and said, never carried again.
        [Cuts.Written] = new("closed", MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved, OnOldPart: false, 1, 0, 1),

        // `sent` written: the word is taken, and the move delivered by it, whether its take was made or not.
        [Cuts.Sent] = new("delivered", null, null, OnOldPart: false, 1, 0, 0),
        [Cuts.Taken] = new("delivered", null, null, OnOldPart: false, 1, 0, 0),
    };

    public static TheoryData<string> Boundaries => [.. Cuts.All];

    /// <summary>
    /// 🔴 The census beside the theory: every boundary the executor passes has a row, and every boundary the sources tell the holder of is one
    /// of them, so a phase added and not proved fails here.
    /// </summary>
    [Fact]
    public void Every_boundary_the_executor_passes_has_a_row_and_each_is_told_where_it_is_passed()
    {
        Assert.Equal(Cuts.All.Order(StringComparer.Ordinal), Table.Keys.Order(StringComparer.Ordinal));
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var passed = string.Concat(
            File.ReadAllText(Path.Combine(driver, "Course", "ConversationMoves.cs")), File.ReadAllText(Path.Combine(driver, "Course", "Chat", "LimitMove.cs")));
        var named = Regex.Matches(passed, @"\bCuts\.(\w+)\b").Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var declared = typeof(ConversationMoves).Assembly.GetType("Daoris.Driver.Cuts")!
            .GetFields().Where(field => field.IsLiteral).Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(declared.Order(StringComparer.Ordinal), named.Where(declared.Contains).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// 🔴 A shell killed at each boundary, then one coming up: the move ends by its phase (§13.4), and the word the open moved is delivered
    /// once, uncertain and said, or carried by its destination's go-on, never twice; the move's key is never asked again.
    /// </summary>
    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task A_shell_killed_at_a_boundary_comes_up_with_the_moved_word_delivered_once_uncertain_and_said_or_carried_never_twice(string cut)
    {
        var expect = Table[cut];
        var shell = await _h.RunShellAsync(cut);
        var left = _h.MoveFrom("c1")!;
        _h.Kill();
        _h.Config();

        var runner = _h.Runner();
        await runner.TakeUpAsync();

        if (expect.Carried > 0) await Poll.Until(() => _h.Heard(LiveLimitHarness.Word) >= expect.Carried, _h.Seen);
        if (expect.Wrote + expect.Carried > 0) await Poll.Until(() => _h.Waiting().Count == 0, _h.Seen);

        // Nothing more is sent after the come-up settles: a second delivery would be heard here.
        await Task.Delay(1500);
        var move = _h.MoveFrom("c1")!;
        var seen = _h.Seen();
        Assert.True(expect.Phase == move.PhaseWord, $"the move is {move.PhaseWord}, not {expect.Phase}: {seen}");
        Assert.Equal((expect.Held, expect.Closed), (move.HeldWhy, move.ClosedWhy));
        if (expect.Phase == "fell-back") Assert.Equal(ConversationMoves.NotOpened, move.FellBackCode);
        Assert.Equal(StoreProcess.Run, move.ClaimRun);
        Assert.True(move.ClaimGeneration > left.ClaimGeneration, "the come-up's take-up is a claim of its own");

        // Delivered once, or carried once, or none: never twice, and never by the move again.
        Assert.Equal((expect.Wrote, expect.Carried), (LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word), _h.Heard(LiveLimitHarness.Word)));
        Assert.True(expect.Wrote + expect.Carried <= 1, "a word is delivered at most once");
        string[] waiting = expect.OnOldPart ? ["c1:k1"] : [];
        Assert.Equal(waiting, _h.Waiting());
        Assert.Equal(expect.Lost, _h.Events.After("c2", 0).Events.Count(e => e.Code == SessionEventCodes.Lost));

        // The open made is never asked again under the move's key, a lost answer included.
        if (left.Opens is { } key) Assert.Equal(cut == Cuts.Opening ? 0 : 1, _h.Ledger.Opened.Count(each => each.Key == key));

        // The killed shell, let go, finds its claim gone and changes nothing.
        var snapshot = Shape(move);
        await _h.ReleaseAsync();
        Assert.Equal(snapshot, Shape(_h.MoveFrom("c1")!));
        Assert.Equal(expect.Wrote + expect.Carried, _h.Heard(LiveLimitHarness.Word) + LiveLimitHarness.Wrote(shell, LiveLimitHarness.Word));
    }

    private static string Shape(MoveRecord move) =>
        $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.ClaimRun}/{move.ClaimGeneration}";
}
