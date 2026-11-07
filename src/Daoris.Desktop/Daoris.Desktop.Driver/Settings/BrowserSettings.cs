using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// What Daoris's browser does with extensions other software registered for Chrome on this machine:
/// <see cref="Offer"/> them for the person's approval, as the engine does, or
/// <see cref="Refuse"/> them before it starts.
/// </summary>
public static class ExtensionsSetting
{
    public const string Offer = "offer";
    public const string Refuse = "refuse";
}

/// <summary>
/// Which browser sessions drive and the person opens: Daoris's own, the engine it ships,
/// or the person's Edge on a profile of Daoris's.
/// </summary>
public static class BrowserChoice
{
    public const string Daoris = "daoris";
    public const string Edge = "edge";
}

/// <summary>
/// Where a link on Daoris's page opens: in the system's browser, as a link always has, or in
/// Daoris's — whichever <see cref="BrowserChoice"/> the file chooses.
/// </summary>
public static class LinksSetting
{
    public const string System = "system";
    public const string Daoris = "daoris";
}

/// <summary>What the settings file holds, and why a file that exists gave only the defaults.</summary>
public sealed record BrowserSettingsRead(string Extensions, string Browser, string Links, string? Problem);

/// <summary>
/// Daoris's browser's settings: <c>&lt;home&gt;/browser/settings.json</c>, which
/// <c>daoris-browser</c> reads each time it starts, and whose <c>links</c> the page reads at each
/// click, so that one holds at once.
/// </summary>
/// <remarks>
/// <para>The one writer of the file: the window's Browser screen, Ask Daoris and the terminal call
/// <see cref="SetExtensions"/>, <see cref="SetBrowser"/> and <see cref="SetLinks"/>.</para>
///
/// <para><b>An editor never writes over what it could not read</b>, and keeps what it has no field
/// for. A value it does not know is the default, never a guess at a neighbouring one.</para>
/// </remarks>
public static class BrowserSettings
{

    public static string FilePath(string home) => DaorisHome.Entry(home, HomeEntry.BrowserSettings);

    public static BrowserSettingsRead Read(string home)
    {
        var (file, problem) = Load(home);
        var extensions = file is not null && Text(file, "extensions") == ExtensionsSetting.Refuse
            ? ExtensionsSetting.Refuse
            : ExtensionsSetting.Offer;
        var browser = file is not null && Text(file, "browser") == BrowserChoice.Edge ? BrowserChoice.Edge : BrowserChoice.Daoris;
        var links = file is not null && Text(file, "links") == LinksSetting.Daoris ? LinksSetting.Daoris : LinksSetting.System;
        return new(extensions, browser, links, problem);
    }

    /// <summary>Set the extensions setting for the browser's next start.</summary>
    /// <exception cref="HomeFileRefusal">A value that is neither, or a file this could not read; nothing is written.</exception>
    public static void SetExtensions(string home, string extensions)
    {
        if (extensions is not (ExtensionsSetting.Offer or ExtensionsSetting.Refuse))
        {
            throw new HomeFileRefusal(
                HomeFileRefusal.ExtensionsUnknown, $"The extensions setting is `offer` or `refuse`, not `{extensions}`.", ("value", extensions));
        }

        Set(home, "extensions", extensions);
    }

    /// <summary>Choose the browser for the next time one is opened.</summary>
    /// <exception cref="HomeFileRefusal">A value that is neither, or a file this could not read; nothing is written.</exception>
    public static void SetBrowser(string home, string browser)
    {
        if (browser is not (BrowserChoice.Daoris or BrowserChoice.Edge))
        {
            throw new HomeFileRefusal(
                HomeFileRefusal.BrowserUnknown, $"The browser is `daoris` or `edge`, not `{browser}`.", ("value", browser));
        }

        Set(home, "browser", browser);
    }

    /// <summary>Choose where the page's links open.</summary>
    /// <exception cref="HomeFileRefusal">A value that is neither, or a file this could not read; nothing is written.</exception>
    public static void SetLinks(string home, string links)
    {
        if (links is not (LinksSetting.System or LinksSetting.Daoris))
        {
            throw new HomeFileRefusal(
                HomeFileRefusal.LinksUnknown, $"Links open in `system` or `daoris`, not `{links}`.", ("value", links));
        }

        Set(home, "links", links);
    }

    private static void Set(string home, string field, string value)
    {
        var (file, problem) = Load(home);
        if (problem is not null)
        {
            throw new HomeFileRefusal(
                HomeFileRefusal.Unreadable,
                $"{problem}. Fix it, or delete it to start from nothing: Daoris will not write over a file it could not read.",
                ("file", FilePath(home)), ("problem", problem));
        }

        // The edit made onto the file as it stands (`JsonKeeping.Save`): its line ends kept, a key written twice
        // refused, no edit nothing written. Written from the nodes alone, it would write LF over a file in CRLF. The CLI's `setField` does the same.
        var read = file is null ? null : JsonFile.Serialize(file);
        file ??= [];
        file[field] = value;
        JsonKeeping.Save(FilePath(home), read is not null, read ?? "{}\n", JsonFile.Serialize(file));
    }

    private static (JsonObject? File, string? Problem) Load(string home)
    {
        var path = FilePath(home);
        if (!File.Exists(path)) return (null, null);

        JsonNode? file;
        try
        {
            // A key written twice reads as the CLI's `JSON.parse` reads it (the last wins), where a node would throw at the first look.
            file = JsonNode.Parse(JsonKeeping.Once(AtomicFile.ReadText(path)));
        }
        catch (JsonException error)
        {
            return (null, $"{path} is not readable JSON ({error.Message})");
        }

        return file is JsonObject top ? (top, null) : (null, $"{path} is not a JSON object");
    }
}
