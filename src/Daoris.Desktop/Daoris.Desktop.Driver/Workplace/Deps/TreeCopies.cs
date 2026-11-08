using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>The hash a copy of a machine-local file is known by, and whether a file is still the copy Daoris made.</summary>
internal static class TreeCopies
{
    /// <summary>The SHA-256 of a file's bytes; null where it cannot be read.</summary>
    internal static string? HashOf(string file)
    {
        try
        {
            return File.Exists(file) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="path"/> in the tree is a file the record holds a copy of, its bytes still the hash written.</summary>
    internal static bool IsOwn(TreeFilesState? record, string tree, string path) =>
        record is not null && record.Copies.TryGetValue(path.TrimEnd('/'), out var written)
        && string.Equals(HashOf(Path.Combine(tree, path.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar))), written, StringComparison.Ordinal);
}
