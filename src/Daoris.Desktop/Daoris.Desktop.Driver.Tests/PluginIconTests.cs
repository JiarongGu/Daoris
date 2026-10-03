using System.Buffers.Binary;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's icon (PLUGUI2, D140, the catalogue design §3): the manifest's <c>icon</c>, a path inside the plugin's folder
/// to an SVG or a PNG, read by the catalogue, and the file judged where it is drawn. The driver's half of a twin with the
/// CLI's <c>plugins.ts</c>: <c>plugin-icons.test.ts</c> parses <see cref="The_icon_reads_as_the_cli_reads_it"/> and holds
/// its own table to it, cell for cell and in order. They share no code.
/// </summary>
/// <remarks>
/// 🔴 <b>An icon's problem never refuses the plugin</b>: an icon is how a plugin is recognised, never what it does, so a
/// plugin whose icon does not draw is sound, wears its monogram, and says why.
/// </remarks>
public sealed class PluginIconTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-plugin-icon-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Plugin(string id, string? icon)
    {
        var folder = Path.Combine(_home, PluginCatalog.Folder, id);
        Directory.CreateDirectory(folder);
        var field = icon is null ? "" : $", \"icon\": {icon}";
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
            $"{{ \"id\": \"{id}\", \"name\": \"Acme gate\", \"hooks\": {{ \"command\": [\"node\", \"gate.mjs\"], \"points\": [\"quest/consider\"] }}{field} }}");
        return folder;
    }

    /// <summary>
    /// The file a row writes, by its kind; the CLI's table writes the same bytes for the same word. A PNG here is its
    /// signature and its header chunk, which is all the reader judges.
    /// </summary>
    private static void Write(string path, string kind)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        switch (kind)
        {
            case "folder":
                Directory.CreateDirectory(path);
                return;
            case "svg":
                File.WriteAllText(path, Svg);
                return;
            case "svg-big":
                File.WriteAllText(path, Svg.Replace("</svg>", "<!--" + new string('x', 33_000) + "--></svg>"));
                return;
            case "svg-entity":
                File.WriteAllText(path, "<?xml version=\"1.0\"?><!DOCTYPE svg [<!ENTITY a \"aaaa\">]>" + Svg);
                return;
            case "png":
                File.WriteAllBytes(path, Png(64, 64));
                return;
            case "png-huge":
                File.WriteAllBytes(path, Png(1024, 1024));
                return;
            case "text":
                File.WriteAllText(path, "hello");
                return;
            case "empty":
                File.WriteAllBytes(path, []);
                return;
            default:
                throw new ArgumentException($"no file kind `{kind}`");
        }
    }

    private const string Svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16"><circle cx="8" cy="8" r="6"/></svg>""";

    /// <summary>PNG's signature, then its IHDR chunk with the width and the height; then IEND.</summary>
    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[8 + 25 + 12];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), 13);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(bytes, 12);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), (uint)height);
        bytes[24] = 8;
        bytes[25] = 6;
        Encoding.ASCII.GetBytes("IEND").CopyTo(bytes, 8 + 25 + 4);
        return bytes;
    }

    /// <summary>
    /// The icon's table (D140 §3.1): [case, the manifest's <c>icon</c> as JSON or null for none, the file a row writes from
    /// the plugin's folder or null, its kind, what it draws as (<c>svg</c>, <c>png</c>) or null, a fragment of its problem
    /// or null]. The rules are checked in this order and the first problem is said. The twin is
    /// <c>plugin-icons.test.ts</c>'s <c>ICON_ROWS</c>.
    /// </summary>
    [Theory]
    [InlineData("no icon", null, null, null, null, null)]
    [InlineData("null is none", "null", null, null, null, null)]
    [InlineData("an svg", "\"icon.svg\"", "icon.svg", "svg", "svg", null)]
    [InlineData("a png in a folder of its own", "\"assets/icon.png\"", "assets/icon.png", "png", "png", null)]
    [InlineData("an extension in capitals", "\"ICON.SVG\"", "ICON.SVG", "svg", "svg", null)]
    [InlineData("not text", "42", null, null, null, "must be the path of an .svg or .png file in the plugin's folder")]
    [InlineData("blank", "\"  \"", null, null, null, "must be the path of an .svg or .png file in the plugin's folder")]
    [InlineData("a whole path", "\"/icon.svg\"", null, null, null, "is not a path inside the plugin's folder")]
    [InlineData("a drive", "\"C:/icon.svg\"", null, null, null, "is not a path inside the plugin's folder")]
    [InlineData("an address", "\"https://example.com/icon.svg\"", null, null, null, "is not a path inside the plugin's folder")]
    [InlineData("a backslash", "\"assets\\\\icon.svg\"", null, null, null, "is not a path inside the plugin's folder")]
    [InlineData("out of the folder", "\"../icon.svg\"", null, null, null, "is not a path inside the plugin's folder")]
    [InlineData("a dot for the folder", "\"./icon.svg\"", null, null, null, "is not a path inside the plugin's folder")]
    [InlineData("an empty name", "\"assets//icon.svg\"", null, null, null, "is not a path inside the plugin's folder")]
    [InlineData("another format", "\"icon.gif\"", null, null, null, "is neither an .svg nor a .png file")]
    [InlineData("no extension", "\"icon\"", null, null, null, "is neither an .svg nor a .png file")]
    [InlineData("not there", "\"icon.svg\"", null, null, null, "is not a file in the plugin's folder")]
    [InlineData("a folder by that name", "\"icon.svg\"", "icon.svg", "folder", null, "is not a file in the plugin's folder")]
    [InlineData("too large", "\"icon.svg\"", "icon.svg", "svg-big", null, "is larger than 32 KiB")]
    [InlineData("text named png", "\"icon.png\"", "icon.png", "text", null, "is not a PNG image")]
    [InlineData("an svg named png", "\"icon.png\"", "icon.png", "svg", null, "is not a PNG image")]
    [InlineData("too many pixels", "\"icon.png\"", "icon.png", "png-huge", null, "is 1024×1024 pixels, and an icon is at most 512 on a side")]
    [InlineData("empty", "\"icon.svg\"", "icon.svg", "empty", null, "is not an SVG image")]
    [InlineData("text named svg", "\"icon.svg\"", "icon.svg", "text", null, "is not an SVG image")]
    [InlineData("a png named svg", "\"icon.svg\"", "icon.svg", "png", null, "is not an SVG image")]
    [InlineData("an entity", "\"icon.svg\"", "icon.svg", "svg-entity", null, "declares an XML entity, which an icon may not")]
    public void The_icon_reads_as_the_cli_reads_it(string name, string? icon, string? file, string? kind, string? drawn, string? problem)
    {
        var folder = Plugin("acme.gate", icon);
        if (file is not null) Write(Path.Combine(folder, file.Replace('/', Path.DirectorySeparatorChar)), kind!);

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        var read = PluginIcon.Read(entry.Folder, entry.Manifest);

        // 🔴 Never the plugin's problem: it is sound whatever its icon.
        Assert.True(entry.Problem is null, $"{name}: {entry.Problem}");
        Assert.True(drawn == read.Type, $"{name}: drawn as {read.Type ?? "nothing"}, {read.Problem}");
        if (problem is null)
        {
            Assert.True(read.Problem is null, $"{name}: {read.Problem}");
        }
        else
        {
            Assert.Null(read.DataUri);
            Assert.True(read.Problem?.Contains(problem, StringComparison.Ordinal) == true, $"{name}: {read.Problem}");
        }
    }

    [Fact]
    public void An_icon_that_draws_is_handed_as_its_own_bytes_never_its_path()
    {
        var folder = Plugin("acme.gate", "\"assets/icon.svg\"");
        Write(Path.Combine(folder, "assets", "icon.svg"), "svg");

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        var read = PluginIcon.Read(entry.Folder, entry.Manifest);

        Assert.Equal("assets/icon.svg", entry.Manifest.Icon);
        Assert.Equal("data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(Svg)), read.DataUri);
        Assert.DoesNotContain(_home, read.DataUri!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_png_is_handed_as_a_png()
    {
        var folder = Plugin("acme.gate", "\"icon.png\"");
        Write(Path.Combine(folder, "icon.png"), "png");

        var read = PluginIcon.Read(folder, Assert.Single(PluginCatalog.Load(_home).Plugins).Manifest);

        Assert.Equal("png", read.Type);
        Assert.StartsWith("data:image/png;base64,", read.DataUri);
        Assert.Equal(Png(64, 64), Convert.FromBase64String(read.DataUri!["data:image/png;base64,".Length..]));
    }

    /// <summary>The sentence names the value as written, so the author finds the line to fix.</summary>
    [Fact]
    public void A_problem_names_the_icon_as_the_manifest_writes_it()
    {
        Plugin("acme.gate", "\"../icon.svg\"");

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);

        Assert.Null(entry.Manifest.Icon);
        Assert.Equal(
            "`icon` `../icon.svg` is not a path inside the plugin's folder: it is written from the folder, with `/` "
            + "between names and no `.` or `..`.",
            entry.Manifest.IconProblem);
        Assert.True(entry.Contributes);
    }

    /// <summary>A refused plugin takes nothing, and is still recognised: its icon is not something it contributes.</summary>
    [Fact]
    public void A_refused_plugin_keeps_its_icon()
    {
        var other = Path.Combine(_home, PluginCatalog.Folder, "acme.later");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, PluginCatalog.ManifestName), """
            { "id": "acme.later", "icon": "icon.svg", "harnesses": [ { "name": "claude-code", "command": ["claude"] } ] }
            """);
        Write(Path.Combine(other, "icon.svg"), "svg");

        var refused = PluginCatalog.Load(_home, AdapterSet.Built().Names).Plugins.Single(p => p.Manifest.Id == "acme.later");

        Assert.NotNull(refused.Problem);
        Assert.Empty(refused.Manifest.Harnesses);
        Assert.Equal("svg", PluginIcon.Read(refused.Folder, refused.Manifest).Type);
    }

    /// <summary>An offer is read as written (PLUG9): its icon is read the same way.</summary>
    [Fact]
    public void A_manifest_read_as_written_reads_its_icon()
    {
        var folder = Plugin("acme.gate", "\"icon.svg\"");

        var (manifest, problem) = PluginCatalog.ReadAsWritten("acme.gate", Path.Combine(folder, PluginCatalog.ManifestName));

        Assert.Null(problem);
        Assert.Equal("icon.svg", manifest.Icon);
        Assert.Null(manifest.IconProblem);
    }
}
