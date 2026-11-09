namespace Daoris.Driver;

/// <summary>
/// One checkout's branches and origin's copies of them (where each points, what it points at, its upstream), its working trees, and
/// what git answered of commits by their ids (<see cref="Asked"/>), each read with one start and handed to the reads of a judgement
/// that ask (null asking git), so a judgement reads a repository's refs once.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Never stale across a write.</b> It is read at its first question, not when taken; every git this flow starts that may
/// write (<see cref="GitCommands.Writes"/>) drops each snapshot the flow holds, so the next question reads again. A write in a flow
/// beside it drops nothing: the judgement again right before an act, and the delete only at the commit judged, guard against it.</para>
/// <para><b>It answers only what git would</b>: a name outside its two prefixes or only case tells apart, a commit of a ref that is none,
/// an upstream it does not hold and a failed read are asked of git. Take it at the start of an <c>async</c> judgement.</para>
/// </remarks>
internal sealed class RefSnapshot
{
    /// <summary>What <c>for-each-ref</c> prints of each ref, a space apart: no ref name holds a space.</summary>
    internal const string Format = "%(refname) %(objectname) %(objecttype) %(upstream) %(upstream:short) %(tree)";

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

    /// <summary>One ref as read.</summary>
    internal sealed record Entry(string Name, string Object, string Type, string? Upstream, string? UpstreamShort, string? Tree = null);

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

    // What git answered this judgement of commits by their ids, by the question's arguments: dropped with the refs.
    private Dictionary<string, Task<(int Code, string Stdout, string Stderr)>> _facts = new(StringComparer.Ordinal);

    /// <summary>
    /// <paramref name="ask"/>'s answer to a question of commits by their full ids alone, or the one this judgement already holds for
    /// <paramref name="root"/>; null where no judgement of that checkout is held in this flow, or the question is not one that is
    /// kept, and the caller asks git as before. <c>merge-base --is-ancestor</c>, the files two commits differ in
    /// (<c>LandedProofs.NamesAsync</c>) and <c>rev-list --count</c> were asked per pair, the same pair by each branch that left the
    /// line at one commit.
    /// </summary>
    /// <remarks>
    /// <para><b>One judgement's, one checkout's.</b> The newest snapshot of <paramref name="root"/> this flow holds answers: a
    /// judgement again takes its own refs, so it asks git afresh, and another repository, or another folder, is never one.</para>
    /// <para><b>Nothing that moves.</b> The question names no ref, no head and no path (<see cref="GitFacts.ById"/>): what it
    /// asks of two commits git answers the same while the objects stay. Every write this flow starts drops it with the refs all the
    /// same, and only an answer git gave is kept (<see cref="GitFacts.Answered"/>): a missing object or a start that failed is
    /// asked again, never read as "no".</para>
    /// </remarks>
    internal static Task<(int Code, string Stdout, string Stderr)>? Asked(
        string root, IReadOnlyList<string> arguments, Func<CancellationToken, Task<(int Code, string Stdout, string Stderr)>> ask, CancellationToken ct)
    {
        if (Held.Value is not { Length: > 0 } held || !(GitFacts.ById(arguments) || RefsListing(arguments) || Layout(arguments))) return null;
        var folder = GitFacts.Folder(root);
        for (var at = held.Length - 1; at >= 0; at--)
        {
            if (held[at]._root == folder) return GitMemoAsks.Joined(held[at].AskAsync(arguments, GitMemoAsks.Under(ask, CancellationToken.None)), ct);
        }

        return null;
    }

    /// <summary>
    /// A listing of this checkout's refs by name alone (<c>for-each-ref</c> with only a format and an order, of patterns under
    /// <c>refs/</c>): what the snapshot's own refs are, kept as they are, until a write of the flow. The content proof listed
    /// every branch by date once per branch it judged.
    /// </summary>
    private static bool RefsListing(IReadOnlyList<string> arguments) =>
        arguments.Count > 1 && arguments[0] == "for-each-ref"
        && arguments.Skip(1).All(each => each.StartsWith("--format=", StringComparison.Ordinal)
            || each.StartsWith("--sort=", StringComparison.Ordinal) || each.StartsWith("refs/", StringComparison.Ordinal))
        && arguments.Skip(1).Any(each => each.StartsWith("refs/", StringComparison.Ordinal));

    /// <summary>
    /// Where the checkout's working tree is and which trees git lists for it, as the snapshot's own list reads them: the look's tidy
    /// asked each once per stage of a repository's pass. A <c>worktree</c> write drops them with the refs.
    /// </summary>
    private static bool Layout(IReadOnlyList<string> arguments) =>
        arguments is ["rev-parse", "--show-toplevel"] or ["worktree", "list", "--porcelain"];

    /// <summary>
    /// A question's key: its arguments, but the two commits the files of a <c>diff</c> are named between in one order, since the
    /// names two commits differ in are the same either way round (the content proof asked each pair both ways).
    /// </summary>
    private static string KeyOf(IReadOnlyList<string> arguments)
    {
        var words = arguments.Where(each => !each.StartsWith('-')).ToList();
        if (words is not ["diff", _, _]) return string.Join('\0', arguments);
        return string.Join('\0', arguments.Where(each => each.StartsWith('-')).Prepend("diff").Concat(words.Skip(1).Order(StringComparer.Ordinal)));
    }

    private Task<(int Code, string Stdout, string Stderr)> AskAsync(
        IReadOnlyList<string> arguments, Func<Task<(int Code, string Stdout, string Stderr)>> ask)
    {
        var key = KeyOf(arguments);
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

    /// <summary>The one read of every branch and origin copy: the library's where admitted (<see cref="GitLibraryRefs"/>), else one start.</summary>
    internal static async Task<string?> ReadAsync(string root, CancellationToken ct)
    {
        if (GitRuns.Runs.Value is null && GitRuns.Answered.Value is null
            && await GitLibraryRefs.ListingAsync(root, ct).ConfigureAwait(false) is { } listed)
        {
            return listed;
        }

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
            if (fields.Length is not (5 or 6) || fields[0].Length == 0 || fields[1].Length == 0 || fields[2].Length == 0) return null;
            entries.Add(new Entry(fields[0], fields[1], fields[2], Empty(fields[3]), Empty(fields[4]), fields.Length == 6 ? Empty(fields[5]) : null));
        }

        return new Refs(entries);

        static string? Empty(string field) => field.Length == 0 ? null : field;
    }

    /// <summary>Drop what has been read, and what git answered of commits: the next question reads again.</summary>
    internal void Invalidate()
    {
        lock (_gate) (_refs, _trees, _facts, Unheld) = (null, null, new(StringComparer.Ordinal), new UnheldTips());
    }

    /// <summary>Which session branches no branch of the person's holds, read once for the judgement; dropped with the refs.</summary>
    internal UnheldTips Unheld { get; private set; } = new();

    /// <summary>The snapshot's refs as one read, for a fact that is of all of them; null where git could not list them.</summary>
    internal Task<Refs?> ListedAsync(CancellationToken ct) => RefsAsync(ct);

    /// <summary>
    /// Until disposed, this flow holds no snapshot (the act journal's reconciler, run inside a writer's judgement): every question
    /// is git's. The judgement's snapshots are this flow's again after, untouched.
    /// </summary>
    internal static IDisposable Outside()
    {
        var before = Held.Value;
        Held.Value = null;
        return new Back(() => Held.Value = before);
    }

    private sealed class Back(Action back) : IDisposable
    {
        public void Dispose() => back();
    }

    /// <summary>A ref may have moved: every snapshot this flow holds is dropped.</summary>
    internal static void Wrote()
    {
        foreach (var each in Held.Value ?? []) each.Invalidate();
    }

    /// <summary>What <see cref="WorkingTree"/>'s runner calls after each git it ran: a write drops this flow's snapshots.</summary>
    internal static void Ran(IReadOnlyList<string> arguments)
    {
        if (GitCommands.Writes(arguments)) Wrote();
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

    /// <summary>
    /// The tree of the commit <paramref name="commit"/>, where a ref here points at exactly it, as <c>&lt;id&gt;^{tree}</c> answers; null
    /// where none does (a branch moved on, or a commit no ref holds), and the caller asks git.
    /// </summary>
    internal async Task<string?> TreeOfAsync(string commit, CancellationToken ct, string? asker = null)
    {
        if (asker is not null && _root is { } root && !GitStamp.SameContext(asker, root)) return null;
        if (await RefsAsync(ct).ConfigureAwait(false) is not { } refs) return null;
        return TreeIn(refs, commit);
    }

    /// <summary>
    /// The tree of <paramref name="commit"/> from a listing this flow holds of a folder reading <paramref name="folder"/>'s configuration
    /// (<see cref="GitStamp.ContextOf"/>). Nothing is started; null where no such listing has a ref at exactly that commit.
    /// </summary>
    internal static string? TreeHeld(string folder, string commit)
    {
        foreach (var held in Held.Value ?? [])
        {
            if (held._root is not { } root || !GitStamp.SameContext(folder, root)) continue;
            Task<Refs?>? read;
            lock (held._gate) read = held._refs;
            if (read is not { IsCompletedSuccessfully: true, Result: { } refs }) continue;
            if (TreeIn(refs, commit) is { } tree) return tree;
        }

        return GitReadOnce.TreeOf(folder, commit);
    }

    /// <summary>
    /// The listing a snapshot of this flow already read, for the repository <paramref name="folder"/> is a folder of: a tree of it and
    /// its checkout reading one configuration list the same refs, so one start serves both. Nothing is started; null where none is read.
    /// </summary>
    internal static Refs? ListedFor(string folder)
    {
        foreach (var held in Held.Value ?? [])
        {
            if (held._root is not { } root || !GitStamp.SameContext(folder, root)) continue;
            Task<Refs?>? read;
            lock (held._gate) read = held._refs;
            if (read is { IsCompletedSuccessfully: true, Result: { } refs }) return refs;
        }

        return null;
    }

    /// <summary>The tree <paramref name="refs"/> read for the ref at exactly <paramref name="commit"/>; null where none is.</summary>
    internal static string? TreeIn(Refs refs, string commit) =>
        refs.Entries.FirstOrDefault(entry => entry.Type == "commit" && entry.Tree is not null
            && string.Equals(entry.Object, commit, StringComparison.OrdinalIgnoreCase))?.Tree;

    /// <summary>The branches under <c>refs/heads/&lt;prefix&gt;</c> by their names under <c>refs/heads/</c>, in git's order; null where the read failed.</summary>
    internal async Task<IReadOnlyList<string>?> BranchesAsync(string prefix, CancellationToken ct)
    {
        if (await RefsAsync(ct).ConfigureAwait(false) is not { } refs) return null;
        return [.. refs.Entries.Where(entry => entry.Name.StartsWith(Heads + prefix, StringComparison.Ordinal)).Select(entry => entry.Name[Heads.Length..])];
    }

    /// <summary>
    /// The working trees git lists for this checkout, read at the first question and kept with the refs, so a
    /// write in the flow drops both; null where git could not list them, which the next question asks again, as each caller
    /// asked before: a list never read is never an empty one. Null for the task itself where this snapshot has no
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
    internal static bool Covers(string reference)
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
            // A read that threw or got cancelled is read again; one git could not give stays unread for this snapshot.
            if (_refs is { } read && !read.IsFaulted && !read.IsCanceled) return read;
            return _refs = LoadAsync(ct);
        }
    }

    private async Task<Refs?> LoadAsync(CancellationToken ct) =>
        await _read(ct).ConfigureAwait(false) is { } output ? Parse(output) : null;
}
