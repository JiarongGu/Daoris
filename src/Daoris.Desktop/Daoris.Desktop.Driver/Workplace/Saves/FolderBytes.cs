namespace Daoris.Driver;

/// <summary>A folder's bytes as the ledger counts them: its own (files of one name) and the linked files it holds, a record counted once.</summary>
/// <param name="Own">The bytes of files only this folder names: what making it wrote and removing it frees.</param>
/// <param name="Linked">The bytes of hard-linked files not counted before in the same measure, each record once.</param>
public readonly record struct FolderSize(long Own, long Linked, int Files);

/// <summary>
/// Measures a copy's folder for the ledger: a link (junction, symbolic link) is never followed, and a hard-linked file
/// (a dependency placed from the store) counts once by its record across every folder one <see cref="Seen"/> set is handed, since the
/// store wrote its bytes once. What could not be read counts nothing.
/// </summary>
public static class FolderBytes
{
    /// <summary>The records counted already, shared across the copies of one repository so a dependency linked into each counts once.</summary>
    public sealed class Seen
    {
        internal HashSet<string> Records { get; } = new(StringComparer.Ordinal);
    }

    public static FolderSize Measure(string folder, Seen? seen = null)
    {
        seen ??= new Seen();
        long own = 0, linked = 0;
        var files = 0;
        var pending = new Stack<string>();
        if (Directory.Exists(folder)) pending.Push(folder);
        while (pending.Count > 0)
        {
            var at = pending.Pop();
            try
            {
                foreach (var entry in new DirectoryInfo(at).EnumerateFileSystemInfos())
                {
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    if (entry is not FileInfo file)
                    {
                        pending.Push(entry.FullName);
                        continue;
                    }

                    files++;
                    if (FileLinks.RecordOf(file.FullName) is { Names: > 1 } record)
                    {
                        if (seen.Records.Add(record.Identity)) linked += file.Length;
                    }
                    else
                    {
                        own += file.Length;
                    }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Counted as far as it read.
            }
        }

        return new(own, linked, files);
    }
}
