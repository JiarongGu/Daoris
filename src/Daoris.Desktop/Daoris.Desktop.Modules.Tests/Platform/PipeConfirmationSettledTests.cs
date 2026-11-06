using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// EVENTHOOK1d-ii (D244 point 3 and its note, the event channel design §3.3): a press no terminal waits on its line for is told to
/// <see cref="PipeConfirmations.Settled"/> once, as it settles, after its record says how it ended, with its own id and its end, whichever
/// of the five. An ending its record could not take, a Confirm the drain holds, and a press a terminal still waits on are told nothing.
/// </summary>
public sealed class PipeConfirmationSettledTests : Bridge
{
    private const string Land = "LAND_SESSION_TREE";

    private static readonly PipeConfirmationField[] Values = [new("/id", "string", "s1a2b3c4")];

    private static readonly PipeAsker Asker = new("daoris-driver", 4242);

    private readonly List<(string Id, PipeConfirmationEnd End)> _settled = [];

    private readonly List<string> _recorded = [];

    private PipeConfirmations Confirmations(MachineLog log, Func<string, PipePersisted>? persisted = null)
    {
        var confirmations = new PipeConfirmations(Bus, log, new HeldClock())
        {
            Persisting = (shown, _) =>
            {
                var kept = persisted?.Invoke(shown.Id) ?? PipePersisted.Written;
                if (kept == PipePersisted.Written) _recorded.Add(shown.Id);
                return kept;
            },
        };
        confirmations.Settled = (press, ending) =>
        {
            // Told only once its record says the ending.
            Assert.Contains(press.Id, _recorded);
            _settled.Add((press.Id, ending.End));
        };
        return confirmations;
    }

    private static string Unwaited(PipeConfirmations confirmations, PipeJudged judged = PipeJudged.Holds)
    {
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker, judge: _ => Task.FromResult(new PipeJudgement(judged)))!.Value;
        Assert.True(confirmations.WaitedOn(asked.Shown.Id, waited: false));
        return asked.Shown.Id;
    }

    [Fact]
    public async Task Each_ending_of_a_press_no_line_waits_on_is_settled_once_after_its_record()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = Confirmations(log);
        var confirmed = Unwaited(confirmations);
        var refused = Unwaited(confirmations);
        var changed = Unwaited(confirmations, PipeJudged.Changed);
        var withdrawn = Unwaited(confirmations);
        var lost = Unwaited(confirmations);

        Assert.Equal(new PipeAnswer(PipeAnswered.Answered), await confirmations.AnswerAsync(confirmed, confirm: true));
        Assert.Equal(new PipeAnswer(PipeAnswered.Answered), await confirmations.AnswerAsync(refused, confirm: false));
        Assert.Equal(PipeAnswered.Changed, (await confirmations.AnswerAsync(changed, confirm: true)).Outcome);
        Assert.Equal(PipeWithdrawal.Withdrawn, confirmations.Withdraw(withdrawn));
        Assert.True(confirmations.Lose(lost));
        // What no longer waits ends nothing again.
        Assert.Equal(new PipeAnswer(PipeAnswered.Gone), await confirmations.AnswerAsync(confirmed, confirm: false));
        Assert.Equal(PipeWithdrawal.Gone, confirmations.Withdraw(refused));

        Assert.Equal(
            [
                (confirmed, PipeConfirmationEnd.Confirmed), (refused, PipeConfirmationEnd.Refused), (changed, PipeConfirmationEnd.Changed),
                (withdrawn, PipeConfirmationEnd.Withdrawn), (lost, PipeConfirmationEnd.Lost),
            ],
            _settled);
    }

    /// <summary>
    /// 🔴 An ending its record could not take and a Confirm the drain holds wait on, told nothing until they end; a terminal waiting on its
    /// line hears its ending there, and a second wake for one answer is none.
    /// </summary>
    [Fact]
    public async Task An_unrecorded_ending_a_drained_confirm_and_a_press_a_line_waits_on_are_told_nothing()
    {
        using var log = new MachineLog(Home, "desktop");
        var mended = false;
        string? unrecorded = null;
        using var confirmations = Confirmations(log, id => id == unrecorded && !mended ? PipePersisted.Unrecorded : PipePersisted.Written);
        unrecorded = Unwaited(confirmations);
        var drained = Unwaited(confirmations);
        var draining = true;
        confirmations.Holding = (_, _) => draining;
        var waited = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        Assert.True(waited.Shown.WaitedOn);

        Assert.Equal(new PipeAnswer(PipeAnswered.Unrecorded), await confirmations.AnswerAsync(unrecorded, confirm: false));
        Assert.Equal(new PipeAnswer(PipeAnswered.Draining), await confirmations.AnswerAsync(drained, confirm: true));
        draining = false;
        Assert.Equal(new PipeAnswer(PipeAnswered.Answered), await confirmations.AnswerAsync(waited.Shown.Id, confirm: false));
        Assert.Equal(PipeConfirmationEnd.Refused, (await waited.Ended).End);
        Assert.Empty(_settled);

        // Each told once it ends.
        mended = true;
        Assert.Equal(new PipeAnswer(PipeAnswered.Answered), await confirmations.AnswerAsync(unrecorded, confirm: false));
        Assert.Equal(new PipeAnswer(PipeAnswered.Answered), await confirmations.AnswerAsync(drained, confirm: true));
        Assert.Equal([(unrecorded, PipeConfirmationEnd.Refused), (drained, PipeConfirmationEnd.Confirmed)], _settled);
    }
}
