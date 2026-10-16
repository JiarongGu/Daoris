using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>A working directory Daoris's own records name (OWNSPACE2): where, from what record kind, and what keeps it from a press.</summary>
/// <param name="Folder">The working directory a session ran in, as the record holds it.</param>
/// <param name="Source"><c>tree</c>, <c>help</c> or <c>intake</c>.</param>
/// <param name="Keeps">Null, or why the folder it made must stay: <c>session</c> (live or waiting), <c>resumable</c> (a legacy conversation that goes on in the person's own home), <c>records-unread</c>.</param>
public sealed record ToolOrigin(string Folder, string Source, string? Keeps);

/// <summary>A project folder in the person's tool home that is provably Daoris's: its name, its place, and the identity it had when listed.</summary>
public sealed record ToolFolder(string Name, string Path, string Source, string? Keeps, string Id);

/// <summary>What was listed, and why not where nothing could be: <c>Why</c> is a code (<c>linked</c>, <c>relative</c>, <c>inside-daoris</c>, <c>no-tool-home</c>, <c>no-home</c>).</summary>
/// <param name="Ambiguous">Folders present whose name two distinct working directories flatten to: listed for neither.</param>
/// <param name="Uncertain">Folders present whose only claimant's path flattens in a way not proven (a non-ASCII letter, an over-long name).</param>
public sealed record ToolListing(IReadOnlyList<ToolFolder> Folders, string? Why, bool CaseInsensitive, int Ambiguous, int Uncertain);

/// <summary>One folder measured: its bytes, its newest file's time, and whether any read failed (so the numbers are not whole).</summary>
public sealed record ToolMeasure(long Bytes, DateTimeOffset? Newest, bool Partial);

/// <summary>A folder the person asks to move aside: its name and the identity the list showed.</summary>
public sealed record ToolAsk(string Name, string Id);

/// <summary>A folder moved aside, with the bytes it held and where it went.</summary>
public sealed record ToolCleared(string Name, long Bytes, string To);

/// <summary>A folder kept, and why: <c>not-listed</c>, <c>changed</c>, <c>session</c>, <c>resumable</c>, <c>records-unread</c>, <c>in-use</c>, <c>unread</c>, <c>linked</c>, <c>not-moved</c>.</summary>
public sealed record ToolKept(string Name, string Why);

/// <summary>What a press did.</summary>
public sealed record ToolClear(IReadOnlyList<ToolCleared> Cleared, IReadOnlyList<ToolKept> Kept);

/// <summary>
/// What Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3; D88's rule for what Daoris made): the
/// project folders Claude Code keyed under <c>projects/</c> by the working directory of a session Daoris ran, listed as
/// Daoris's, and moved aside (never deleted) by the person's press after the list.
/// </summary>
/// <remarks>
/// <para><b>Ownership is provenance, never a pattern.</b> Claude Code names a project folder by the working directory with every
/// character that is not a letter or a digit turned into <c>-</c> (<see cref="Flatten"/>), which loses what the character was. So a
/// folder is Daoris's only where its name EQUALS the flattening of a working directory Daoris's own records hold
/// (<see cref="Origins"/>: each session record's tree, the help room, the intake rooms), inside Daoris's own working roots. No
/// prefix is ever matched. Two distinct paths flattening alike are listed for neither, and a path not proven to flatten as
/// Claude Code does (a non-ASCII letter, a name past 200 characters, a relative path) is not Daoris's.</para>
///
/// <para><b>What may still be used stays.</b> A folder whose record is live or waiting, or whose record began in the tool's own
/// home and has a conversation kept to resume, stays whatever its age; so does one written to within <see cref="Quiet"/>, or one
/// not fully read. Each is judged again right before it moves.</para>
///
/// <para><b>Moved, not deleted.</b> A folder goes into <c>trees/.tidied/tool-home/projects/</c> under Daoris's own home, named
/// as the trees' moved-aside folders are, so the look's own purge takes it after <see cref="RecoveryDays"/> days with nothing
/// written in it since. Putting one back within those days is a row of its own.</para>
///
/// <para><b>Codex is not listed.</b> Its home files a session by the date it ran, and the folder it ran in is said only inside
/// the session's file, which this never reads.</para>
/// </remarks>
public static class ToolHomeLeftovers
{
    /// <summary>The variable naming Claude Code's configuration home: the person's own, where they set one.</summary>
    public const string Variable = "CLAUDE_CONFIG_DIR";

    /// <summary>How lately a folder was written to for a session to be taken as running in it: a second guard beside the records.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromMinutes(10);

    /// <summary>How long a folder moved aside is kept: the trees' moved-aside wait (<see cref="BranchTidying.SetAsideFor"/>).</summary>
    public const int RecoveryDays = 14;

    /// <summary>Claude Code names a folder past this many characters with a hash of its own, which is not proven here.</summary>
    private const int LongestName = 200;

    private static readonly Regex Other = new("[^a-zA-Z0-9]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>A path as Claude Code names its project folder: every character but a letter or a digit becomes <c>-</c>.</summary>
    public static string Flatten(string path) => Other.Replace(path, "-");

    // ---------------------------------------------------------------------------------------------------------------------
    // The person's tool home

    /// <summary>
    /// The person's own tool home, where it was read from and why it cannot be used. <c>process</c> where they set
    /// <see cref="Variable"/> in this process's environment, <c>account</c> where only the account's stored variable names it,
    /// else <c>default</c>, the <c>.claude</c> folder of their profile. A variable that is relative, or that names a place inside
    /// Daoris's own folder (a Daoris profile, never the person's), is no tool home: its path is null with the code.
    /// </summary>
    /// <param name="daorisHome">Daoris's own home, which a tool home must not be inside; null where none.</param>
    public static (string? Path, string From, string? Why) OwnHome(
        Func<string, string?> process, Func<string, string?>? account, string profile, string? daorisHome)
    {
        foreach (var (read, from) in new[] { (process, "process"), (account, "account") })
        {
            if (read?.Invoke(Variable) is not { } set || string.IsNullOrWhiteSpace(set)) continue;
            var named = set.Trim();
            if (!System.IO.Path.IsPathFullyQualified(named)) return (null, from, "relative");
            var full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(named));
            foreach (var daoris in new[] { daorisHome, SystemWiring.RootOf(daorisHome) }.Where(each => each is not null).Distinct())
            {
                if (IsUnder(full, System.IO.Path.GetFullPath(daoris!), StringComparer.OrdinalIgnoreCase)) return (null, from, "inside-daoris");
            }

            return (full, from, null);
        }

        return (System.IO.Path.Combine(profile, ".claude"), "default", null);
    }

    /// <summary>The own home as a real program reads it: this process's environment, the account's, the user profile.</summary>
    public static (string? Path, string From, string? Why) OwnHome(WiringWorld world) =>
        OwnHome(world.Process, world.Account, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), world.Home);

    /// <summary>
    /// The tool home a world reads: the one it was handed, or none. 🔴 A world made by hand names none, so a test that builds one
    /// reads no machine's tool home; the window's and the terminal's worlds are handed theirs (<see cref="OwnHome(WiringWorld)"/>).
    /// </summary>
    public static (string? Path, string From, string? Why) ToolHomeOf(WiringWorld world) => (world.ToolHome, world.ToolHomeFrom, world.ToolHomeWhy);

    /// <summary>The folder Claude Code keeps its projects in, in the person's tool home.</summary>
    public static string? ProjectsOf(WiringWorld world) => world.ToolHome is { } tool ? System.IO.Path.Combine(tool, "projects") : null;

    // ---------------------------------------------------------------------------------------------------------------------
    // Provenance

    /// <summary>
    /// The working directories Daoris's own records hold: the help room and each intake room folder as they stand, and, from the
    /// service's session records (<paramref name="recordsJson"/>), each record's tree, the help room for Ask Daoris's, and an
    /// intake's room. Each carries what keeps it. Where the records could not be read (null, or not JSON) the rooms are listed
    /// with every one kept <c>records-unread</c>: activity is unknown, so nothing is removable.
    /// </summary>
    public static IReadOnlyList<ToolOrigin> Origins(string home, string? recordsJson)
    {
        var floor = recordsJson is null ? "records-unread" : null;
        var origins = new List<ToolOrigin> { new(HelpRoom.PathOf(home), "help", floor) };
        try
        {
            var intake = DaorisHome.Entry(home, HomeEntry.Intake);
            if (Directory.Exists(intake))
            {
                foreach (var room in new DirectoryInfo(intake).EnumerateDirectories().Where(each => !each.Attributes.HasFlag(FileAttributes.ReparsePoint)))
                {
                    origins.Add(new ToolOrigin(room.FullName, "intake", floor));
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A room folder that cannot be listed claims nothing.
        }

        if (recordsJson is null) return origins;
        try
        {
            using var document = JsonDocument.Parse(recordsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return Origins(home, null);
            var conversations = new HarnessConversations(home);
            foreach (var record in document.RootElement.EnumerateArray())
            {
                if (record.ValueKind != JsonValueKind.Object || Text(record, "id") is not { Length: > 0 } id || id.Contains('/')) continue;
                var live = new SessionRecord(id, "", Text(record, "state") ?? "").Live;
                var keeps = live ? "session"
                    : string.IsNullOrWhiteSpace(Text(record, "profile")) && conversations.Read(id) is not null ? "resumable"
                    : null;
                if (Text(record, "tree") is { Length: > 0 } tree) origins.Add(new ToolOrigin(tree, "tree", keeps));
                if (Text(record, "repository") == "daoris:help") origins.Add(new ToolOrigin(HelpRoom.PathOf(home), "help", keeps));
                if (Text(record, "ask") is { Length: > 0 })
                {
                    origins.Add(new ToolOrigin(IntakeRoom.PathOf(home, RemoteTarget.Workspace(Text(record, "workspace"))), "intake", keeps));
                }
            }
        }
        catch (JsonException)
        {
            return Origins(home, null);
        }

        return origins;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Daoris's working roots in a home: each <c>work</c> entry's folder where it holds its items, new place and old.</summary>
    public static IReadOnlyList<string> Roots(string home) =>
        [.. HomeEntry.Table.Where(entry => entry.Category == HomeCategory.Work)
            .SelectMany(entry => DaorisHome.Places(home, entry))
            .Select(place => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(place)))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    // ---------------------------------------------------------------------------------------------------------------------
    // The list

    /// <summary>
    /// The project folders that are provably Daoris's, by name, each bound to the identity it has now. Read from the folder's
    /// listing alone, nothing opened. A folder reached through a link is never listed: where any ancestor of the tool home or its
    /// projects folder is a link, nothing is, and <c>Why</c> is <c>linked</c>.
    /// </summary>
    public static ToolListing List(WiringWorld world)
    {
        if (world.Home is not { } home) return new ToolListing([], "no-home", false, 0, 0);
        if (world.ToolHome is not { } tool) return new ToolListing([], world.ToolHomeWhy ?? "no-tool-home", false, 0, 0);
        var projects = System.IO.Path.Combine(tool, "projects");
        if (!Directory.Exists(projects)) return new ToolListing([], null, false, 0, 0);
        if (LinkOnChain(projects) is not null) return new ToolListing([], "linked", false, 0, 0);

        var insensitive = CaseInsensitive(projects);
        var comparer = insensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        List<DirectoryInfo> present;
        try
        {
            present = [.. new DirectoryInfo(projects).EnumerateDirectories().Where(folder => !folder.Attributes.HasFlag(FileAttributes.ReparsePoint))];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new ToolListing([], "unread", insensitive, 0, 0);
        }

        var names = present.Select(folder => folder.Name).ToHashSet(comparer);
        var roots = Roots(home);
        var origins = world.ToolOrigins ?? Origins(home, null);
        var claims = new Dictionary<string, List<(string Full, ToolOrigin Origin, bool Sure)>>(comparer);
        foreach (var origin in origins)
        {
            if (!System.IO.Path.IsPathFullyQualified(origin.Folder)) continue;
            var full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(origin.Folder));
            // Only a working directory under Daoris's own working roots is Daoris's to claim: a record naming the person's own
            // checkout names nothing of ours.
            if (!roots.Any(root => comparer.Equals(full, root) || IsUnder(full, root, comparer))) continue;
            var flat = Flatten(full);
            var sure = full.All(letter => letter < 128) && flat.Length <= LongestName;
            if (!claims.TryGetValue(flat, out var group)) claims[flat] = group = [];
            group.Add((full, origin, sure));
        }

        var folders = new List<ToolFolder>();
        var ambiguous = 0;
        var uncertain = 0;
        foreach (var (flat, group) in claims.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!names.Contains(flat)) continue;
            // Paths spelled apart are two claims even where this folder's file system would fold them: never merged.
            if (group.Select(claim => claim.Full).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                ambiguous++;
                continue;
            }

            if (group.Any(claim => !claim.Sure))
            {
                uncertain++;
                continue;
            }

            var folder = present.First(each => comparer.Equals(each.Name, flat));
            var keeps = group.Select(claim => claim.Origin.Keeps).FirstOrDefault(why => why == "session")
                ?? group.Select(claim => claim.Origin.Keeps).FirstOrDefault(why => why == "resumable")
                ?? group.Select(claim => claim.Origin.Keeps).FirstOrDefault(why => why is not null);
            folders.Add(new ToolFolder(folder.Name, folder.FullName, group[0].Origin.Source, keeps, FileId(folder.FullName)));
        }

        return new ToolListing(folders, null, insensitive, ambiguous, uncertain);
    }

    /// <summary>Whether a folder's own file system folds case, read from it: the folder's name with its case swapped is the same folder.</summary>
    private static bool CaseInsensitive(string folder)
    {
        var name = System.IO.Path.GetFileName(folder);
        var swapped = new string([.. name.Select(letter => char.IsUpper(letter) ? char.ToLowerInvariant(letter) : char.ToUpperInvariant(letter))]);
        if (swapped == name) return OperatingSystem.IsWindows();
        return Directory.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(folder)!, swapped));
    }

    private static bool IsUnder(string path, string root, StringComparer comparer)
    {
        var top = System.IO.Path.TrimEndingDirectorySeparator(root);
        return path.Length > top.Length && comparer.Equals(path[..top.Length], top) && path[top.Length] == System.IO.Path.DirectorySeparatorChar;
    }

    /// <summary>The first link on the way from <paramref name="path"/> up to the root, the path itself included; null where there is none.</summary>
    internal static string? LinkOnChain(string path, string? stopAt = null)
    {
        var at = new DirectoryInfo(System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path)));
        var stop = stopAt is null ? null : System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(stopAt));
        for (; at is not null; at = at.Parent)
        {
            if (stop is not null && string.Equals(at.FullName.TrimEnd(System.IO.Path.DirectorySeparatorChar), stop, StringComparison.OrdinalIgnoreCase)) break;
            try
            {
                if (at.Exists && at.Attributes.HasFlag(FileAttributes.ReparsePoint)) return at.FullName;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return at.FullName;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Identity

    /// <summary>
    /// A folder's identity: on Windows its volume and file index, which a folder made anew in the same place does not have, as
    /// the file system gives them; where that is not read, its creation time, a weaker stand-in.
    /// </summary>
    internal static string FileId(string folder)
    {
        if (OperatingSystem.IsWindows())
        {
            using var handle = CreateFileW(folder, 0, 7, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
            if (!handle.IsInvalid && GetFileInformationByHandle(handle, out var info))
            {
                return string.Create(CultureInfo.InvariantCulture, $"{info.VolumeSerialNumber:x}-{info.FileIndexHigh:x}{info.FileIndexLow:x8}");
            }
        }

        return "created-" + Directory.GetCreationTimeUtc(folder).Ticks.ToString(CultureInfo.InvariantCulture);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation information);

    // ---------------------------------------------------------------------------------------------------------------------
    // Size

    /// <summary>Each folder's size and newest file through the existing measuring (<see cref="WiringSizes.MeasureFolder"/>), by name.</summary>
    public static IReadOnlyDictionary<string, ToolMeasure> Measure(IReadOnlyList<ToolFolder> folders, WiringBudget budget, CancellationToken cancellationToken)
    {
        var measured = new Dictionary<string, ToolMeasure>(StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var (bytes, newest, partial) = WiringSizes.MeasureFolder(folder.Path, budget, cancellationToken);
            measured[folder.Name] = new ToolMeasure(bytes, newest, partial);
        }

        return measured;
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The press

    /// <summary>
    /// The person's press: move aside exactly the folders asked for that are still Daoris's, each judged again inside the
    /// action. A name not in the list now stays <c>not-listed</c>; one whose identity changed since the list, <c>changed</c>; one
    /// its records say a session may use, <c>session</c>, <c>resumable</c> or <c>records-unread</c>; one not fully read,
    /// <c>unread</c>; one written to within <see cref="Quiet"/> of <paramref name="now"/>, <c>in-use</c>. Where a link lies on the
    /// way to the projects folder or to where it would go, nothing moves. Never deleted: see the class remarks.
    /// </summary>
    /// <param name="budget">What the walk of each folder may count; null is the home's own budget.</param>
    /// <param name="copy">Copy then remove, as a move across volumes must; a test's seam to prove it on one volume.</param>
    public static ToolClear Clear(
        WiringWorld world, IReadOnlyList<ToolAsk> asked, DateTimeOffset now, WiringBudget? budget = null, bool copy = false)
    {
        var cleared = new List<ToolCleared>();
        var kept = new List<ToolKept>();
        var listing = List(world);
        var comparer = listing.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var home = world.Home;
        foreach (var ask in asked.GroupBy(each => each.Name, comparer).Select(group => group.First()))
        {
            if (listing.Why is { } why) { kept.Add(new ToolKept(ask.Name, why)); continue; }
            if (listing.Folders.FirstOrDefault(each => comparer.Equals(each.Name, ask.Name)) is not { } folder)
            {
                kept.Add(new ToolKept(ask.Name, "not-listed"));
                continue;
            }

            if (folder.Id != ask.Id) { kept.Add(new ToolKept(folder.Name, "changed")); continue; }
            if (folder.Keeps is { } keeps) { kept.Add(new ToolKept(folder.Name, keeps)); continue; }

            var (bytes, newest, partial) = WiringSizes.MeasureFolder(folder.Path, budget ?? WiringBudget.Default, CancellationToken.None);
            if (partial) { kept.Add(new ToolKept(folder.Name, "unread")); continue; }
            if (Lately(folder.Path, newest, now)) { kept.Add(new ToolKept(folder.Name, "in-use")); continue; }

            // Right before it moves: the folder is the one listed, no link lies on the way, and nothing wrote to it meanwhile.
            if (LinkOnChain(folder.Path) is not null || FileId(folder.Path) != ask.Id) { kept.Add(new ToolKept(folder.Name, "changed")); continue; }
            if (Lately(folder.Path, null, now)) { kept.Add(new ToolKept(folder.Name, "in-use")); continue; }

            var aside = Path.Combine(DaorisHome.Entry(home!, HomeEntry.Trees), ".tidied", "tool-home", "projects");
            var to = Path.Combine(aside, $"{folder.Name}-{DateTimeOffset.UtcNow.ToString(TreeSetAside.SetAsideStamp, CultureInfo.InvariantCulture)}");
            try
            {
                Directory.CreateDirectory(aside);
                if (LinkOnChain(aside, DaorisHome.Entry(home!, HomeEntry.Trees)) is not null) { kept.Add(new ToolKept(folder.Name, "linked")); continue; }
                var sameVolume = string.Equals(Path.GetPathRoot(folder.Path), Path.GetPathRoot(to), StringComparison.OrdinalIgnoreCase);
                if (!copy && sameVolume) Directory.Move(folder.Path, to);
                else if (!CopyThenRemove(folder.Path, to)) { kept.Add(new ToolKept(folder.Name, "linked")); continue; }
                cleared.Add(new ToolCleared(folder.Name, bytes, to));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                kept.Add(new ToolKept(folder.Name, "not-moved"));
            }
        }

        return new ToolClear(cleared, kept);
    }

    /// <summary>Whether the folder or its newest file was written within <see cref="Quiet"/> of <paramref name="now"/>.</summary>
    private static bool Lately(string folder, DateTimeOffset? newest, DateTimeOffset now) =>
        newest is { } at && at > now - Quiet || Directory.GetLastWriteTimeUtc(folder) > (now - Quiet).UtcDateTime;

    /// <summary>
    /// The folder copied to <paramref name="to"/>, checked file for file and byte for byte, then removed: a move across volumes
    /// is no rename. A link met on the way is never followed: the copy is taken away and nothing is removed (false). Where the
    /// removal fails after a whole copy, the folder is counted moved and what is left of it is the person's to see.
    /// </summary>
    private static bool CopyThenRemove(string from, string to)
    {
        try
        {
            var (files, bytes) = CopyInto(from, to);
            var (copiedFiles, copiedBytes) = Count(to);
            if (files != copiedFiles || bytes != copiedBytes) throw new IOException("the copy did not match what it was copied from");
        }
        catch (NotSupportedException)
        {
            Directory.Delete(to, recursive: true);
            return false;
        }
        catch (IOException)
        {
            if (Directory.Exists(to)) Directory.Delete(to, recursive: true);
            throw;
        }

        try
        {
            Remove(from);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The copy is whole and kept; a partly removed original is left where it is.
        }

        return true;
    }

    private static (int Files, long Bytes) CopyInto(string from, string to)
    {
        Directory.CreateDirectory(to);
        var (files, bytes) = (0, 0L);
        foreach (var entry in new DirectoryInfo(from).EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false }))
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new NotSupportedException("a link is never followed");
            var target = Path.Combine(to, entry.Name);
            if (entry is DirectoryInfo inner)
            {
                var (more, size) = CopyInto(inner.FullName, target);
                files += more;
                bytes += size;
            }
            else
            {
                File.Copy(entry.FullName, target);
                files++;
                bytes += ((FileInfo)entry).Length;
            }
        }

        return (files, bytes);
    }

    private static (int Files, long Bytes) Count(string folder)
    {
        var (files, bytes) = (0, 0L);
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            files++;
            bytes += file.Length;
        }

        return (files, bytes);
    }

    private static void Remove(string folder)
    {
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            if (file.Attributes.HasFlag(FileAttributes.ReadOnly)) file.Attributes &= ~FileAttributes.ReadOnly;
        }

        Directory.Delete(folder, recursive: true);
    }
}
