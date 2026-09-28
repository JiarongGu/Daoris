using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Daoris's browser, as a Settings domain (CHR5, CHR7, D50): its favorites, which it shows in a
/// <i>Daoris</i> folder on its bookmarks bar, and whether other software's Chrome extensions are
/// offered or refused. The page asks, this edits <c>favorites.json</c> and <c>settings.json</c> under
/// the home: the same files <c>daoris browser</c> edits and <c>daoris-browser</c> reads each time it
/// starts.
/// </summary>
/// <remarks>
/// <para><b>The files are the API; this is an editor.</b> A change lands in the file at once and in
/// the browser the next time it starts, which the page says.</para>
///
/// <para><b>Shell-only, like every machine domain</b> (D47 §4): the service has no door onto these
/// files, and a browser over a keyed remote learns nothing of them.</para>
/// </remarks>
public sealed class BrowserModule(IEventBus events) : ModuleBase(events: events)
{
    public override string ModuleName => "DAORIS.BROWSER";

    /// <summary>The home, resolved per call: the person may edit the files by hand between calls.</summary>
    private static string Home => Path.GetDirectoryName(DaorisHome.Require("browser"))!;

    protected override Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        var home = Home;
        switch (request.Type)
        {
            case "STATE":
                return Task.FromResult<object?>(State(home));

            case "ADD_FAVORITE":
            {
                var address = PayloadHelper.GetRequiredValue<string>(request.Payload, "address");
                var title = PayloadHelper.GetOptionalValue<string>(request.Payload, "title");
                if (BrowserFavorites.Address(address) is null)
                {
                    throw Refusals.Because(
                        Refusals.BrowserNotAPage,
                        $"`{address}` is not a web page, so it cannot be a favorite.",
                        ("address", address));
                }

                Unreadable(BrowserFavorites.Read(home).Problem, BrowserFavorites.FilePath(home));
                BrowserFavorites.Add(home, address, title);
                return Task.FromResult<object?>(State(home));
            }

            case "REMOVE_FAVORITE":
            {
                var address = PayloadHelper.GetRequiredValue<string>(request.Payload, "address");
                Unreadable(BrowserFavorites.Read(home).Problem, BrowserFavorites.FilePath(home));
                BrowserFavorites.Remove(home, address);
                return Task.FromResult<object?>(State(home));
            }

            case "SET_EXTENSIONS":
            {
                var extensions = PayloadHelper.GetRequiredValue<string>(request.Payload, "extensions");
                if (extensions is not (ExtensionsSetting.Offer or ExtensionsSetting.Refuse))
                {
                    throw Refusals.Because(
                        Refusals.BrowserSettingUnknown,
                        $"The extensions setting is `offer` or `refuse`, not `{extensions}`.",
                        ("value", extensions));
                }

                Unreadable(BrowserSettings.Read(home).Problem, BrowserSettings.FilePath(home));
                BrowserSettings.SetExtensions(home, extensions);
                return Task.FromResult<object?>(State(home));
            }

            case "SET_BROWSER":
            {
                var browser = PayloadHelper.GetRequiredValue<string>(request.Payload, "browser");
                if (browser is not (BrowserChoice.Daoris or BrowserChoice.Edge))
                {
                    throw Refusals.Because(
                        Refusals.BrowserChoiceUnknown,
                        $"The browser is `daoris` or `edge`, not `{browser}`.",
                        ("value", browser));
                }

                Unreadable(BrowserSettings.Read(home).Problem, BrowserSettings.FilePath(home));
                BrowserSettings.SetBrowser(home, browser);
                return Task.FromResult<object?>(State(home));
            }

            default:
                throw UnknownType(request);
        }
    }

    /// <summary>A file this could not read is never written over, and the page says which, and why.</summary>
    private static void Unreadable(string? problem, string file)
    {
        if (problem is null) return;
        throw Refusals.Because(
            Refusals.BrowserFileUnreadable,
            $"{problem}. Fix it, or delete it to start from nothing: Daoris will not write over a file it could not read.",
            ("file", file), ("problem", problem));
    }

    private static object State(string home)
    {
        var favorites = BrowserFavorites.Read(home);
        var settings = BrowserSettings.Read(home);
        return new
        {
            FavoritesPath = BrowserFavorites.FilePath(home),
            Favorites = favorites.Favorites.Select(favorite => new { favorite.Url, favorite.Title }),
            FavoritesProblem = favorites.Problem,
            SettingsPath = BrowserSettings.FilePath(home),
            settings.Extensions,
            settings.Browser,
            // Whether the Edge option has an Edge to start on this machine, so the page can say so
            // before it is chosen rather than after a press does nothing.
            EdgeFound = EdgeBrowser.Locate() is not null,
            EdgeProfile = EdgeBrowser.ProfileFolder(home),
            SettingsProblem = settings.Problem,
        };
    }
}
