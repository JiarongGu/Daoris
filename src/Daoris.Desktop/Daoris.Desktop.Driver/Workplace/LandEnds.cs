namespace Daoris.Driver;

/// <summary>
/// Each end of a <c>land</c>'s try as the press that waits on it reads it (<see cref="LandOutcomes"/>): a landing as it is, and every other end
/// (closed, nothing to land, a gate's wait, a moved tip, uncommitted work, a carry-on) not landed, under its own code, so a press never says
/// <i>landing</i> where nothing will land.
/// </summary>
public static class LandEnds
{
    /// <param name="code">The try's <see cref="LandingCode"/>, which the end carries as its <see cref="TreeLanding.Refusal"/>.</param>
    /// <param name="landed">What the landing or the gate said, where the try reached one.</param>
    /// <param name="said">The try's own words, for an end with no sentence of its own.</param>
    public static TreeLanding Of(string code, TreeLanding? landed, string said) => landed switch
    {
        { Landed: true } => landed,
        { Message.Length: > 0 } => landed with { Refusal = code },
        _ => new TreeLanding(false, said) { Refusal = code },
    };
}
