using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The window's half of a steward's <c>land</c> that a supervisor allowed: the lands waiting their window, and the person's
/// <i>Overrule</i> of one, which hands it to the person's own card. No verb sends it; the page's steward line does.
/// </summary>
public sealed class StewardLandRoutes(DriverLoop loop)
{
    // The allowed lands waiting their window now: `{ lands: [{ ask, act, repository?, quest?, session?, landsAt }] }`, soonest first. A read off
    // the pipe's memory, which a restart empties; the press's ground.
    [DriverRoute("STEWARD_LANDS", PipeClass.Open)]
    private Task<object?> StewardLandsAsync(NoPayload _, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new { Lands = loop.Pipe?.StewardAllows.Lands().Select(LandRow).ToList() ?? [] });

    // The person's *Overrule* of an allowed land inside its window: it does not land, and the person's card for it follows. `{ done }`, false
    // where the window ended, the steward withdrew the request or another press settled it first.
    [DriverRoute("STEWARD_LAND_OVERRULE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("STEWARD_LAND_OVERRULE", Session = "session")]
    [GroundRead("STEWARD_LAND_OVERRULE", nameof(PipeReads.StewardLand))]
    [Publishes("STEWARD_LAND_OVERRULE", StateScopes.Driver, Session = "session")]
    private Task<object?> StewardLandOverruleAsync(StewardLandOverrule press, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new
        {
            // The session the press marks is the one the request lands, never one it merely names.
            Done = loop.Pipe is { } pipe && pipe.StewardAllows.Lands().Any(each => each.Ask == press.Ask && each.Session == press.Session)
                   && pipe.StewardAllows.Overrule(press.Ask),
        });

    internal static object LandRow(StewardLandNow land) => new
    {
        land.Ask, land.Act, land.Repository, land.Quest, land.Session, LandsAt = Moments.Write(land.LandsAt),
    };
}
