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

/// <summary>What the settings file holds, and why a file that was there gave only the defaults.</summary>
public sealed record BrowserSettingsRead(string Extensions, string? Problem);

/// <summary>
/// Daoris's browser's settings (CHR7): <c>&lt;home&gt;/browser/settings.json</c>, which
/// <c>daoris-browser</c> reads each time it starts.
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
        return new(extensions, problem);
    }

    /// <summary>Set the extensions setting for the browser's next start.</summary>
    /// <exception cref="InvalidOperationException">A value that is neither, or a file this could not read.</exception>
    public static void SetExtensions(string home, string extensions)
    {
        if (extensions is not (ExtensionsSetting.Offer or ExtensionsSetting.Refuse))
        {
            throw new InvalidOperationException($"The extensions setting is `offer` or `refuse`, not `{extensions}`.");
        }

        var (file, problem) = Load(home);
        if (problem is not null)
        {
            throw new InvalidOperationException(
                $"{problem}. Fix it, or delete it to start from the defaults: the browser's settings will not write over a file they could not read.");
        }

        file ??= [];
        file["extensions"] = extensions;
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
