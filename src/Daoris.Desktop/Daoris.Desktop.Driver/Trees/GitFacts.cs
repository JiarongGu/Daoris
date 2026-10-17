namespace Daoris.Driver;

/// <summary>
/// One landing's planning asks git once per fact (LANDGIT1, after STARTGIT1): within it, a read whose answer an id or the layout
/// decides (a commit's `merge-base` or `log` by full ids, `--show-toplevel`, `--git-common-dir`, origin's HEAD) is asked of git once,
/// and every later asker in the flow is handed the answer.
/// <b>Nothing another process moves is kept</b>: a read naming `HEAD` or a ref is always git's, so a judgement, a replay's source
/// and the tidy's re-reads see what is there. The landing closes the table (<see cref="Close"/>) before the end keeping and the tidy.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Never kept across a write.</b> Every git this flow starts that may move a ref or an upstream
/// (<see cref="RefSnapshot.MayWrite"/>, the same list PROC1d-ii's snapshot is dropped by) drops every fact before it starts and
/// after it ends, through <see cref="WorkingTree"/>'s one runner, so what a merge, a replay, a branch made or a worktree removed
/// changed is asked again. A step that is not git (a plugin pushing) calls <see cref="Dropped"/> itself. A write in a flow beside
/// this one drops nothing of it, as the snapshot's remark says: the judgement again right before an act is what guards it (D88).</para>
/// <para><b>Reads only, and nothing the files decide.</b> `status`, and a `diff` that reads the working tree or the index, are never
/// kept: what the working tree holds is not a ref (a `diff` of two commits by their ids is, PROC1d-iv). A git that did not start
/// (code -1), a read that was cancelled and a question of commits git could not answer (<see cref="Answered"/>) are not kept, so
/// the next asker asks again. A call with its own environment or bound (a fetch) and a test's stand-in answer
/// (<see cref="WorkingTree.Answered"/>) are not touched. Outside a landing, a judgement keeps the same questions of commits for
/// itself (<see cref="RefSnapshot.Asked"/>).</para>
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
        if (Live is not null) return Nothing.Instance;
        var facts = new GitFacts();
        Current.Value = facts;
        return new Ending(facts);
    }

    /// <summary>
    /// End the planning's table now, whoever opened it (a landing the look opened the table for ends it too): what follows asks git
    /// as it stands. The opener's own dispose is then no more than the end of its scope.
    /// </summary>
    internal static void Close()
    {
        if (Live is { } facts) facts._closed = true;
        Current.Value = null;
    }

    // Closed by whoever ends the scope, so a child task that inherited the async local after it began never answers from it: the
    // value is cleared only in the context that disposes, which no child shares.
    private volatile bool _closed;

    private static GitFacts? Live => Current.Value is { _closed: false } facts ? facts : null;

    /// <summary>Drop every fact this flow holds: the next ask is git's. For a write that is not a git (a plugin pushing).</summary>
    internal static void Dropped() => Live?.Drop();

    /// <summary>What <see cref="WorkingTree"/>'s runner calls around each git it runs: a write drops this flow's facts.</summary>
    internal static void Ran(IReadOnlyList<string> arguments)
    {
        if (Live is not { } facts || !RefSnapshot.MayWrite(arguments)) return;
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
        if (Live is not { } facts) return;
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
        if (Live is not { } facts || !Kept(arguments)) return null;
        var folder = Folder(root);
        // A question of refs and commits by full name is the repository's, whichever of its working trees asked it.
        var wide = ById(arguments);
        if (wide)
        {
            lock (facts._gate) folder = facts._pairs.GetValueOrDefault(folder, folder);
        }

        // PROC1d-iv: a question of commits keeps only an answer git gave; a layout read, whatever git said, as before.
        return facts.AskAsync(folder + "\n" + string.Join('\0', arguments), wide, code => wide ? Answered(arguments, code) : code >= 0, ask);
    }

    private void Unpair()
    {
        lock (_gate) _pairs.Clear();
    }

    private Task<(int Code, string Stdout, string Stderr)> AskAsync(
        string key, bool wide, Func<int, bool> keeps, Func<Task<(int Code, string Stdout, string Stderr)>> ask)
    {
        Task<(int Code, string Stdout, string Stderr)> task;
        Dictionary<string, Task<(int Code, string Stdout, string Stderr)>> held;
        lock (_gate)
        {
            held = _asked;
            // An answer that came and is not kept is asked again even before the removal below has run.
            if (held.TryGetValue(key, out var known)
                && (!known.IsCompleted || known.IsCompletedSuccessfully && keeps(known.Result.Code)))
            {
                return known;
            }

            held[key] = task = ask();
            if (wide) _wide.Add(key);
        }

        // Not kept where it did not come: a git that would not start, a read that ended by an exception or a cancel, and a question
        // of commits git could not answer.
        _ = task.ContinueWith(done =>
        {
            if (done.IsCompletedSuccessfully && keeps(done.Result.Code)) return;
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

    /// <summary>A folder as one spelling: Windows compares blind to case, as <c>TreeGit.SamePath</c> does.</summary>
    internal static string Folder(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).Replace('\\', '/');
        return OperatingSystem.IsWindows() ? full.ToLowerInvariant() : full;
    }

    // Folder to the checkout of the repository it is a working tree of (SameRepository).
    private readonly Dictionary<string, string> _pairs = new(StringComparer.Ordinal);

    // A commit or tree by its full id: what it names never changes, and no working tree reads it differently.
    private static readonly System.Text.RegularExpressions.Regex Whole = new(
        @"^([0-9a-f]{40}|[0-9a-f]{64})(\^\{(commit|tree)\})?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// A question of commits by their full ids alone (`rev-parse --verify`, `merge-base`, `log` of them or of a range between them;
    /// PROC1d-iv: `rev-list --count`, and the files two commits differ in as `LandedProofs.NamesAsync` asks): no ref, no HEAD, no
    /// path, and no option that reads the refs (`--branches`, `--remotes`, `--all`, `--tags`, `--glob`). Its answer is the
    /// repository's and does not move while the objects stay, so whichever working tree asked it, and when, it is one fact.
    /// </summary>
    /// <remarks>
    /// The files two commits differ in are kept only in the one form whose answer no configuration changes: names alone, renames
    /// off (`diff.renames` would name a renamed file once) and every submodule seen (`diff.ignoreSubmodules` would hide one).
    /// </remarks>
    internal static bool ById(IReadOnlyList<string> arguments)
    {
        var at = CommandAt(arguments);
        if (at >= arguments.Count) return false;
        var rest = arguments.Skip(at + 1).ToList();
        var positional = rest.Where(each => !each.StartsWith('-')).ToList();
        if (positional.Count == 0 || positional.Any(each => !Names(each))) return false;
        var options = rest.Where(each => each.StartsWith('-')).ToList();
        return arguments[at] switch
        {
            "rev-parse" => options.All(each => each is "--verify" or "--quiet" or "-q"),
            "merge-base" => options.All(each => each is "--is-ancestor" or "--all"),
            "log" => options.All(each => each is "--reverse" or "--no-merges" or "--oneline" or "--not"
                || each.StartsWith("--format=", StringComparison.Ordinal)),
            "rev-list" => options.Contains("--count") && options.All(each => each is "--count" or "--not"),
            "diff" => positional.Count == 2 && positional.All(Whole.IsMatch)
                && options.Contains("--name-only") && options.Contains("--no-renames") && options.Contains("--ignore-submodules=none")
                && options.All(each => each is "--name-only" or "--no-renames" or "--ignore-submodules=none" or "--no-ext-diff" or "-z"),
            _ => false,
        };

        // A range names its two ends.
        static bool Names(string each) => each.Split(["...", ".."], StringSplitOptions.None).All(end => Whole.IsMatch(end));
    }

    /// <summary>
    /// Whether git gave an answer to a question of commits by their ids (<see cref="ById"/>), so it may be kept: 0, or the "no" (1) of
    /// `merge-base` (the one is not in the other; they share none) and of `rev-parse --verify --quiet`. Anything else (a missing
    /// object's 128, a git that did not start) is a read not made, asked again and never read as "no" (RECORDUNREAD1, PROC1d-iv).
    /// </summary>
    internal static bool Answered(IReadOnlyList<string> arguments, int code) =>
        code == 0 || (code == 1 && Command(arguments) is "merge-base" or "rev-parse");

    // What no write of ours changes and no other process moves: where a folder's working tree and repository are.
    private static bool Layout(IReadOnlyList<string> arguments) =>
        arguments is ["rev-parse", "--show-toplevel"] or ["rev-parse", "--path-format=absolute", "--git-common-dir"]
        or ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"];

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
    internal static bool Kept(IReadOnlyList<string> arguments) => ById(arguments) || Layout(arguments);

    private sealed class Ending(GitFacts facts) : IDisposable
    {
        public void Dispose()
        {
            facts._closed = true;
            Current.Value = null;
        }
    }

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing Instance = new();

        public void Dispose()
        {
        }
    }
}
