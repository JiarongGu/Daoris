using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris place standing</c> as the card shows it (<c>SET_STANDING</c>): the person's words for one repository, handed to every session
/// there beneath its quest, or taken back. Refused before any card where the repository is not registered here or the words are ones the
/// config would not keep.
/// </summary>
public sealed class PlaceStandingView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SET_STANDING";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var scoped = await PlaceScope.ReadAsync(world, payload, "a standing answer", false, cancellationToken).ConfigureAwait(false);
        if (scoped.Refusal is not null) return PlaceScope.Refusal(scoped);

        var says = PressPayload.Text(payload, "says") is { Length: > 0 } text ? text : null;
        try
        {
            _ = DriverConfig.Empty.WithStanding(scoped.Repository!, says, DateTimeOffset.UtcNow);
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
        }

        var facts = scoped.Facts();
        if (says is not null) facts["says"] = says;
        var words = says is null ? "--clear" : $"\"{says.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        return PressViewed.Shown(new PressView(says is null ? "standing-clear" : "standing-set", facts, $"daoris place standing {scoped.Words} {words}"));
    }
}
