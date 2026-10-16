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
/// folder the scan reads that no watcher covers (a room's own <c>AGENTS.md</c> deep in the tree) moves unheard. A watched
/// reading is therefore read again once it is <see cref="Deadline"/> old, whatever was heard.</para>
///
/// <para>Why not the driver's <c>ChangeWatch</c>: it lives in the desktop's driver, and the service is the layer beneath the
/// desktop, which hosts it, so it cannot reference upward. The service needs one flag, not a topic queue (the driver's
/// debounce and per-subject judgement have nothing to coalesce, since marking stale twice is marking once), so this is its own
/// small watcher over the same <see cref="FileSystemWatcher"/>, with the same rules: a hint is a wake, an overflow or an
/// error is never taken for fresh.</para>
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

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly object _gate = new();
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
        // The checkout's own top: its AGENTS.md, README, backlog and the lock that says what it owns, not every file there.
        OpenFolder(root, recursive: false, hears: name => IsRecord(name));
        foreach (var folder in new[] { ".claude", "docs" }) OpenFolder(Path.Combine(root, folder), recursive: true);
        foreach (var named in new[] { _documents, _index })
        {
            if (string.IsNullOrWhiteSpace(named)) continue;
            var folder = Path.Combine(root, named.Replace('/', Path.DirectorySeparatorChar));
            if (!IsUnder(folder, Path.Combine(root, "docs")) && !IsUnder(folder, Path.Combine(root, ".claude"))) OpenFolder(folder, recursive: true);
        }

        if (GitFolders(root) is { } git)
        {
            // HEAD (a switch), the branches (a commit) and packed-refs (a pack): what a merge or a switch moves.
            OpenFolder(git.Own, recursive: false, hears: name => name == "HEAD");
            OpenFolder(git.Common, recursive: false, hears: name => name is "HEAD" or "packed-refs");
            OpenFolder(Path.Combine(git.Common, "refs", "heads"), recursive: true);
        }
    }

    /// <summary>Opens a watcher, or says nothing when the folder is absent: a folder made later is found at the deadline.</summary>
    private void OpenFolder(string folder, bool recursive, Func<string, bool>? hears = null)
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
                if (hears is null || hears(Path.GetFileName(path ?? string.Empty))) MarkStale();
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
