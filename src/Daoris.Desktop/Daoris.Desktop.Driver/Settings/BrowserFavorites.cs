using System.Text.Json.Nodes;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>One favorite: the page's address in the bar's form, and what the bar calls it.</summary>
public sealed record Favorite(string Url, string Title);

/// <summary>What the favorites file holds, and why a file that exists gave none.</summary>
public sealed record FavoritesRead(IReadOnlyList<Favorite> Favorites, string? Problem);

/// <summary>
/// The in-app browser's favorites: <c>&lt;home&gt;/browser/favorites.json</c>, the person's and never a session's. The window's
/// Browser screen, Ask Daoris and the terminal edit it through <see cref="Add"/> and <see cref="Remove"/>, the one writer.
/// </summary>
/// <remarks>
/// An editor never writes over what it could not read: a reader answers an unreadable file with none, which is the wrong starting
/// point for an edit, and writing it back would lose every favorite. And it keeps what it has no field for, on the file and on each row.
/// </remarks>
public static class BrowserFavorites
{
    public static string FilePath(string home) => DaorisHome.Entry(home, HomeEntry.BrowserFavorites);

    public static FavoritesRead Read(string home)
    {
        var (rows, problem) = Rows(home);
        if (rows is null) return new([], problem);

        var favorites = new List<Favorite>();
        foreach (var row in rows)
        {
            if (row is not JsonObject item || Text(item, "url") is not { } url || BrowserAddress.Page(url) is not { } page) continue;
            favorites.Add(new Favorite(page, Title(item, page)));
        }

        return new(favorites, null);
    }

    /// <summary>
    /// Keep a page: appended, or, when it is kept already, left in its place and given <paramref name="title"/> if one is given.
    /// Returns it as kept.
    /// </summary>
    /// <exception cref="HomeFileRefusal">Not a web page, or a file this could not read; nothing is written.</exception>
    public static Favorite Add(string home, string typed, string? title)
    {
        var page = BrowserAddress.Page(typed)
                   ?? throw new HomeFileRefusal(
                       HomeFileRefusal.NotAPage, $"`{typed}` is not a web page, so it cannot be a favorite.", ("address", typed));
        var (file, rows) = Editable(home);

        var kept = rows.OfType<JsonObject>().FirstOrDefault(row => Text(row, "url") is { } url && BrowserAddress.Page(url) == page);
        if (kept is null)
        {
            kept = new JsonObject { ["url"] = page };
            rows.Add(kept);
        }

        if (!string.IsNullOrWhiteSpace(title)) kept["title"] = title.Trim();
        JsonFile.Write(FilePath(home), file);
        return new Favorite(page, Title(kept, page));
    }

    /// <summary>Stop keeping a page, by address under the same rule. False when it is not kept.</summary>
    /// <exception cref="HomeFileRefusal">A file this could not read; nothing is written.</exception>
    public static bool Remove(string home, string typed)
    {
        var (file, rows) = Editable(home);
        if (BrowserAddress.Page(typed) is not { } page) return false;

        var gone = rows.OfType<JsonObject>().Where(row => Text(row, "url") is { } url && BrowserAddress.Page(url) == page).ToList();
        if (gone.Count == 0) return false;
        foreach (var row in gone) rows.Remove(row);
        JsonFile.Write(FilePath(home), file);
        return true;
    }

    private static (JsonArray? Rows, string? Problem) Rows(string home)
    {
        var path = FilePath(home);
        if (!File.Exists(path)) return (null, null);

        JsonNode? file;
        try
        {
            file = JsonNode.Parse(AtomicFile.ReadText(path));
        }
        catch (System.Text.Json.JsonException error)
        {
            return (null, $"{path} is not readable JSON ({error.Message})");
        }

        if (file is not JsonObject top) return (null, $"{path} is not a JSON object");
        if (!top.TryGetPropertyValue("favorites", out var list)) return (new JsonArray(), null);
        return list is JsonArray rows ? (rows, null) : (null, $"{path}'s favorites is not a list");
    }

    /// <summary>The file as an object to edit, refused when there is one this could not read.</summary>
    private static (JsonObject File, JsonArray Rows) Editable(string home)
    {
        var path = FilePath(home);
        if (!File.Exists(path))
        {
            var fresh = new JsonArray();
            return (new JsonObject { ["favorites"] = fresh }, fresh);
        }

        var (_, problem) = Rows(home);
        if (problem is not null)
        {
            throw new HomeFileRefusal(
                HomeFileRefusal.Unreadable,
                $"{problem}. Fix it, or delete it to start from nothing: Daoris will not write over a file it could not read.",
                ("file", path), ("problem", problem));
        }

        var file = JsonNode.Parse(AtomicFile.ReadText(path))!.AsObject();
        if (file["favorites"] is not JsonArray rows)
        {
            rows = [];
            file["favorites"] = rows;
        }

        return (file, rows);
    }

    private static string Title(JsonObject row, string page) =>
        Text(row, "title") is { } title && !string.IsNullOrWhiteSpace(title) ? title.Trim() : new Uri(page).Host;
}
