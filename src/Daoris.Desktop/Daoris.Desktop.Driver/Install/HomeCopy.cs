namespace Daoris.Driver;

/// <summary>
/// The copy of a home a proof is made on (<see cref="HomeProbe"/>), and its removal. The home is in use while it is copied: a file the
/// running application or a check holds so it cannot be read (a seat's lock, a build's output) is copied as an empty stand-in, one that is
/// gone by the time it is read is left out, and only the copy's own writes fail it. A read-only file or a hard link is copied as its
/// content, and nothing in the copy carries a read-only mark.
/// </summary>
/// <remarks>
/// <para>The copy holds the home's own folder to <see cref="Depth"/> levels (its files above <see cref="MaxFile"/> as empty files, a folder
/// at the last level empty) and, of every other folder of the Daoris folder, its names to <see cref="Depth"/> levels as empty files and
/// folders, which is all a reading of accounts and of paths no entry names needs. What lies deeper in the home is a place's content, a
/// check's work tree and its dependencies, never a record a start reads: it holds most of a home's bytes and is what a running check
/// changes.</para>
/// </remarks>
internal static class HomeCopy
{
    /// <summary>A file above this is copied as an empty one: a record that large is read in no start.</summary>
    internal const long MaxFile = 64L * 1024 * 1024;

    /// <summary>How many levels of the home's own folder are copied, and how many of any other folder are named.</summary>
    internal const int Depth = 5;

    /// <summary>What the copy names of each folder outside the home's own, at most.</summary>
    private const int Names = 200_000;

    /// <summary>The copy of <paramref name="home"/> in <paramref name="into"/>, laid out as the source is (an install, or a home that is its own folder); the copy's home.</summary>
    internal static string Make(string home, string into)
    {
        var folder = DaorisHome.FolderOf(home);
        var own = Path.GetFullPath(home).TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
        Directory.CreateDirectory(into);
        File.WriteAllText(Path.Combine(into, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var inside = !string.Equals(own, root, StringComparison.OrdinalIgnoreCase) && own.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        var copyHome = inside ? Path.Combine(into, Path.GetRelativePath(root, own)) : into;
        if (inside) Copy(own, copyHome, 1);
        foreach (var child in Listed(root))
        {
            var name = Path.GetFileName(child);
            if (IsTheInstalls(name) || string.Equals(Path.GetFullPath(child), own, StringComparison.OrdinalIgnoreCase)) continue;
            // A budget of its own for each folder: a large cache's names never use up what `profiles` needs, which the accounts are read from.
            var left = Names;
            Mirror(child, Path.Combine(into, name), 1, ref left);
        }

        if (!inside) Copy(own, copyHome, 1);
        return copyHome;
    }

    /// <summary>The folders of an install that are its own, not the home's (<see cref="HomeStrays"/> passes them too).</summary>
    private static bool IsTheInstalls(string name) =>
        new[] { DaorisHome.InstallApp, DaorisHome.InstallMarker, "purged", DaorisHome.InstallUpdate, DaorisHome.InstallLauncher }
            .Any(own => string.Equals(name, own, StringComparison.OrdinalIgnoreCase));

    /// <summary>The home's own folder, its entries at <paramref name="level"/>: each file as it is (<see cref="CopyFile"/>), a folder at <see cref="Depth"/> empty. A link is not followed.</summary>
    private static void Copy(string from, string to, int level)
    {
        Directory.CreateDirectory(to);
        foreach (var entry in Listed(from))
        {
            var target = Path.Combine(to, Path.GetFileName(entry));
            if (Directory.Exists(entry))
            {
                if (level >= Depth || IsLink(entry)) Directory.CreateDirectory(target);
                else Copy(entry, target, level + 1);
            }
            else if (!Skipped(entry)) CopyFile(entry, target);
        }
    }

    /// <summary>A file the copy leaves out: what only a running application holds, so a copy of it would hold nothing true.</summary>
    private static bool Skipped(string file) =>
        string.Equals(Path.GetFileName(file), "home.lease", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// One file, read beside its writers: a store the running application holds open is read as it stands. One above <see cref="MaxFile"/>,
    /// or one its holder lets no one read, is an empty stand-in; one gone by the time it is read, left out. Only the copy's own writes throw.
    /// </summary>
    private static void CopyFile(string from, string to)
    {
        FileStream source;
        try
        {
            source = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            File.WriteAllBytes(to, []);
            return;
        }

        using (source)
        {
            using var target = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None);
            if (source.Length > MaxFile) return;
            var buffer = new byte[81920];
            while (true)
            {
                int read;
                try
                {
                    read = source.Read(buffer, 0, buffer.Length);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // A range its holder locks: the file is stood in whole rather than copied in part.
                    target.SetLength(0);
                    return;
                }

                if (read == 0) return;
                target.Write(buffer, 0, read);
            }
        }
    }

    /// <summary>The entries of <paramref name="folder"/>; none where it is gone or cannot be listed, so it is copied empty.</summary>
    private static List<string> Listed(string folder)
    {
        try
        {
            return [.. Directory.EnumerateFileSystemEntries(folder)];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void Mirror(string from, string to, int depth, ref int left)
    {
        if (left-- <= 0) return;
        if (!Directory.Exists(from))
        {
            File.WriteAllBytes(to, []);
            return;
        }

        Directory.CreateDirectory(to);
        if (depth >= Depth || IsLink(from)) return;
        foreach (var child in Listed(from)) Mirror(child, Path.Combine(to, Path.GetFileName(child)), depth + 1, ref left);
    }

    private static bool IsLink(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>Removes a copy this proof made; a read-only mark in it (a copy another build made) is cleared first.</summary>
    internal static void Remove(string made)
    {
        try
        {
            if (!Directory.Exists(made)) return;
            try
            {
                Directory.Delete(made, recursive: true);
            }
            catch (UnauthorizedAccessException)
            {
                foreach (var file in new DirectoryInfo(made).EnumerateFiles("*", SearchOption.AllDirectories).Where(file => file.IsReadOnly))
                {
                    file.IsReadOnly = false;
                }

                Directory.Delete(made, recursive: true);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left for the next proof's sweep.
        }
    }

    private const string Live = ".live";

    /// <summary>Marks <paramref name="made"/> as a proof's in progress: a file beside it, held open for as long as the proof lasts.</summary>
    internal static IDisposable Hold(string made)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(made)!);
        return new FileStream(made + Live, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
    }

    /// <summary>Removes the copies an earlier proof left: those whose mark no proof holds any more (one that ended, or died).</summary>
    internal static void Sweep(string scratch)
    {
        if (!Directory.Exists(scratch)) return;
        foreach (var old in Directory.GetDirectories(scratch))
        {
            try
            {
                using var _ = new FileStream(old + Live, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                Remove(old);
            }
            catch (IOException)
            {
                // A proof under way holds it.
            }
        }
    }
}
