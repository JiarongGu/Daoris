using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris ask --delete &lt;id&gt;</c> as the card shows it: an ask made by mistake gone with every quest it became. Refused before any
/// card where the service has no such ask, or says one of the quests it became must stay (taken, closed or started on).
/// </summary>
public sealed class AskDeleteView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "ASK_DELETE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var id = PressPayload.Text(payload, "ask").TrimStart('#');
        if (id.Length == 0) return PressViewed.Refused("a delete names the ask by its id.");
        if (await world.AskAsync(id, cancellationToken).ConfigureAwait(false) is null) return PressViewed.Refused($"there is no ask `#{id}` on this machine.");
        if (await world.AskDeletableAsync(id, cancellationToken).ConfigureAwait(false) == false)
        {
            return PressViewed.Refused($"ask `#{id}` stays, with its quests: an ask is deleted only with every quest it became, and one of them is taken, closed or started on. Close the ask instead, with the reason.");
        }

        return PressViewed.Shown(new PressView(
            "ask-delete", new Dictionary<string, string>(StringComparer.Ordinal) { ["ask"] = id }, $"daoris ask --delete {ShellWord.Of(id, ShellWord.Name)}"));
    }
}
