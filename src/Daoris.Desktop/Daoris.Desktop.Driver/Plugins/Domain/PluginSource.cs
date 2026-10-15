using System.Text.Encodings.Web;
using System.Text.Json;
using static Daoris.Driver.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The package a plugin was installed from (PLUGDIST1a, D120 §4, the distribution design §5.7 step 5): its
/// package id, its version, the SHA-512 of the package file in standard base64, and the source it came from: a
/// package source's index address, or the whole path of the folder that held the file.
/// </summary>
public sealed record PluginPackageOrigin(string Package, string Version, string Sha512, string Source);

/// <summary>
/// Where an installed plugin came from (PLUG9 c, D103): the folder it was added from, a whole path; the
/// offer of this install it was installed from, by id; or, since PLUGDIST1a (D120), the package it was
/// installed from. Exactly one of the three.
/// </summary>
/// <remarks>
/// <para>It is <see cref="FileName"/> in the plugin's install folder, written into the staged copy before
/// the swap: replaced with the install, removed with it, and absent from a folder copied in by hand, so
/// "no record" is said and never guessed. The catalogue reads nothing but the manifest.</para>
///
/// <para>A twin of the CLI's <c>readPluginSource</c> in <c>plugins.ts</c>: the same file, the same shapes,
/// the same problems in the same words. <c>PluginSourceTests</c> holds the table, and the CLI's
/// <c>plugin-sources.test.ts</c> parses its rows and holds its own to them, cell for cell.</para>
///
/// <para>The record and its rules are pure (STRUCT3a, FLAT1b-i); <c>Read</c>, <c>Write</c> and <c>FromFolder</c> touch the
/// machine through <see cref="PluginFolders"/>, which does the I/O, so this file names none itself.</para>
/// </remarks>
public sealed record PluginSource(string? Folder, string? Offer, PluginPackageOrigin? Package = null)
{
    public const string FileName = ".daoris-source.json";

    public static PluginSource FromOffer(string id) => new(null, id);

    /// <summary>A folder a plugin is added from, recorded as its whole path on this machine.</summary>
    public static PluginSource FromFolder(string folder) => new(PluginFolders.WholePath(folder), null);

    /// <summary>An installed plugin's record: none where there is no file, or the named reason one does not read.</summary>
    public static (PluginSource? Source, string? Problem) Read(string installFolder) =>
        PluginFolders.TextIfThere(Path.Combine(installFolder, FileName)) is { } text
            ? Parse(text)
            : (null, null);

    /// <summary>Written into a staged copy before it is renamed into place, so the record and the install move together.</summary>
    internal static void Write(string installFolder, PluginSource source) =>
        PluginFolders.WriteFile(Path.Combine(installFolder, FileName), source.Json());

    public static PluginSource FromPackage(PluginPackageOrigin package) => new(null, null, package);

    /// <summary>Where it came from, said for a person: a package as the distribution design §6.2 says it.</summary>
    public string Said => Package is { } package
        ? $"{package.Source}, package `{package.Package}` {package.Version}"
        : Offer is { } offer ? $"Daoris's own plugins, offered by this install (`{offer}`)" : Folder!;

    /// <summary>
    /// A NuGet package id: words of letters, digits and underscores joined by dots or dashes, at most 100
    /// characters (NuGet's own rule). ASCII only, as the CLI's twin spells it; <c>\z</c>, since .NET's <c>$</c>
    /// passes a final newline.
    /// </summary>
    private static readonly Regex PackageId = new(@"^[A-Za-z0-9_]+(?:[.-][A-Za-z0-9_]+)*\z", RegexOptions.CultureInvariant);

    /// <summary>A package's version: one to four numbers, then a prerelease label and build metadata, each optional.</summary>
    private static readonly Regex PackageVersion = new(
        @"^[0-9]+(?:\.[0-9]+){0,3}(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z",
        RegexOptions.CultureInvariant);

    /// <summary>A SHA-512 in standard base64: 64 bytes are 86 characters and two of padding.</summary>
    private static readonly Regex Sha512 = new(@"^[A-Za-z0-9+/]{86}==\z", RegexOptions.CultureInvariant);

    /// <summary>Whether a name is a NuGet package id. The CLI's <c>isPackageId</c>.</summary>
    public static bool IsPackageId(string id) => id.Length <= 100 && PackageId.IsMatch(id);

    /// <summary>Whether a text is a package's version. The CLI's <c>isPackageVersion</c>.</summary>
    public static bool IsPackageVersion(string version) => PackageVersion.IsMatch(version);

    /// <summary>An installed plugin's record, from its file's text: the record, or the named reason it does not read.</summary>
    internal static (PluginSource? Source, string? Problem) Parse(string text)
    {
        static (PluginSource?, string?) Unread(string why) => (null, $"`{FileName}` does not read ({why}).");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException error)
        {
            return Unread($"it is not JSON: {error.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Unread("it is not a JSON object");
            var folder = Text(root, "folder");
            var offer = Text(root, "offer");
            var package = Text(root, "package");
            if (package is not null && folder is not null) return Unread("it names both a package and a folder");
            if (package is not null && offer is not null) return Unread("it names both a package and an offer");
            if (folder is not null && offer is not null) return Unread("it names both a folder and an offer");
            if (folder is not null) return Path.IsPathFullyQualified(folder) ? (new PluginSource(folder, null), null) : Unread("its folder is not a whole path");
            if (offer is not null) return PluginCatalog.IsId(offer) ? (new PluginSource(null, offer), null) : Unread("its offer is not a plugin id");
            if (package is null) return Unread("it names no folder, offer or package");

            // A package's record (PLUGDIST1a), each field judged in the CLI's order and words.
            if (!IsPackageId(package)) return Unread($"its package `{package}` is not a package id");
            if (Text(root, "version") is not { } version) return Unread("a package needs its `version`");
            if (!IsPackageVersion(version)) return Unread($"its version `{version}` is not a package version");
            if (Text(root, "sha512") is not { } sha512) return Unread("a package needs its `sha512`");
            if (!Sha512.IsMatch(sha512)) return Unread("its sha512 is not a SHA-512 hash in base64");
            if (Text(root, "source") is not { } source) return Unread("a package needs its `source`");
            if (!Tools.IsAddress(source) && !Path.IsPathFullyQualified(source))
            {
                return Unread($"its source `{source}` is neither a whole path nor an address — https://, or http:// to this machine");
            }

            return (FromPackage(new PluginPackageOrigin(package, version, sha512, source)), null);
        }
    }

    /// <summary>The record's file as written: indented JSON, LF, a final newline.</summary>
    // A hash's `+` and `/` are written as they are, as the CLI's `JSON.stringify` writes them (JsonFile's encoder).
    internal string Json() => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        switch (this)
        {
            case { Package: { } package }:
                writer.WriteString("package", package.Package);
                writer.WriteString("version", package.Version);
                writer.WriteString("sha512", package.Sha512);
                writer.WriteString("source", package.Source);
                break;
            case { Offer: { } offer }:
                writer.WriteString("offer", offer);
                break;
            default:
                writer.WriteString("folder", Folder);
                break;
        }

        writer.WriteEndObject();
    });
}

/// <summary>One thing an update changes (PLUG9 c): `version`, `command`, `points`, `harnesses`, `servers` or (HELPPLUG1b) `skills`, each side as its manifest writes it; empty is none.</summary>
public sealed record PluginChange(string What, string Was, string Now)
{
    /// <summary>What an update changes, from what is installed to what the source now holds, each as written — the CLI's <c>changesBetween</c>.</summary>
    public static IReadOnlyList<PluginChange> Between(PluginManifest was, PluginManifest now)
    {
        static Dictionary<string, string> Said(PluginManifest manifest) => new(StringComparer.Ordinal)
        {
            ["version"] = manifest.Version,
            ["command"] = Line(manifest.Hooks?.Command ?? []),
            ["points"] = string.Join(", ", manifest.Hooks?.Points ?? []),
            ["harnesses"] = string.Join("; ", manifest.Harnesses.Select(harness => $"{harness.Name} ({Line(harness.Command)})")),
            ["servers"] = string.Join("; ", manifest.Servers.Select(server => $"{server.Name} ({Line(server.Command)}){ServerTo(server)}")),
            // HELPPLUG1b: each skill and who it is handed to. Twin: the CLI's `changesBetween`.
            ["skills"] = string.Join("; ", manifest.Skills.Select(skill => $"{skill.Name} to {string.Join(" and ", skill.To)}")),
        };
        var before = Said(was);
        var after = Said(now);
        return [.. new[] { "version", "command", "points", "harnesses", "servers", "skills" }
            .Where(what => before[what] != after[what])
            .Select(what => new PluginChange(what, before[what], after[what]))];
    }

    /// <summary>
    /// Who a server is handed to and what it allows Ask Daoris, as an update's change says it (HELPPLUG1a): nothing for a
    /// server handed to sessions alone, as every server was before, so an update that keeps one changes nothing. Twin: the
    /// CLI's <c>serverTo</c>.
    /// </summary>
    private static string ServerTo(PluginServer server) =>
        (server.To is [PluginServerTo.Sessions] ? "" : $" to {string.Join(" and ", server.To)}")
        + (server.Allow.Count > 0 ? $" allowing {string.Join(", ", server.Allow)}" : "");

    /// <summary>A command as a terminal takes it: a word holding a space quoted, as the proposal card spells it.</summary>
    private static string Line(IEnumerable<string> command) =>
        string.Join(" ", command.Select(word => word.Contains(' ') ? $"\"{word}\"" : word));
}

/// <summary>What updating an installed plugin would do: the source it re-reads, the folder that is, and what changes.</summary>
/// <param name="From">The folder the update copies from, resolved on this machine.</param>
public sealed record PluginUpdatePlan(string Id, PluginSource Source, string From, IReadOnlyList<PluginChange> Changes);
