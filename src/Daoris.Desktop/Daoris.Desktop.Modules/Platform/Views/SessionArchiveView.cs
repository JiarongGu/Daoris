using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris sessions archive|unarchive &lt;id&gt;…</c> as the card shows it: marks of this machine's list, no record changed, refused
/// before any card for a session working, pending or needing you, or an id no record has; an unarchive of none archived changes nothing.
/// </summary>
public sealed class SessionArchiveView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SESSION_ARCHIVE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var ids = PressPayload.Strings(payload, "ids");
        if (ids.Count == 0) return PressViewed.Refused("a press names the sessions by id, as Sessions lists them.");
        var archive = payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("archived", out var flag) && flag.ValueKind == JsonValueKind.True;

        var read = await world.ReadAsync(DriverModule.Name, "SESSION_GROUPS", new { ids }, cancellationToken).ConfigureAwait(false);
        var placed = PressPayload.Rows(read, "sessions")
            .ToDictionary(row => PressPayload.Field(row, "session"), row => PressPayload.Field(row, "group"), StringComparer.Ordinal);
        var kept = new List<string>();
        foreach (var id in ids)
        {
            if (!placed.TryGetValue(id, out var group)) kept.Add($"there is no session `{id}` on this machine");
            else if (archive && group is SessionGroup.Working or SessionGroup.Pending) kept.Add($"`{id}` is still running: stop it first");
            else if (archive && group == SessionGroup.NeedsYou) kept.Add($"`{id}` needs you, and archive never hides what needs you");
        }

        if (kept.Count > 0) return PressViewed.Refused($"{string.Join("; ", kept)}.");
        return PressViewed.Shown(new PressView(
            archive ? "session-archive" : "session-unarchive",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["sessions"] = string.Join(", ", ids), ["count"] = ids.Count.ToString() },
            $"daoris sessions {(archive ? "archive" : "unarchive")} {ShellWord.Words(ids, ShellWord.Name)}"));
    }
}
