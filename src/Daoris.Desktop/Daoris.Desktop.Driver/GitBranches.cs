namespace Daoris.Driver;

/// <summary>The kinds a branch is listed under (GIT1a, D147 §2.2), in the order a list shows them.</summary>
public static class GitBranchKind
{
    /// <summary>The repository's line (D86), the local branch of that name.</summary>
    public const string Line = "line";

    /// <summary>A session's branch: the <c>daoris/</c> namespace Daoris's proofs read as its own sessions' (D88).</summary>
    public const string Session = "session";

    /// <summary>A branch a landing made that <c>landings.json</c> still records (D102), while it holds the commit it was made at.</summary>
    public const string Landed = "landed";

    /// <summary>Every other local branch: the person's own, a recorded name a branch no longer matches among them (D113).</summary>
    public const string Yours = "yours";

    /// <summary>A branch only origin holds here, as this checkout last fetched it.</summary>
    public const string Origin = "origin";

    public static IReadOnlyList<string> Order { get; } = [Line, Session, Landed, Yours, Origin];
}

/// <summary>How a landed branch stands to its own name on origin (D147 §2.2), as this checkout last fetched origin.</summary>
public static class GitOrigin
{
    public const string InStep = "in-step";

    /// <summary>It holds commits origin's copy does not.</summary>
    public const string Ahead = "ahead";

    /// <summary>Origin's copy holds commits it does not.</summary>
    public const string Behind = "behind";

    public const string Diverged = "diverged";

    /// <summary>The landing recorded a push, and origin holds no branch of its name now: deleted there, or pruned after its pull request merged.</summary>
    public const string Gone = "gone";

    /// <summary>Origin holds no branch of its name, and no push was recorded.</summary>
    public const string NotPushed = "not-pushed";

    /// <summary>Origin's copy differs, and git could not count how.</summary>
    public const string Unknown = "unknown";
}

/// <summary>The line's head (D147 §2.2): which branch, what set it (D86), and how it stands to origin's copy of it.</summary>
/// <param name="Branch">The line, or null where nothing names one.</param>
/// <param name="Source">What set it, one of <see cref="LineSource"/>.</param>
public sealed record GitLine(string? Branch, string Source)
{
    /// <summary>The local branch's commit, or null where this checkout has no branch of that name.</summary>
    public string? Commit { get; init; }

    /// <summary>Origin's copy's commit, as last fetched, or null where it holds none here.</summary>
    public string? Origin { get; init; }

    /// <summary>The line's commits origin's copy lacks, or null where not counted.</summary>
    public int? Ahead { get; init; }

    /// <summary>Origin's commits the line lacks, or null where not counted.</summary>
    public int? Behind { get; init; }
}

/// <summary>What names a session's branch (D147 §2.2): the newest session record on the tree of its name, and where it grew from (WSR6).</summary>
public sealed record GitSessionBranch
{
    /// <summary>The newest session record whose tree has the branch's name, or null where no record read names it.</summary>
    public string? Session { get; init; }

    public string? State { get; init; }

    public string? Quest { get; init; }

    /// <summary>The chain step's branch it grew from (CHAIN2), as <c>session-branches.json</c> records it; null where it grew from the line.</summary>
    public string? GrewFrom { get; init; }

    /// <summary>The commit it started at, as <c>session-branches.json</c> records it; null where nothing recorded it.</summary>
    public string? From { get; init; }
}

/// <summary>What a landed branch's record says (D102), and how it stands to its own name on origin.</summary>
/// <param name="Origin">One of <see cref="GitOrigin"/>.</param>
public sealed record GitLandedBranch(string Session, DateTimeOffset LandedAt, string Origin)
{
    public string? Quest { get; init; }

    public string? Title { get; init; }

    /// <summary>The plugin that answered that it pushed it (D100), or null.</summary>
    public string? Plugin { get; init; }

    /// <summary>Whether a push was recorded.</summary>
    public bool Pushed { get; init; }

    /// <summary>The pull request a plugin answered with: the link the landing kept, or null.</summary>
    public string? PullRequest { get; init; }

    /// <summary>The commit a plugin pushed, or null.</summary>
    public string? PushedTip { get; init; }

    /// <summary>Origin's copy's commit, as last fetched, or null where it holds none.</summary>
    public string? OriginCommit { get; init; }

    /// <summary>Its commits origin's copy lacks, where counted.</summary>
    public int? OriginAhead { get; init; }

    /// <summary>Origin's copy's commits it lacks, where counted.</summary>
    public int? OriginBehind { get; init; }

    /// <summary>
    /// What its plugin last answered about its pull request, with when (PLUGHOOK1c, D148 point 6, D147 §2.2), read from the
    /// landing record; null where none is kept. A read never asks.
    /// </summary>
    public PullRequestState? PullRequestState { get; init; }

    /// <summary>The latest ask about it that failed since that answer, where one did.</summary>
    public PullRequestAskFailed? PullRequestAskFailed { get; init; }
}

/// <summary>One branch in the list (D147 §2.2).</summary>
/// <param name="Name">As a person reads it: <c>topic</c>, or <c>origin/release</c> for one only origin holds.</param>
/// <param name="Kind">One of <see cref="GitBranchKind"/>.</param>
public sealed record GitBranch(string Name, string Kind, string Commit)
{
    public DateTimeOffset? At { get; init; }

    public string Subject { get; init; } = "";

    /// <summary>The working tree it is checked out in, the repository's own checkout included; null where none.</summary>
    public string? Worktree { get; init; }

    /// <summary>Its commits the line does not hold, or null where not counted (no line, or origin's on a git without the atom).</summary>
    public int? Ahead { get; init; }

    /// <summary>The line's commits it does not hold, or null where not counted.</summary>
    public int? Behind { get; init; }

    /// <summary>For a session's branch, what names it; null otherwise.</summary>
    public GitSessionBranch? Session { get; init; }

    /// <summary>For a landed branch, its record and how it stands to origin; null otherwise.</summary>
    public GitLandedBranch? Landed { get; init; }
}

/// <summary>When this checkout last fetched (D147 §2.2): <c>FETCH_HEAD</c>'s time, git's own.</summary>
/// <param name="Heard">False for an empty <c>FETCH_HEAD</c>: git empties it as a fetch begins, so one left empty was tried and heard nothing.</param>
public sealed record GitFetch(DateTimeOffset At, bool Heard);

/// <summary>One repository's group in the list (D147 §2.2): its line, its last fetch, its branches by kind, and how git answered.</summary>
public sealed record RepositoryBranches(string Repository, string Workspace, GitLine Line)
{
    /// <summary>The line first, then each kind in <see cref="GitBranchKind.Order"/>, each newest commit first.</summary>
    public IReadOnlyList<GitBranch> Branches { get; init; } = [];

    /// <summary>The last fetch, or null where this checkout never fetched.</summary>
    public GitFetch? Fetch { get; init; }

    /// <summary>
    /// Whether a branch of Daoris's stands here, a session's or a landed one (D112). A repository git could not answer for
    /// counts as holding one, so git's words reach its row rather than the repository vanishing.
    /// </summary>
    public bool Holds { get; init; }

    /// <summary>The atoms this git lacked (<see cref="GitRefs.WorktreePath"/>, <see cref="GitRefs.AheadBehind"/>), each answered another way.</summary>
    public IReadOnlyList<string> Missing { get; init; } = [];

    /// <summary>Each git call the read made, as a person types it in the checkout (D147 §3.3), in order.</summary>
    public IReadOnlyList<string> Commands { get; init; } = [];

    /// <summary>Why git could not list it, in git's words where it said any; null where it answered.</summary>
    public string? Problem { get; init; }
}

/// <summary>The list (D147 §2.2): the repositories a scope takes, and every other one named apart (D112).</summary>
public sealed record GitBranchList(IReadOnlyList<RepositoryBranches> Repositories, IReadOnlyList<SyncRepository> Apart);

/// <summary>One repository as the list reads it: its line, already resolved, and the records that name its branches.</summary>
public sealed record GitRepositoryAsk(string Repository, string Workspace, Line Line)
{
    /// <summary>The landings record (D102); only this repository's standing entries are read.</summary>
    public IReadOnlyList<LandedBranch> Landings { get; init; } = [];

    /// <summary>Where session branches grew from (WSR6); only this repository's are read.</summary>
    public IReadOnlyList<GrownBranch> Grown { get; init; } = [];

    /// <summary>The session records, closed ones included, or null where none were read: a session branch is then listed unnamed.</summary>
    public IReadOnlyList<SessionRecord>? Sessions { get; init; }

    /// <summary>The last fetch, read from the repository's git directory before git is asked anything.</summary>
    public GitFetch? Fetch { get; init; }
}

/// <summary>How the list asks git: the arguments after <c>git</c>, and its exit code, output and error.</summary>
public delegate Task<(int Code, string Stdout, string Stderr)> GitRun(IReadOnlyList<string> arguments, CancellationToken ct);

/// <summary>
/// The branch list, read (GIT1a, D147 §2.2, §4.1): each repository's line and branches by kind, from one
/// <c>for-each-ref</c> per repository through the git the agents run.
/// </summary>
/// <remarks>
/// <para><b>Through Tools' git</b> (D121 §3): every call starts through <see cref="WorkingTree.GitAsync(string, IReadOnlyList{string}, CancellationToken)"/>,
/// which runs the file Tools resolves by its path, so the page reads the git the agents run.</para>
///
/// <para>🔴 <b>One process per repository, never one per branch</b> (REVIEW3: a start costs about 100 ms, and a start per
/// file was the measured cost). Three things add a call, each bounded by what is exceptional, and each is said in
/// <see cref="RepositoryBranches.Commands"/>:</para>
/// <list type="bullet">
/// <item>a landed branch whose commit moved since its landing: one <c>merge-base --is-ancestor</c>, since it is the landing's
/// only while it holds the commit the landing made it at (D102);</item>
/// <item>a landed branch whose copy on origin differs from it: one <c>rev-list --left-right --count</c>;</item>
/// <item>a git older than an atom: <see cref="ReadAsync"/>'s fallbacks.</item>
/// </list>
/// <para>The line itself is <see cref="CanonicalLine"/>'s, read as every door reads it: no call where the person set it, and up
/// to three cheap ones where the checkout's guess answers.</para>
///
/// <para><b>A git without an atom is still answered</b>, and which atom it lacks is found by asking a form any git answers,
/// never by reading git's message, which a localised git words in its own language. Without <c>%(ahead-behind)</c> (before
/// Git 2.41) each local branch is counted on its own, one <c>rev-list --left-right --count</c> each, and origin's branches are
/// not counted; without <c>%(worktreepath)</c> (before 2.23, so without both) one <c>worktree list --porcelain</c> says which
/// tree holds each. A line set to a branch this checkout has only on origin is counted from origin's copy, after the
/// list's call said it found no local one: two calls more.</para>
///
/// <para><b>Plan and apply apart.</b> <see cref="GitRefs"/> reads each answer and <see cref="Sort"/> decides each kind,
/// both pure; this class's process half only asks.</para>
///
/// <para><b>Nothing is written</b>, anywhere: every call is a read, and the records are opened to read.</para>
/// </remarks>
public static class GitBranches
{
    /// <summary>How many repositories are read at once, as many as bringing up to date fetches (WSR7).</summary>
    public const int ReadsAtOnce = SyncBounds.FetchesAtOnce;

    /// <summary>
    /// Every repository with a checkout here, read, and listed by D112's scope: by default those holding a branch of
    /// Daoris's first, every other one named apart; <see cref="SyncScope.Everything"/> lists every one, and a repository
    /// named is listed. Each is read to know whether it holds one, so an apart repository costs its one call too.
    /// </summary>
    /// <param name="sessions">The session records, closed ones included, which name each session's branch; null where they could not be read.</param>
    public static async Task<GitBranchList> ListAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, DriverConfig config, string home,
        IReadOnlyList<SessionRecord>? sessions, SyncScope? scope = null, CancellationToken ct = default)
    {
        var landings = new LandedBranches(home).All();
        var grown = new SessionBranches(home).All();
        var present = repositories.Where(each => !string.IsNullOrWhiteSpace(each.Root) && Directory.Exists(each.Root)).ToList();
        var read = await SessionTrees.AtMostAsync(present, ReadsAtOnce, async (each, token) =>
        {
            var workspace = RemoteTarget.Workspace(each.Workspace);
            var root = each.Root!;
            if (GitDirectory(root) is not { } directory)
            {
                // 🔴 git walks up: asked here, it would answer for whichever repository holds this folder.
                var set = CanonicalLine.Choose(config, each.Repository, workspace, guess: null);
                return new RepositoryBranches(each.Repository, workspace, new GitLine(set.Branch, set.Source))
                {
                    Holds = true,
                    Problem = $"{root} is not the top of a repository of its own, so git is not asked: it would answer for "
                              + "whichever repository holds it.",
                };
            }

            var line = await CanonicalLine.ResolveAsync(root, each.Repository, workspace, config, token).ConfigureAwait(false);
            var ask = new GitRepositoryAsk(each.Repository, workspace, line)
            {
                Landings = landings, Grown = grown, Sessions = sessions, Fetch = FetchOf(directory),
            };
            return await ReadAsync(ask, (arguments, run) => WorkingTree.GitAsync(root, arguments, run), token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        var taken = scope ?? SyncScope.Held;
        bool Includes(RepositoryBranches each) => taken.Includes(new SyncRepository(each.Repository, each.Workspace, each.Holds));
        return new GitBranchList(
            [.. read.Where(Includes).OrderByDescending(each => each.Holds)],
            [.. read.Where(each => !Includes(each)).Select(each => new SyncRepository(each.Repository, each.Workspace, each.Holds))]);
    }

    /// <summary>
    /// 🔴 The repository's own git directory, found on disk, or null where <paramref name="root"/> has no <c>.git</c> of its
    /// own: git walks up, so a folder inside another repository would be answered for by it. A linked worktree's
    /// <c>.git</c> file names its directory.
    /// </summary>
    public static string? GitDirectory(string root)
    {
        try
        {
            var own = Path.Combine(root, ".git");
            if (Directory.Exists(own)) return Path.GetFullPath(own);
            if (!File.Exists(own)) return null;

            var text = File.ReadAllText(own).Trim();
            if (!text.StartsWith("gitdir:", StringComparison.Ordinal)) return null;
            var named = text["gitdir:".Length..].Trim();
            var full = Path.GetFullPath(Path.IsPathRooted(named) ? named : Path.Combine(root, named));
            return Directory.Exists(full) ? full : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The last fetch, from <c>FETCH_HEAD</c> in the git directory: its time where it is, never a guess where it is not. It is
    /// the file a fetch writes there, the person's own; Daoris's fetch when it brings a repository up to date writes none (D109).
    /// </summary>
    public static GitFetch? FetchOf(string gitDirectory)
    {
        try
        {
            var file = new FileInfo(Path.Combine(gitDirectory, "FETCH_HEAD"));
            return file.Exists ? new GitFetch(new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), file.Length > 0) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// One repository's list: the refs, then a call for each exceptional landed branch, or for each branch where the git
    /// lacks <c>%(ahead-behind)</c>, then each branch sorted into its kind. Every call is said in the answer's commands.
    /// </summary>
    internal static async Task<RepositoryBranches> ReadAsync(GitRepositoryAsk ask, GitRun git, CancellationToken ct = default)
    {
        var commands = new List<string>();
        async Task<(int Code, string Stdout, string Stderr)> Asked(IReadOnlyList<string> arguments)
        {
            commands.Add(GitRefs.Command(arguments));
            return await git(arguments, ct).ConfigureAwait(false);
        }

        var (read, problem) = await RefsAsync(ask.Line.Branch, Asked).ConfigureAwait(false);
        if (read is null)
        {
            return new RepositoryBranches(ask.Repository, ask.Workspace, new GitLine(ask.Line.Branch, ask.Line.Source))
            {
                Holds = true, Fetch = ask.Fetch, Problem = problem, Commands = commands,
            };
        }

        var answers = new Dictionary<string, (int Code, string Stdout)>(StringComparer.Ordinal);
        foreach (var (key, arguments) in Owed(ask, read))
        {
            var (code, stdout, _) = await Asked(arguments).ConfigureAwait(false);
            answers[key] = (code, stdout);
        }

        return Sort(ask, read, answers) with { Commands = commands };
    }

    /// <summary>What the list's call answered: the refs, the ref their distances count from, and the atoms the git lacked.</summary>
    internal sealed record GitRefRead(IReadOnlyList<GitRef> Refs, string? Against, IReadOnlyList<string> Missing);

    /// <summary>
    /// The refs, in one call where the git knows both atoms and the line is a local branch. Otherwise what failed is found by
    /// asking a form any git answers (<see cref="GitBranches"/>' remarks), and the refs come from the first form that answers.
    /// </summary>
    private static async Task<(GitRefRead? Read, string? Problem)> RefsAsync(
        string? line, Func<IReadOnlyList<string>, Task<(int Code, string Stdout, string Stderr)>> asked)
    {
        var first = line is null ? null : GitRefs.Heads + line;
        var whole = await asked(GitRefs.Arguments(trees: true, first)).ConfigureAwait(false);
        if (whole.Code == 0) return (new GitRefRead(GitRefs.Parse(whole.Stdout), first, []), null);

        var plain = await asked(GitRefs.Arguments(trees: false, against: null)).ConfigureAwait(false);
        if (plain.Code != 0)
        {
            return (null, FirstLine(plain.Stderr) is { Length: > 0 } said ? said : "git could not list the branches, and said nothing.");
        }

        var refs = GitRefs.Parse(plain.Stdout);
        var against = line is null ? null
            : refs.Any(each => each.Name == GitRefs.Heads + line) ? GitRefs.Heads + line
            : refs.Any(each => each.Name == GitRefs.Origin + line) ? GitRefs.Origin + line
            : null;

        // The line is a branch this checkout has only on origin: counted from origin's copy.
        if (against is not null && against != first)
        {
            var counted = await asked(GitRefs.Arguments(trees: true, against)).ConfigureAwait(false);
            if (counted.Code == 0) return (new GitRefRead(GitRefs.Parse(counted.Stdout), against, []), null);
        }

        // The first call asked a distance: the trees alone answer where %(ahead-behind) is what this git lacks.
        if (first is not null)
        {
            var trees = await asked(GitRefs.Arguments(trees: true, against: null)).ConfigureAwait(false);
            if (trees.Code == 0)
            {
                return (new GitRefRead(GitRefs.Parse(trees.Stdout), against, against is null ? [] : [GitRefs.AheadBehind]), null);
            }
        }

        // No %(worktreepath): a git before 2.23, and so before %(ahead-behind) too.
        var listed = await asked(["worktree", "list", "--porcelain"]).ConfigureAwait(false);
        var held = listed.Code == 0 ? GitRefs.ParseWorktrees(listed.Stdout) : new Dictionary<string, string>(StringComparer.Ordinal);
        IReadOnlyList<string> missing = against is null ? [GitRefs.WorktreePath] : [GitRefs.WorktreePath, GitRefs.AheadBehind];
        return (new GitRefRead([.. refs.Select(each => each with { Worktree = held.GetValueOrDefault(each.Name) })], against, missing), null);
    }

    /// <summary>
    /// The calls the refs leave owed, each keyed by what it answers: a moved landed branch's tip (<c>holds:</c>), a landed
    /// branch against its copy on origin (<c>origin:</c>), and where the git lacks <c>%(ahead-behind)</c> each local branch
    /// and the line against origin's copy (<c>count:</c>). Pure.
    /// </summary>
    internal static IReadOnlyList<(string Key, IReadOnlyList<string> Arguments)> Owed(GitRepositoryAsk ask, GitRefRead read)
    {
        var owed = new List<(string, IReadOnlyList<string>)>();
        var refs = ByName(read.Refs);
        var landings = LandingsOf(ask);
        var candidates = read.Refs
            .Where(each => each.Local && landings.ContainsKey(each.Short) && MayBeLanded(ask, each, landings[each.Short]))
            .ToList();

        foreach (var each in candidates.Where(each => !AtTip(landings[each.Short], each)))
        {
            owed.Add(($"holds:{each.Name}", ["merge-base", "--is-ancestor", landings[each.Short].Tip, each.Name]));
        }

        foreach (var each in candidates)
        {
            if (refs.TryGetValue(GitRefs.Origin + each.Short, out var copy) && !Same(copy.Commit, each.Commit))
            {
                owed.Add(($"origin:{each.Name}", ["rev-list", "--left-right", "--count", $"{each.Name}...{copy.Name}"]));
            }
        }

        if (read.Missing.Contains(GitRefs.AheadBehind) && read.Against is { } against)
        {
            foreach (var each in read.Refs.Where(each => each.Local && each.Name != against))
            {
                owed.Add(($"count:{each.Name}", ["rev-list", "--left-right", "--count", $"{each.Name}...{against}"]));
            }

            var line = ask.Line.Branch;
            if (against == GitRefs.Heads + line && refs.TryGetValue(GitRefs.Origin + line, out var origin) && !Same(origin.Commit, refs[against].Commit))
            {
                owed.Add(("count:line", ["rev-list", "--left-right", "--count", $"{against}...{origin.Name}"]));
            }
        }

        return owed;
    }

    /// <summary>
    /// Each ref sorted into its kind, from facts (D147 §2.2): the line by its name; a session's branch by the <c>daoris/</c>
    /// namespace, named by the newest session record on the tree of its name; a landed one by the record, while it holds the
    /// commit the landing made it at; every other local branch the person's; origin's where no local branch has its name.
    /// Pure: what git answered beyond the refs comes in <paramref name="answers"/>, keyed as <see cref="Owed"/> keys them.
    /// </summary>
    internal static RepositoryBranches Sort(
        GitRepositoryAsk ask, GitRefRead read, IReadOnlyDictionary<string, (int Code, string Stdout)> answers)
    {
        var refs = ByName(read.Refs);
        var name = ask.Line.Branch;
        var local = name is not null && refs.TryGetValue(GitRefs.Heads + name, out var own) ? own : null;
        var origin = name is not null && refs.TryGetValue(GitRefs.Origin + name, out var theirs) ? theirs : null;

        var line = new GitLine(name, ask.Line.Source) { Commit = local?.Commit, Origin = origin?.Commit };
        if (local is not null && origin is not null)
        {
            if (Same(local.Commit, origin.Commit)) line = line with { Ahead = 0, Behind = 0 };
            // Origin's copy counted against the line: what it holds beyond the line is what the line lacks.
            else if (read.Against == local.Name && origin is { Ahead: { } more, Behind: { } fewer }) line = line with { Ahead = fewer, Behind = more };
            else if (Counted(answers, "count:line") is { } counted) line = line with { Ahead = counted.Left, Behind = counted.Right };
        }

        var landings = LandingsOf(ask);
        var grown = ask.Grown
            .Where(each => Same(each.Repository, ask.Repository))
            .GroupBy(each => each.Branch, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        var sessions = (ask.Sessions ?? [])
            .Where(each => !each.Teammate && each.Tree is { Length: > 0 } && Same(each.Repository, ask.Repository))
            .GroupBy(each => TreeName(each.Tree!), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(each => each.Created).ThenByDescending(each => each.Id, StringComparer.Ordinal).First(),
                StringComparer.Ordinal);

        var branches = new List<GitBranch>();
        foreach (var each in read.Refs)
        {
            if (each.Local)
            {
                branches.Add(Branch(each));
            }
            else if (each.Name.StartsWith(GitRefs.Origin, StringComparison.Ordinal) && each.Symref is null && each != origin
                     && !refs.ContainsKey(GitRefs.Heads + each.Name[GitRefs.Origin.Length..]))
            {
                branches.Add(new GitBranch(each.Short, GitBranchKind.Origin, each.Commit)
                {
                    At = each.At, Subject = each.Subject, Ahead = each.Ahead, Behind = each.Behind,
                });
            }
        }

        var order = GitBranchKind.Order.ToList();
        return new RepositoryBranches(ask.Repository, ask.Workspace, line)
        {
            Branches =
            [
                .. branches
                    .OrderBy(each => order.IndexOf(each.Kind))
                    .ThenByDescending(each => each.At ?? DateTimeOffset.MinValue)
                    .ThenBy(each => each.Name, StringComparer.Ordinal),
            ],
            Fetch = ask.Fetch,
            Holds = branches.Any(each => each.Kind is GitBranchKind.Session or GitBranchKind.Landed),
            Missing = read.Missing,
        };

        GitBranch Branch(GitRef each)
        {
            var (ahead, behind) = each.Ahead is not null ? (each.Ahead, each.Behind)
                : each.Name == read.Against ? (0, 0)
                : Counted(answers, $"count:{each.Name}") is { } counted ? (counted.Left, counted.Right)
                : ((int?)null, (int?)null);
            var branch = new GitBranch(each.Short, GitBranchKind.Yours, each.Commit)
            {
                At = each.At, Subject = each.Subject, Worktree = each.Worktree, Ahead = ahead, Behind = behind,
            };

            if (each == local) return branch with { Kind = GitBranchKind.Line };
            if (each.Short.StartsWith("daoris/", StringComparison.Ordinal))
            {
                var record = sessions.GetValueOrDefault(each.Short["daoris/".Length..]);
                var start = grown.GetValueOrDefault(each.Short);
                return branch with
                {
                    Kind = GitBranchKind.Session,
                    Session = new GitSessionBranch
                    {
                        Session = record?.Id, State = record?.State, Quest = record?.Quest, GrewFrom = start?.GrewFrom, From = start?.From,
                    },
                };
            }

            // A recorded name the branch no longer matches is the person's (D102, D113): it holds the recorded commit, or
            // git said it does not. An answer git could not give leaves it the record's, since nothing here acts on it.
            if (landings.TryGetValue(each.Short, out var entry) && MayBeLanded(ask, each, entry)
                && (AtTip(entry, each) || !answers.TryGetValue($"holds:{each.Name}", out var holds) || holds.Code != 1))
            {
                return branch with { Kind = GitBranchKind.Landed, Landed = LandedOf(entry, each) };
            }

            return branch;
        }

        GitLandedBranch LandedOf(LandedBranch entry, GitRef each)
        {
            var copy = refs.GetValueOrDefault(GitRefs.Origin + each.Short);
            var said = new GitLandedBranch(entry.Session, entry.LandedAt, GitOrigin.Unknown)
            {
                Quest = entry.Quest, Title = entry.Title, Plugin = entry.Plugin, Pushed = entry.Pushed, PullRequest = entry.PullRequest,
                PushedTip = entry.PushedTip, OriginCommit = copy?.Commit,
                PullRequestState = entry.PullRequestState, PullRequestAskFailed = entry.PullRequestAskFailed,
            };

            if (copy is null) return said with { Origin = entry.Pushed ? GitOrigin.Gone : GitOrigin.NotPushed };
            if (Same(copy.Commit, each.Commit)) return said with { Origin = GitOrigin.InStep, OriginAhead = 0, OriginBehind = 0 };
            if (Counted(answers, $"origin:{each.Name}") is not { } counted) return said;
            return said with
            {
                OriginAhead = counted.Left,
                OriginBehind = counted.Right,
                Origin = counted is { Left: > 0, Right: > 0 } ? GitOrigin.Diverged
                    : counted.Right > 0 ? GitOrigin.Behind
                    : counted.Left > 0 ? GitOrigin.Ahead
                    : GitOrigin.InStep,
            };
        }
    }

    /// <summary>A recorded landing that could name this branch: not the line, not a session's, and a tip that is a commit id (the record's words become git's arguments).</summary>
    private static bool MayBeLanded(GitRepositoryAsk ask, GitRef each, LandedBranch entry) =>
        each.Short != ask.Line.Branch && !each.Short.StartsWith("daoris/", StringComparison.Ordinal) && WorkingTree.IsCommitId(entry.Tip);

    /// <summary>Whether the branch is still at the commit the landing made it at, or the one a plugin pushed of it.</summary>
    private static bool AtTip(LandedBranch entry, GitRef each) =>
        Same(each.Commit, entry.Tip) || (entry.PushedTip is { } pushed && Same(each.Commit, pushed));

    /// <summary>This repository's standing landings, by branch, the newest landing of a name winning, as the record's own reader has it.</summary>
    private static Dictionary<string, LandedBranch> LandingsOf(GitRepositoryAsk ask) =>
        ask.Landings
            .Where(each => each.GoneAt is null && Same(each.Repository, ask.Repository))
            .GroupBy(each => each.Branch, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

    private static Dictionary<string, GitRef> ByName(IReadOnlyList<GitRef> refs) =>
        refs.GroupBy(each => each.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private static (int Left, int Right)? Counted(IReadOnlyDictionary<string, (int Code, string Stdout)> answers, string key) =>
        answers.TryGetValue(key, out var answer) && answer.Code == 0 ? GitRefs.ParseCounts(answer.Stdout) : null;

    /// <summary>A tree's folder name: a session's branch is <c>daoris/&lt;name&gt;</c> for the tree at <c>…/&lt;name&gt;</c> (D51).</summary>
    private static string TreeName(string tree)
    {
        var path = tree.Replace('\\', '/').TrimEnd('/');
        return path[(path.LastIndexOf('/') + 1)..];
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var newline = trimmed.IndexOf('\n');
        return (newline < 0 ? trimmed : trimmed[..newline]).Trim();
    }
}
