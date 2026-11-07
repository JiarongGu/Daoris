using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// One bounded walk of an ignored top (the tree-pool design §28.4): its files and bytes, stopping at 20,000 entries or two seconds
/// and saying <i>more than</i>; whether it is itself a repository (a <c>.git</c> at its top, or a bare repository's shape); and how
/// many repositories lie below it, none read into. Names and stats only, never contents, never following a link.
/// </summary>
internal sealed record FileWalk(FileSize Size, bool Repository, int Nested)
{
    internal const int Bound = 20_000;

    internal static readonly TimeSpan Time = TimeSpan.FromSeconds(2);

    internal bool Holds => Repository || Nested > 0;

    internal static FileWalk Of(string folder)
    {
        if (!Directory.Exists(folder)) return new(new FileSize(File.Exists(folder) ? 1 : 0, File.Exists(folder) ? new FileInfo(folder).Length : 0, false), false, 0);
        if (Repo(folder)) return new(new FileSize(0, 0, More: true), true, 0);

        var began = Stopwatch.GetTimestamp();
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0, RecurseSubdirectories = false };
        long files = 0, bytes = 0, entries = 0;
        var nested = 0;
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var next))
        {
            foreach (var entry in new DirectoryInfo(next).EnumerateFileSystemInfos("*", options))
            {
                if (++entries > Bound || Stopwatch.GetElapsedTime(began) > Time) return new(new FileSize(files, bytes, More: true), false, nested);
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (entry is FileInfo file)
                {
                    files++;
                    bytes += file.Length;
                }
                else if (Repo(entry.FullName))
                {
                    nested++;
                }
                else
                {
                    pending.Push(entry.FullName);
                }
            }
        }

        return new(new FileSize(files, bytes, More: false), false, nested);
    }

    private static bool Repo(string folder) =>
        File.Exists(Path.Combine(folder, ".git")) || Directory.Exists(Path.Combine(folder, ".git")) || FileFloor.LooksBare(folder);
}
