namespace Daoris.Driver;

/// <summary>
/// Whether git is rewriting a repository's pack, multi-pack index or commit graph now, read from the files it leaves while it does
/// (<see cref="GitLibraryAdmission"/> sends the question to git while any stands). libgit2 maps those files for a call, and Windows
/// refuses a rename over a mapped one however the file is shared, so git's <c>repack</c> or <c>gc</c> that builds the pack it already
/// has (the same name) fails on a call that outlasts git's short retry; a question asked while git writes never opens them.
/// </summary>
/// <remarks>
/// A marker older than <see cref="Stale"/> is a crashed run's leftover and stops no question.
/// </remarks>
internal static class GitLibraryHousekeeping
{
    private static readonly TimeSpan Stale = TimeSpan.FromHours(12);

    /// <summary>Whether a rewrite of <paramref name="commonDir"/>'s packs, index or graph is under way.</summary>
    internal static bool Running(string commonDir)
    {
        var now = DateTime.UtcNow;
        var pack = Path.Combine(commonDir, "objects", "pack");
        var info = Path.Combine(commonDir, "objects", "info");
        return Fresh(Path.Combine(commonDir, "gc.pid"), now)
            || Written(pack, now, name => name.StartsWith(".tmp-", StringComparison.Ordinal) || name.EndsWith(".lock", StringComparison.Ordinal))
            || Written(info, now, name => name.EndsWith(".lock", StringComparison.Ordinal))
            || Written(Path.Combine(info, "commit-graphs"), now, name => name.EndsWith(".lock", StringComparison.Ordinal));
    }

    private static bool Fresh(string file, DateTime now) => File.Exists(file) && now - File.GetLastWriteTimeUtc(file) < Stale;

    private static bool Written(string folder, DateTime now, Func<string, bool> named)
    {
        if (!Directory.Exists(folder)) return false;
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            if (named(Path.GetFileName(file)) && now - File.GetLastWriteTimeUtc(file) < Stale) return true;
        }

        return false;
    }
}
