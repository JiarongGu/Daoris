namespace Daoris.Desktop;

/// <summary>
/// An Ask Daoris press put to the person on a card in restricted mode, kept in the run's record of Ask Daoris's presses apart from
/// those that ran (`docs/2026-11-11-ask-scope-ux-design.md` §4, <i>Asked you</i>): with the place it named, waiting from its card's
/// asking, then its card's end as its state. The windows are told at each.
/// </summary>
public static class HelpAskedPress
{
    /// <summary>What the press that waited on <paramref name="card"/> made, from its confirmed run's answer, kept with its record; the windows are told it.</summary>
    public static void Answered(InstancePipe pipe, string card, string answer)
    {
        if (PressReceipts.Of(answer) is not { } receipt) return;
        pipe.StandingConfirmation.HelpPresses.ReceivedByCard(card, receipt);
        pipe.TellDriverState();
    }

    /// <summary>A press of Ask Daoris's a rule refused at its ask, before any card: kept as refused with the words it met, naming the growth switch where that is the rule.</summary>
    internal static void Barred(InstancePipe pipe, PipeReserved reserved, string why)
    {
        pipe.StandingConfirmation.HelpPresses.Barred(reserved.Request, reserved.Asker.Session ?? "", why);
        pipe.TellDriverState();
    }

    internal static void Kept(InstancePipe pipe, PipeReserved reserved, Daoris.Driver.HelpPressPlace? place, string card, Task<PipeEnding> ended)
    {
        var presses = pipe.StandingConfirmation.HelpPresses;
        var kept = presses.Asked(reserved.Request, reserved.Asker.Session ?? "", place, card);
        pipe.TellDriverState();
        _ = ended.ContinueWith(done =>
        {
            presses.Answered(kept, done.IsCompletedSuccessfully ? done.Result.End : PipeConfirmationEnd.Lost);
            pipe.TellDriverState();
        }, TaskScheduler.Default);
    }
}
