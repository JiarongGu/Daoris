using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What Daoris's browser does with extensions other software registered for Chrome on this machine
/// (CHR7): <see cref="Offer"/> them for the person's approval, as the engine does, or
/// <see cref="Refuse"/> them before it starts.
/// </summary>
public static class ExtensionsSetting
{
    public const string Offer = "offer";
    public const string Refuse = "refuse";
}

/// <summary>
/// Which browser sessions drive and the person opens (BRW12, D84): Daoris's own, the engine it ships,
/// or the person's Edge on a profile of Daoris's.
/// </summary>
public static class BrowserChoice
{
    public const string Daoris = "daoris";
    public const string Edge = "edge";
}

/// <summary>
/// Where a link on Daoris's page opens (BRW7): in the system's browser, as a link always has, or in
/// Daoris's — whichever <see cref="BrowserChoice"/> the file chooses.
/// </summary>
public static class LinksSetting
{
    public const string System = "system";
    public const string Daoris = "daoris";
}

/// <summary>What the settings file holds, and why a file that was there gave only the defaults.</summary>
public sealed record BrowserSettingsRead(string Extensions, string Browser, string Links, string? Problem);

/// <summary>
/// Daoris's browser's settings (CHR7): <c>&lt;home&gt;/browser/settings.json</c>, which
/// <c>daoris-browser</c> reads each time it starts, and whose <c>links</c> the page reads at each
/// click (BRW7), so that one holds at once.
/// </summary>
/// <remarks>
/// <para>🔴 <b>A twin file</b>, as the favorites are. The CLI's <c>browser.ts</c> reads and edits it
/// with its own code, for <c>daoris browser extensions</c>, and each side carries the same test table.
/// A rule changed here is changed there, in the same commit.</para>
///
/// <para><b>An editor never writes over what it could not read</b>, and keeps what it has no field
/// for. A value it does not know is the default, never a guess at a neighbouring one.</para>
/// </remarks>
public static class BrowserSettings
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string FilePath(string home) => Path.Combine(home, "browser", "settings.json");

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
    /// <exception cref="InvalidOperationException">A value that is neither, or a file this could not read.</exception>
    public static void SetExtensions(string home, string extensions)
    {
        if (extensions is not (ExtensionsSetting.Offer or ExtensionsSetting.Refuse))
        {
            throw new InvalidOperationException($"The extensions setting is `offer` or `refuse`, not `{extensions}`.");
        }

        Set(home, "extensions", extensions);
    }

    /// <summary>Choose the browser for the next time one is opened.</summary>
    /// <exception cref="InvalidOperationException">A value that is neither, or a file this could not read.</exception>
    public static void SetBrowser(string home, string browser)
    {
        if (browser is not (BrowserChoice.Daoris or BrowserChoice.Edge))
        {
            throw new InvalidOperationException($"The browser is `daoris` or `edge`, not `{browser}`.");
        }

        Set(home, "browser", browser);
    }

    /// <summary>Choose where the page's links open (BRW7).</summary>
    /// <exception cref="InvalidOperationException">A value that is neither, or a file this could not read.</exception>
    public static void SetLinks(string home, string links)
    {
        if (links is not (LinksSetting.System or LinksSetting.Daoris))
        {
            throw new InvalidOperationException($"Links open in `system` or `daoris`, not `{links}`.");
        }

        Set(home, "links", links);
    }

    private static void Set(string home, string field, string value)
    {
        var (file, problem) = Load(home);
        if (problem is not null)
        {
            throw new InvalidOperationException(
                $"{problem}. Fix it, or delete it to start from the defaults: the browser's settings will not write over a file they could not read.");
        }

        file ??= [];
        file[field] = value;
        var path = FilePath(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, file.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n");
    }

    private static (JsonObject? File, string? Problem) Load(string home)
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

        return file is JsonObject top ? (top, null) : (null, $"{path} is not a JSON object");
    }

    private static string? Text(JsonObject row, string name) =>
        row.TryGetPropertyValue(name, out var value) && value is JsonValue text && text.TryGetValue<string>(out var s) ? s : null;
}
