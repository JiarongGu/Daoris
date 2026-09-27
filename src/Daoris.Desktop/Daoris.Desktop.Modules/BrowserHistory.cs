using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>One page in the history: its address in the bar's form, its title, when last, how often.</summary>
public sealed record Visit(string Url, string Title, DateTimeOffset Last, int Count);

/// <summary>What the history file holds, most recent first, and why a file that was there gave none.</summary>
public sealed record HistoryRead(IReadOnlyList<Visit> Visits, string? Problem);

/// <summary>One completion for the address bar: a favorite, or a page from the history.</summary>
public sealed record Suggestion(string Url, string Title, bool Favorite);

/// <summary>
/// The in-app browser's history (BRW6): <c>&lt;home&gt;/browser/history.json</c>, kept on this
/// machine, and the address bar's completions from it and the favorites.
/// </summary>
/// <remarks>
/// <para><b>Daoris's own record.</b> WebView2 keeps a history of its own in the profile and offers no
/// way to read it, so the window records each page it finishes loading here, and the bar completes from
/// that. Clearing clears both.</para>
///
/// <para>🔴 <b>A twin file for reading and clearing</b> (the in-app browser design, §3b): the CLI's
/// <c>browser.ts</c> lists and clears it with its own code, and each side carries the same reading
/// table. Recording a visit and completing are the window's alone.</para>
/// </remarks>
public static class BrowserHistory
{
    /// <summary>The most pages kept. Past it, the least recent go: history to complete from, not an archive.</summary>
    public const int Kept = 500;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string FilePath(string home) => Path.Combine(home, "browser", "history.json");

    public static HistoryRead Read(string home)
    {
        var (rows, problem) = Rows(home);
        if (rows is null) return new([], problem);

        var visits = new List<Visit>();
        foreach (var row in rows)
        {
            if (row is not JsonObject item || Text(item, "url") is not { } url || BrowserFavorites.Address(url) is not { } page) continue;
            visits.Add(new Visit(page, Title(item, page), Last(item), Count(item)));
        }

        return new([.. visits.OrderByDescending(visit => visit.Last)], null);
    }

    /// <summary>
    /// A page finished loading: counted again and moved forward if it is there, added if not, and the
    /// least recent dropped past <see cref="Kept"/>. False when it is no web page.
    /// </summary>
    /// <exception cref="InvalidOperationException">A file this could not read, which is not written over.</exception>
    public static bool Visit(string home, string typed, string? title, DateTimeOffset at)
    {
        if (BrowserFavorites.Address(typed) is not { } page) return false;
        var (file, rows) = Editable(home);

        var row = rows.OfType<JsonObject>().FirstOrDefault(r => Text(r, "url") is { } url && BrowserFavorites.Address(url) == page);
        if (row is null)
        {
            row = new JsonObject { ["url"] = page, ["count"] = 1 };
            rows.Add(row);
        }
        else
        {
            row["count"] = Count(row) + 1;
        }

        row["last"] = at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(title)) row["title"] = title.Trim();

        // The least recent go first, and a row this build cannot read goes before any it can.
        while (rows.Count > Kept)
        {
            var oldest = rows.OrderBy(r => r is JsonObject o && Text(o, "url") is { } u && BrowserFavorites.Address(u) is not null
                ? Last(o) : DateTimeOffset.MinValue).First();
            rows.Remove(oldest);
        }

        Save(home, file);
        return true;
    }

    /// <summary>Forget every page. What an editor has no field for, on the file, stays.</summary>
    /// <exception cref="InvalidOperationException">A file this could not read, which is not written over.</exception>
    public static void Clear(string home)
    {
        if (!File.Exists(FilePath(home))) return;
        var (file, _) = Editable(home);
        file["visits"] = new JsonArray();
        Save(home, file);
    }

    /// <summary>
    /// What the address bar offers for <paramref name="typed"/>: favorites first, then history, each
    /// page once, matched in its address or its title whatever the case. A host that starts with what
    /// was typed comes before one that only contains it; history then goes by how often, then how lately.
    /// </summary>
    public static IReadOnlyList<Suggestion> Suggest(
        string typed, IReadOnlyList<Favorite> favorites, IReadOnlyList<Visit> visits, int limit)
    {
        var query = typed.Trim();
        foreach (var scheme in new[] { "https://", "http://" })
        {
            if (query.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) query = query[scheme.Length..];
        }

        if (query.Length == 0) return [];

        var found = new List<Suggestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var favorite in favorites.Where(f => Matches(f.Url, f.Title, query)).OrderBy(f => HostStarts(f.Url, query) ? 0 : 1))
        {
            if (seen.Add(favorite.Url)) found.Add(new Suggestion(favorite.Url, favorite.Title, Favorite: true));
        }

        foreach (var visit in visits.Where(v => Matches(v.Url, v.Title, query))
                     .OrderBy(v => HostStarts(v.Url, query) ? 0 : 1)
                     .ThenByDescending(v => v.Count)
                     .ThenByDescending(v => v.Last))
        {
            if (seen.Add(visit.Url)) found.Add(new Suggestion(visit.Url, visit.Title, Favorite: false));
        }

        return [.. found.Take(limit)];
    }

    private static bool Matches(string url, string title, string query) =>
        WithoutScheme(url).Contains(query, StringComparison.OrdinalIgnoreCase)
        || title.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool HostStarts(string url, string query) =>
        new Uri(url).Host.StartsWith(query, StringComparison.OrdinalIgnoreCase);

    private static string WithoutScheme(string url)
    {
        var at = url.IndexOf("://", StringComparison.Ordinal);
        return at < 0 ? url : url[(at + 3)..];
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
        if (!top.TryGetPropertyValue("visits", out var list)) return (new JsonArray(), null);
        return list is JsonArray rows ? (rows, null) : (null, $"{path}'s visits is not a list");
    }

    private static (JsonObject File, JsonArray Rows) Editable(string home)
    {
        var path = FilePath(home);
        if (!File.Exists(path))
        {
            var fresh = new JsonArray();
            return (new JsonObject { ["visits"] = fresh }, fresh);
        }

        var (_, problem) = Rows(home);
        if (problem is not null)
        {
            throw new InvalidOperationException(
                $"{problem}. Fix it, or delete it to start from nothing: the history will not write over a file it could not read.");
        }

        var file = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (file["visits"] is not JsonArray rows)
        {
            rows = [];
            file["visits"] = rows;
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

    /// <summary>When it was last visited, or the earliest time there is when the row does not say in a time.</summary>
    private static DateTimeOffset Last(JsonObject row) =>
        Text(row, "last") is { } last
        && DateTimeOffset.TryParse(last, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at
            : DateTimeOffset.MinValue;

    /// <summary>How often, or once when the row does not say in a positive whole number.</summary>
    private static int Count(JsonObject row) =>
        row.TryGetPropertyValue("count", out var value) && value is JsonValue number && number.TryGetValue<int>(out var n) && n > 0
            ? n
            : 1;

    private static string? Text(JsonObject row, string name) =>
        row.TryGetPropertyValue(name, out var value) && value is JsonValue text && text.TryGetValue<string>(out var s) ? s : null;
}
