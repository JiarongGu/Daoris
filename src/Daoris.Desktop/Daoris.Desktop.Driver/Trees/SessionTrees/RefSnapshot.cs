namespace Daoris.Driver;

/// <summary>
/// One checkout's branches and origin's copies of them, read with one git start (PROC1d-ii): where each points, what it points
/// at, and its upstream. <c>rev-parse --verify</c> was 389 of PROC1a's 1278 product starts, each a branch's tip, a form of the
/// line or an upstream asked one at a time; a judgement takes one of these and hands it to the reads that ask (an optional
/// parameter, null asking git as before), so a judgement reads a repository's refs once. Its working trees, which branch each
/// holds, are one more start kept the same way (PROC1d-iii): <c>worktree list</c> was 47 of those starts, asked by each part
/// of a judgement in turn.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Never stale across a write.</b> It is read at its first question, not when taken, so one taken before a fetch is
/// read after it; and every git this flow starts that may move a ref or an upstream (<see cref="MayWrite"/>) drops each snapshot
/// the flow holds, through <see cref="WorkingTree"/>'s one runner, so the next question reads again. A write in a flow beside
/// it drops nothing of it: that is news from another moment, as the person's own git is, and what guards against it is what
/// always did, the judgement again right before an act and the delete only at the commit judged (D88).</para>
/// <para><b>It answers only what git would.</b> A revision that is not a full name under <c>refs/heads/</c> or
/// <c>refs/remotes/origin/</c>, a name only case tells from one it holds (git finds a loose ref so named on a disk blind to
/// case), a commit asked of a ref that is no commit, an upstream asked of a branch it does not hold (its configuration may
/// outlive it), and everything where its own read failed, it does not answer: git is asked, as before.</para>
/// <para>Take it in an <c>async</c> method, at the start of a judgement: the flow holds it until that method returns.</para>
/// </remarks>
internal sealed class RefSnapshot
{
    /// <summary>What <c>for-each-ref</c> prints of each ref, a space apart: no ref name holds a space.</summary>
    internal const string Format = "%(refname) %(objectname) %(objecttype) %(upstream) %(upstream:short)";

    private const string Heads = "refs/heads/";
    private const string Origin = "refs/remotes/origin/";

    // The snapshots this flow holds: an async local, so a write drops this flow's and never a neighbour's.
    private static readonly AsyncLocal<RefSnapshot[]?> Held = new();

    private readonly Func<CancellationToken, Task<string?>> _read;
    private readonly Func<CancellationToken, Task<string?>>? _list;
    private readonly object _gate = new();
    private Task<Refs?>? _refs;
    private Task<IReadOnlyList<WorktreeEntry>?>? _trees;

    private RefSnapshot(Func<CancellationToken, Task<string?>> read, Func<CancellationToken, Task<string?>>? list) =>
        (_read, _list) = (read, list);

    /// <summary>One ref as it was read.</summary>
    internal sealed record Entry(string Name, string Object, string Type, string? Upstream, string? UpstreamShort);

    /// <summary>A read's refs by their full names, in git's order, and the same names blind to case.</summary>
    internal sealed class Refs(List<Entry> entries)
    {
        internal IReadOnlyList<Entry> Entries { get; } = entries;

        internal Dictionary<string, Entry> ByName { get; } = entries.ToDictionary(entry => entry.Name, StringComparer.Ordinal);

        internal HashSet<string> Blind { get; } = entries.Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A snapshot of <paramref name="root"/>'s refs and working trees for this flow. Nothing is started until it is first asked.</summary>
    internal static RefSnapshot Take(string root) => Take(ct => ReadAsync(root, ct), ct => TreeWorktrees.PorcelainAsync(root, ct), root);

    /// <summary>
    /// The test seam: a snapshot whose read is <paramref name="read"/> and whose list of the working trees is <paramref name="list"/>
    /// (each one's output, or null where git could not give it). Without a list, the trees are asked of git as before. Without a
    /// <paramref name="root"/>, it answers no question of commits (<see cref="Asked"/>): it is no checkout's.
    /// </summary>
    internal static RefSnapshot Take(
        Func<CancellationToken, Task<string?>> read, Func<CancellationToken, Task<string?>>? list = null, string? root = null)
    {
        var taken = new RefSnapshot(read, list) { _root = root is null ? null : GitFacts.Folder(root) };
        Held.Value = Held.Value is { } held ? [.. held, taken] : [taken];
        return taken;
    }

    // The checkout this judgement is of, as one spelling (GitFacts.Folder); null for a seam's snapshot.
    private string? _root;

    // What git answered this judgement of commits by their ids (PROC1d-iv), by the question's arguments: dropped with the refs.
    private Dictionary<string, Task<(int Code, string Stdout, string Stderr)>> _facts = new(StringComparer.Ordinal);

    /// <summary>
    /// <paramref name="ask"/>'s answer to a question of commits by their full ids alone, or the one this judgement already holds for
    /// <paramref name="root"/>; null where no judgement of that checkout is held in this flow, or the question is not one that is
    /// kept, and the caller asks git as before (PROC1d-iv). <c>merge-base --is-ancestor</c>, the files two commits differ in
    /// (<c>LandedProofs.NamesAsync</c>) and <c>rev-list --count</c> were asked per pair, the same pair by each branch that left the
    /// line at one commit.
    /// </summary>
    /// <remarks>
    /// <para><b>One judgement's, one checkout's.</b> The newest snapshot of <paramref name="root"/> this flow holds answers: a
    /// judgement again takes its own refs, so it asks git afresh (D88), and another repository, or another folder, is never one.</para>
    /// <para><b>Nothing that moves.</b> The question names no ref, no head and no path (<see cref="GitFacts.ById"/>): what it
    /// asks of two commits git answers the same while the objects stay. Every write this flow starts drops it with the refs all the
    /// same, and only an answer git gave is kept (<see cref="GitFacts.Answered"/>): a missing object or a start that failed is
    /// asked again, never read as "no" (RECORDUNREAD1).</para>
    /// </remarks>
    internal static Task<(int Code, string Stdout, string Stderr)>? Asked(
        string root, IReadOnlyList<string> arguments, Func<Task<(int Code, string Stdout, string Stderr)>> ask)
    {
        if (Held.Value is not { Length: > 0 } held || !GitFacts.ById(arguments)) return null;
        var folder = GitFacts.Folder(root);
        for (var at = held.Length - 1; at >= 0; at--)
        {
            if (held[at]._root == folder) return held[at].AskAsync(arguments, ask);
        }

        return null;
    }

    private Task<(int Code, string Stdout, string Stderr)> AskAsync(
        IReadOnlyList<string> arguments, Func<Task<(int Code, string Stdout, string Stderr)>> ask)
    {
        var key = string.Join('\0', arguments);
        lock (_gate)
        {
            // An answer in flight is shared; one that came is handed on only where git gave it.
            if (_facts.TryGetValue(key, out var known)
                && (!known.IsCompleted || known.IsCompletedSuccessfully && GitFacts.Answered(arguments, known.Result.Code)))
            {
                return known;
            }

            return _facts[key] = ask();
        }
    }

    /// <summary>The one start: every branch here and every copy of origin's, with what each points at and its upstream.</summary>
    internal static async Task<string?> ReadAsync(string root, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(
            root, ["for-each-ref", $"--format={Format}", "refs/heads", "refs/remotes/origin"], ct).ConfigureAwait(false);
        return code == 0 ? output : null;
    }

    /// <summary>The read's refs; null where a line is not the five fields asked for, so nothing is answered from a read half understood.</summary>
    internal static Refs? Parse(string output)
    {
        var entries = new List<Entry>();
        foreach (var line in output.Split('\n'))
        {
            var row = line.TrimEnd('\r');
            if (row.Trim().Length == 0) continue;
            var fields = row.Split(' ');
            if (fields.Length != 5 || fields[0].Length == 0 || fields[1].Length == 0 || fields[2].Length == 0) return null;
            entries.Add(new Entry(fields[0], fields[1], fields[2], Empty(fields[3]), Empty(fields[4])));
        }

        return new Refs(entries);

        static string? Empty(string field) => field.Length == 0 ? null : field;
    }

    /// <summary>Drop what was read, and what git answered of commits (PROC1d-iv): the next question reads again.</summary>
    internal void Invalidate()
    {
        lock (_gate) (_refs, _trees, _facts) = (null, null, new(StringComparer.Ordinal));
    }

    /// <summary>A ref may have moved: every snapshot this flow holds is dropped.</summary>
    internal static void Wrote()
    {
        foreach (var each in Held.Value ?? []) each.Invalidate();
    }

    /// <summary>What <see cref="WorkingTree"/>'s runner calls after each git it ran: a write drops this flow's snapshots.</summary>
    internal static void Ran(IReadOnlyList<string> arguments)
    {
        if (MayWrite(arguments)) Wrote();
    }

    /// <summary>
    /// Whether a git may move a ref or an upstream: every command but the reads named here, so a command this does not know
    /// costs a read again and never a stale answer.
    /// </summary>
    internal static bool MayWrite(IReadOnlyList<string> arguments)
    {
        var at = 0;
        // git's own options before the command: `-c name=value`, `-C path`, `--no-pager`.
        while (at < arguments.Count && arguments[at].StartsWith('-'))
        {
            at += arguments[at] is "-c" or "-C" ? 2 : 1;
        }

        if (at >= arguments.Count) return true;
        var rest = arguments.Skip(at + 1).ToList();
        return arguments[at] switch
        {
            "rev-parse" or "for-each-ref" or "merge-base" or "rev-list" or "diff" or "log" or "show" or "status" or "cat-file"
                or "ls-files" or "ls-tree" or "show-ref" or "name-rev" or "describe" or "blame" or "check-ignore" or "check-attr"
                or "cherry" or "ls-remote" or "version" => false,
            "worktree" => rest.FirstOrDefault() != "list",
            "branch" => !rest.Any(ListsBranches) || rest.Any(MovesBranches),
            "symbolic-ref" => rest.Any(each => each is "-d" or "--delete" or "-m") || rest.Count(each => !each.StartsWith('-')) != 1,
            "config" => !rest.Any(each => each is "--get" or "--get-all" or "--get-regexp" or "--get-urlmatch" or "--list" or "-l"),
            "remote" => rest.FirstOrDefault() is not ("get-url" or "-v"),
            _ => true,
        };

        static bool ListsBranches(string each) =>
            each is "--list" or "-l" or "--contains" or "--no-contains" or "--merged" or "--no-merged" or "--points-at" or "--show-current";

        static bool MovesBranches(string each) =>
            each is "-d" or "-D" or "--delete" or "-m" or "-M" or "--move" or "-c" or "-C" or "--copy" or "-f" or "--force"
                or "-u" or "--unset-upstream" or "-t" or "--track" or "--no-track" or "--edit-description" or "--create-reflog"
            || each.StartsWith("--set-upstream-to", StringComparison.Ordinal) || each.StartsWith("--track=", StringComparison.Ordinal);
    }

    /// <summary>
    /// The commit <paramref name="reference"/> points at, as <c>rev-parse --verify --quiet &lt;ref&gt;^{commit}</c> answers:
    /// null where there is no such ref. Not <c>Known</c> where git must be asked.
    /// </summary>
    internal async Task<(bool Known, string? Commit)> CommitAsync(string reference, CancellationToken ct)
    {
        var (known, entry) = await LookAsync(reference, ct).ConfigureAwait(false);
        if (!known) return (false, null);
        if (entry is null) return (true, null);
        return entry.Type == "commit" ? (true, entry.Object) : (false, null);
    }

    /// <summary>Whether <paramref name="reference"/> is here, as <c>rev-parse --verify --quiet &lt;ref&gt;</c> answers; null where git must be asked.</summary>
    internal async Task<bool?> HasAsync(string reference, CancellationToken ct)
    {
        var (known, entry) = await LookAsync(reference, ct).ConfigureAwait(false);
        return known ? entry is not null : null;
    }

    /// <summary>
    /// <paramref name="branch"/>'s upstream as <c>&lt;branch&gt;@{upstream}</c> resolves it, in full and as
    /// <c>--abbrev-ref</c> shortens it: both null where it has none git can follow. Not <c>Known</c> where the branch is not
    /// here or the read failed, since its configuration may still name one.
    /// </summary>
    internal async Task<(bool Known, string? Full, string? Short)> UpstreamAsync(string branch, CancellationToken ct)
    {
        var (known, entry) = await LookAsync(Heads + branch, ct).ConfigureAwait(false);
        return known && entry is not null ? (true, entry.Upstream, entry.UpstreamShort) : (false, null, null);
    }

    /// <summary>The branches under <c>refs/heads/&lt;prefix&gt;</c> by their names under <c>refs/heads/</c>, in git's order; null where the read failed.</summary>
    internal async Task<IReadOnlyList<string>?> BranchesAsync(string prefix, CancellationToken ct)
    {
        if (await RefsAsync(ct).ConfigureAwait(false) is not { } refs) return null;
        return [.. refs.Entries.Where(entry => entry.Name.StartsWith(Heads + prefix, StringComparison.Ordinal)).Select(entry => entry.Name[Heads.Length..])];
    }

    /// <summary>
    /// The working trees git lists for this checkout (PROC1d-iii), read at the first question and kept with the refs, so a
    /// write in the flow drops both; null where git could not list them, which the next question asks again, as each caller
    /// asked before: a list never read is never an empty one (AUTOTIDY1). Null for the task itself where this snapshot has no
    /// list to read, and the caller asks git.
    /// </summary>
    internal Task<IReadOnlyList<WorktreeEntry>?>? WorktreesAsync(CancellationToken ct)
    {
        if (_list is not { } list) return null;
        lock (_gate)
        {
            if (_trees is { } read && !read.IsFaulted && !read.IsCanceled && !(read.IsCompletedSuccessfully && read.Result is null)) return read;
            return _trees = ListAsync(list, ct);
        }

        static async Task<IReadOnlyList<WorktreeEntry>?> ListAsync(Func<CancellationToken, Task<string?>> list, CancellationToken ct) =>
            await list(ct).ConfigureAwait(false) is { } porcelain ? TreeWorktrees.Parse(porcelain) : null;
    }

    private async Task<(bool Known, Entry? Entry)> LookAsync(string reference, CancellationToken ct)
    {
        if (!Covers(reference) || await RefsAsync(ct).ConfigureAwait(false) is not { } refs) return (false, null);
        if (refs.ByName.TryGetValue(reference, out var entry)) return (true, entry);
        return refs.Blind.Contains(reference) ? (false, null) : (true, null);
    }

    /// <summary>A full ref name it read: under <c>refs/heads/</c> or <c>refs/remotes/origin/</c>, and nothing git would read as more than a name.</summary>
    private static bool Covers(string reference)
    {
        var under = reference.StartsWith(Heads, StringComparison.Ordinal) ? Heads
            : reference.StartsWith(Origin, StringComparison.Ordinal) ? Origin
            : null;
        if (under is null || reference.Length == under.Length) return false;
        return reference.IndexOfAny([' ', '~', '^', ':', '?', '*', '[', '\\', '\t', '\n']) < 0
               && !reference.Contains("..", StringComparison.Ordinal)
               && !reference.Contains("@{", StringComparison.Ordinal)
               && !reference.Contains("//", StringComparison.Ordinal)
               && !reference.EndsWith('/') && !reference.EndsWith('.')
               && !reference.EndsWith(".lock", StringComparison.Ordinal);
    }

    private Task<Refs?> RefsAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            // A read that threw or was cancelled is read again; one git could not give stays unread for this snapshot.
            if (_refs is { } read && !read.IsFaulted && !read.IsCanceled) return read;
            return _refs = LoadAsync(ct);
        }
    }

    private async Task<Refs?> LoadAsync(CancellationToken ct) =>
        await _read(ct).ConfigureAwait(false) is { } output ? Parse(output) : null;
}
