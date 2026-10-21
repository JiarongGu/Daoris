namespace Daoris.Knowledge;

/// <summary>
/// Whether the one checkout a knowledge server reads has moved since it was read, by a watch of its git refs and its
/// knowledge folders rather than a timer (FABRIC-EVENT6; the events design §A1, §A4.1).
/// </summary>
/// <remarks>
/// <para><b>A hint is a wake, never a fact.</b> A watcher marks the reading stale and says nothing of what changed; the
/// next search reads the checkout again. An overflow marks stale; so does any watcher error, which also takes the watch out
/// of service, so the server falls back to its minute (<see cref="ServiceOptions.CheckoutRereadAfter"/>) until the process
/// restarts. A watch never marks fresh.</para>
///
/// <para><b>The deadline is the reconciliation behind it</b> (§A3): a watcher fails quietly as well as loudly, and a
/// folder that no watcher could open moves unheard. A watched reading is therefore read again once it is
/// <see cref="Deadline"/> old, whatever was heard.</para>
///
/// <para><b>A folder made later is watched when it is made</b> (FABRIC-EVENT6b): a knowledge folder or a declared room's
/// folder that is not there is awaited in the deepest folder of its path that is, and opened when that hears it made; a
/// folder made inside one watched in parts is opened the same way. Each declared room (<see cref="RepositoryLayout.Rooms"/>)
/// outside the knowledge folders is watched for its <c>AGENTS.md</c> alone, and the rooms and roots are read again when the
/// checkout's <c>daoris.json</c> or <c>daoris.lock</c> moves.</para>
///
/// <para>Why not the driver's <c>ChangeWatch</c>: it lives in the desktop's driver, and the service is the layer beneath the
/// desktop, which hosts it, so it cannot reference upward. The service needs one flag, not a topic queue (the driver's
/// debounce and per-subject judgement have nothing to coalesce, since marking stale twice is marking once), so this is its own
/// small watcher over the same <see cref="FileSystemWatcher"/>, with the same rules: a hint is a wake, an overflow or an
/// error is never taken for fresh.</para>
///
/// <para><b>No watch reaches what git ignores</b> (KNOWWALK1): a watched folder with an ignored folder in it is watched in
/// parts around it (<see cref="IgnoredPaths"/>), so <c>.claude/worktrees/</c>, every agent's checkout, wakes nothing.</para>
///
/// <para>Its own writes wake nothing: a refresh only reads the checkout.</para>
/// </remarks>
public sealed class CheckoutFreshness : IDisposable
{
    /// <summary>
    /// How old a watched reading may be before a search reads it again whatever the watch heard. Five minutes: the
    /// lower end of the design's range, since a silenced watcher costs a stale answer for at most this long and a read is
    /// one to two seconds (this repository's 7,724 entries, measured under ORIENT1c) paid at most once per window by the search
    /// that finds it due.
    /// </summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromMinutes(5);

    /// <summary>A room's instructions, the one file of its folder the scan reads (D117 §2.2).</summary>
    private const string RoomInstructions = "AGENTS.md";

    /// <summary>A path's case as the disk compares it: Windows and macOS fold it, Linux does not.</summary>
    private static readonly StringComparer Paths = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly object _gate = new();
    private readonly object _reconciling = new();
    // Each folder watched, by what it is watched for, so a folder heard made twice is opened once.
    private readonly HashSet<string> _opened = new(Paths);
    // Each folder that is there, and the names awaited in it: the next step of a knowledge folder's or a room's path.
    private readonly Dictionary<string, HashSet<string>> _awaited = new(Paths);
    private readonly string? _checkout;
    private readonly string? _documents;
    private readonly string? _index;
    private int _stale;
    private bool _lost;
    private bool _silent;
    private bool _disposed;

    /// <summary>Watches a checkout's refs, its knowledge folders and the documents and index a deployment names.</summary>
    /// <param name="checkout">The checkout's root.</param>
    /// <param name="documents">The documents folder the deployment names, relative to the checkout, or null.</param>
    /// <param name="index">The index folder the deployment names, relative to the checkout, or null.</param>
    public CheckoutFreshness(string checkout, string? documents, string? index)
    {
        _checkout = checkout;
        _documents = documents;
        _index = index;
        OpenAll();
    }

    private CheckoutFreshness() => _silent = true;

    /// <summary>A watch that hears nothing and has not failed: what a test holds to see the deadline catch a silenced source.</summary>
    internal static CheckoutFreshness Silent() => new();

    /// <summary>Whether the watch is in service: at least one watcher open, and none lost to an error.</summary>
    public bool Watching
    {
        get
        {
            lock (_gate) return !_lost && (_silent || _watchers.Count > 0);
        }
    }

    /// <summary>Whether something moved under the watch since the reading was last taken.</summary>
    public bool IsStale => Volatile.Read(ref _stale) != 0;

    /// <summary>Says the checkout moved.</summary>
    public void MarkStale() => Volatile.Write(ref _stale, 1);

    /// <summary>
    /// Takes the mark, clearing it, and says whether it was set. Taken when a read BEGINS, so a change heard during the
    /// read marks the next one.
    /// </summary>
    public bool Take() => Interlocked.Exchange(ref _stale, 0) != 0;

    /// <summary>A watcher's error: stale at once, and out of service unless it was only an overflow, which loses events and not the watcher.</summary>
    internal void Failed(Exception error)
    {
        MarkStale();
        if (error is InternalBufferOverflowException) return;
        lock (_gate) _lost = true;
    }

    private void OpenAll()
    {
        var root = _checkout!;
        // The checkout's own top: its AGENTS.md, README, backlog and the lock that says what it owns, not every file there;
        // and a knowledge folder, or the first folder of a room's path, made there after the watch opened (FABRIC-EVENT6b).
        OpenFolder(
            root,
            recursive: false,
            hears: name => IsRecord(name) || Awaits(root, name),
            then: name =>
            {
                if (name is "daoris.json" or "daoris.lock" || Awaits(root, name)) Reconciled();
            });
        Reconcile();

        if (GitFolders(root) is { } git)
        {
            // HEAD (a switch), the branches (a commit) and packed-refs (a pack): what a merge or a switch moves.
            OpenFolder(git.Own, recursive: false, hears: name => name == "HEAD");
            OpenFolder(git.Common, recursive: false, hears: name => name is "HEAD" or "packed-refs");
            OpenFolder(Path.Combine(git.Common, "refs", "heads"), recursive: true);
        }
    }

    /// <summary>
    /// Opens a watch for each knowledge folder and each declared room that has none and is there, and awaits each that is
    /// not (FABRIC-EVENT6b). Run at the start, when a folder awaited is heard made, and when the checkout's
    /// <c>daoris.json</c> or <c>daoris.lock</c> moves, since they say which roots and rooms the scan reads.
    /// </summary>
    private void Reconcile()
    {
        var root = _checkout!;
        lock (_reconciling)
        {
            var layout = RepositoryLayout.Of(root, DaorisLock.Read(root));
            var trees = Trees(root, layout);
            var late = new List<string>();
            foreach (var tree in trees)
            {
                if (Opened("tree", Absolute(root, tree))) continue;
                if (Directory.Exists(Absolute(root, tree))) late.Add(tree);
                else Await(root, tree);
            }

            if (late.Count > 0)
            {
                // What git ignores in them, asked once for the folders opened together (KNOWWALK1): `.claude/worktrees/` is
                // every agent's checkout. At the start that is every folder there; later, the one just made.
                var ignored = IgnoredPaths.Of(root, [.. late]);
                foreach (var tree in late) OpenTree(root, tree, ignored);
            }

            foreach (var room in layout.Rooms)
            {
                // A room inside a knowledge folder is heard by that folder's watch.
                if (trees.Any(tree => RepositoryLayout.Within(room, tree))) continue;
                var folder = Absolute(root, room);
                if (!Directory.Exists(folder))
                {
                    Await(root, room);
                    continue;
                }

                if (First("room", folder))
                {
                    OpenFolder(folder, recursive: false, hears: name => string.Equals(name, RoomInstructions, StringComparison.OrdinalIgnoreCase));
                }
            }
        }
    }

    /// <summary>
    /// Reconciles after a folder was heard made or the declarations moved, then marks the reading stale again: what was written
    /// in a new folder before its watch opened is read at the next search, not at the deadline.
    /// </summary>
    private void Reconciled()
    {
        Reconcile();
        MarkStale();
    }

    /// <summary>
    /// The folders watched whole or in parts, repository-relative with forward slashes, each once: the layout's roots, the
    /// documents and the named folders not already within one.
    /// </summary>
    private List<string> Trees(string root, RepositoryLayout layout)
    {
        var trees = new List<string>();
        foreach (var named in layout.Roots.Append("docs").Append(_documents).Append(_index))
        {
            if (string.IsNullOrWhiteSpace(named)) continue;
            var tree = named.Replace('\\', '/').Trim('/');
            var folder = Absolute(root, tree);
            if (!trees.Any(other => Paths.Equals(Absolute(root, other), folder) || IsUnder(folder, Absolute(root, other)))) trees.Add(tree);
        }

        return trees;
    }

    /// <summary>
    /// Awaits a folder that is not there: the deepest folder of its path that is, watched for the next name on the path. The
    /// checkout's top is watched already; any other is opened once, whatever is awaited in it.
    /// </summary>
    /// <param name="relative">The folder awaited, repository-relative with forward slashes.</param>
    private void Await(string root, string relative)
    {
        var folder = root;
        foreach (var name in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Combine(folder, name);
            if (Directory.Exists(next))
            {
                folder = next;
                continue;
            }

            bool first;
            lock (_gate)
            {
                first = !_awaited.TryGetValue(folder, out var names);
                if (first) _awaited[folder] = names = new HashSet<string>(Paths);
                names!.Add(name);
            }

            var watched = folder;
            if (first && !Paths.Equals(watched, root))
            {
                OpenFolder(watched, recursive: false, hears: heard => Awaits(watched, heard), then: _ => Reconciled());
            }

            return;
        }
    }

    /// <summary>Whether a name is awaited in a folder: the next step of a folder's path not made yet.</summary>
    private bool Awaits(string folder, string name)
    {
        lock (_gate) return _awaited.TryGetValue(folder, out var names) && names.Contains(name);
    }

    /// <summary>Whether a folder was opened for this purpose already.</summary>
    private bool Opened(string purpose, string folder)
    {
        lock (_gate) return _opened.Contains($"{purpose}:{folder}");
    }

    /// <summary>Claims a folder's opening for this purpose: true the first time, so two hearings open it once.</summary>
    private bool First(string purpose, string folder)
    {
        lock (_gate) return _opened.Add($"{purpose}:{folder}");
    }

    private static string Absolute(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// Watches a folder and all of it git does not ignore (KNOWWALK1): one watch of the whole where nothing within is
    /// ignored, else this folder alone, hearing none of its ignored entries, and each folder in it git does not ignore, the
    /// same way. A recursive watch hears every write beneath it, and under <c>.claude/</c> that was every agent's build and
    /// install in <c>.claude/worktrees/</c>, each a reading marked stale for nothing. A folder made later, inside a folder
    /// watched alone, is heard being made and watched the same way from then (FABRIC-EVENT6b), by git's answer as it was
    /// asked: a folder git would ignore that is made later inside one watched alone is heard, for nothing, until the next start.
    /// </summary>
    /// <param name="relative">The folder, repository-relative with forward slashes.</param>
    private void OpenTree(string root, string relative, IgnoredPaths ignored)
    {
        var folder = Absolute(root, relative);
        if (!First("tree", folder) || ignored.Covers($"{relative}/")) return;
        if (!ignored.AnyWithin(relative))
        {
            OpenFolder(folder, recursive: true);
            return;
        }

        OpenFolder(
            folder,
            recursive: false,
            hears: name => !ignored.Covers($"{relative}/{name}") && !ignored.Covers($"{relative}/{name}/"),
            then: name =>
            {
                if (!Directory.Exists(Path.Combine(folder, name))) return;
                OpenTree(root, $"{relative}/{name}", ignored);
                MarkStale();
            });
        IEnumerable<string> below;
        try
        {
            below = Directory.EnumerateDirectories(folder).Select(Path.GetFileName).OfType<string>().ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var name in below) OpenTree(root, $"{relative}/{name}", ignored);
    }

    /// <summary>Opens a watcher, or says nothing when the folder is absent: a folder made later is awaited by its caller.</summary>
    /// <param name="hears">Which events mark stale, by the name of the file or folder that moved; null hears every one.</param>
    /// <param name="then">What follows an event heard, after the mark, given the same name.</param>
    private void OpenFolder(string folder, bool recursive, Func<string, bool>? hears = null, Action<string>? then = null)
    {
        if (!Directory.Exists(folder)) return;

        try
        {
            var watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = recursive,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
            };
            void Heard(string? path)
            {
                var name = Path.GetFileName(path ?? string.Empty);
                if (hears is not null && !hears(name)) return;
                MarkStale();
                try
                {
                    then?.Invoke(name);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
                {
                    // A folder heard made that could not be opened: the mark is set, and the deadline covers what follows.
                }
            }

            watcher.Changed += (_, e) => Heard(e.Name);
            watcher.Created += (_, e) => Heard(e.Name);
            watcher.Deleted += (_, e) => Heard(e.Name);
            watcher.Renamed += (_, e) =>
            {
                Heard(e.OldName);
                Heard(e.Name);
            };
            watcher.Error += (_, e) => Failed(e.GetException());
            watcher.EnableRaisingEvents = true;
            lock (_gate)
            {
                if (_disposed)
                {
                    watcher.Dispose();
                    return;
                }

                _watchers.Add(watcher);
            }
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // This folder is unwatched; the deadline covers it, and the others still say.
        }
    }

    private static bool IsRecord(string name) =>
        name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string folder, string parent)
    {
        var relative = Path.GetRelativePath(parent, folder);
        return relative != "." && !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    /// <summary>
    /// The checkout's git folder and the common one its branches live in: the same for a plain clone; for a worktree,
    /// <c>.git</c> is a file naming its own folder, whose <c>commondir</c> names the shared one.
    /// </summary>
    private static (string Own, string Common)? GitFolders(string root)
    {
        try
        {
            var dot = Path.Combine(root, ".git");
            if (Directory.Exists(dot)) return (dot, dot);
            if (!File.Exists(dot)) return null;

            var line = File.ReadLines(dot).FirstOrDefault(text => text.StartsWith("gitdir:", StringComparison.Ordinal));
            if (line is null) return null;

            var own = Path.GetFullPath(line["gitdir:".Length..].Trim(), root);
            if (!Directory.Exists(own)) return null;

            var commonFile = Path.Combine(own, "commondir");
            var common = File.Exists(commonFile)
                ? Path.GetFullPath(File.ReadAllText(commonFile).Trim(), own)
                : own;
            return Directory.Exists(common) ? (own, common) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        List<FileSystemWatcher> open;
        lock (_gate)
        {
            _disposed = true;
            open = [.. _watchers];
            _watchers.Clear();
        }

        foreach (var watcher in open) watcher.Dispose();
    }
}
