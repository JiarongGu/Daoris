using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace Daoris.Driver;

/// <summary>
/// A package file, open for reading only, judged whole (PLUGDIST1a, D120; the distribution design §5.7 steps 1 and 2): what it
/// says it is, and the plugin's entries, by the Domain's <see cref="PluginPackageRules"/>. Opened once, shared for reading
/// only, so what is hashed is what is extracted (STRUCT3a: the file is this layer's, its words the Domain's).
/// </summary>
internal sealed class PluginPackageFile : IDisposable
{
    private readonly FileStream _stream;
    private readonly ZipArchive _zip;
    private readonly IReadOnlyList<(int Entry, string Path, bool Folder)> _parts;

    public PluginPackageRead Read { get; }

    private PluginPackageFile(FileStream stream, ZipArchive zip, PluginPackageRead read, IReadOnlyList<(int Entry, string Path, bool Folder)> parts) =>
        (_stream, _zip, Read, _parts) = (stream, zip, read, parts);

    public void Dispose()
    {
        _zip.Dispose();
        _stream.Dispose();
    }

    /// <exception cref="PluginPackageRefusal">It is not there, does not read, or is no plugin this build installs.</exception>
    public static PluginPackageFile Open(string file)
    {
        var name = Path.GetFileName(file);
        if (!File.Exists(file))
        {
            throw new PluginPackageRefusal($"no file {file} — `plugins install` takes a package file, `<id>.<version>{PluginPackageRules.Extension}`.");
        }

        FileStream stream;
        try
        {
            // Shared for reading only while it is read, hashed and extracted: what is hashed is what lands.
            stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new PluginPackageRefusal($"{name} could not be read: {error.Message}");
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
                throw new PluginPackageRefusal($"{name} is not a package: it does not open as a zip ({error.Message}).");
            }

            try
            {
                var names = zip.Entries.Select(entry => entry.FullName).ToList();
                var nuspec = PluginPackageRules.Nuspec(names, name);
                var nuspecName = names[nuspec].Replace('\\', '/');
                var (read, parts) = PluginPackageRules.Judge(Nuspec(zip.Entries[nuspec], nuspecName, name), nuspecName, names, name, sha512);
                return new PluginPackageFile(stream, zip, read, parts);
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
            throw new PluginPackageRefusal($"{name} could not be read: {error.Message}");
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
    /// <exception cref="PluginPackageRefusal">A part would land outside the stage.</exception>
    public void Extract(string stage)
    {
        var whole = Path.GetFullPath(stage);
        Directory.CreateDirectory(whole);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var (at, path, folder) in _parts)
        {
            var entry = _zip.Entries[at];
            var target = Path.GetFullPath(Path.Combine(whole, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(whole + Path.DirectorySeparatorChar, comparison))
            {
                throw new PluginPackageRefusal($"{PluginPackageRules.Said(Read.Package, Read.Version)} holds `{entry.FullName}`, which would land outside its plugin's folder "
                    + "— the whole package is refused.");
            }

            if (folder)
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
    }

    /// <summary>The nuspec read as data: no DTD, and nothing it names is fetched.</summary>
    /// <exception cref="PluginPackageRefusal">It does not read.</exception>
    private static XDocument Nuspec(ZipArchiveEntry entry, string nuspecName, string file)
    {
        try
        {
            using var reader = XmlReader.Create(entry.Open(),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = true });
            return XDocument.Load(reader);
        }
        catch (Exception error) when (error is XmlException or InvalidDataException or IOException)
        {
            throw new PluginPackageRefusal($"{file}'s `{nuspecName}` does not read: {error.Message}");
        }
    }
}
