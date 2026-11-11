using System.Text.Json;
using static Daoris.Driver.PluginInterfaceFields;

namespace Daoris.Driver;

/// <summary>A view a plugin shows, as a tree Daoris draws (the plugin UI design's §4.1), asked at <c>hook/view/read</c>.</summary>
/// <param name="Id">Its id among the plugin's views: lowercase letters, digits and dashes.</param>
/// <param name="Title">The words its section or its tab is headed by.</param>
public sealed record PluginViewDeclared(string Id, AuthoredText Title)
{
    /// <summary>A path inside the plugin's folder to its tab's icon; null for the plugin's own icon or monogram.</summary>
    public string? Icon { get; init; }

    /// <summary>Where it may stand: <c>page</c>, a section of its plugin's page, and <c>side</c>, a tab the person adds.</summary>
    public IReadOnlyList<string> Where { get; init; } = [PluginViewReader.Page];

    /// <summary>The kinds of place it is about, asked with the place in force.</summary>
    public IReadOnlyList<string> Places { get; init; } = [PluginViewReader.Machine];

    /// <summary>What it is told of the window beyond its place: ids and names of these, never content.</summary>
    public IReadOnlyList<string> Context { get; init; } = [];
}

/// <summary>A view entry read: the view, or the problem that refuses it alone, by its id where it has one.</summary>
internal sealed record PluginViewRead(PluginViewDeclared? View, string? Id, string? Problem);

/// <summary>Reads one entry of <c>interfaces.views</c>, each field as the plugin UI design's §3.3 declares it.</summary>
internal static class PluginViewReader
{
    public const string Page = "page";

    public const string Side = "side";

    public const string Machine = "machine";

    public static readonly IReadOnlyList<string> Stands = [Side, Page];

    public static readonly IReadOnlyList<string> PlaceKinds = [Machine, "workspace", "repository"];

    public static readonly IReadOnlyList<string> Told = ["session", "quest"];

    /// <summary>The one body this build draws: a tree. A frame of the plugin's own waits on the design's §4.2.</summary>
    public const string Drawn = "drawn";

    public static PluginViewRead Read(JsonElement row, IReadOnlyList<PluginViewDeclared> before)
    {
        var id = Text(row, "id");
        if (id is null || !PluginIds.NameShape.IsMatch(id))
            return new(null, null, "a declared view needs an `id` — lowercase letters, digits and dashes, at most 40.");
        PluginViewRead Refused(string problem) => new(null, id, problem);
        if (before.Any(view => view.Id == id)) return Refused($"view `{id}` is declared twice.");
        if (!Words(row, "title", PluginCommandReader.TitleMost, oneLine: true, null, out var title) || title is null)
            return Refused($"view `{id}` needs a `title`: words on one line of at most {PluginCommandReader.TitleMost}.");
        string? icon = null;
        if (row.TryGetProperty("icon", out _))
        {
            icon = Text(row, "icon");
            if (icon is null || Path.IsPathRooted(icon) || icon.Replace('\\', '/').Split('/').Any(part => part is ".." or ""))
                return Refused($"view `{id}`'s `icon` must be a path inside the plugin's folder.");
        }

        if (Closed(row, "where", Stands, [Page], atLeastOne: true) is not { } where)
            return Refused($"view `{id}`'s `where` must list where it may stand, at least one, each once: {Listed(Stands)}.");
        if (Closed(row, "places", PlaceKinds, [Machine], atLeastOne: true) is not { } places)
            return Refused($"view `{id}`'s `places` must list the kinds of place it is about, at least one, each once: {Listed(PlaceKinds)}.");
        if (Closed(row, "context", Told, [], atLeastOne: false) is not { } context)
            return Refused($"view `{id}`'s `context` must list what it is told of the window, each once: {Listed(Told)}.");
        if (row.TryGetProperty("body", out _) && Text(row, "body") is var body && body != Drawn)
        {
            return Refused(body == "frame"
                ? $"view `{id}` draws its own body (`frame`), which this build does not draw: its `body` is `{Drawn}`."
                : $"view `{id}`'s `body` must be `{Drawn}`.");
        }

        return new(new(id, title) { Icon = icon, Where = where, Places = places, Context = context }, id, null);
    }
}
