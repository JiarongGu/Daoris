using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Daoris's browser, as a Settings domain (CHR5, CHR7, D50): its favorites, which it shows in a
/// <i>Daoris</i> folder on its bookmarks bar, whether other software's Chrome extensions are offered
/// or refused, which browser it is (BRW12), and where the page's links open (BRW7). The page asks,
/// this edits <c>favorites.json</c> and <c>settings.json</c> under the home: the same files
/// <c>daoris browser</c> edits and <c>daoris-browser</c> reads each time it starts.
/// </summary>
/// <remarks>
/// <para><b>The files are the API; this is an editor.</b> A change lands in the file at once and in
/// the browser the next time it starts, which the page says. The links setting is the page's own, read
/// back from this answer, so it holds at once.</para>
///
/// <para><b>Shell-only, like every machine domain</b> (D47 §4): the service has no door onto these
/// files, and a browser over a keyed remote learns nothing of them.</para>
///
/// <para><b>Each change is one method</b> (HELP10) that its route and Ask Daoris's door both call, so the
/// two cannot drift: the same check, the same refusal for a file it could not read, the same write.</para>
/// </remarks>
public sealed class BrowserModule(IEventBus events) : ModuleBase(events: events)
{
    public override string ModuleName => "DAORIS.BROWSER";

    /// <summary>The home, resolved per call: the person may edit the files by hand between calls.</summary>
    public static string Home => Path.GetDirectoryName(DaorisHome.Require("browser"))!;

    protected override Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        var home = Home;
        switch (request.Type)
        {
            case "STATE":
                return Task.FromResult<object?>(State(home));

            case "ADD_FAVORITE":
                AddFavorite(
                    home, PayloadHelper.GetRequiredValue<string>(request.Payload, "address"),
                    PayloadHelper.GetOptionalValue<string>(request.Payload, "title"));
                return Task.FromResult<object?>(State(home));

            case "REMOVE_FAVORITE":
                RemoveFavorite(home, PayloadHelper.GetRequiredValue<string>(request.Payload, "address"));
                return Task.FromResult<object?>(State(home));

            case "SET_EXTENSIONS":
                SetExtensions(home, PayloadHelper.GetRequiredValue<string>(request.Payload, "extensions"));
                return Task.FromResult<object?>(State(home));

            case "SET_BROWSER":
                SetBrowser(home, PayloadHelper.GetRequiredValue<string>(request.Payload, "browser"));
                return Task.FromResult<object?>(State(home));

            // Where the page's links open (BRW7): the page reads the answer back and routes each click
            // by it, so this one holds at once rather than at the browser's next start.
            case "SET_LINKS":
                SetLinks(home, PayloadHelper.GetRequiredValue<string>(request.Payload, "links"));
                return Task.FromResult<object?>(State(home));

            default:
                throw UnknownType(request);
        }
    }

    /// <summary>Keep a page: <c>ADD_FAVORITE</c>'s check and write, and Ask Daoris's door's (HELP10).</summary>
    public static void AddFavorite(string home, string address, string? title)
    {
        if (BrowserFavorites.Address(address) is null)
        {
            throw Refusals.Because(
                Refusals.BrowserNotAPage,
                $"`{address}` is not a web page, so it cannot be a favorite.",
                ("address", address));
        }

        Unreadable(BrowserFavorites.Read(home).Problem, BrowserFavorites.FilePath(home));
        BrowserFavorites.Add(home, address, title);
    }

    /// <summary>Stop keeping a page: <c>REMOVE_FAVORITE</c>'s write, and Ask Daoris's door's (HELP10).</summary>
    public static void RemoveFavorite(string home, string address)
    {
        Unreadable(BrowserFavorites.Read(home).Problem, BrowserFavorites.FilePath(home));
        BrowserFavorites.Remove(home, address);
    }

    /// <summary>Offer or refuse other software's extensions: <c>SET_EXTENSIONS</c>'s check and write, and Ask Daoris's door's (HELP10).</summary>
    public static void SetExtensions(string home, string extensions)
    {
        if (extensions is not (ExtensionsSetting.Offer or ExtensionsSetting.Refuse))
        {
            throw Refusals.Because(
                Refusals.BrowserSettingUnknown,
                $"The extensions setting is `offer` or `refuse`, not `{extensions}`.",
                ("value", extensions));
        }

        Unreadable(BrowserSettings.Read(home).Problem, BrowserSettings.FilePath(home));
        BrowserSettings.SetExtensions(home, extensions);
    }

    /// <summary>Choose the browser (BRW12): <c>SET_BROWSER</c>'s check and write, and Ask Daoris's door's (HELP10).</summary>
    public static void SetBrowser(string home, string browser)
    {
        if (browser is not (BrowserChoice.Daoris or BrowserChoice.Edge))
        {
            throw Refusals.Because(
                Refusals.BrowserChoiceUnknown,
                $"The browser is `daoris` or `edge`, not `{browser}`.",
                ("value", browser));
        }

        Unreadable(BrowserSettings.Read(home).Problem, BrowserSettings.FilePath(home));
        BrowserSettings.SetBrowser(home, browser);
    }

    /// <summary>Choose where the page's links open (BRW7): <c>SET_LINKS</c>'s check and write, and Ask Daoris's door's (HELP10).</summary>
    public static void SetLinks(string home, string links)
    {
        if (links is not (LinksSetting.System or LinksSetting.Daoris))
        {
            throw Refusals.Because(
                Refusals.BrowserLinksUnknown,
                $"Links open in `system` or `daoris`, not `{links}`.",
                ("value", links));
        }

        Unreadable(BrowserSettings.Read(home).Problem, BrowserSettings.FilePath(home));
        BrowserSettings.SetLinks(home, links);
    }

    /// <summary>
    /// What Ask Daoris judges a browser proposal by (HELP10): the two files as <c>STATE</c> reads them, why either could
    /// not be read, whether an Edge is installed, and this module's own reader of an address.
    /// </summary>
    public static HelpBrowserFacts HelpFacts(string home)
    {
        var favorites = BrowserFavorites.Read(home);
        var settings = BrowserSettings.Read(home);
        return new HelpBrowserFacts(settings.Browser, settings.Links, settings.Extensions, [.. favorites.Favorites.Select(favorite => favorite.Url)])
        {
            Problem = settings.Problem,
            FavoritesProblem = favorites.Problem,
            EdgeFound = EdgeBrowser.Locate() is not null,
            Page = BrowserFavorites.Address,
        };
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
            settings.Links,
            // Whether the Edge option has an Edge to start on this machine, so the page can say so
            // before it is chosen rather than after a press does nothing.
            EdgeFound = EdgeBrowser.Locate() is not null,
            EdgeProfile = EdgeBrowser.ProfileFolder(home),
            SettingsProblem = settings.Problem,
        };
    }
}
