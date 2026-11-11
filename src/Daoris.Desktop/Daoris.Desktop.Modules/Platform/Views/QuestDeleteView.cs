using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris quest delete &lt;id&gt;</c> as the card shows it: a quest made by mistake gone from every list. Refused before any card
/// where the service has no such quest, it is taken, done or declined, whose record stays (declining is the way instead), or the service says work stands on it (an open one a session
/// started on).
/// </summary>
public sealed class QuestDeleteView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "QUEST_DELETE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var id = PressPayload.Text(payload, "quest").TrimStart('#');
        if (id.Length == 0) return PressViewed.Refused("a delete names the quest by its id.");
        if (await world.QuestAsync(id, cancellationToken).ConfigureAwait(false) is not { } status) return PressViewed.Refused($"there is no quest `#{id}` on this machine.");

        var kept = status.ToLowerInvariant() switch
        {
            "taken" => "someone is working it, and its record stays",
            "done" => "it is the record of work that was answered",
            "declined" => "the decline is the trace of a decision",
            _ => null,
        };
        if (kept is not null) return PressViewed.Refused($"quest `#{id}` is {status.ToLowerInvariant()}: {kept}. Decline it instead, with the reason.");
        if (await world.QuestDeletableAsync(id, cancellationToken).ConfigureAwait(false) == false)
        {
            return PressViewed.Refused($"quest `#{id}` is {status.ToLowerInvariant()}, but work stands on it (a session started on it, or a quest waits on it), so its record stays. Decline it instead, with the reason.");
        }

        return PressViewed.Shown(new PressView(
            "quest-delete", new Dictionary<string, string>(StringComparer.Ordinal) { ["quest"] = id }, $"daoris quest delete {ShellWord.Of(id, ShellWord.Name)}"));
    }
}
