using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace Daoris.Driver;

/// <summary>What a package file says it is, read before anything is extracted (the distribution design §5.7 step 1).</summary>
/// <param name="Package">Its package id: the publisher's, global on a package source, never the plugin's id.</param>
/// <param name="Version">Its version, which must be its plugin's (§5.1).</param>
/// <param name="ApiVersion">The plugin API it speaks: the major number of its <c>DaorisPlugin</c> type's version.</param>
/// <param name="Sha512">The SHA-512 of the whole file in standard base64, as a NuGet catalog leaf's <c>packageHash</c> is.</param>
public sealed record PluginPackageRead(string Package, string Version, int ApiVersion, string Sha512);

/// <summary>A package installed: its plugin's manifest as written, and the record of where it came from.</summary>
public sealed record PluginPackageInstalled(PluginManifest Manifest, PluginPackageOrigin Origin);

/// <summary>
/// A Daoris plugin package and its reader, offline (PLUGDIST1a, D120; the distribution design §5.1 and §5.7). A
/// package is a NuGet <c>.nupkg</c>, a zip, of the custom package type <see cref="Type"/> alone, whose type version's
/// major number is the plugin API it speaks, with no dependencies, and its plugin folder under <see cref="Root"/>.
/// </summary>
/// <remarks>
/// <para><b>Read before extract.</b> The nuspec's id, version, type and dependencies, and every entry under
/// <c>plugin/</c>, are judged before anything is written: a refusal leaves the home as it was. The file is opened
/// once, shared for reading only, so what is hashed is what is extracted.</para>
///
/// <para><b>The <c>plugin/</c> guard.</b> Only <c>plugin/**</c> is extracted, into a stage under the home that the
/// catalogue skips; NuGet's own parts at the package root are never written. An entry's name is read as NuGet reads
/// it (unescaped, either slash), and one that is rooted, holds <c>..</c> or a drive, or would land outside the stage
/// refuses the whole package. A name twice, as Windows compares names, refuses it too.</para>
///
/// <para><b>Then the catalogue's own reader</b> (<see cref="PluginCatalog.ReadAsWritten"/>,
/// <see cref="PluginCatalog.RefusedByThisBuild"/>) reads the stage, the plugin's version is held to the package's and
/// its API to the type's, and <see cref="PluginInstall"/> adds it, never replacing an installed plugin, with the
/// package recorded in <see cref="PluginSource.FileName"/>.</para>
///
/// <para>No NuGet client, and no network: a package source over HTTP is PLUGDIST1c's, and hands this the file it
/// fetched. 🔴 Nothing a plugin declares runs here; the loop starts it at its next look.</para>
/// </remarks>
public static class PluginPackage
{
    /// <summary>The package type a Daoris plugin is (D120 §4). NuGet compares type names without case.</summary>
    public const string Type = "DaorisPlugin";

    /// <summary>Where a package keeps its plugin folder.</summary>
    public const string Root = "plugin/";

    /// <summary>A package file's extension.</summary>
    public const string Extension = ".nupkg";

    /// <summary>What a package file says it is, or why it is no plugin this build installs. Nothing is written.</summary>
    public static (PluginPackageRead? Package, string? Refusal) Read(string file)
    {
        try
        {
            using var opened = Opened.Open(file);
            return (opened.Read, null);
        }
        catch (Refused refused)
        {
            return (null, refused.Message);
        }
    }

    /// <summary>
    /// Install a package file's plugin: read, extract <c>plugin/**</c> into a stage under the home, read the stage
    /// with the catalogue's reader, and add it through <see cref="PluginInstall"/>, recording the package and the
    /// folder that held the file as its source. The stage is gone either way.
    /// </summary>
    /// <exception cref="DriverException">Refused, installed already, or the extraction failed; nothing is installed.</exception>
    public static PluginPackageInstalled Install(string home, string file, IEnumerable<string> reservedHarnesses)
    {
        var reserved = reservedHarnesses.ToList();
        var root = Path.Combine(home, PluginCatalog.Folder);
        string? stage = null;
        try
        {
            using var opened = Opened.Open(file);
            var read = opened.Read;
            Directory.CreateDirectory(root);
            stage = Path.Combine(root, $".unpacking-{Guid.NewGuid():N}");
            opened.Extract(stage);

            var manifest = ReadStage(stage, read, reserved);
            var target = Path.Combine(root, manifest.Id);
            if (Directory.Exists(target)) throw new Refused(Installed(manifest.Id, target));

            var origin = new PluginPackageOrigin(read.Package, read.Version, read.Sha512, Path.GetDirectoryName(Path.GetFullPath(file))!);
            return new(PluginInstall.Add(home, stage, origin, reserved), origin);
        }
        catch (Refused refused)
        {
            throw new DriverException($"{refused.Message} Nothing was installed.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new DriverException($"{Path.GetFileName(file)} was not installed: {error.Message} Nothing was installed.");
        }
        finally
        {
            if (stage is not null) Forget(stage);
        }
    }

    /// <summary>§5.7 step 3: the stage read as the catalogue reads a plugin, then held to the package that carried it.</summary>
    private static PluginManifest ReadStage(string stage, PluginPackageRead read, IReadOnlyCollection<string> reserved)
    {
        var said = Said(read.Package, read.Version);
        var path = Path.Combine(stage, PluginCatalog.ManifestName);
        var (manifest, problem) = PluginCatalog.ReadAsWritten(PluginInstall.IdOf(path), path);
        if (problem is not null) throw new Refused($"{said}'s `{PluginCatalog.ManifestName}`: {problem}");
        if (PluginCatalog.RefusedByThisBuild(manifest, reserved) is { } refused) throw new Refused($"{said}'s plugin `{manifest.Id}` {refused}");
        if (!string.Equals(manifest.Version, read.Version, StringComparison.OrdinalIgnoreCase))
        {
            throw new Refused($"{said} holds plugin `{manifest.Id}` "
                + (manifest.Version.Length > 0 ? $"at version `{manifest.Version}`" : "with no version")
                + " — a package's version is its plugin's, so the two must be one.");
        }

        if (manifest.ApiVersion != read.ApiVersion)
        {
            throw new Refused($"{said}: its type says plugin API {read.ApiVersion}, and its `{PluginCatalog.ManifestName}` says "
                + $"{manifest.ApiVersion} — the type's version is the API its plugin speaks.");
        }

        return manifest;
    }

    /// <summary>§5.7 step 4: an add never replaces, and the refusal names where the installed one came from.</summary>
    private static string Installed(string id, string folder)
    {
        var (source, problem) = PluginSource.Read(folder);
        var from = source is not null
            ? $", from {source.Said}"
            : problem is not null ? ", and its record of where it came from does not read" : ", with no record of where it came from";
        return $"plugin `{id}` is already installed on this machine{from} — a package installs a plugin not yet here. "
            + $"`daoris plugin remove {id}` takes it out first, and what it kept stays where it is.";
    }

    private static string Said(string package, string version) => $"package `{package}` {version}";

    private static void Forget(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A refusal of the package as it stands; the caller says whether anything was installed.</summary>
    private sealed class Refused(string message) : Exception(message);

    /// <summary>One entry under <c>plugin/</c>: its path inside the plugin folder, in <c>/</c> form, and whether it is a folder.</summary>
    private sealed record Part(ZipArchiveEntry Entry, string Path, bool Folder);

    /// <summary>A package file, open for reading only, judged whole: what it says it is, and the plugin's entries.</summary>
    private sealed class Opened : IDisposable
    {
        private readonly FileStream _stream;
        private readonly ZipArchive _zip;
        private readonly List<Part> _parts;

        public PluginPackageRead Read { get; }

        private Opened(FileStream stream, ZipArchive zip, PluginPackageRead read, List<Part> parts) =>
            (_stream, _zip, Read, _parts) = (stream, zip, read, parts);

        public void Dispose()
        {
            _zip.Dispose();
            _stream.Dispose();
        }

        public static Opened Open(string file)
        {
            var name = System.IO.Path.GetFileName(file);
            if (!File.Exists(file))
            {
                throw new Refused($"no file {file} — `plugins install` takes a package file, `<id>.<version>{Extension}`.");
            }

            FileStream stream;
            try
            {
                // Shared for reading only while it is read, hashed and extracted: what is hashed is what lands.
                stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new Refused($"{name} could not be read: {error.Message}");
            }

            try
            {
                var sha512 = Convert.ToBase64String(SHA512.HashData(stream));
                stream.Position = 0;
                ZipArchive zip;
                try
                {
                    zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                }
                catch (InvalidDataException error)
                {
                    throw new Refused($"{name} is not a package: it does not open as a zip ({error.Message}).");
                }

                try
                {
                    var (read, parts) = Judge(zip, name, sha512);
                    return new Opened(stream, zip, read, parts);
                }
                catch
                {
                    zip.Dispose();
                    throw;
                }
            }
            catch (IOException error)
            {
                stream.Dispose();
                throw new Refused($"{name} could not be read: {error.Message}");
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// §5.7 step 2: <c>plugin/**</c> alone, into the stage. Every part was judged inside it already; each
        /// target is checked again against the stage's whole path, since a refusal here costs nothing.
        /// </summary>
        public void Extract(string stage)
        {
            var whole = System.IO.Path.GetFullPath(stage);
            Directory.CreateDirectory(whole);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var part in _parts)
            {
                var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(whole, part.Path.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                if (!target.StartsWith(whole + System.IO.Path.DirectorySeparatorChar, comparison))
                {
                    throw new Refused($"{Said(Read.Package, Read.Version)} holds `{part.Entry.FullName}`, which would land outside its plugin's folder "
                        + "— the whole package is refused.");
                }

                if (part.Folder)
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                part.Entry.ExtractToFile(target, overwrite: false);
            }
        }

        /// <summary>§5.1 and §5.7 step 1: the nuspec, then every entry under <c>plugin/</c>, each refused by name.</summary>
        private static (PluginPackageRead Read, List<Part> Parts) Judge(ZipArchive zip, string name, string sha512)
        {
            var entries = zip.Entries.Select(entry => (Entry: entry, Name: entry.FullName.Replace('\\', '/'))).ToList();
            var nuspecs = entries
                .Where(entry => !entry.Name.Contains('/') && entry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (nuspecs.Count == 0) throw new Refused($"{name} is not a package: it holds no `.nuspec` at its root.");
            if (nuspecs.Count > 1) throw new Refused($"{name} is not a package: it holds {nuspecs.Count} `.nuspec` files at its root, and a package holds one.");
            var nuspec = nuspecs[0];

            XDocument document;
            try
            {
                // A nuspec is data: no DTD, and nothing it names is fetched.
                using var reader = XmlReader.Create(nuspec.Entry.Open(),
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = true });
                document = XDocument.Load(reader);
            }
            catch (Exception error) when (error is XmlException or InvalidDataException or IOException)
            {
                throw new Refused($"{name}'s `{nuspec.Name}` does not read: {error.Message}");
            }

            var metadata = Child(document.Root, "metadata")
                ?? throw new Refused($"{name}'s `{nuspec.Name}` has no `metadata` — a nuspec names its package there.");
            var id = Child(metadata, "id")?.Value.Trim() ?? "";
            if (!PluginSource.IsPackageId(id)) throw new Refused($"{name}'s id `{id}` is not a package id.");
            var version = Child(metadata, "version")?.Value.Trim() ?? "";
            if (!PluginSource.IsPackageVersion(version)) throw new Refused($"package `{id}`'s version `{version}` is not a package version.");
            var said = Said(id, version);

            // The type names what the package is; a package without it is not a plugin, whatever its name says.
            var types = (Child(metadata, "packageTypes")?.Elements() ?? [])
                .Where(element => element.Name.LocalName == "packageType")
                .Select(element => (Name: element.Attribute("name")?.Value ?? "", Version: element.Attribute("version")?.Value))
                .ToList();
            var own = types.Where(type => string.Equals(type.Name, Type, StringComparison.OrdinalIgnoreCase)).ToList();
            if (own.Count == 0)
            {
                throw new Refused(types.Count == 0
                    ? $"{said} is not a Daoris plugin: it declares no package type, which NuGet reads as a library (`Dependency`), and a plugin's is `{Type}`."
                    : $"{said} is not a Daoris plugin: it is of the type {Names(types.Select(type => type.Name))}, and a plugin's is `{Type}`.");
            }

            var others = types.Where(type => !string.Equals(type.Name, Type, StringComparison.OrdinalIgnoreCase)).Select(type => type.Name).ToList();
            if (others.Count > 0) throw new Refused($"{said} is of the type `{Type}` and {Names(others)} — a Daoris plugin package is of that type alone.");

            // The type's version is the plugin API (D120 §4), read before anything is extracted: D64's rule, both numbers named.
            var typeVersion = own[0].Version;
            if (string.IsNullOrWhiteSpace(typeVersion))
            {
                throw new Refused($"{said} names no version for its type `{Type}`, which is the plugin API it speaks, like `{PluginCatalog.ApiVersion}.0`.");
            }

            if (!Version.TryParse(typeVersion, out var api))
            {
                throw new Refused($"{said}: its type's version `{typeVersion}` is not a version like `1.0`, whose first number is the plugin API it speaks.");
            }

            if (api.Major > PluginCatalog.ApiVersion)
            {
                throw new Refused($"{said} needs plugin API {api.Major}, and this build speaks {PluginCatalog.ApiVersion} — "
                    + $"update Daoris, or use a plugin written for {PluginCatalog.ApiVersion}.");
            }

            if (api.Major < 1) throw new Refused($"{said}: its type's version `{typeVersion}` names no plugin API — its first number is the API, like `1.0`.");

            // No dependencies (§5.1): a plugin carries everything it runs, and Daoris installs nothing else.
            var dependencies = (Child(metadata, "dependencies")?.Descendants() ?? [])
                .Where(element => element.Name.LocalName == "dependency")
                .Select(element => element.Attribute("id")?.Value ?? "")
                .ToList();
            if (dependencies.Count > 0)
            {
                throw new Refused($"{said} declares dependencies ({Names(dependencies)}) — a plugin carries everything it runs, "
                    + "and Daoris installs nothing else.");
            }

            // The plugin/ guard: every part judged before one is written.
            var parts = new List<Part>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (entry, slashed) in entries)
            {
                // As NuGet reads a part's name: unescaped, then either slash a separator.
                var read = Uri.UnescapeDataString(slashed).Replace('\\', '/');
                if (!read.StartsWith(Root, StringComparison.Ordinal)) continue;
                var inside = read[Root.Length..];
                if (inside.Length == 0) continue;
                var folder = inside.EndsWith('/');
                var path = folder ? inside.TrimEnd('/') : inside;
                if (!Tools.IsInside(path))
                {
                    throw new Refused($"{said} holds `{entry.FullName}`, which would land outside its plugin's folder — the whole package is refused.");
                }

                if (!folder && !seen.Add(path))
                {
                    throw new Refused($"{said} holds `{entry.FullName}` twice, as this machine compares names — the whole package is refused.");
                }

                parts.Add(new Part(entry, path, folder));
            }

            if (!parts.Any(part => !part.Folder && part.Path == PluginCatalog.ManifestName))
            {
                throw new Refused($"{said} holds no `{Root}{PluginCatalog.ManifestName}` — a plugin package keeps its plugin folder under `{Root}`.");
            }

            return (new PluginPackageRead(id, version, api.Major, sha512), parts);
        }

        private static XElement? Child(XElement? element, string name) =>
            element?.Elements().FirstOrDefault(child => child.Name.LocalName == name);

        private static string Names(IEnumerable<string> names) => string.Join(" and ", names.Select(each => $"`{each}`"));
    }
}

/// <summary>
/// `daoris-driver plugins install &lt;file.nupkg&gt;` (PLUGDIST1a, the distribution design §6.4): a package file's plugin
/// installed from a terminal, through <see cref="PluginPackage.Install"/>. A package source's <c>install &lt;Id&gt;</c>
/// is PLUGDIST1c's.
/// </summary>
/// <remarks>
/// <para><b>Why the driver's.</b> The package reader is the driver's alone (D120 §4): the CLI reads and lists the record
/// a package leaves, and its <c>daoris plugin install</c> says this door is where a package is installed.</para>
///
/// <para>The printing lives in the library so a test runs the whole door in-process. Exit codes keep the family
/// contract: 0 installed · 2 it could not do what was asked, a refused package among them.</para>
/// </remarks>
public static class PluginPackageCommand
{
    public const string Usage = "usage: daoris-driver plugins install <file.nupkg>";

    /// <param name="reservedHarnesses">The harnesses this build carries; null is <see cref="AdapterSet.Built"/>'s.</param>
    public static int Install(string[] args, TextWriter output, string? home, IEnumerable<string>? reservedHarnesses = null)
    {
        try
        {
            string? file = null;
            foreach (var arg in args)
            {
                if (arg.StartsWith("--", StringComparison.Ordinal) || file is not null)
                {
                    throw new DriverException($"`{arg}` is not something `plugins install` takes.\n{Usage}");
                }

                file = arg;
            }

            if (file is null)
            {
                throw new DriverException($"`plugins install` needs a package file — e.g. `daoris-driver plugins install ./Acme.Gate.1.0.0{PluginPackage.Extension}`.\n{Usage}");
            }

            if (!file.EndsWith(PluginPackage.Extension, StringComparison.OrdinalIgnoreCase) && !File.Exists(file))
            {
                throw new DriverException($"`{file}` is not a package file — `plugins install` takes a `{PluginPackage.Extension}` file, "
                    + "and `daoris plugin add <folder>` a plugin's folder.");
            }

            if (home is null) throw new DriverException($"no Daoris home: a plugin is installed under it. Set {DaorisHome.Variable}.");

            var installed = PluginPackage.Install(home, Path.GetFullPath(file), reservedHarnesses ?? AdapterSet.Built().Names);
            var manifest = installed.Manifest;
            var folder = Path.Combine(home, PluginCatalog.Folder, manifest.Id);
            output.WriteLine($"plugins: installed `{manifest.Id}`{(manifest.Version.Length > 0 ? " " + manifest.Version : "")} at {folder}");
            output.WriteLine($"  {Describe(manifest)}.");
            output.WriteLine($"  From {PluginSource.FromPackage(installed.Origin).Said}, sha512 {installed.Origin.Sha512}.");
            var data = Path.Combine(home, PluginCatalog.Folder, PluginCatalog.DataFolder, manifest.Id);
            if (Directory.Exists(data)) output.WriteLine($"  {data} kept, untouched.");
            output.WriteLine("  It takes effect at the driver's next look — a harness on the roster, a hook at the next tick.");
            return 0;
        }
        catch (DriverException error)
        {
            output.WriteLine($"plugins: {error.Message}");
            return 2;
        }
    }

    /// <summary>What a plugin declares and speaks, as the CLI's <c>describe</c> says it.</summary>
    private static string Describe(PluginManifest manifest)
    {
        var parts = new List<string>();
        if (manifest.Harnesses.Count > 0) parts.Add($"declares {string.Join(", ", manifest.Harnesses.Select(harness => harness.Name))}");
        if (manifest.Hooks is { } hooks) parts.Add($"speaks on {string.Join(", ", hooks.Points)}");
        if (manifest.Servers.Count > 0) parts.Add($"hands sessions {string.Join(", ", manifest.Servers.Select(server => server.Name))}");
        return parts.Count > 0 ? string.Join("; ", parts) : "declares nothing and speaks nothing";
    }
}
