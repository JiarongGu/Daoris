using System.Security.Cryptography;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The SHA-256 a maker's channel verified a managed binary by (STEWARDROLE1a-i, D209 §6.1), kept as <see cref="FileName"/> in the managed
/// install's folder, where the install's one folder move puts it with the binary: <c>{ "file": "bin/claude.exe", "sha256": "…" }</c>.
/// Read by the one start that hands a binary a role's key, which refuses a binary whose bytes are not these, or a pin with no record.
/// </summary>
/// <remarks>
/// A pin installed before this record existed has none, and so is refused by that start until it is installed again; every other
/// caller of the managed binary reads nothing of it.
/// </remarks>
public static class VerifiedBinary
{
    /// <summary>The record's name, in the managed install's folder.</summary>
    public const string FileName = "verified.json";

    /// <summary>The record written into an install's folder (a staging's package): the executable, relative and <c>/</c>-separated, and its hash.</summary>
    public static void Write(string folder, string file, string sha256) => JsonFile.Write(Path.Combine(folder, FileName), writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("file", file);
        writer.WriteString("sha256", sha256.ToLowerInvariant());
        writer.WriteEndObject();
    });

    /// <summary>The hash recorded for <paramref name="binary"/>, an executable inside <paramref name="folder"/>; null where none is, or it names another file.</summary>
    public static string? Recorded(string folder, string binary)
    {
        var path = Path.Combine(folder, FileName);
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Json.Text(root, "file") is not { } file || Json.Text(root, "sha256") is not { Length: 64 } sha256)
            {
                return null;
            }

            var named = Path.GetFullPath(Path.Combine([folder, .. file.Split('/')]));
            return string.Equals(named, Path.GetFullPath(binary), StringComparison.OrdinalIgnoreCase) ? sha256.ToLowerInvariant() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The SHA-256 of a file's bytes now, lower case hex; null where it cannot be read.</summary>
    public static string? HashOf(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
