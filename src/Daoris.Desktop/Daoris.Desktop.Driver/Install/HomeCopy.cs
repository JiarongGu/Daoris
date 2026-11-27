using System.IO.Enumeration;

namespace Daoris.Driver;

/// <summary>What a copy of a home holds and what it did not read (<see cref="HomeCopy.Make"/>), counted, never named.</summary>
/// <param name="Home">The copy's home, laid out as the source is.</param>
/// <param name="Files">The files copied as they are.</param>
/// <param name="Bytes">Their bytes.</param>
/// <param name="StandIns">Files another holder let no one read, each an empty stand-in; none is a record the proof's reading reads.</param>
/// <param name="LeftOut">Files and folders not copied: gone by the time they were read, a file above <see cref="HomeCopy.MaxFile"/> (an empty one in its place), a stand-in that could not be written.</param>
/// <param name="Unlisted">Folders that could not be listed, kept empty; none is one the proof's reading lists.</param>
/// <param name="Content">Content folders left out by name, kept empty (<see cref="HomeCopy.IsContent"/>).</param>
/// <param name="Seats">Seats and leases a running service holds or takes, named as empty files and never opened.</param>
internal sealed record HomeCopyMade(string Home, int Files, long Bytes, int StandIns, int LeftOut, int Unlisted, int Content, int Seats)
{
    /// <summary>What the copy did not read, in the words a refusal's sentence carries.</summary>
    public string Said =>
        $"the copy read {Files} files ({Bytes / (1024 * 1024)} MB) and not {StandIns} held, {LeftOut} left out, {Unlisted} unlisted folders, "
        + $"{Content} content folders or {Seats} seats";
}

/// <summary>A copy that cannot be true to the home: its sentence names the place, from the Daoris folder, and why.</summary>
internal sealed class HomeCopyRefused(string sentence) : IOException(sentence);

/// <summary>
/// The copy of a home a proof is made on (<see cref="HomeProbe"/>), and its removal. The copy is never vacuous: the Daoris folder, its
/// <c>data</c> folder and every folder and record the proof's reading reads (<see cref="HomeProof.Reads"/>) are listed and read, or the
/// copy is refused with its sentence (<see cref="HomeCopyRefused"/>). The home is in use while it is copied: a seat a running service holds
/// or takes is named and never opened; a file another holder lets no one read (a sharing or lock violation, nothing else) is an empty
/// stand-in, one that is gone by the time it is read is left out, and any other fault refuses. A read-only file or a hard link is copied
/// as its content, and nothing in the copy carries a read-only mark. What the copy did not read is counted (<see cref="HomeCopyMade"/>).
/// </summary>
/// <remarks>
/// <para>The <c>data</c> folder, which holds the home's records, is copied with its files at any depth (a file above <see cref="MaxFile"/>
/// as an empty one, unless the reading reads it), but for its content folders, left out by name (<see cref="IsContent"/>): a line's work
/// trees, the plugins' packages and trials, which hold most of a home's files and are what a running check changes. Every other folder of
/// the Daoris folder is copied as names, empty, as deep as the home's deepest place (<see cref="HomeEntry.Table"/>), which is all a
/// reading of accounts and of paths no entry names meets; no link is followed but where the reading follows it.</para>
/// </remarks>
internal static class HomeCopy
{
    /// <summary>A file above this is copied as an empty one, unless the proof's reading reads it.</summary>
    internal const long MaxFile = 64L * 1024 * 1024;

    /// <summary>How many names the copy keeps of each folder outside <c>data</c>, at most; past it a folder is counted unlisted.</summary>
    private const int Names = 200_000;

    /// <summary>
    /// The copy of <paramref name="home"/> in <paramref name="into"/>, laid out as the source is (an install, or a home that is its own
    /// folder), reading every place of <paramref name="reads"/> (places as the home's table writes them, from the Daoris folder).
    /// </summary>
    /// <exception cref="HomeCopyRefused">A place the reading reads, or the home itself, could not be listed or read.</exception>
    /// <exception cref="IOException">The copy's own write failed.</exception>
    internal static HomeCopyMade Make(string home, string into, IReadOnlyList<string> reads)
    {
        var root = Path.GetFullPath(DaorisHome.FolderOf(home)).TrimEnd(Path.DirectorySeparatorChar);
        var own = Path.GetFullPath(home).TrimEnd(Path.DirectorySeparatorChar);
        var walk = new Walk([.. reads.Select(read => read.Split('/', StringSplitOptions.RemoveEmptyEntries))]);
        var top = walk.List(root, "") ?? [];
        Directory.CreateDirectory(into);
        File.WriteAllText(Path.Combine(into, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        walk.Entries(top, into, "", content: false, depth: 0);
        var copyHome = string.Equals(own, root, StringComparison.OrdinalIgnoreCase) ? into : Path.Combine(into, Path.GetRelativePath(root, own));
        return new(copyHome, walk.Files, walk.Bytes, walk.StandIns, walk.LeftOut, walk.Unlisted, walk.Content, walk.Seats);
    }

    /// <summary>
    /// A content folder, left out by name: a line's work trees (<c>workflows/lines/trees</c>), a plugin's package (a folder of
    /// <c>plugins</c> whose name does not start with a dot) and the plugins' trials (<c>plugins/.trials</c>), never a record a start reads.
    /// </summary>
    /// <param name="relative">The folder from the Daoris folder, <c>/</c>-separated.</param>
    internal static bool IsContent(string relative)
    {
        if (relative.Equals("data/workflows/lines/trees", StringComparison.OrdinalIgnoreCase)) return true;
        var slash = relative.LastIndexOf('/');
        if (slash < 0 || !relative[..slash].Equals("data/plugins", StringComparison.OrdinalIgnoreCase)) return false;
        var name = relative[(slash + 1)..];
        return !name.StartsWith('.') || name.Equals(".trials", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether <paramref name="error"/> is another holder's: a sharing or a lock violation, which a stand-in may stand for. Any other fault is the home's.</summary>
    internal static bool Held(Exception error) => error is IOException { HResult: var code } && (code & 0xFFFF) is 0x20 or 0x21;

    /// <summary>A seat or a lease a running service holds or takes: opened by the copy, the service could not take it.</summary>
    private static bool IsSeat(string name) =>
        name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) || name.Equals("home.lease", StringComparison.OrdinalIgnoreCase);

    /// <summary>The folders of an install that are its own, not the home's (<see cref="HomeStrays"/> passes them too).</summary>
    private static bool IsTheInstalls(string name) =>
        new[] { DaorisHome.InstallApp, DaorisHome.InstallMarker, "purged", DaorisHome.InstallUpdate, DaorisHome.InstallLauncher }
            .Any(own => string.Equals(name, own, StringComparison.OrdinalIgnoreCase));

    /// <summary>How deep the home's deepest place lies: what a reading of names outside <c>data</c> meets.</summary>
    private static readonly int NameDepth = HomeEntry.Table.Max(entry => entry.New.Split('/', StringSplitOptions.RemoveEmptyEntries).Length);

    private sealed class Walk(IReadOnlyList<string[]> reads)
    {
        public int Files, StandIns, LeftOut, Unlisted, Content, Seats;
        public long Bytes;
        private int _names;

        /// <summary>Whether the reading lists the folder <paramref name="relative"/>: the Daoris folder, <c>data</c>, a folder a place passes through or names.</summary>
        private bool Lists(string relative) =>
            relative.Length == 0 || relative.Equals(DaorisHome.InstallData, StringComparison.OrdinalIgnoreCase) || Matches(relative, whole: false);

        /// <summary>Whether the reading reads the file <paramref name="relative"/>: a place names it whole.</summary>
        private bool Reads(string relative) => Matches(relative, whole: true);

        private bool Matches(string relative, bool whole)
        {
            var parts = relative.Split('/');
            return reads.Any(place => (whole ? place.Length == parts.Length : place.Length >= parts.Length)
                && parts.Select((part, at) => FileSystemName.MatchesSimpleExpression(place[at], part, ignoreCase: true)).All(match => match));
        }

        private static string Shown(string relative) => relative.Length == 0 ? "the Daoris folder" : $"`{relative}`";

        /// <summary>The entries of <paramref name="folder"/>; null where it is gone or, nothing reading it, cannot be listed.</summary>
        public List<string>? List(string folder, string relative)
        {
            try
            {
                return [.. Directory.EnumerateFileSystemEntries(folder)];
            }
            catch (DirectoryNotFoundException error) when (relative.Length == 0)
            {
                throw new HomeCopyRefused($"{Shown(relative)} could not be listed: {error.Message}");
            }
            catch (DirectoryNotFoundException)
            {
                // Gone since its parent's listing: the home holds it no longer.
                LeftOut++;
                return null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (Lists(relative)) throw new HomeCopyRefused($"{Shown(relative)}, which the proof reads, could not be listed: {error.Message}");
                Unlisted++;
                return [];
            }
        }

        /// <summary>
        /// The <paramref name="entries"/> of the folder <paramref name="relative"/> into <paramref name="to"/>: with their files where
        /// <paramref name="content"/>, else as names.
        /// </summary>
        public void Entries(List<string> entries, string to, string relative, bool content, int depth)
        {
            Directory.CreateDirectory(to);
            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                var place = relative.Length == 0 ? name : relative + "/" + name;
                var target = Path.Combine(to, name);
                if (relative.Length == 0 && IsTheInstalls(name)) continue;
                if (!content) _names++;
                if (Directory.Exists(entry)) Folder(entry, target, place, content || (relative.Length == 0 && name.Equals(DaorisHome.InstallData, StringComparison.OrdinalIgnoreCase)), depth + 1);
                else if (!content) System.IO.File.WriteAllBytes(target, []);
                else if (IsSeat(name)) Empty(target, ref Seats);
                else File(entry, target, place);
            }
        }

        private void Folder(string from, string to, string relative, bool content, int depth)
        {
            if (IsContent(relative))
            {
                Directory.CreateDirectory(to);
                Content++;
                return;
            }

            // A folder the reading lists is listed, through a link too, as the reading follows it.
            if (!Lists(relative))
            {
                var link = IsLink(from);
                var unlisted = link is null || !content && depth < NameDepth && _names >= Names;
                // Not followed: a link's target is not the home's. Outside `data`, names go as deep as the home's deepest place.
                if (link != false || unlisted || !content && depth >= NameDepth)
                {
                    Directory.CreateDirectory(to);
                    if (unlisted) Unlisted++;
                    return;
                }
            }

            if (List(from, relative) is { } entries) Entries(entries, to, relative, content, depth);
        }

        /// <summary>One file of <c>data</c>, read beside its writers: a store the running application holds open is read as it stands.</summary>
        private void File(string from, string to, string relative)
        {
            var read = Reads(relative);
            FileStream source;
            try
            {
                source = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                LeftOut++;
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (!System.IO.File.Exists(from))
                {
                    LeftOut++;
                    return;
                }

                Unread(to, relative, read, error);
                return;
            }

            using (source)
            {
                if (!read && source.Length > MaxFile)
                {
                    Empty(to, ref LeftOut);
                    return;
                }

                Exception? unread = null;
                long copied = 0;
                using (var target = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    while (true)
                    {
                        int count;
                        try
                        {
                            count = source.Read(buffer, 0, buffer.Length);
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                        {
                            // A range its holder locks: the file is stood in whole, never copied in part.
                            target.SetLength(0);
                            unread = error;
                            break;
                        }

                        if (count == 0) break;
                        target.Write(buffer, 0, count);
                        copied += count;
                    }
                }

                if (unread is not null)
                {
                    Unread(to, relative, read, unread, written: true);
                    return;
                }

                Files++;
                Bytes += copied;
            }
        }

        /// <summary>A file that could not be read: an empty stand-in for one another holder keeps, which the reading does not read; else the copy is refused.</summary>
        private void Unread(string to, string relative, bool read, Exception error, bool written = false)
        {
            if (!Held(error)) throw new HomeCopyRefused($"`{relative}` could not be read: {error.Message}");
            if (read) throw new HomeCopyRefused($"`{relative}`, a record the proof reads, is held by another process: {error.Message}");
            if (written)
            {
                StandIns++;
                return;
            }

            try
            {
                System.IO.File.WriteAllBytes(to, []);
                StandIns++;
            }
            catch (Exception unwritten) when (unwritten is IOException or UnauthorizedAccessException)
            {
                // A stand-in whose path the copy cannot hold is skipped and counted, never a failed copy.
                LeftOut++;
            }
        }

        private static void Empty(string to, ref int counted)
        {
            System.IO.File.WriteAllBytes(to, []);
            counted++;
        }

        /// <summary>Whether <paramref name="folder"/> is a link; null where its marks cannot be read.</summary>
        private static bool? IsLink(string folder)
        {
            try
            {
                return new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>Removes a copy this proof made (<see cref="FolderRemoval.Delete"/>, read-only marks cleared); one that cannot be is left for the next proof's sweep.</summary>
    internal static void Remove(string made)
    {
        try
        {
            if (Directory.Exists(made)) FolderRemoval.Delete(made);
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
