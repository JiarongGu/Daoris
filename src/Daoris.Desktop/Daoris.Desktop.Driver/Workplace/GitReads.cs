namespace Daoris.Driver;

/// <summary>Why a read behind a page was not answered (GIT1b), or <see cref="None"/> where it was.</summary>
public enum GitReadRefusal
{
    /// <summary>Answered: the answer carries its value.</summary>
    None,

    /// <summary>The root is not the top of a repository of its own: git walks up, so it is not asked.</summary>
    NotTop,

    /// <summary>A commit was named by anything but its whole id (a ref moves, and a read is asked of commits), or a name to resolve could be read as an option.</summary>
    NotACommit,

    /// <summary>The path is empty, rooted, or climbs out of the repository.</summary>
    NotAPath,

    /// <summary>A page before the first or past <see cref="GitReads.LastPage"/>.</summary>
    NotAPage,

    /// <summary>More tips than one call carries on its command line (<see cref="GitReads.GraphTips"/>).</summary>
    TooManyTips,

    /// <summary>git did not answer: a commit this repository does not hold, a path the commit lacks, or git not starting.</summary>
    NotAnswered,
}

/// <summary>Which side of a walk of two holds a commit (D147 §2.3, §2.6).</summary>
public static class GitSide
{
    /// <summary>The first named: the branch on its page, the left of a compare.</summary>
    public const string Left = "left";

    /// <summary>The second named: the line on a branch's page, the right of a compare.</summary>
    public const string Right = "right";
}

/// <summary>A read's answer (GIT1b): its value or why not, and the git commands a person types for the same answer (D147 §3.3).</summary>
/// <param name="Problem">The refusal said in a sentence, or null where answered.</param>
public sealed record GitAnswer<T>(T? Value, GitReadRefusal Refusal, string? Problem) where T : class
{
    /// <summary>Each git command that answers this read, as a person types it in the checkout, in git's own form.</summary>
    public IReadOnlyList<string> Commands { get; init; } = [];

    /// <summary>Whether the answer came from memory, with no git started (D147 §4.2).</summary>
    public bool Remembered { get; init; }
}

/// <summary>A commit as a list draws it: a graph's row, a file's history's, a compare's.</summary>
/// <param name="Parents">Whole ids, the first parent first; none for a root commit.</param>
public sealed record GitLogCommit(string Commit, IReadOnlyList<string> Parents, string Author, string AuthorEmail, string Subject)
{
    public DateTimeOffset? AuthoredAt { get; init; }

    public DateTimeOffset? CommittedAt { get; init; }

    /// <summary>For a walk of two sides, which holds it, one of <see cref="GitSide"/>; null for a walk from tips.</summary>
    public string? Side { get; init; }
}

/// <summary>A page of history as a graph is drawn from it (D147 §2.3): the lanes are the page's to draw, from each commit's parents.</summary>
/// <param name="Tips">The whole ids walked from, in the order they were asked and kept.</param>
/// <param name="SinceParted">Whether it walks two tips since they parted, each commit marked with its side.</param>
public sealed record GitGraphPage(IReadOnlyList<string> Tips, bool SinceParted, int Page)
{
    /// <summary>Up to <see cref="GitReads.GraphPage"/> commits, in date order.</summary>
    public IReadOnlyList<GitLogCommit> Commits { get; init; } = [];

    /// <summary>Whether another page follows.</summary>
    public bool More { get; init; }
}

/// <summary>A commit's page (D147 §2.4): its fields, its whole message, and its changes against its first parent.</summary>
public sealed record GitCommitDetail(string Commit, IReadOnlyList<string> Parents, string Subject, string Message)
{
    public string Author { get; init; } = "";

    public string AuthorEmail { get; init; } = "";

    public DateTimeOffset? AuthoredAt { get; init; }

    public string Committer { get; init; } = "";

    public string CommitterEmail { get; init; } = "";

    public DateTimeOffset? CommittedAt { get; init; }

    /// <summary>What its changes are read against: its first parent, or null for a root commit, read against the empty tree.</summary>
    public string? Against => Parents.Count > 0 ? Parents[0] : null;

    /// <summary>A merge commit, whose changes are read against its first parent, which its page says.</summary>
    public bool Merge => Parents.Count > 1;

    /// <summary>Its changes, as the review reads them (REVIEW3), with the review's bound and the bound's sentence.</summary>
    public WorkingTree.TreeDiff? Changes { get; init; }
}

/// <summary>One commit in a file's history, and the path the file had there (D147 §2.5).</summary>
/// <param name="Status">As the review says it: <c>added</c>, <c>modified</c>, <c>deleted</c>, <c>renamed</c>, <c>copied</c>.</param>
/// <param name="Path">The file's path at that commit, the one to blame there; empty where git named no change.</param>
public sealed record GitFileChange(GitLogCommit Commit, string Status, string Path)
{
    /// <summary>Where a rename or a copy came from.</summary>
    public string? From { get; init; }
}

/// <summary>A page of a file's history at a commit, following renames (D147 §2.5).</summary>
public sealed record GitFileHistory(string Commit, string Path, int Page)
{
    /// <summary>Up to <see cref="GitReads.HistoryPage"/> commits, newest first.</summary>
    public IReadOnlyList<GitFileChange> Changes { get; init; } = [];

    public bool More { get; init; }
}

/// <summary>A commit a blame names, said once however many lines it holds.</summary>
public sealed record GitBlameCommit(string Commit, string Author, string AuthorEmail, string Summary)
{
    public DateTimeOffset? AuthoredAt { get; init; }

    /// <summary>The walk's edge (git's <c>boundary</c>): the line is at least as old as this commit, which may not have made it.</summary>
    public bool Boundary { get; init; }
}

/// <summary>One line of a blamed file.</summary>
/// <param name="Original">Its line number in the commit that last changed it.</param>
public sealed record GitBlameLine(int Original, string Text);

/// <summary>A run of lines from one commit, under one path (D147 §2.5).</summary>
/// <param name="Line">Its first line's number in the file blamed.</param>
/// <param name="Path">The file's path in that commit.</param>
public sealed record GitBlameRun(string Commit, int Line, string Path)
{
    /// <summary>The commit before it that held these lines' file, where git names one: what <i>Before this change</i> blames.</summary>
    public string? Previous { get; init; }

    /// <summary>That file's path there.</summary>
    public string? PreviousPath { get; init; }

    public IReadOnlyList<GitBlameLine> Lines { get; init; } = [];
}

/// <summary>A file as a commit holds it, each line with the commit that last changed it (D147 §2.5).</summary>
public sealed record GitBlame(string Commit, string Path)
{
    /// <summary>Binary by git's own test (D111): such a file has a history and no blame, and no runs.</summary>
    public bool Binary { get; init; }

    public IReadOnlyList<GitBlameRun> Runs { get; init; } = [];

    /// <summary>Each commit the runs name, by its whole id.</summary>
    public IReadOnlyDictionary<string, GitBlameCommit> Commits { get; init; } = new Dictionary<string, GitBlameCommit>();

    /// <summary>The last line shown where the file holds more than the bound (D111), or null where the blame is whole.</summary>
    public int? StoppedAt { get; init; }
}

/// <summary>Two commits compared (D147 §2.6): the commits only on each side, and the changes.</summary>
/// <param name="Everything">The changes are everything that differs (<c>A..B</c>), not what the right side changed since the two parted (<c>A...B</c>).</param>
public sealed record GitCompare(string Left, string Right, bool Everything)
{
    /// <summary>Up to <see cref="GitReads.CompareCommits"/> commits only one side holds, each marked with its side.</summary>
    public IReadOnlyList<GitLogCommit> Commits { get; init; } = [];

    public bool More { get; init; }

    /// <summary>The changes, as the review reads them (REVIEW3), with its bound.</summary>
    public WorkingTree.TreeDiff? Changes { get; init; }
}

/// <summary>The branches whose history holds a commit (D147 §2.4), local and origin's, by their whole names.</summary>
public sealed record GitHolding(string Commit, IReadOnlyList<string> Branches);

/// <summary>A name resolved to the commit it names now (D147 §4.2).</summary>
public sealed record GitResolved(string Revision, string Commit);

/// <summary>
/// The reads behind Git's pages (GIT1b, D147 §2.3–§2.6, §4): a page of a graph, a commit, a file's history, a blame and a
/// compare, each one call in a machine format, or two for a commit and a compare, and an answer named only by whole commit
/// ids kept in memory.
/// </summary>
/// <remarks>
/// <para><b>Through Tools' git</b> (D121 §3): every call is <see cref="WorkingTree.ReadGitAsync"/>'s, which starts the file
/// Tools resolves through <see cref="WorkingTree.GitStart"/>, so the page reads the git the agents run.</para>
///
/// <para>🔴 <b>One process per view, never one per file</b> (REVIEW3). A commit is two calls: its fields and parents, then
/// REVIEW3's one-call reader over its first parent to it. A compare is two: the commits only on each side, then the same
/// reader. A graph page, a file's history and a blame are one each.</para>
///
/// <para>🔴 <b>A ref is never a key</b> (D147 §4.2). Every read is asked of whole commit ids, and anything else is refused
/// before git is asked: a ref is resolved first, by the list or <see cref="ResolveAsync"/>, which is never kept. An answer
/// named only by ids cannot change, so it is kept under the repository's git directory, the ids, the path and the page. Two
/// reads are never kept, since what they answer moves: the branches holding a commit, and a name's commit.</para>
///
/// <para><b>What is not kept.</b> An answer git did not give (a fetch may bring the commit), and anything read from a shallow
/// repository, whose history moves when it is deepened. Each is found with no process.</para>
///
/// <para>🔴 <b>git walks up</b>: a root with no <c>.git</c> of its own is refused before git is asked, found on disk as the
/// list finds it (<see cref="GitBranches.GitDirectory"/>). <b>Nothing is written</b>, in the repository or anywhere.</para>
/// </remarks>
public sealed class GitReads
{
    /// <summary>A graph page's commits (D147 §2.3).</summary>
    public const int GraphPage = 200;

    /// <summary>A file's history's commits a page (D147 §2.5).</summary>
    public const int HistoryPage = 100;

    /// <summary>A compare's commits, the rest said as more.</summary>
    public const int CompareCommits = 200;

    /// <summary>
    /// The tips one call carries: each is an argument, and a command line has a length (Windows': 32,767 characters, which
    /// 256 ids of 64 characters fit with room).
    /// </summary>
    public const int GraphTips = 256;

    /// <summary>The last page asked of: past it, git would be asked to walk more than a person pages through.</summary>
    public const int LastPage = 9_999;

    /// <summary>The empty tree's id in a repository of SHA-1: what a root commit's changes are read against.</summary>
    public const string EmptyTree = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

    /// <summary>The empty tree's id in a repository of SHA-256.</summary>
    public const string EmptyTreeSha256 = "6ef19b41225c5369f1c104d45d8d85efa9b057b53b14b4b9b939dd74decc5321";

    private readonly WorkingTree.GitRead _git;

    /// <summary>The reads through the git the agents run, keeping their answers in <paramref name="cache"/>, or in one of their own.</summary>
    public GitReads(GitReadCache? cache = null)
        : this(cache ?? new GitReadCache(), WorkingTree.ReadGitAsync)
    {
    }

    /// <param name="git">How git is read: the seam the tests count processes through, as REVIEW3's are.</param>
    internal GitReads(GitReadCache cache, WorkingTree.GitRead git)
    {
        Cache = cache;
        _git = git;
    }

    /// <summary>What the reads keep.</summary>
    public GitReadCache Cache { get; }

    /// <summary>A whole commit id: 40 hex digits in a repository of SHA-1, 64 in one of SHA-256.</summary>
    public static bool IsWholeId(string? text) => text is { Length: 40 or 64 } && text.All(char.IsAsciiHexDigit);

    /// <summary>
    /// A page of history from <paramref name="tips"/> (D147 §2.3): one <c>log</c> of <see cref="GraphPage"/> commits in date
    /// order. The tips are put in one order first, so the call and the key agree. <paramref name="sinceParted"/> walks exactly
    /// two, in the order named, since they parted (<c>A...B</c>): a branch beside its line.
    /// </summary>
    public async Task<GitAnswer<GitGraphPage>> GraphAsync(
        string root, IReadOnlyList<string> tips, int page = 0, bool sinceParted = false, CancellationToken ct = default)
    {
        if (tips.Count == 0 || !tips.All(IsWholeId)) return Refused<GitGraphPage>(GitReadRefusal.NotACommit, NotACommit(tips.FirstOrDefault(tip => !IsWholeId(tip)) ?? ""));
        List<string> named = sinceParted
            ? [.. tips.Select(Lower)]
            : [.. tips.Select(Lower).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        if (sinceParted && named.Count != 2)
        {
            return Refused<GitGraphPage>(GitReadRefusal.NotACommit, "A walk since two parted names exactly two commits: the branch, then its line.");
        }

        if (named.Count > GraphTips)
        {
            return Refused<GitGraphPage>(
                GitReadRefusal.TooManyTips, $"One graph is drawn from at most {GraphTips} tips, and {named.Count} were named: one call carries them all.");
        }

        if (NotAPage(page) is { } refused) return Refused<GitGraphPage>(GitReadRefusal.NotAPage, refused);
        if (Repository.Of(root) is not { } repository) return Refused<GitGraphPage>(GitReadRefusal.NotTop, NotTop(root));

        return await AnsweredAsync(
                repository,
                Key(repository, ["graph", sinceParted ? "parted" : "tips", Number(page), .. named]),
                async () =>
                {
                    var (code, text) = await WholeAsync(root, GitFormats.GraphArguments(named, sinceParted, page), ct).ConfigureAwait(false);
                    if (code != 0) return null;
                    var commits = GitFormats.ParseListed(text, sinceParted);
                    return new GitGraphPage(named, sinceParted, page) { Commits = [.. commits.Take(GraphPage)], More = commits.Count > GraphPage };
                },
                _ => [GitFormats.GraphCommand(named, sinceParted)],
                $"git could not walk the history from {string.Join(", ", named.Select(GitFormats.Short))} here: this repository may not hold "
                + "every commit named, or git did not start.")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// A commit's page (D147 §2.4): its fields and whole message, then its changes against its first parent through
    /// REVIEW3's one-call reader, with the review's bound. Two calls.
    /// </summary>
    public async Task<GitAnswer<GitCommitDetail>> CommitAsync(string root, string commit, CancellationToken ct = default)
    {
        if (!IsWholeId(commit)) return Refused<GitCommitDetail>(GitReadRefusal.NotACommit, NotACommit(commit));
        var id = Lower(commit);
        if (Repository.Of(root) is not { } repository) return Refused<GitCommitDetail>(GitReadRefusal.NotTop, NotTop(root));

        return await AnsweredAsync(
                repository,
                Key(repository, "commit", id),
                async () =>
                {
                    var (code, text) = await WholeAsync(root, GitFormats.CommitArguments(id), ct).ConfigureAwait(false);
                    if (code != 0 || GitFormats.ParseCommit(text) is not { } detail || !Same(detail.Commit, id)) return null;

                    var against = detail.Against ?? (id.Length == 64 ? EmptyTreeSha256 : EmptyTree);
                    var rest = detail.Against is null
                        ? $"`git show {GitFormats.Short(id)}` in the repository's checkout has all of it."
                        : $"`git diff {GitFormats.Short(against)} {GitFormats.Short(id)}` in the repository's checkout has all of it.";
                    var changes = await WorkingTree.RangeAsync(root, against, $"{against}..{id}", rest, _git, ct).ConfigureAwait(false);
                    return changes is null ? null : detail with { Changes = changes };
                },
                // Unanswered, its parents are not known, so only its fields' command is said.
                detail => detail is null ? [GitFormats.CommitCommands(id, against: null)[0]] : GitFormats.CommitCommands(id, detail.Against),
                $"git could not read commit {GitFormats.Short(id)} here: this repository may not hold it, or git did not start.")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// A page of the history of <paramref name="path"/> at <paramref name="commit"/> (D147 §2.5): one <c>log --follow</c>,
    /// following renames, <see cref="HistoryPage"/> commits a page, each with the path the file had there.
    /// </summary>
    public async Task<GitAnswer<GitFileHistory>> HistoryAsync(string root, string commit, string path, int page = 0, CancellationToken ct = default)
    {
        if (!IsWholeId(commit)) return Refused<GitFileHistory>(GitReadRefusal.NotACommit, NotACommit(commit));
        if (!IsRepositoryPath(path)) return Refused<GitFileHistory>(GitReadRefusal.NotAPath, NotAPath(path));
        if (NotAPage(page) is { } refused) return Refused<GitFileHistory>(GitReadRefusal.NotAPage, refused);
        var id = Lower(commit);
        if (Repository.Of(root) is not { } repository) return Refused<GitFileHistory>(GitReadRefusal.NotTop, NotTop(root));

        return await AnsweredAsync(
                repository,
                Key(repository, "history", id, Number(page), path),
                async () =>
                {
                    var (code, text) = await WholeAsync(root, GitFormats.HistoryArguments(id, path, page), ct).ConfigureAwait(false);
                    if (code != 0) return null;
                    var changes = GitFormats.ParseHistory(text);
                    return new GitFileHistory(id, path, page)
                    {
                        Changes = [.. changes.Skip(page * HistoryPage).Take(HistoryPage)],
                        More = changes.Count > (page + 1) * HistoryPage,
                    };
                },
                _ => [GitFormats.HistoryCommand(id, path)],
                $"git could not read the history of {path} at {GitFormats.Short(id)} here: this repository may not hold the commit, "
                + "or git did not start.")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <paramref name="path"/> as <paramref name="commit"/> holds it, each line with the commit that last changed it (D147
    /// §2.5): one <c>blame --porcelain</c>, with D111's bound and binary test. <i>Before this change</i> is this read of a
    /// run's <see cref="GitBlameRun.Previous"/> at its <see cref="GitBlameRun.PreviousPath"/>.
    /// </summary>
    public async Task<GitAnswer<GitBlame>> BlameAsync(string root, string commit, string path, CancellationToken ct = default)
    {
        if (!IsWholeId(commit)) return Refused<GitBlame>(GitReadRefusal.NotACommit, NotACommit(commit));
        if (!IsRepositoryPath(path)) return Refused<GitBlame>(GitReadRefusal.NotAPath, NotAPath(path));
        var id = Lower(commit);
        if (Repository.Of(root) is not { } repository) return Refused<GitBlame>(GitReadRefusal.NotTop, NotTop(root));

        return await AnsweredAsync(
                repository,
                Key(repository, "blame", id, path),
                async () =>
                {
                    var split = new GitBlameSplit();
                    var code = await _git(root, GitFormats.BlameArguments(id, path), piece => split.Take(piece.Span), ct).ConfigureAwait(false);
                    return code != 0 && !split.Stopped ? null : split.End(id, path);
                },
                _ => [GitFormats.BlameCommand(id, path)],
                $"git could not blame {path} at {GitFormats.Short(id)} here: the commit may hold no file there, this repository may not "
                + "hold the commit, or git did not start.")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Two commits compared (D147 §2.6): one <c>log</c> of the commits only on each side, then REVIEW3's reader over what the
    /// right side changed since the two parted (<c>A...B</c>), or everything that differs (<c>A..B</c>). Two calls.
    /// </summary>
    public async Task<GitAnswer<GitCompare>> CompareAsync(
        string root, string left, string right, bool everything = false, CancellationToken ct = default)
    {
        if (!IsWholeId(left) || !IsWholeId(right)) return Refused<GitCompare>(GitReadRefusal.NotACommit, NotACommit(IsWholeId(left) ? right : left));
        var (a, b) = (Lower(left), Lower(right));
        if (Repository.Of(root) is not { } repository) return Refused<GitCompare>(GitReadRefusal.NotTop, NotTop(root));

        return await AnsweredAsync(
                repository,
                Key(repository, "compare", everything ? "everything" : "parted", a, b),
                async () =>
                {
                    var (code, text) = await WholeAsync(root, GitFormats.CompareArguments(a, b), ct).ConfigureAwait(false);
                    if (code != 0) return null;
                    var commits = GitFormats.ParseListed(text, marked: true);

                    var range = everything ? $"{a}..{b}" : $"{a}...{b}";
                    var changes = await WorkingTree.RangeAsync(
                            root, a, range, $"`git diff {GitFormats.Short(a)}{(everything ? ".." : "...")}{GitFormats.Short(b)}` in the repository's checkout has all of it.",
                            _git, ct)
                        .ConfigureAwait(false);
                    return changes is null
                        ? null
                        : new GitCompare(a, b, everything) { Commits = [.. commits.Take(CompareCommits)], More = commits.Count > CompareCommits, Changes = changes };
                },
                _ => GitFormats.CompareCommands(a, b, everything),
                $"git could not compare {GitFormats.Short(a)} with {GitFormats.Short(b)} here: this repository may not hold both, or git did not start.")
            .ConfigureAwait(false);
    }

    /// <summary>The local branches and origin's whose history holds <paramref name="commit"/> (D147 §2.4): one call, never kept.</summary>
    public async Task<GitAnswer<GitHolding>> HoldingAsync(string root, string commit, CancellationToken ct = default)
    {
        if (!IsWholeId(commit)) return Refused<GitHolding>(GitReadRefusal.NotACommit, NotACommit(commit));
        var id = Lower(commit);
        if (Repository.Of(root) is null) return Refused<GitHolding>(GitReadRefusal.NotTop, NotTop(root));

        IReadOnlyList<string> commands = [GitFormats.HoldingCommand(id)];
        var (code, text) = await WholeAsync(root, GitFormats.HoldingArguments(id), ct).ConfigureAwait(false);
        return code != 0
            ? new(null, GitReadRefusal.NotAnswered, $"git could not say which branches hold {GitFormats.Short(id)} here.") { Commands = commands }
            : new(new GitHolding(id, GitFormats.ParseHolding(text)), GitReadRefusal.None, null) { Commands = commands };
    }

    /// <summary>
    /// The commit <paramref name="revision"/> names now (D147 §4.2): one <c>rev-parse</c>, never kept, since a ref moves. A
    /// name git could read as an option, or one holding a space or a control character, is refused before git is asked.
    /// </summary>
    public async Task<GitAnswer<GitResolved>> ResolveAsync(string root, string revision, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(revision) || revision[0] == '-' || revision.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return Refused<GitResolved>(GitReadRefusal.NotACommit, $"“{revision}” is not a name git resolves to a commit here.");
        }

        if (Repository.Of(root) is null) return Refused<GitResolved>(GitReadRefusal.NotTop, NotTop(root));

        IReadOnlyList<string> commands = [GitFormats.ResolveCommand(revision)];
        var (code, text) = await WholeAsync(root, GitFormats.ResolveArguments(revision), ct).ConfigureAwait(false);
        var commit = text.Trim();
        return code == 0 && IsWholeId(commit)
            ? new(new GitResolved(revision, Lower(commit)), GitReadRefusal.None, null) { Commands = commands }
            : new(null, GitReadRefusal.NotAnswered, $"“{revision}” names no commit here.") { Commands = commands };
    }

    /// <summary>
    /// A path in the repository: not empty, not rooted, never climbing out, and holding no NUL, which no argument can. git's
    /// own spelling, forward slashes; a back slash is a separator only on Windows, where no file name holds one.
    /// </summary>
    internal static bool IsRepositoryPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\0') || path[0] == '/') return false;
        char[] separators = OperatingSystem.IsWindows() ? ['/', '\\'] : ['/'];
        if (OperatingSystem.IsWindows() && (path[0] == '\\' || (path.Length >= 2 && path[1] == ':'))) return false;
        return !path.Split(separators).Any(segment => segment == "..");
    }

    /// <summary>
    /// The answer kept under <paramref name="key"/>, or the read's, kept where it answered and the repository is not shallow.
    /// The commands are the value's, so a kept answer names the same commands a fresh one does.
    /// </summary>
    private async Task<GitAnswer<T>> AnsweredAsync<T>(
        Repository repository, string key, Func<Task<T?>> read, Func<T?, IReadOnlyList<string>> commands, string unanswered) where T : class
    {
        if (repository.Remembers && Cache.TryGet<T>(key, out var kept))
        {
            return new(kept, GitReadRefusal.None, null) { Commands = commands(kept), Remembered = true };
        }

        var value = await read().ConfigureAwait(false);
        if (value is null) return new(null, GitReadRefusal.NotAnswered, unanswered) { Commands = commands(null) };

        if (repository.Remembers) Cache.Keep(key, value, GitReadWeight.Of(value));
        return new(value, GitReadRefusal.None, null) { Commands = commands(value) };
    }

    /// <summary>git's whole answer, through the review's reader (REVIEW3), for a call whose answer its own <c>--max-count</c> bounds.</summary>
    private async Task<(int Code, string Text)> WholeAsync(string root, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var text = new System.Text.StringBuilder();
        var code = await _git(root, arguments, piece =>
        {
            text.Append(piece.Span);
            return true;
        }, ct).ConfigureAwait(false);
        return (code, text.ToString());
    }

    private static string Key(Repository repository, params string[] parts) => string.Join('\0', [repository.Directory, .. parts]);

    private static string Number(int page) => page.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static GitAnswer<T> Refused<T>(GitReadRefusal why, string problem) where T : class => new(null, why, problem);

    private static string? NotAPage(int page) =>
        page is < 0 or > LastPage ? $"Page {page} is not one a read is asked of: the first is 0, the last {LastPage}." : null;

    private static string NotTop(string root) =>
        $"{root} is not the top of a repository of its own, so git is not asked: it would answer for whichever repository holds it.";

    private static string NotACommit(string named) =>
        $"“{named}” is not a whole commit id: a read is asked of commits, and a branch's name is resolved to its commit first, since a branch moves.";

    private static string NotAPath(string path) => $"“{path}” is not a path in the repository.";

    private static string Lower(string id) => id.ToLowerInvariant();

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A repository as the reads key it: its common git directory, which a linked worktree shares with its checkout, found on
    /// disk; and whether its answers may be kept, which a shallow one's may not.
    /// </summary>
    private sealed record Repository(string Directory, bool Remembers)
    {
        public static Repository? Of(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !System.IO.Directory.Exists(root) || GitBranches.GitDirectory(root) is not { } own) return null;

            var common = Common(own);
            var directory = Path.TrimEndingDirectorySeparator(common);
            if (OperatingSystem.IsWindows()) directory = directory.ToLowerInvariant();
            return new Repository(directory, !File.Exists(Path.Combine(common, "shallow")));
        }

        /// <summary>A linked worktree's git directory names its repository's in <c>commondir</c>; any other is its own.</summary>
        private static string Common(string own)
        {
            try
            {
                var file = Path.Combine(own, "commondir");
                if (!File.Exists(file)) return own;
                var named = File.ReadAllText(file).Trim();
                var full = Path.GetFullPath(Path.IsPathRooted(named) ? named : Path.Combine(own, named));
                return System.IO.Directory.Exists(full) ? full : own;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return own;
            }
        }
    }
}

/// <summary>What an answer weighs in memory, as the cache counts it: two bytes a character, and a little for each object.</summary>
internal static class GitReadWeight
{
    private const long Object = 48;

    public static long Of(object? value) => value switch
    {
        null => 0,
        string text => Object + (2L * text.Length),
        GitLogCommit commit => Object + Of(commit.Commit) + Many(commit.Parents) + Of(commit.Author) + Of(commit.AuthorEmail) + Of(commit.Subject) + Of(commit.Side),
        GitGraphPage page => Object + Many(page.Tips) + Many(page.Commits),
        GitCommitDetail detail => Object + Of(detail.Commit) + Many(detail.Parents) + Of(detail.Subject) + Of(detail.Message) + Of(detail.Author)
                                  + Of(detail.AuthorEmail) + Of(detail.Committer) + Of(detail.CommitterEmail) + Of(detail.Changes),
        WorkingTree.TreeDiff diff => Object + Of(diff.Base) + Of(diff.Truncated) + Many(diff.Files),
        WorkingTree.DiffFile file => Object + Of(file.Path) + Of(file.Status) + Of(file.Patch),
        GitFileHistory history => Object + Of(history.Commit) + Of(history.Path) + Many(history.Changes),
        GitFileChange change => Object + Of(change.Commit) + Of(change.Status) + Of(change.Path) + Of(change.From),
        GitBlame blame => Object + Of(blame.Commit) + Of(blame.Path) + Many(blame.Runs) + Many(blame.Commits.Values),
        GitBlameRun run => Object + Of(run.Commit) + Of(run.Path) + Of(run.Previous) + Of(run.PreviousPath) + Many(run.Lines),
        GitBlameLine line => Object + Of(line.Text),
        GitBlameCommit commit => Object + Of(commit.Commit) + Of(commit.Author) + Of(commit.AuthorEmail) + Of(commit.Summary),
        GitCompare compare => Object + Of(compare.Left) + Of(compare.Right) + Many(compare.Commits) + Of(compare.Changes),
        _ => Object,
    };

    private static long Many<T>(IEnumerable<T> values) => values.Sum(value => Of(value));
}
