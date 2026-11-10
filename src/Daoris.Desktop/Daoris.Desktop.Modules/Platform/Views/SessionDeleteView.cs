using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris sessions delete &lt;id&gt;</c> as the card shows it: a conversation that served no quest, its record and what this machine
/// kept of it gone for good. Refused before any card where no record here has the id, it is a teammate's, it is still running, or the
/// list does not offer Delete for it (it worked on a quest, or a tree or a landing holds it).
/// </summary>
public sealed class SessionDeleteView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SESSION_DELETE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var id = PressPayload.Text(payload, "id").TrimStart('#');
        if (id.Length == 0) return PressViewed.Refused("a delete names the session by its id, as Sessions lists it.");

        var read = await world.ReadAsync(DriverModule.Name, "SESSION_GROUPS", new { ids = new[] { id } }, cancellationToken).ConfigureAwait(false);
        if (PressPayload.Rows(read, "sessions").FirstOrDefault(row => PressPayload.Field(row, "session") == id) is not { ValueKind: JsonValueKind.Object } row)
        {
            return PressViewed.Refused($"there is no session `{id}` on this machine.");
        }

        if (PressPayload.Flag(row, "teammate")) return PressViewed.Refused($"session `{id}` is a teammate's, so its record is theirs — archive it here instead.");
        if (PressPayload.Field(row, "group") == SessionGroup.Working) return PressViewed.Refused($"session `{id}` is still running; stop it first.");
        if (!PressPayload.Flag(row, "deletable"))
        {
            return PressViewed.Refused($"session `{id}`'s record stays: it worked on a quest, or a tree or a landing still holds it — archive it instead.");
        }

        return PressViewed.Shown(new PressView(
            "session-delete", new Dictionary<string, string>(StringComparer.Ordinal) { ["session"] = id }, $"daoris sessions delete {ShellWord.Of(id, ShellWord.Name)}"));
    }
}
