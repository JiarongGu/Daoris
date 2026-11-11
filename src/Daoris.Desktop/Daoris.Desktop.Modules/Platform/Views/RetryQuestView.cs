using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris quest retry</c> as the card shows it (<c>RETRY_QUEST</c>): a quest the loop's last look parked by its strikes is marked
/// forgiven at the failures that look counted, and one the person's stop holds is released from the session the look named. Refused before
/// any card where the quest is on neither list: forgiving one that is not parked lets it run past its strikes.
/// </summary>
public sealed class RetryQuestView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "RETRY_QUEST";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var quest = PressPayload.Text(payload, "quest").TrimStart('#');
        if (quest.Length == 0) return PressViewed.Refused("`retry` names the quest its failed sessions parked or your stop holds, by id — `daoris quest retry <quest>`.");

        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        var command = $"daoris quest retry {quest}";
        if (machine.Facts.Held.FirstOrDefault(held => string.Equals(held.Quest, quest, StringComparison.OrdinalIgnoreCase)) is { } stopped)
        {
            return Shown("retry-held", new() { ["quest"] = quest, ["session"] = stopped.Session }, command);
        }

        if (machine.Facts.Parked.FirstOrDefault(parked => string.Equals(parked.Quest, quest, StringComparison.OrdinalIgnoreCase)) is not { } parkedQuest)
        {
            static string Listed(IEnumerable<string> ids) => ids.Any() ? string.Join(", ", ids.Select(id => $"`#{id}`")) : "none";
            return PressViewed.Refused(
                $"`#{quest}` is neither parked nor held by your stop on this machine — only such a quest starts again, as its page's *Try again* does; "
                + $"at the driver's last look it parked {Listed(machine.Facts.Parked.Select(each => each.Quest))} and held {Listed(machine.Facts.Held.Select(each => each.Quest))}.");
        }

        var strikes = parkedQuest.ParksAt ?? 0;
        var mark = parkedQuest.Failures ?? machine.Config.ForgivenAt(quest) + strikes;
        return Shown("retry-parked", new()
        {
            ["quest"] = quest,
            ["failures"] = mark.ToString(CultureInfo.InvariantCulture),
            ["more"] = strikes > 0 ? strikes.ToString(CultureInfo.InvariantCulture) : "no",
        }, command);
    }

    private static PressViewed Shown(string kind, Dictionary<string, string> facts, string command) =>
        PressViewed.Shown(new PressView(kind, facts, command));
}
