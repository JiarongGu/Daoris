namespace Daoris.Driver;

/// <summary>
/// One landing asks git once per fact (LANDGIT1, after STARTGIT1): within it, a read of git's answer that names a ref or a
/// repository (`rev-parse`, `symbolic-ref`, `merge-base`, `log`, `for-each-ref`) is asked of git once for the same folder and the
/// same arguments, and every later asker in the flow is handed the answer. A landing was about 50 git starts, some facts asked
/// five times by the steps that each read what they needed (`rev-parse --show-toplevel`, `symbolic-ref --short -q HEAD`).
/// </summary>
/// <remarks>
/// <para>🔴 <b>Never kept across a write.</b> Every git this flow starts that may move a ref or an upstream
/// (<see cref="RefSnapshot.MayWrite"/>, the same list PROC1d-ii's snapshot is dropped by) drops every fact before it starts and
/// after it ends, through <see cref="WorkingTree"/>'s one runner, so what a merge, a replay, a branch made or a worktree removed
/// changed is asked again. A step that is not git (a plugin pushing) calls <see cref="Dropped"/> itself. A write in a flow beside
/// this one drops nothing of it, as the snapshot's remark says: the judgement again right before an act is what guards it (D88).</para>
/// <para><b>Reads only, and nothing the files decide.</b> `status` and `diff` are never kept: what the working tree holds is not
/// a ref. A git that did not start (code -1) and a read that was cancelled are not kept, so the next asker asks again. A call with
/// its own environment or bound (a fetch) and a test's stand-in answer (<see cref="WorkingTree.Answered"/>) are not touched.</para>
/// </remarks>
internal sealed class GitFacts
{
    // The facts this flow holds, or null: an async local, so a flow beside it shares nothing and a write there drops nothing here.
    private static readonly AsyncLocal<GitFacts?> Current = new();

    private readonly object _gate = new();
    private Dictionary<string, Task<(int Code, string Stdout, string Stderr)>> _asked = new(StringComparer.Ordinal);

    /// <summary>
    /// Until disposed, this flow asks git each fact once. Within a flow that holds them, this is that one's: a landing inside a
    /// landing shares its facts, as <c>TreeContext.OneLook</c> does.
    /// </summary>
    internal static IDisposable Within()
    {
        if (Current.Value is not null) return Nothing.Instance;
        Current.Value = new GitFacts();
        return new Ending();
    }

    /// <summary>Drop every fact this flow holds: the next ask is git's. For a write that is not a git (a plugin pushing).</summary>
    internal static void Dropped() => Current.Value?.Drop();

    /// <summary>What <see cref="WorkingTree"/>'s runner calls around each git it runs: a write drops this flow's facts.</summary>
    internal static void Ran(IReadOnlyList<string> arguments)
    {
        if (Current.Value is not { } facts || !RefSnapshot.MayWrite(arguments)) return;
        facts.Drop();
        // A working tree added, moved or removed changes which folders belong to a repository: the pairs are read again.
        if (Command(arguments) == "worktree") facts.Unpair();
    }

    /// <summary>
    /// <paramref name="folder"/> is a working tree of the repository whose checkout is <paramref name="commonDir"/>'s parent (read
    /// once, as git's `--git-common-dir`): a question about refs and commits asked in either is one question. A common dir that is
    /// not a checkout's `.git` pairs nothing.
    /// </summary>
    internal static void SameRepository(string folder, string commonDir)
    {
        if (Current.Value is not { } facts) return;
        var dir = Path.TrimEndingDirectorySeparator(commonDir.Trim());
        if (!string.Equals(Path.GetFileName(dir), ".git", StringComparison.Ordinal) || Path.GetDirectoryName(dir) is not { } checkout) return;
        var (tree, root) = (Folder(folder), Folder(checkout));
        lock (facts._gate)
        {
            facts._pairs[tree] = root;
            // What the tree was asked before it was known to be the checkout's is the checkout's answer from now on.
            foreach (var key in facts._wide.Where(each => each.StartsWith(tree + "\n", StringComparison.Ordinal)).ToList())
            {
                facts._wide.Remove(key);
                var moved = root + key[tree.Length..];
                if (facts._asked.Remove(key, out var held) && facts._wide.Add(moved)) facts._asked.TryAdd(moved, held);
            }
        }
    }

    /// <summary>
    /// <paramref name="ask"/>'s answer, or the one this flow already holds for the same folder and arguments; null where this flow
    /// keeps no facts or the command is not one that is kept, and the caller asks git as before.
    /// </summary>
    internal static Task<(int Code, string Stdout, string Stderr)>? Asked(
        string root, IReadOnlyList<string> arguments, Func<Task<(int Code, string Stdout, string Stderr)>> ask)
    {
        if (Current.Value is not { } facts || !Kept(arguments)) return null;
        var folder = Folder(root);
        // A question of refs and commits by full name is the repository's, whichever of its working trees asked it.
        var wide = RepositoryWide(arguments);
        if (wide)
        {
            lock (facts._gate) folder = facts._pairs.GetValueOrDefault(folder, folder);
        }

        return facts.AskAsync(folder + "\n" + string.Join('\0', arguments), wide, ask);
    }

    private void Unpair()
    {
        lock (_gate) _pairs.Clear();
    }

    private Task<(int Code, string Stdout, string Stderr)> AskAsync(
        string key, bool wide, Func<Task<(int Code, string Stdout, string Stderr)>> ask)
    {
        Task<(int Code, string Stdout, string Stderr)> task;
        Dictionary<string, Task<(int Code, string Stdout, string Stderr)>> held;
        lock (_gate)
        {
            held = _asked;
            if (held.TryGetValue(key, out var known) && !known.IsFaulted && !known.IsCanceled) return known;
            held[key] = task = ask();
            if (wide) _wide.Add(key);
        }

        // Not kept where it did not come: a git that would not start, or a read that ended by an exception or a cancel.
        _ = task.ContinueWith(done =>
        {
            if (done.IsCompletedSuccessfully && done.Result.Code >= 0) return;
            lock (_gate)
            {
                if (held.TryGetValue(key, out var now) && ReferenceEquals(now, task)) held.Remove(key);
            }
        }, TaskScheduler.Default);
        return task;
    }

    private void Drop()
    {
        // A new table rather than a cleared one: a read in flight keeps writing to the table it began in, which no one asks.
        lock (_gate)
        {
            _asked = new Dictionary<string, Task<(int Code, string Stdout, string Stderr)>>(StringComparer.Ordinal);
            _wide.Clear();
        }
    }

    // The keys of what was asked of a repository by full names, kept to move to the checkout once its folder is known (SameRepository).
    private readonly HashSet<string> _wide = new(StringComparer.Ordinal);

    // A folder as one spelling: Windows compares blind to case, as TreeGit.SamePath does.
    private static string Folder(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).Replace('\\', '/');
        return OperatingSystem.IsWindows() ? full.ToLowerInvariant() : full;
    }

    // Folder to the checkout of the repository it is a working tree of (SameRepository).
    private readonly Dictionary<string, string> _pairs = new(StringComparer.Ordinal);

    // `rev-parse --verify --quiet <ref or commit>`, `merge-base`, `log` and `symbolic-ref` of full names: no HEAD, no path, no
    // layout option (`--show-toplevel`, `--git-dir`, `--abbrev-ref`), so no answer that depends on which working tree asked.
    private static readonly System.Text.RegularExpressions.Regex Whole = new(
        @"^(refs/(heads|remotes|tags)/\S+|[0-9a-f]{40}|[0-9a-f]{64})(\^\{(commit|tree)\})?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool RepositoryWide(IReadOnlyList<string> arguments)
    {
        var at = CommandAt(arguments);
        var rest = arguments.Skip(at + 1).ToList();
        if (rest.Any(each => each == "--")) return false;
        var positional = rest.Where(each => !each.StartsWith('-')).ToList();
        if (positional.Count == 0) return false;
        return arguments[at] switch
        {
            "rev-parse" => rest.Where(each => each.StartsWith('-')).All(each => each is "--verify" or "--quiet" or "-q") && positional.All(Names),
            "merge-base" or "log" or "symbolic-ref" => positional.All(Names),
            "for-each-ref" => positional.All(each => each.StartsWith("refs/", StringComparison.Ordinal)),
            _ => false,
        };

        // A range names its two ends.
        static bool Names(string each) => each.Split(["...", ".."], StringSplitOptions.None).All(end => Whole.IsMatch(end));
    }

    private static int CommandAt(IReadOnlyList<string> arguments)
    {
        var at = 0;
        while (at < arguments.Count && arguments[at].StartsWith('-'))
        {
            at += arguments[at] is "-c" or "-C" ? 2 : 1;
        }

        return at;
    }

    private static string? Command(IReadOnlyList<string> arguments) => CommandAt(arguments) is var at && at < arguments.Count ? arguments[at] : null;

    /// <summary>The reads whose answer is a ref's or a repository's and never the files': git's own options before the command are skipped.</summary>
    internal static bool Kept(IReadOnlyList<string> arguments)
    {
        var at = CommandAt(arguments);
        if (at >= arguments.Count) return false;
        // A command that reads its revisions from the input is not a question of its arguments alone.
        if (arguments.Skip(at + 1).Any(each => each is "--stdin" or "--batch" or "--batch-check")) return false;
        return arguments[at] switch
        {
            "rev-parse" or "merge-base" or "log" or "for-each-ref" => true,
            "symbolic-ref" => !RefSnapshot.MayWrite(arguments),
            _ => false,
        };
    }

    private sealed class Ending : IDisposable
    {
        public void Dispose() => Current.Value = null;
    }

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing Instance = new();

        public void Dispose()
        {
        }
    }
}
