namespace Daoris.Driver;

/// <summary>
/// A room read's working files: a recursive watch tells edits, including ignored output, without walking that output on every
/// look. The index is also stamped directly, since staging can finish before its watch's callback runs. A loss or a replaced
/// folder discards the generation; a tree that cannot be watched cannot keep its status.
/// </summary>
internal sealed class TreeReadChanges(IChangeSource source) : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Watch> _trees = new(StringComparer.OrdinalIgnoreCase);

    internal sealed record Version(object Watch, long Revision, string? Index);

    internal Version? Read(string tree, string git)
    {
        lock (_gate)
        {
            if (_trees.TryGetValue(tree, out var previous) && (previous.Git != git || !previous.Same))
            {
                previous.Dispose();
                _trees.Remove(tree);
            }

            if (!_trees.TryGetValue(tree, out var watch))
            {
                watch = new Watch(git);
                watch.Files = source.Open(tree, true, watch);
                watch.Index = source.Open(git, false, new GitSink(watch));
                if (!watch.Same)
                {
                    watch.Dispose();
                    return null;
                }

                _trees[tree] = watch;
            }

            var revision = watch.Revision;
            var index = FileStamp.Of(Path.Combine(git, "index"));
            return !index.Unread && watch.Same && revision == watch.Revision ? new Version(watch, revision, index.Content) : null;
        }
    }

    internal void Prune()
    {
        lock (_gate)
        {
            foreach (var tree in _trees.Keys.ToArray())
            {
                if (Directory.Exists(tree)) continue;
                _trees[tree].Dispose();
                _trees.Remove(tree);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var watch in _trees.Values) watch.Dispose();
            _trees.Clear();
        }
    }

    private sealed class Watch(string git) : IChangeSink, IDisposable
    {
        private long _revision;
        private int _lost;
        internal string Git { get; } = git;
        internal IChangeHandle? Files { get; set; }
        internal IChangeHandle? Index { get; set; }
        internal long Revision => Interlocked.Read(ref _revision);
        internal bool Same => Volatile.Read(ref _lost) == 0 && Files is { Same: true } && Index is { Same: true };
        public void Moved(string name, ChangeKind kind) => Interlocked.Increment(ref _revision);
        public void Lost(string why) => Interlocked.Exchange(ref _lost, 1);
        public void Dispose()
        {
            Interlocked.Exchange(ref _lost, 1);
            Files?.Dispose();
            Index?.Dispose();
        }
    }

    private sealed class GitSink(Watch watch) : IChangeSink
    {
        public void Moved(string name, ChangeKind kind)
        {
            // Status writes no index, but other readers' lock files and reflogs need not cost another read.
            if (name is "index" or "HEAD" or "config.worktree" || name.StartsWith("sharedindex.", StringComparison.Ordinal)) watch.Moved(name, kind);
        }

        public void Lost(string why) => watch.Lost(why);
    }
}
