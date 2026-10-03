using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What a plugin's icon draws as: its type (<c>svg</c>, <c>png</c>) and its bytes as a data URI, or why it does not draw.</summary>
/// <param name="Type"><c>svg</c> or <c>png</c>, or null where nothing draws.</param>
/// <param name="DataUri">The icon's own bytes, <c>data:image/svg+xml;base64,…</c> or <c>data:image/png;base64,…</c>: what the page is handed, never a path.</param>
/// <param name="Problem">Why the declared icon does not draw, in a sentence naming it as written; null with none declared or one that draws.</param>
public sealed record PluginIconRead(string? Type, string? DataUri, string? Problem)
{
    public static PluginIconRead None { get; } = new(null, null, null);
}

/// <summary>
/// A plugin's icon (PLUGUI2, D140; the catalogue design §3): the manifest's <c>icon</c>, a path inside the plugin's folder to
/// an SVG or a PNG. The catalogue reads the path (<see cref="Declared"/>), and whoever draws or lists the icon judges its
/// file (<see cref="Read"/>).
/// </summary>
/// <remarks>
/// <para><b>The CLI's <c>plugins.ts</c> is the twin</b> (<c>readIcon</c>): the same rules in the same order, the same
/// sentences, held by one table (<c>PluginIconTests</c>, which <c>plugin-icons.test.ts</c> parses). They share no code.</para>
///
/// <para>🔴 <b>An icon's problem never refuses the plugin.</b> An icon is how a plugin is recognised, never what it does.</para>
///
/// <para><b>The page is handed the bytes, never the path</b>, as a data URI, and draws an SVG as an image: none of its
/// scripts run, nothing outside it loads, none of its styles reach the page. So an SVG is checked only for being one and
/// for not expanding (an entity declaration), and a PNG for its pixels, which a small file can inflate into many.</para>
/// </remarks>
public static class PluginIcon
{
    /// <summary>The most an icon file may be: it is drawn at 48 px at most, and the list carries every plugin's.</summary>
    public const int MaxBytes = 32 * 1024;

    /// <summary>The most a PNG icon may be on a side: 48 px at any screen's density, and a bound on what it inflates to.</summary>
    public const int MaxPixels = 512;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly Regex SvgElement = new(@"<svg[\s>/]", RegexOptions.CultureInvariant);

    /// <summary>
    /// The manifest's <c>icon</c> by the manifest's rules, in order: none for <c>null</c>; text that is not blank; a path
    /// inside the folder, names joined by <c>/</c> with none empty, <c>.</c> or <c>..</c>, and no <c>\</c> or <c>:</c>; an
    /// <c>.svg</c> or a <c>.png</c>, case aside. The path as written, or the first rule's sentence.
    /// </summary>
    public static (string? Icon, string? Problem) Declared(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return (null, null);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            return (null, "`icon` must be the path of an .svg or .png file in the plugin's folder.");
        }

        var icon = value.GetString()!;
        var names = icon.Split('/');
        if (icon.Contains('\\') || icon.Contains(':') || names.Any(name => name is "" or "." or ".."))
        {
            return (null, $"`icon` `{icon}` is not a path inside the plugin's folder: it is written from the folder, with `/` "
                + "between names and no `.` or `..`.");
        }

        return TypeOf(icon) is null
            ? (null, $"`icon` `{icon}` is neither an .svg nor a .png file.")
            : (icon, null);
    }

    /// <summary>
    /// The icon a manifest declares, judged as a file in <paramref name="folder"/>, by the file's rules in order: there and a
    /// file; at most <see cref="MaxBytes"/>; a PNG that starts with its signature and header chunk, at most
    /// <see cref="MaxPixels"/> on a side; an SVG that is UTF-8 text holding an <c>&lt;svg</c> element and declaring no
    /// entity. A manifest's own problem is said first.
    /// </summary>
    public static PluginIconRead Read(string folder, PluginManifest manifest)
    {
        if (manifest.IconProblem is { } declared) return new(null, null, declared);
        if (manifest.Icon is not { } icon) return PluginIconRead.None;

        var path = Path.Combine([folder, .. icon.Split('/')]);
        PluginIconRead Refused(string why) => new(null, null, $"`icon` `{icon}` {why}");

        var file = new FileInfo(path);
        if (!file.Exists) return Refused("is not a file in the plugin's folder.");
        if (file.Length > MaxBytes) return Refused("is larger than 32 KiB, the most an icon may be.");

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Refused($"could not be read: {error.Message}");
        }

        // Read once, so the bytes judged are the bytes handed.
        if (bytes.Length > MaxBytes) return Refused("is larger than 32 KiB, the most an icon may be.");

        if (TypeOf(icon) == "png")
        {
            if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(PngSignature) || Encoding.ASCII.GetString(bytes, 12, 4) != "IHDR")
            {
                return Refused("is not a PNG image.");
            }

            var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16));
            var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20));
            if (width == 0 || height == 0) return Refused("is not a PNG image.");
            if (width > MaxPixels || height > MaxPixels)
            {
                return Refused($"is {width}×{height} pixels, and an icon is at most {MaxPixels} on a side.");
            }

            return new("png", "data:image/png;base64," + Convert.ToBase64String(bytes), null);
        }

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Refused("is not an SVG image.");
        }

        if (text.Contains('\0') || !SvgElement.IsMatch(text)) return Refused("is not an SVG image.");
        if (text.Contains("<!ENTITY", StringComparison.Ordinal)) return Refused("declares an XML entity, which an icon may not.");
        return new("svg", "data:image/svg+xml;base64," + Convert.ToBase64String(bytes), null);
    }

    private static string? TypeOf(string icon) =>
        icon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? "svg"
        : icon.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "png"
        : null;
}
