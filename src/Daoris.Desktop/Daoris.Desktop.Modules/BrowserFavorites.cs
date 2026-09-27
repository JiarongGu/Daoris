using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>One favorite: the page's address in the bar's form, and what the bar calls it.</summary>
public sealed record Favorite(string Url, string Title);

/// <summary>What the favorites file holds, and why a file that was there gave none.</summary>
public sealed record FavoritesRead(IReadOnlyList<Favorite> Favorites, string? Problem);

/// <summary>
/// The in-app browser's favorites (BRW5): <c>&lt;home&gt;/browser/favorites.json</c>, the person's and
/// never a session's.
/// </summary>
/// <remarks>
/// <para>🔴 <b>A twin file</b> (the in-app browser design, §3a). The CLI's <c>browser.ts</c> reads and
/// edits it with its own code, for <c>daoris browser favorite</c>, and each side carries the same test
/// table. A rule changed here is changed there, in the same commit.</para>
///
/// <para><b>An editor never writes over what it could not read</b>: a reader answers an unreadable
/// file with none, which is the wrong starting point for an edit, and writing it back would lose every
/// favorite. And it keeps what it has no field for, on the file and on each row.</para>
/// </remarks>
public static class BrowserFavorites
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string FilePath(string home) => Path.Combine(home, "browser", "favorites.json");

    /// <summary>
    /// A page's address as a favorite keeps it — the bar's rule (<see cref="InAppBrowser.Address"/>) —
    /// or null when it is no web page. <c>about:blank</c> is no page to keep.
    /// </summary>
    public static string? Address(string typed) =>
        InAppBrowser.Address(typed) is { } page && page != "about:blank" ? page : null;

    public static FavoritesRead Read(string home)
    {
        var (rows, problem) = Rows(home);
        if (rows is null) return new([], problem);

        var favorites = new List<Favorite>();
        foreach (var row in rows)
        {
            if (row is not JsonObject item || Text(item, "url") is not { } url || Address(url) is not { } page) continue;
            favorites.Add(new Favorite(page, Title(item, page)));
        }

        return new(favorites, null);
    }

    /// <summary>
    /// Keep a page: appended, or, when it is kept already, left in its place and given
    /// <paramref name="title"/> if one was given. Returns it as kept.
    /// </summary>
    /// <exception cref="InvalidOperationException">Not a web page, or a file this could not read.</exception>
    public static Favorite Add(string home, string typed, string? title)
    {
        var page = Address(typed)
                   ?? throw new InvalidOperationException($"`{typed}` is not a web page, so it cannot be a favorite.");
        var (file, rows) = Editable(home);

        var kept = rows.OfType<JsonObject>().FirstOrDefault(row => Text(row, "url") is { } url && Address(url) == page);
        if (kept is null)
        {
            kept = new JsonObject { ["url"] = page };
            rows.Add(kept);
        }

        if (!string.IsNullOrWhiteSpace(title)) kept["title"] = title.Trim();
        Save(home, file);
        return new Favorite(page, Title(kept, page));
    }

    /// <summary>Stop keeping a page, by address under the same rule. False when it was not kept.</summary>
    /// <exception cref="InvalidOperationException">A file this could not read.</exception>
    public static bool Remove(string home, string typed)
    {
        if (Address(typed) is not { } page) return false;
        var (file, rows) = Editable(home);

        var gone = rows.OfType<JsonObject>().Where(row => Text(row, "url") is { } url && Address(url) == page).ToList();
        if (gone.Count == 0) return false;
        foreach (var row in gone) rows.Remove(row);
        Save(home, file);
        return true;
    }

    private static (JsonArray? Rows, string? Problem) Rows(string home)
    {
        var path = FilePath(home);
        if (!File.Exists(path)) return (null, null);

        JsonNode? file;
        try
        {
            file = JsonNode.Parse(File.ReadAllText(path));
        }
        catch (JsonException error)
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
            throw new InvalidOperationException(
                $"{problem}. Fix it, or delete it to start from nothing: favorites will not write over a file they could not read.");
        }

        var file = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (file["favorites"] is not JsonArray rows)
        {
            rows = [];
            file["favorites"] = rows;
        }

        return (file, rows);
    }

    private static void Save(string home, JsonObject file)
    {
        var path = FilePath(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, file.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n");
    }

    private static string Title(JsonObject row, string page) =>
        Text(row, "title") is { } title && !string.IsNullOrWhiteSpace(title) ? title.Trim() : new Uri(page).Host;

    private static string? Text(JsonObject row, string name) =>
        row.TryGetPropertyValue(name, out var value) && value is JsonValue text && text.TryGetValue<string>(out var s) ? s : null;
}
