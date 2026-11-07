using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A build of the events relay is its whole bundle: the SHA-256, in lowercase
/// hex, of one line <c>&lt;path&gt;\t&lt;size&gt;\t&lt;sha256&gt;\n</c> for each file under the folder, its path relative with <c>/</c>, the
/// lines in ordinal order, as a staged build's manifest lists every file. So a changed companion library is another build. The set tells it
/// in <c>work/event</c>'s handshake, and the relay's copy is named by it.
/// </summary>
public static class RelayBuild
{
    /// <summary>The build where there is no install to hash.</summary>
    public static readonly string None = new('0', 64);

    /// <summary>The build of <paramref name="folder"/>'s files, or <see cref="None"/> where the folder is not there.</summary>
    public static string Of(string folder)
    {
        if (!Directory.Exists(folder)) return None;
        var lines = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(file => (Path: Path.GetRelativePath(folder, file).Replace('\\', '/'), File: file))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .Select(entry =>
            {
                using var stream = File.OpenRead(entry.File);
                var size = stream.Length.ToString(CultureInfo.InvariantCulture);
                return $"{entry.Path}\t{size}\t{Convert.ToHexStringLower(SHA256.HashData(stream))}\n";
            });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(lines))));
    }
}
