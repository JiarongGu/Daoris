using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a press on a run whose <c>land</c> the runtime performs answers from the end it waited for (<see cref="PerformedWaits"/>): every end
/// of the performer's try is kept (<see cref="LandEnds"/>), so <i>accepted; landing</i> is said only where none came within the bound, and the
/// carry-on's words only for an end that carried the session on.
/// </summary>
public static class PerformedAnswers
{
    public static TreeLanding Of(TreeLanding? end) => end switch
    {
        null => new TreeLanding(false, LandPresses.Landing),
        { Landed: false, Refusal: LandingCode.CarriedOn } => end with { Message = LandPresses.Carried },
        _ => end,
    };
}
