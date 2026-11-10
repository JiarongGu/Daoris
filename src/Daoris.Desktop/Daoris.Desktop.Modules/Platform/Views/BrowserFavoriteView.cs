using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris browser favorite add|remove</c> as the card shows it (<c>BROWSER_FAVORITE</c>): a page kept in the Daoris folder of the browser's
/// bookmarks bar, or one stopped being kept. Judged by the Browser screen's own rules, so a refusal is back to the terminal with no card: a
/// file it could not read is never written over, an address that is not a web page cannot be a favorite, and only a kept one is dropped.
/// </summary>
public sealed class BrowserFavoriteView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "BROWSER_FAVORITE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var action = PressPayload.Text(payload, "action").Trim();
        if (action is not ("add" or "remove")) return PressViewed.Refused($"a favorite is `add` or `remove`, not `{action}`.");

        var typed = PressPayload.Text(payload, "address").Trim();
        if (typed.Length == 0) return PressViewed.Refused("a favorite names its address — the page to keep, or the one to stop keeping.");

        var files = await world.ReadAsync(BrowserModule.Name, "STATE", null, cancellationToken).ConfigureAwait(false);
        if (PressPayload.Field(files, "favoritesProblem") is { Length: > 0 } problem) return PressViewed.Refused(BrowserFiles.Unreadable(problem));
        if (BrowserAddress.Page(typed) is not { } page) return PressViewed.Refused($"`{typed}` is not a web page, so it cannot be a favorite.");

        var title = PressPayload.Text(payload, "title").Trim();
        if (action == "remove")
        {
            var kept = PressPayload.Rows(files, "favorites").Select(row => PressPayload.Field(row, "url")).ToList();
            if (!kept.Contains(page, StringComparer.Ordinal))
            {
                return PressViewed.Refused($"`{typed}` is not a favorite — the ones kept are {(kept.Count > 0 ? string.Join(", ", kept) : "none")}.");
            }

            return Shown("browser-favorite-remove", page, $"daoris browser favorite remove {ShellWord.Of(page, "<address>")}");
        }

        var command = $"daoris browser favorite add {ShellWord.Of(page, "<address>")}" + (title.Length == 0 ? "" : $" --title {ShellWord.Of(title, "<title>")}");
        return title.Length == 0
            ? Shown("browser-favorite-add", page, command)
            : PressViewed.Shown(new PressView("browser-favorite-add-titled", new Dictionary<string, string> { ["page"] = page, ["title"] = title }, command));
    }

    private static PressViewed Shown(string kind, string page, string command) =>
        PressViewed.Shown(new PressView(kind, new Dictionary<string, string> { ["page"] = page }, command));
}

/// <summary>The words the browser's two views share.</summary>
internal static class BrowserFiles
{
    /// <summary>The Browser screen's refusal for a file it could not read, in its words.</summary>
    public static string Unreadable(string problem) =>
        $"{problem}. Fix it, or delete it to start from nothing: Daoris will not write over a file it could not read.";
}
