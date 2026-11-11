using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// How a file is copied into the salvage folder: a SQLite store through SQLite's own backup, which reads the store as it stands with its
/// write-ahead log applied and writes one consistent file, never the log or its index by hand; any other file as bytes. The copy is
/// written beside its target and moved into place, so a copy that stopped half-way is never taken for the file.
/// </summary>
internal static class SalvageCopy
{
    private static readonly byte[] Header = "SQLite format 3\0"u8.ToArray();

    internal static void Into(string source, string target)
    {
        var partial = target + ".copying";
        File.Delete(partial);
        if (IsStore(source)) Backup(source, partial);
        else File.Copy(source, partial, overwrite: true);
        File.Move(partial, target, overwrite: true);
    }

    private static bool IsStore(string path)
    {
        var read = new byte[Header.Length];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return stream.Read(read, 0, read.Length) == read.Length && read.AsSpan().SequenceEqual(Header);
    }

    private static void Backup(string source, string target)
    {
        using (var from = new SqliteConnection($"Data Source={source};Mode=ReadWrite;Pooling=False"))
        using (var to = new SqliteConnection($"Data Source={target};Pooling=False"))
        {
            from.Open();
            to.Open();
            from.BackupDatabase(to);
        }

        SqliteConnection.ClearAllPools();
    }
}
