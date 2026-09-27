using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Desktop;

/// <summary>What Daoris did to the engine's profile, kept beside it so it can be undone exactly.</summary>
/// <param name="Refused">The extensions Daoris refused, and only those: one the person removed in the browser is theirs.</param>
/// <param name="BarShown">Whether Daoris has turned the bookmarks bar on once, for its folder's first appearance.</param>
public sealed record EngineRecord(IReadOnlyList<string> Refused, bool BarShown);

/// <summary>
/// The engine's profile files, as <c>daoris-browser</c> edits them before the engine starts (CHR5,
/// CHR7): Daoris's favorites as a folder on the bookmarks bar, and other software's Chrome extensions
/// refused or offered. Pure, so every rule is tested without an engine.
/// </summary>
/// <remarks>
/// <para><b>Measured before written</b> (`docs/2026-09-28-chromium-embedding-evidence.md` §20–§21):
/// the engine takes a bookmarks file written without its checksum and shows its folder, and it keeps
/// an extension id seeded into <c>extensions.external_uninstalls</c> and stops offering it.</para>
///
/// <para><b>Never written over what could not be read</b>: a file that is not what the engine writes
/// comes back as null, and nothing is changed.</para>
/// </remarks>
public static class EngineProfile
{
    /// <summary>The engine's own ids for its three root folders, which a file written from nothing must carry.</summary>
    public const string BarGuid = "0bc5d13f-2cba-5d74-951f-3f233fe6c908";
    public const string OtherGuid = "82b081ec-3dd3-529c-8475-ab6c344590dd";
    public const string MobileGuid = "4cf2e351-0e85-532b-bb37-df045d8f8d0f";

    /// <summary>Daoris's folder on the bar: found by this id, wherever the person moved it and whatever they named it.</summary>
    public const string DaorisFolderGuid = "d4a0f15c-0000-4000-8000-00000000da01";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private static readonly long Epoch1601 = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    public static string BookmarksPath(string profileFolder) => Path.Combine(EngineBrowser.ProfileDirectory(profileFolder), "Bookmarks");

    public static string PreferencesPath(string profileFolder) => Path.Combine(EngineBrowser.ProfileDirectory(profileFolder), "Preferences");

    /// <summary>Daoris's own record, beside the engine's files and never among them.</summary>
    public static string RecordPath(string profileFolder) => Path.Combine(profileFolder, "daoris.json");

    /// <summary>A Chrome extension id: 32 letters from <c>a</c> to <c>p</c>.</summary>
    public static bool IsExtensionId(string text) => text.Length == 32 && text.All(c => c is >= 'a' and <= 'p');

    /// <summary>
    /// The bookmarks with Daoris's folder holding exactly <paramref name="favorites"/>, in their order,
    /// and whether the folder is new. Null when nothing changes, or when the file is not one this can read.
    /// With no favorites the folder goes; the person's own bookmarks are never touched.
    /// </summary>
    public static (string Json, bool Created)? WithFavorites(string? bookmarks, IReadOnlyList<Favorite> favorites, DateTimeOffset now)
    {
        JsonObject file;
        if (bookmarks is null)
        {
            if (favorites.Count == 0) return null;
            file = Fresh(now);
        }
        else
        {
            try
            {
                if (JsonNode.Parse(bookmarks) is not JsonObject parsed) return null;
                file = parsed;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        if (file["roots"]?["bookmark_bar"]?["children"] is not JsonArray bar) return null;
        var folder = bar.OfType<JsonObject>().FirstOrDefault(node => Text(node, "guid") == DaorisFolderGuid);
        if (favorites.Count == 0)
        {
            if (folder is null) return null;
            bar.Remove(folder);
            file.Remove("checksum");
            return (Save(file), false);
        }

        var next = MaxId(file) + 1;
        var created = folder is null;
        if (folder is null)
        {
            folder = Node(next++, DaorisFolderGuid, "Daoris", now);
            folder["type"] = "folder";
            folder["date_modified"] = Time(now);
            bar.Add(folder);
        }

        var children = new JsonArray();
        foreach (var favorite in favorites)
        {
            var link = Node(next++, GuidFor(favorite.Url), favorite.Title, now);
            link["type"] = "url";
            link["url"] = favorite.Url;
            children.Add(link);
        }

        folder["children"] = children;
        folder["date_modified"] = Time(now);

        // A checksum left from before would disagree with the edit; the engine takes a file with none.
        file.Remove("checksum");
        return (Save(file), created);
    }

    /// <summary>
    /// The preferences with every registered extension refused, and the ids Daoris now holds refused:
    /// those it refused before and those it refused now, never one the list already held for the person.
    /// Null when the preferences cannot be read.
    /// </summary>
    public static (string Json, IReadOnlyList<string> Refused)? Refuse(string? preferences, IReadOnlyList<string> registered, IReadOnlyList<string> refusedBefore)
    {
        if (Preferences(preferences) is not { } prefs) return null;
        var list = UninstallList(prefs);
        var held = list.Select(AsString).OfType<string>().ToHashSet(StringComparer.Ordinal);

        var refused = new SortedSet<string>(refusedBefore.Where(held.Contains), StringComparer.Ordinal);
        foreach (var id in registered.Where(IsExtensionId).Distinct(StringComparer.Ordinal))
        {
            if (held.Add(id))
            {
                list.Add(id);
                refused.Add(id);
            }
        }

        return (Save(prefs), [.. refused]);
    }

    /// <summary>The preferences offering again what Daoris refused, and only that. Null when they cannot be read.</summary>
    public static string? Offer(string? preferences, IReadOnlyList<string> refusedByDaoris)
    {
        if (Preferences(preferences) is not { } prefs) return null;
        var list = UninstallList(prefs);
        foreach (var item in list.Where(item => AsString(item) is { } id && refusedByDaoris.Contains(id)).ToList())
        {
            list.Remove(item);
        }

        return Save(prefs);
    }

    /// <summary>The preferences with the bookmarks bar shown on every tab. Null when they cannot be read.</summary>
    public static string? ShowBar(string? preferences)
    {
        if (Preferences(preferences) is not { } prefs) return null;
        if (prefs["bookmark_bar"] is not JsonObject bar)
        {
            bar = [];
            prefs["bookmark_bar"] = bar;
        }

        bar["show_on_all_tabs"] = true;
        return Save(prefs);
    }

    public static EngineRecord ReadRecord(string? json)
    {
        try
        {
            if (json is not null && JsonNode.Parse(json) is JsonObject record)
            {
                var refused = record["refused"] is JsonArray ids
                    ? ids.Select(AsString).OfType<string>().Where(IsExtensionId).ToList()
                    : [];
                var shown = record["barShown"] is JsonValue flag && flag.TryGetValue<bool>(out var b) && b;
                return new EngineRecord(refused, shown);
            }
        }
        catch (JsonException)
        {
            // A record Daoris cannot read is no record: nothing was refused by it, the bar never shown.
        }

        return new EngineRecord([], false);
    }

    public static string WriteRecord(EngineRecord record) =>
        JsonSerializer.Serialize(new { refused = record.Refused, barShown = record.BarShown }, Indented).ReplaceLineEndings("\n") + "\n";

    private static JsonObject? Preferences(string? json)
    {
        if (json is null) return [];
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonArray UninstallList(JsonObject prefs)
    {
        if (prefs["extensions"] is not JsonObject extensions)
        {
            extensions = [];
            prefs["extensions"] = extensions;
        }

        if (extensions["external_uninstalls"] is not JsonArray list)
        {
            list = [];
            extensions["external_uninstalls"] = list;
        }

        return list;
    }

    private static JsonObject Fresh(DateTimeOffset now)
    {
        JsonObject Root(int id, string guid, string name)
        {
            var root = Node(id, guid, name, now);
            root["type"] = "folder";
            root["date_modified"] = "0";
            root["children"] = new JsonArray();
            return root;
        }

        return new JsonObject
        {
            ["roots"] = new JsonObject
            {
                ["bookmark_bar"] = Root(1, BarGuid, "Bookmarks bar"),
                ["other"] = Root(2, OtherGuid, "Other bookmarks"),
                ["synced"] = Root(3, MobileGuid, "Mobile bookmarks"),
            },
            ["version"] = 1,
        };
    }

    private static JsonObject Node(long id, string guid, string name, DateTimeOffset now) => new()
    {
        ["date_added"] = Time(now),
        ["date_last_used"] = "0",
        ["guid"] = guid,
        ["id"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["name"] = name,
    };

    /// <summary>The engine's time: microseconds since 1601, as text.</summary>
    private static string Time(DateTimeOffset now) =>
        ((now.UtcTicks - Epoch1601) / 10).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The same id for the same page each start, in the form the engine accepts.</summary>
    public static string GuidFor(string url)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(url));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x40);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var hex = Convert.ToHexStringLower(hash);
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    private static long MaxId(JsonNode? node) => node switch
    {
        JsonObject o => Math.Max(
            long.TryParse(Text(o, "id"), out var id) ? id : 0,
            o.Select(pair => MaxId(pair.Value)).DefaultIfEmpty(0).Max()),
        JsonArray a => a.Select(MaxId).DefaultIfEmpty(0).Max(),
        _ => 0,
    };

    private static string Save(JsonObject file) => file.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n";

    private static string? AsString(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;

    private static string? Text(JsonObject node, string name) =>
        node.TryGetPropertyValue(name, out var value) && value is JsonValue text && text.TryGetValue<string>(out var s) ? s : null;
}
