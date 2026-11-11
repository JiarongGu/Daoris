using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris ask --pause &lt;id&gt;</c> and <c>quest pause &lt;id&gt;</c> as the card shows it: what of the work runs here stopped, and
/// nothing of it started until resumed. Refused before any card where the service has no such ask or quest; a pause with nothing open or
/// taken to hold, or of work already paused with nothing running, is shown as changing nothing.
/// </summary>
public sealed class WorkPauseView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "WORK_PAUSE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var (named, refusal) = await WorkNamed.ReadAsync(world, payload, cancellationToken).ConfigureAwait(false);
        if (named is not { } work) return PressViewed.Refused(refusal!);

        var plan = await world.ReadAsync(DriverModule.Name, "WORK_PLAN", work.Payload, cancellationToken).ConfigureAwait(false);
        var stops = PressPayload.Rows(plan, "sessions").Count(row => PressPayload.Field(row, "pause") == PauseAct.Stopped);
        var paused = PressPayload.Held(plan, "paused");
        var kind = !paused && !PressPayload.Flag(plan, "pausable") ? "work-pause-nothing" : paused && stops == 0 ? "work-pause-already" : "work-pause";
        return PressViewed.Shown(new PressView(
            kind,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["scope"] = work.Scope, ["id"] = work.Id, ["stops"] = stops.ToString() },
            work.Command("pause")));
    }
}
