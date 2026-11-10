using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris browser use|links|extensions</c> as the card shows it (<c>BROWSER_SETTING</c>): which browser Sessions and the person use, where
/// the page's links open, and whether other software's Chrome extensions are offered. A value the screen's route would refuse, or a
/// settings file it could not read, is refused at the ask in its words, before any card. Choosing an Edge none is installed for says so.
/// </summary>
public sealed class BrowserSettingView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "BROWSER_SETTING";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var setting = PressPayload.Text(payload, "setting").Trim();
        var value = PressPayload.Text(payload, "value").Trim();
        var kind = (setting, value) switch
        {
            ("use", BrowserChoice.Daoris) => "browser-use-daoris",
            ("use", BrowserChoice.Edge) => "browser-use-edge",
            ("links", LinksSetting.Daoris) => "browser-links-daoris",
            ("links", LinksSetting.System) => "browser-links-system",
            ("extensions", ExtensionsSetting.Offer) => "browser-extensions-offer",
            ("extensions", ExtensionsSetting.Refuse) => "browser-extensions-refuse",
            _ => null,
        };
        if (kind is null) return PressViewed.Refused(Unknown(setting, value));

        var files = await world.ReadAsync(BrowserModule.Name, "STATE", null, cancellationToken).ConfigureAwait(false);
        if (PressPayload.Field(files, "settingsProblem") is { Length: > 0 } problem) return PressViewed.Refused(BrowserFiles.Unreadable(problem));

        var also = kind == "browser-use-edge" && !PressPayload.Flag(files, "edgeFound") ? new[] { new PressSaid("browser-edge-missing") } : null;
        return PressViewed.Shown(new PressView(kind, new Dictionary<string, string>(), $"daoris browser {setting} {value}", also));
    }

    private static string Unknown(string setting, string value) => setting switch
    {
        "use" => $"The browser is `daoris` or `edge`, not `{value}`.",
        "links" => $"Links open in `system` or `daoris`, not `{value}`.",
        "extensions" => $"The extensions setting is `offer` or `refuse`, not `{value}`.",
        _ => $"`{setting}` is not a setting of Daoris's browser — one of use, links or extensions.",
    };
}
