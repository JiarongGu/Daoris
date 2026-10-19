using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>
/// A new session tree's dependencies (TREEDEPS1, D51's note): what its checkout installed with npm at the lockfile the tree holds,
/// copied into the tree as it opens, so its session does not run <c>npm ci</c> from nothing. A real copy, never a link to the
/// checkout's folder: a session's install deletes and rewrites its folder and its tools write caches there, which a link would
/// carry into the checkout, and node resolves a module through its real path.
/// </summary>
/// <remarks>
/// <para><b>The gate</b>, for each npm project, a folder holding a tracked lockfile, judged on its own: npm installs it (no pnpm or
/// yarn lockfile beside it); the tree's <c>package-lock.json</c> is the checkout's, byte for byte; the checkout's install is at it
/// (npm's hidden lockfile there, and not older than the lockfile, npm's own rule); a workspace's own lockfile, where one has one,
/// matches too; and git ignores what is copied, in the tree as in the checkout. Any miss copies nothing of that project, and the
/// opening says which and why.</para>
///
/// <para><b>The copy</b> is made under a name of its own beside where it goes, and renamed into place once whole, so an open cut
/// off leaves no half copy; the open's undo deletes the tree it is in. A link in it (npm's workspace link, on Windows a junction to
/// its absolute path) is made again as a link leading to the same place in the tree, never the checkout's; one that leads
/// anywhere else copies nothing. A cache at the top of a <c>node_modules</c> (<c>.cache</c>, <c>.vite</c>) names where it was
/// made, and is left for the tree's tools to make again.</para>
///
/// <para>It runs inline, eight files at once, on the measurement in D51's note: a fraction of the <c>npm ci</c> it saves.</para>
/// </remarks>
internal static class TreeDependencies
{
    private const string Lockfile = "package-lock.json";
    private const string Modules = "node_modules";
    private const string Hidden = ".package-lock.json";

    /// <summary>The name a copy is made under beside where it goes, a random tail after it.</summary>
    internal const string CopyingPrefix = ".daoris-copying-";

    /// <summary>Another manager's lockfile: its project is copied nothing.</summary>
    private static readonly string[] OtherLockfiles = ["pnpm-lock.yaml", "yarn.lock"];

    /// <summary>The caches at the top of a <c>node_modules</c> a copy leaves behind: each names where it was made.</summary>
    private static readonly HashSet<string> Caches = new(StringComparer.OrdinalIgnoreCase) { ".cache", ".vite", ".vite-temp" };

    /// <summary>git's pathspecs for every tracked lockfile, at any depth.</summary>
    private static readonly string[] Pathspecs = [$":(glob)**/{Lockfile}", .. OtherLockfiles.Select(name => $":(glob)**/{name}")];

    /// <summary>How many files are copied at once: eight, past which the measurement in D51's note found nothing faster.</summary>
    private static readonly int AtOnce = Math.Clamp(Environment.ProcessorCount, 1, 8);

    /// <summary>
    /// The test seam: run with each file's path in the checkout before it is copied, where a test cuts the open off. Production
    /// sets none.
    /// </summary>
    internal static readonly AsyncLocal<Action<string>?> Copying = new();

    /// <summary>
    /// The checkout's dependencies copied into <paramref name="tree"/>, a tree of <paramref name="root"/> git has just checked out:
    /// git names the tracked lockfiles and what it ignores, and <see cref="Copy"/> does the rest. A cancel stops it, leaving no half
    /// copy; anything else that goes wrong copies nothing of that project, and says so.
    /// </summary>
    internal static async Task<DependencyCopy> CopyAsync(string root, string tree, CancellationToken ct)
    {
        var (code, listed, _) = await WorkingTree.GitAsync(tree, ["ls-files", "-z", "--", .. Pathspecs], ct).ConfigureAwait(false);
        if (code != 0) return DependencyCopy.None;
        var plans = Plan(root, tree, listed.Split('\0', StringSplitOptions.RemoveEmptyEntries));
        var folders = plans.Where(plan => plan.Why is null).SelectMany(plan => plan.Folders).ToList();
        var ignored = folders.Count == 0 ? [] : await IgnoredAsync(tree, folders, ct).ConfigureAwait(false);
        return await Task.Run(() => Copy(root, tree, plans, ignored.Contains, ct), ct).ConfigureAwait(false);
    }

    /// <summary>The gate and the copy, git's facts handed in: <paramref name="lockfiles"/> tracked, <paramref name="ignored"/> by folder.</summary>
    internal static DependencyCopy Reuse(
        string root, string tree, IReadOnlyCollection<string> lockfiles, Func<string, bool> ignored, CancellationToken ct) =>
        Copy(root, tree, Plan(root, tree, lockfiles), ignored, ct);

    /// <summary>
    /// Each npm project's copy: its folders, or why it takes none. A project is a folder of a tracked lockfile, in git's spelling;
    /// a workspace's own lockfile is its project's to judge, never a project of its own.
    /// </summary>
    internal static IReadOnlyList<DependencyPlan> Plan(string root, string tree, IReadOnlyCollection<string> lockfiles)
    {
        var projects = lockfiles
            .GroupBy(FolderOf, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(NameOf).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var workspaces = projects.Keys
            .Where(folder => projects[folder].Contains(Lockfile))
            .ToDictionary(folder => folder, folder => WorkspacesOf(Path.Combine(tree, folder, Lockfile)), StringComparer.Ordinal);
        var owned = workspaces.SelectMany(each => each.Value?.Select(workspace => Join(each.Key, workspace)) ?? []).ToHashSet(StringComparer.Ordinal);

        var plans = new List<DependencyPlan>();
        foreach (var folder in projects.Keys.Where(folder => !owned.Contains(folder)).Order(StringComparer.Ordinal))
        {
            var modules = Join(folder, Modules);
            if (projects[folder].Overlaps(OtherLockfiles) || !projects[folder].Contains(Lockfile))
            {
                plans.Add(new(modules, [], UncopiedWhy.Manager));
                continue;
            }

            var checkoutLock = Path.Combine(root, folder, Lockfile);
            if (!SameBytes(Path.Combine(tree, folder, Lockfile), checkoutLock))
            {
                plans.Add(new(modules, [], UncopiedWhy.Moved));
                continue;
            }

            // The tree's lockfile is the checkout's, so its workspaces are the ones the checkout installed.
            var own = workspaces[folder];
            var moved = own?.FirstOrDefault(workspace => projects.TryGetValue(Join(folder, workspace), out var names)
                && (names.Overlaps(OtherLockfiles) || !SameBytes(Path.Combine(tree, folder, workspace, Lockfile), Path.Combine(root, folder, workspace, Lockfile))));
            if (moved is not null)
            {
                plans.Add(new(modules, [], UncopiedWhy.Workspace, moved));
                continue;
            }

            var hidden = Path.Combine(root, folder, Modules, Hidden);
            if (own is null || !File.Exists(hidden) || File.GetLastWriteTimeUtc(hidden) < File.GetLastWriteTimeUtc(checkoutLock))
            {
                plans.Add(new(modules, [], UncopiedWhy.Uninstalled));
                continue;
            }

            // npm installs a workspace's own dependencies beside it where they cannot be hoisted: copied with the project's.
            var folders = new List<string> { modules };
            folders.AddRange(own.Select(workspace => Join(folder, workspace, Modules)).Where(each => Path.Exists(Path.Combine(root, each))));
            plans.Add(new(modules, folders, null));
        }

        return plans;
    }

    /// <summary>Each plan copied whole, or nothing of it; a cancel undoes the plan under way and stops.</summary>
    internal static DependencyCopy Copy(string root, string tree, IReadOnlyList<DependencyPlan> plans, Func<string, bool> ignored, CancellationToken ct)
    {
        if (plans.Count == 0) return DependencyCopy.None;
        root = Path.GetFullPath(root);
        tree = Path.GetFullPath(tree);
        var copied = new List<string>();
        var declined = new List<Uncopied>();
        foreach (var plan in plans)
        {
            if (plan.Why is { } why)
            {
                declined.Add(new(plan.Modules, why, plan.Workspace));
                continue;
            }

            if (!plan.Folders.All(ignored))
            {
                declined.Add(new(plan.Modules, UncopiedWhy.Unignored));
                continue;
            }

            // Everything this plan made, deleted again if it does not finish.
            var made = new List<string>();
            try
            {
                var whole = new List<(string Under, string Final)>();
                string? refused = null;
                foreach (var folder in plan.Folders)
                {
                    var final = Path.Combine(tree, folder);
                    var under = Path.Combine(Path.GetDirectoryName(final)!, CopyingPrefix + Guid.NewGuid().ToString("N")[..8]);
                    made.Add(under);
                    if ((refused = CopyFolder(root, tree, folder, Path.Combine(under, Modules), plan.Folders, ct)) is not null) break;
                    whole.Add((under, final));
                }

                if (refused is not null)
                {
                    Discard(made);
                    declined.Add(new(plan.Modules, refused));
                    continue;
                }

                // Whole: each renamed into place, tried again while the virus scanner holds a file just written in it (FIX-LOG
                // 2026-10-07, as an installed binary is), and the empty folder it was made under goes where it can.
                foreach (var (under, final) in whole)
                {
                    AtomicFile.MoveFolder(Path.Combine(under, Modules), final);
                    made.Add(final);
                    try
                    {
                        Directory.Delete(under);
                        made.Remove(under);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        // Empty, so git shows nothing of it, and the tree's removal takes it.
                    }
                }

                copied.AddRange(plan.Folders);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                Discard(made);
                ct.ThrowIfCancellationRequested();
                throw;
            }
            catch (Exception error) when (Failure(error) is { } said)
            {
                Discard(made);
                declined.Add(new(plan.Modules, UncopiedWhy.Failed, Detail: said));
            }
        }

        return new(copied, declined);
    }

    /// <summary>
    /// One folder of the checkout's copied <paramref name="into"/> a folder of its own beside where it goes, made folder by folder
    /// and link by link, then its files eight at once. Null once whole, or why it copied nothing.
    /// </summary>
    private static string? CopyFolder(string root, string tree, string folder, string into, IReadOnlyList<string> copying, CancellationToken ct)
    {
        var source = Path.Combine(root, folder);
        if (new DirectoryInfo(source).Attributes.HasFlag(FileAttributes.ReparsePoint)) return UncopiedWhy.Link;

        var files = new List<(string From, string To)>();
        (string From, string To)? hidden = null;
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var walk = new Stack<(string From, string To)>();
        walk.Push((source, into));
        while (walk.TryPop(out var at))
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(at.To);
            var top = at.From == source;
            foreach (var entry in new DirectoryInfo(at.From).EnumerateFileSystemInfos("*", options))
            {
                var to = Path.Combine(at.To, entry.Name);
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    // 🔴 Never followed, and made to lead to the same place in the tree, never where the checkout's does: git's
                    // removal of a tree follows a junction and deletes what it leads to (D51's note).
                    if (LeadsInTree(root, tree, copying, entry) is not { } there) return UncopiedWhy.Link;
                    MakeLink(to, Path.Combine(tree, Path.GetRelativePath(root, entry.FullName)), there, entry is DirectoryInfo);
                }
                else if (entry is DirectoryInfo)
                {
                    if (!(top && Caches.Contains(entry.Name))) walk.Push((entry.FullName, to));
                }
                else if (top && entry.Name == Hidden)
                {
                    hidden = (entry.FullName, to);
                }
                else
                {
                    files.Add((entry.FullName, to));
                }
            }
        }

        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = AtOnce, CancellationToken = ct }, each =>
        {
            Copying.Value?.Invoke(each.From);
            File.Copy(each.From, each.To);
        });
        ct.ThrowIfCancellationRequested();

        // npm trusts its hidden lockfile only where no folder of the install is newer: written last, and now.
        if (hidden is { } lockfile)
        {
            File.Copy(lockfile.From, lockfile.To);
            File.SetLastWriteTimeUtc(lockfile.To, DateTime.UtcNow);
        }

        return null;
    }

    /// <summary>
    /// Where a link of the checkout's leads in the tree: the same place, relative to the checkout, where that is inside the
    /// checkout and the tree holds it or this copy makes it; null anywhere else. Read off the link, never through it.
    /// </summary>
    private static string? LeadsInTree(string root, string tree, IReadOnlyList<string> copying, FileSystemInfo link)
    {
        if (link.LinkTarget is not { Length: > 0 } target) return null;
        var leads = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(link.FullName)!, StripPrefix(target)));
        var within = Path.GetRelativePath(root, leads);
        if (within == "." || within == ".." || within.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(within))
        {
            return null;
        }

        var there = Path.Combine(tree, within);
        var made = copying.Any(folder =>
        {
            var spelled = folder.Replace('/', Path.DirectorySeparatorChar);
            return Same(within, spelled) || within.StartsWith(spelled + Path.DirectorySeparatorChar, PathComparison);
        });
        return made || Path.Exists(there) ? there : null;
    }

    /// <summary>
    /// A link at <paramref name="path"/> to the folder <paramref name="target"/>: a junction on Windows, which needs no privilege
    /// and is what npm makes there, and a symbolic link relative to where it is elsewhere.
    /// </summary>
    internal static void Link(string path, string target) => MakeLink(path, path, target, directory: true);

    /// <summary>
    /// A link made at <paramref name="path"/> that will stand at <paramref name="final"/> once its copy is renamed into place: a
    /// junction to the full path on Windows for a folder; elsewhere, and for a file, a symbolic link relative to where it will stand.
    /// </summary>
    private static void MakeLink(string path, string final, string target, bool directory)
    {
        if (directory && OperatingSystem.IsWindows())
        {
            Junction(Path.GetFullPath(path), Path.GetFullPath(target));
            return;
        }

        var relative = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(final))!, target);
        if (directory) Directory.CreateSymbolicLink(path, relative);
        else File.CreateSymbolicLink(path, relative);
    }

    /// <summary>
    /// A junction at <paramref name="path"/> to <paramref name="target"/>: an empty folder given a mount point's reparse data, as
    /// <c>mklink /J</c> and npm make one, with no process started. A junction that could not be made leaves no folder.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void Junction(string path, string target)
    {
        target = StripPrefix(target);
        var substitute = Encoding.Unicode.GetBytes(target.StartsWith(@"\\", StringComparison.Ordinal) ? @"\??\UNC\" + target[2..] : @"\??\" + target);
        var print = Encoding.Unicode.GetBytes(target);
        // The mount point's reparse data: its tag and length, then the two names' offsets and lengths, then each name and its NUL.
        var data = 8 + substitute.Length + 2 + print.Length + 2;
        var buffer = new byte[8 + data];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0), 0xA0000003);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4), checked((ushort)data));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(8), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(10), checked((ushort)substitute.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(12), checked((ushort)(substitute.Length + 2)));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(14), checked((ushort)print.Length));
        substitute.CopyTo(buffer, 16);
        print.CopyTo(buffer, 16 + substitute.Length + 2);

        Directory.CreateDirectory(path);
        try
        {
            using var handle = CreateFileW(
                path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path,
                GenericWrite, 0, IntPtr.Zero, OpenExisting, BackupSemantics | OpenReparsePoint, IntPtr.Zero);
            if (handle.IsInvalid) throw new IOException($"could not open {path} to make it a link: {new Win32Exception(Marshal.GetLastPInvokeError()).Message}");
            if (!DeviceIoControl(handle, SetReparsePoint, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
            {
                throw new IOException($"could not make {path} a link: {new Win32Exception(Marshal.GetLastPInvokeError()).Message}");
            }
        }
        catch
        {
            try
            {
                Directory.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The copy it was part of is discarded whole.
            }

            throw;
        }
    }

    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint SetReparsePoint = 0x000900A4;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device, uint code, byte[] input, int inputSize, IntPtr output, int outputSize, out int returned, IntPtr overlapped);

    /// <summary>The folders git ignores in the tree among <paramref name="folders"/>, as given; none where git cannot say.</summary>
    private static async Task<HashSet<string>> IgnoredAsync(string tree, IReadOnlyList<string> folders, CancellationToken ct)
    {
        // A folder's own slash makes git read it as one, though the tree does not hold it yet.
        var (code, said, _) = await WorkingTree.GitAsync(tree, ["check-ignore", "-z", "--", .. folders.Select(folder => folder + "/")], ct)
            .ConfigureAwait(false);
        var ignored = new HashSet<string>(StringComparer.Ordinal);
        if (code is not (0 or 1)) return ignored;
        foreach (var each in said.Split('\0', StringSplitOptions.RemoveEmptyEntries)) ignored.Add(each.TrimEnd('/'));
        return ignored;
    }

    /// <summary>What was made of a copy that did not finish, deleted never following a link (TIDIEDLINK1), as far as it goes.</summary>
    private static void Discard(IEnumerable<string> made)
    {
        foreach (var each in made.Where(Path.Exists))
        {
            try
            {
                TreeSetAside.DeleteNeverFollowing(each);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Inside the tree, which an open cut off removes whole, and which a session's own install then replaces.
            }
        }
    }

    /// <summary>The words of a copy that failed, where it failed as a file can; null for anything else, which is not caught.</summary>
    private static string? Failure(Exception error) => error switch
    {
        IOException or UnauthorizedAccessException => error.Message.Trim(),
        AggregateException all when all.Flatten().InnerExceptions is { Count: > 0 } inner
            && inner.All(each => each is IOException or UnauthorizedAccessException) => inner[0].Message.Trim(),
        _ => null,
    };

    /// <summary>The workspaces a lockfile names, each a folder relative to it; null where it does not read as npm's.</summary>
    private static IReadOnlyList<string>? WorkspacesOf(string lockfile)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(lockfile));
            if (!document.RootElement.TryGetProperty("packages", out var packages) || packages.ValueKind != JsonValueKind.Object) return null;
            // Every package npm installs is under a `node_modules`; one that is not is a workspace or a folder linked in, whose
            // own dependencies npm installs beside it.
            return
            [
                .. packages.EnumerateObject()
                    .Select(package => package.Name)
                    .Where(name => name.Length > 0 && !name.Split('/').Contains(Modules) && !name.Split('/').Contains("..") && !Path.IsPathRooted(name)),
            ];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static bool SameBytes(string one, string other)
    {
        try
        {
            return File.Exists(one) && File.Exists(other) && File.ReadAllBytes(one).AsSpan().SequenceEqual(File.ReadAllBytes(other));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>A path a link names as Windows keeps it, its <c>\\?\</c> or <c>\??\</c> prefix gone.</summary>
    private static string StripPrefix(string path) =>
        path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal) || path.StartsWith(@"\??\UNC\", StringComparison.Ordinal) ? @"\\" + path[8..]
        : path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\??\", StringComparison.Ordinal) ? path[4..]
        : path;

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool Same(string one, string other) => string.Equals(one, other, PathComparison);

    /// <summary>A lockfile's folder in git's spelling: empty for the repository's own.</summary>
    private static string FolderOf(string lockfile) => lockfile.LastIndexOf('/') is var slash and > 0 ? lockfile[..slash] : "";

    private static string NameOf(string lockfile) => lockfile[(lockfile.LastIndexOf('/') + 1)..];

    /// <summary>Folders joined in git's spelling, an empty one dropped.</summary>
    private static string Join(params string[] parts) => string.Join('/', parts.Where(part => part.Length > 0));
}

/// <summary>One npm project's copy (TREEDEPS1): its own <c>node_modules</c> as the opening names it, the folders it takes, or why none.</summary>
/// <param name="Modules">The project's <c>node_modules</c>, relative to the repository in git's spelling.</param>
/// <param name="Folders">Every folder it copies, its workspaces' own among them; empty where it copies none.</param>
/// <param name="Why">Why it copies none (<see cref="UncopiedWhy"/>); null where it copies.</param>
/// <param name="Workspace">The workspace whose own lockfile moved, where that is why.</param>
internal sealed record DependencyPlan(string Modules, IReadOnlyList<string> Folders, string? Why, string? Workspace = null);

/// <summary>What a tree's open copied of its checkout's dependencies (TREEDEPS1), and why it copied none of the rest.</summary>
/// <param name="Copied">The folders copied, relative to the repository in git's spelling.</param>
/// <param name="Declined">Each project that copied nothing, and why.</param>
internal sealed record DependencyCopy(IReadOnlyList<string> Copied, IReadOnlyList<Uncopied> Declined)
{
    /// <summary>A repository with no npm project: nothing copied, and nothing to say.</summary>
    public static DependencyCopy None { get; } = new([], []);

    /// <summary>The folders copied, each in code, as a sentence lists them.</summary>
    public string Listed => string.Join(", ", Copied.Select(folder => $"`{folder}`"));

    /// <summary>
    /// What the session's agent is told, where anything was copied: without it the session runs the <c>npm ci</c> it always ran,
    /// which deletes the copy first, and the copy saves nothing.
    /// </summary>
    public string? Told => Copied.Count == 0
        ? null
        : $"Daoris copied this tree's installed dependencies from the repository's checkout, whose lockfile matches this tree's: {Listed}. "
          + "They are ready to use: do not run `npm ci` where they are, which deletes them and installs everything again, unless that "
          + "folder's `package-lock.json` changes.";
}

/// <summary>A project whose dependencies were not copied into a new tree (TREEDEPS1), and why: a line of the tree's opening.</summary>
/// <param name="Modules">The project's <c>node_modules</c>, relative to the repository in git's spelling.</param>
/// <param name="Why">Why (<see cref="UncopiedWhy"/>).</param>
/// <param name="Workspace">The workspace whose own lockfile moved, where that is why.</param>
/// <param name="Detail">What failed, in the program's words, where the copy did.</param>
internal sealed record Uncopied(string Modules, string Why, string? Workspace = null, string? Detail = null)
{
    /// <summary>Its line of the opening, with its code.</summary>
    public Noted Line()
    {
        var not = $"`{Modules}` was not copied from your checkout: ";
        var modules = ("modules", (object?)Modules);
        return Why switch
        {
            UncopiedWhy.Manager => Noted.Of(NoteCodes.StartedUncopiedManager, not + "pnpm or yarn installs it, and Daoris copies only what npm installed.", modules),
            UncopiedWhy.Moved => Noted.Of(NoteCodes.StartedUncopiedMoved, not + "this tree's lockfile beside it differs from the checkout's, so it is installed afresh.", modules),
            UncopiedWhy.Uninstalled => Noted.Of(NoteCodes.StartedUncopiedUninstalled, not + "the checkout holds no install of it at its lockfile.", modules),
            UncopiedWhy.Workspace => Noted.Of(
                NoteCodes.StartedUncopiedWorkspace, not + $"the lockfile of its npm workspace `{Workspace}` differs from the checkout's.", modules, ("workspace", Workspace)),
            UncopiedWhy.Link => Noted.Of(NoteCodes.StartedUncopiedLink, not + "a link in it leads outside what this tree holds.", modules),
            UncopiedWhy.Unignored => Noted.Of(NoteCodes.StartedUncopiedUnignored, not + "git does not ignore it here, so a copy would read as this tree's work.", modules),
            _ when Detail is { Length: > 0 } said => Noted.Of(NoteCodes.StartedUncopiedFailed, not + $"the copy failed ({said}).", modules)
                .Also(NotePart.Said(said, NoteBy.Program)),
            _ => Noted.Of(NoteCodes.StartedUncopiedFailed, not + "the copy failed.", modules),
        };
    }
}

/// <summary>Why a project's dependencies were not copied into a new tree (TREEDEPS1): each its own line's code.</summary>
internal static class UncopiedWhy
{
    /// <summary>pnpm or yarn installs it.</summary>
    public const string Manager = "manager";

    /// <summary>The tree's lockfile is not the checkout's.</summary>
    public const string Moved = "moved";

    /// <summary>The checkout holds no install at its lockfile: none, no hidden lockfile, or one older than the lockfile.</summary>
    public const string Uninstalled = "uninstalled";

    /// <summary>A workspace's own lockfile is not the checkout's.</summary>
    public const string Workspace = "workspace";

    /// <summary>A link in it leads anywhere but the checkout's own files the tree holds.</summary>
    public const string Link = "link";

    /// <summary>git does not ignore it in the tree.</summary>
    public const string Unignored = "unignored";

    /// <summary>The copy failed as a file can.</summary>
    public const string Failed = "failed";
}
