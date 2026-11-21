using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A repository's local branches newest first, as the content proof's last comparison walks them: one listing for as long as the refs the
/// proof counts (<see cref="CountedRefs"/>) stand as they were. The listing is the repository's whichever tree asks, and a session branch,
/// which a live session moves at every commit, is never a holder, so a tree that moved alone asks git nothing for it.
/// </summary>
/// <remarks>
/// The look's stages walk a repository's trees at once, so one listing in flight is shared by every walk asking for the same refs: the
/// fan-out starts it once, not once per tree. Each asker says the listing's wait for a place as its own stage's queue; one leaving ends
/// only its own wait, and the last leaving ends git's read, as the scheduler ends a read nobody waits on.
/// </remarks>
internal static class BranchesByDate
{
    private static readonly ConcurrentDictionary<string, Listing> Kept = new(StringComparer.OrdinalIgnoreCase);

    internal static string[] Ask => ["for-each-ref", "--sort=-committerdate", "--format=%(objectname) %(refname)", "refs/heads/"];

    internal static void Forget() => Kept.Clear();

    /// <summary>
    /// The listing from <paramref name="cwd"/>'s repository: the one kept or in flight where its counted refs are the same, else git's,
    /// kept where it answered and the refs stood while git listed.
    /// </summary>
    internal static async Task<(int Code, string Stdout, string Stderr)> ReadAsync(string cwd, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var common = RefFiles.CommonDirOf(cwd);
        var version = common is null ? null : CountedRefs.Read(common)?.Version;
        if (common is null || version is null) return await WorkingTree.GitAsync(cwd, Ask, ct).ConfigureAwait(false);
        while (true)
        {
            if (Kept.TryGetValue(common, out var kept) && kept.Version == version)
            {
                if (kept.Join()) return await kept.AwaitAsync(ct).ConfigureAwait(false);
                Kept.TryRemove(new KeyValuePair<string, Listing>(common, kept));
                continue;
            }

            var made = new Listing(version);
            if (kept is null ? !Kept.TryAdd(common, made) : !Kept.TryUpdate(common, made, kept)) continue;
            made.Join();
            _ = made.RunAsync(cwd, common, () => Kept.TryRemove(new KeyValuePair<string, Listing>(common, made)));
            return await made.AwaitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>One listing at one version of the refs, with the askers waiting on it and whether git waits for its place now.</summary>
    private sealed class Listing(string version)
    {
        private readonly object _gate = new();
        private readonly CancellationTokenSource _ending = new();
        private readonly TaskCompletionSource<(int Code, string Stdout, string Stderr)> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource _moved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _askers;
        private int _waits;
        private bool _left;

        internal string Version => version;

        /// <summary>Joins it as one more asker; false where its last asker left it, so it is ended and another is made.</summary>
        internal bool Join()
        {
            lock (_gate)
            {
                if (_left) return false;
                _askers++;
                return true;
            }
        }

        /// <summary>
        /// Its answer, said as this asker's queue while git waits for a place; cancelled, this asker leaves, and the last to leave ends
        /// git's read and waits it out.
        /// </summary>
        internal async Task<(int Code, string Stdout, string Stderr)> AwaitAsync(CancellationToken ct)
        {
            try
            {
                while (!_answer.Task.IsCompleted)
                {
                    bool queued;
                    Task moved;
                    lock (_gate) (queued, moved) = (_waits > 0, _moved.Task);
                    using (queued ? ProcessStarts.Queued() : null) await Task.WhenAny(moved, _answer.Task).WaitAsync(ct).ConfigureAwait(false);
                }

                return await _answer.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                if (Leave())
                {
                    _ending.Cancel();
                    await _answer.Task.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
                }

                throw;
            }
        }

        private bool Leave()
        {
            lock (_gate)
            {
                if (--_askers > 0 || _answer.Task.IsCompleted) return false;
                return _left = true;
            }
        }

        /// <summary>Git's one listing, let go where git fails or is ended, or a ref moves during the listing.</summary>
        internal async Task RunAsync(string cwd, string common, Action forget)
        {
            try
            {
                var listed = await ProcessStarts.QueuedToAsync(Waiting, () => WorkingTree.GitAsync(cwd, Ask, _ending.Token)).ConfigureAwait(false);
                if (listed.Code != 0 || CountedRefs.Read(common)?.Version != version) forget();
                _answer.TrySetResult(listed);
            }
            catch (Exception error)
            {
                forget();
                if (error is OperationCanceledException) _answer.TrySetCanceled();
                else _answer.TrySetException(error);
            }
        }

        private IDisposable Waiting()
        {
            Wait(+1);
            return new Waited(this);
        }

        private void Wait(int by)
        {
            TaskCompletionSource moved;
            lock (_gate)
            {
                _waits += by;
                if (_waits != (by > 0 ? 1 : 0)) return;
                (moved, _moved) = (_moved, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            }

            moved.TrySetResult();
        }

        private sealed class Waited(Listing listing) : IDisposable
        {
            private int _ended;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _ended, 1) == 0) listing.Wait(-1);
            }
        }
    }
}
