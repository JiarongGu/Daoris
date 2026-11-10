using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris ask --resume &lt;id&gt;</c> and <c>quest resume &lt;id&gt;</c> as the card shows it: the pause lifted and each stop it made
/// released. Refused before any card where the service has no such ask or quest; a resume of work not paused here is shown as
/// changing nothing.
/// </summary>
public sealed class WorkResumeView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "WORK_RESUME";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var (named, refusal) = await WorkNamed.ReadAsync(world, payload, cancellationToken).ConfigureAwait(false);
        if (named is not { } work) return PressViewed.Refused(refusal!);

        var plan = await world.ReadAsync(DriverModule.Name, "WORK_PLAN", work.Payload, cancellationToken).ConfigureAwait(false);
        return PressViewed.Shown(new PressView(
            PressPayload.Held(plan, "paused") ? "work-resume" : "work-resume-nothing",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["scope"] = work.Scope, ["id"] = work.Id },
            work.Command("resume")));
    }
}
