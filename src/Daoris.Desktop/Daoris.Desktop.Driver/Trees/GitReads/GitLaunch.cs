using System.Collections.Concurrent;
using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>Which of <see cref="WorkingTree"/>'s three runners started a git (GITAPP1a-i, D202 point 2).</summary>
public enum GitRunner
{
    /// <summary>Read to its end, both streams whole: <c>RunGitAsync</c>, beneath every <c>GitAsync</c>.</summary>
    Whole,

    /// <summary>Read as it answers, stopped once its reader has what it needs: <c>ReadGitAsync</c>, the review's seam (REVIEW3).</summary>
    Streamed,

    /// <summary>Its first bytes, as bytes: <c>GitBytesAsync</c>, a blob's preview (REVIEW2).</summary>
    Bytes,
}

/// <summary>
/// What a git start reads, by its command (the git-app design §1.1's kinds, GITAPP1a-i): what part two's effect policy will decide a
/// read's flags by. In part one the kind is counted and never changes an argument (Codex R3-9).
/// </summary>
public enum GitKind
{
    /// <summary>Names and layout: <c>rev-parse</c>, <c>symbolic-ref</c>, <c>show-ref</c>, <c>for-each-ref</c>, <c>config</c>, <c>var</c>, <c>worktree list</c>.</summary>
    Layout,

    /// <summary>The commit graph without a patch: <c>merge-base</c>, <c>rev-list</c>, <c>cherry</c>, a <c>log</c> with no <c>-p</c>.</summary>
    Walk,

    /// <summary>The working tree's state: <c>status</c>.</summary>
    Status,

    /// <summary>A diff against the working tree (<c>diff HEAD</c>): the tree's clean filters and textconv run.</summary>
    WorktreeDiff,

    /// <summary>A diff of the index (<c>diff --cached</c>).</summary>
    IndexDiff,

    /// <summary>A diff of two objects (<c>a..b</c>, or two ids), a <c>show</c> or a <c>log -p</c>: textconv runs.</summary>
    ObjectDiff,

    /// <summary>An untracked file as an addition (<c>diff --no-index</c>): the clean filter and textconv run.</summary>
    UntrackedDiff,

    /// <summary><c>merge-tree --write-tree</c>: a configured merge driver runs (REVIEWBASE3).</summary>
    MergeTree,

    /// <summary><c>blame</c>.</summary>
    Blame,

    /// <summary>An object by its id: <c>cat-file</c>, <c>ls-tree</c>.</summary>
    Object,

    /// <summary>Anything else: a write, a fetch, a merge, a command the census does not class.</summary>
    Other,
}

/// <summary>
/// Whose a git start is (D202 point 2, the git-app design P1.5): the review, its untracked tail, the landed or gone review, the
/// Accept offer, or none. Set for a span of work by <see cref="GitLaunch.Within"/>; a start outside every span is <see cref="None"/>.
/// </summary>
public enum GitBudget
{
    /// <summary>A start no review's read made.</summary>
    None,

    /// <summary>Review's own chain: the uncommitted diff, the range and its line, the committed diff, the branch.</summary>
    Review,

    /// <summary>The untracked files' reads, one each, while the patch budget is unspent (REVIEWDIRTY1-a).</summary>
    Untracked,

    /// <summary>A landed or gone session's review: <c>LandedReviews</c>' and <c>LandedProofs</c>' reads (REVIEW2).</summary>
    Landed,

    /// <summary>The Accept offer's chain (ACCEPTFOOT1): <c>TreeWorkReads</c>, <c>TreeFollows</c> and the content holds.</summary>
    Offer,
}

/// <summary>
/// One git start, as the seam saw it (GITAPP1a-i): its runner, kind, node and budget, its arguments as they reached git, and the
/// three parts of its time apart (the git-app design §1.2): the gate's wait for a place, the creation inside it, and the rest.
/// </summary>
/// <remarks>The times are filled as the start goes, so a record read before its runner returns says only what has happened.</remarks>
internal sealed class GitLaunched(GitRunner runner, GitKind kind, string node, GitBudget budget, IReadOnlyList<string> arguments)
{
    private long _waited = -1;
    private long _created = -1;
    private long _whole = -1;

    public GitRunner Runner { get; } = runner;

    public GitKind Kind { get; } = kind;

    /// <summary>The review's node (the design's P1.2: <c>T</c>, <c>P</c>, <c>K</c>, <c>E</c>, <c>G1</c>-<c>G3</c>, <c>B1</c>, <c>B2</c>,
    /// <c>N1</c>, <c>N2</c>, <c>I</c>, <c>M</c>, <c>D</c>, <c>W</c>, <c>S</c>, <c>U</c>, <c>H</c>) where the start is the review's, else its subcommand.</summary>
    public string Node { get; } = node;

    public GitBudget Budget { get; } = budget;

    public IReadOnlyList<string> Arguments { get; } = arguments;

    /// <summary>Whether it passed the process gate: false for a test's stand-in answer, and for a git that could not be resolved.</summary>
    public bool Gated => Volatile.Read(ref _created) >= 0;

    /// <summary>How long it waited for a place at the gate, in ticks of <see cref="Stopwatch"/>; 0 where it did not pass it.</summary>
    public long WaitedTicks => Math.Max(0, Volatile.Read(ref _waited));

    /// <summary>How long <c>Process.Start</c> took inside its place.</summary>
    public long CreatedTicks => Math.Max(0, Volatile.Read(ref _created));

    /// <summary>The whole start, from the seam to its runner's return.</summary>
    public long WholeTicks => Math.Max(0, Volatile.Read(ref _whole));

    /// <summary>The run: the whole less the wait and the creation.</summary>
    public long RanTicks => Math.Max(0, WholeTicks - WaitedTicks - CreatedTicks);

    internal void Gate(long waited, long created)
    {
        Volatile.Write(ref _waited, waited);
        Volatile.Write(ref _created, created);
    }

    internal void End(long whole) => Volatile.Write(ref _whole, whole);

    /// <summary>Ticks as milliseconds.</summary>
    public static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}

/// <summary>
/// The one counting seam beneath every git the driver starts (GITAPP1a-i, D202 point 2, the git-app design P1.5): <c>RunGitAsync</c>,
/// <c>ReadGitAsync</c> and <c>GitBytesAsync</c> each start through <see cref="LaunchAsync"/>, which raises
/// <see cref="WorkingTree.Started"/> for all three, classifies the start by kind and node, counts it in the budget its flow is in, and
/// times the gate's wait, the creation and the run apart.
/// </summary>
/// <remarks>
/// <para>🔴 <b>It adds no flag and changes no answer</b> (Codex R3-9). The arguments go on to <c>WorkingTree.GitStart</c> and the gate
/// (<see cref="ProcessStarts"/>, <see cref="StartClass.Background"/>) exactly as the call site passed them; what a read kind may add is
/// part two's effect policy. <c>GitLaunchTests</c> holds the arguments unchanged per kind, and a source census refuses a git started
/// under <c>Trees/</c> outside this seam.</para>
///
/// <para><b>Flow-scoped, like the seams beside it.</b> The budget (<see cref="Within"/>) and the count (<see cref="Count"/>) are async
/// locals: work begun inside a span carries it, and classes running side by side never see each other's starts.</para>
/// </remarks>
internal static class GitLaunch
{
    private static readonly AsyncLocal<GitBudget> s_budget = new();
    private static readonly AsyncLocal<GitCount?> s_count = new();

    /// <summary>The budget this flow's starts are counted in now.</summary>
    internal static GitBudget Budget => s_budget.Value;

    /// <summary>
    /// Count this flow's starts in <paramref name="budget"/> until disposed, the budget before restored then. Set inside the caller's
    /// own flow, so it reaches what the caller awaits and starts, never the caller's caller.
    /// </summary>
    internal static IDisposable Within(GitBudget budget)
    {
        var before = s_budget.Value;
        s_budget.Value = budget;
        return new Restore(() => s_budget.Value = before);
    }

    /// <summary>
    /// Keep every start this flow makes from here until disposed (the terminal door's <c>--time</c>, the route's <c>review.read</c>,
    /// a test's count). A count begun inside another's keeps its starts too, so an outer count still sees them all.
    /// </summary>
    internal static GitCount Count()
    {
        var count = new GitCount(s_count.Value);
        s_count.Value = count;
        return count;
    }

    internal static void Ended(GitCount count, GitCount? outer)
    {
        if (ReferenceEquals(s_count.Value, count)) s_count.Value = outer;
    }

    /// <summary>
    /// The seam (P1.5): one start of git by <paramref name="runner"/>, of <paramref name="kind"/>, in <paramref name="budget"/>,
    /// <paramref name="work"/> being the runner's own start and run. Raises <see cref="WorkingTree.Started"/> with the arguments, as
    /// <c>RunGitAsync</c> alone once did, then runs the work with the gate's timing kept on this start.
    /// </summary>
    internal static async Task<T> LaunchAsync<T>(
        GitKind kind, IReadOnlyList<string> arguments, GitBudget budget, GitRunner runner, Func<Task<T>> work)
    {
        var launched = new GitLaunched(runner, kind, NodeOf(arguments, budget), budget, arguments);
        s_count.Value?.Add(launched);
        WorkingTree.Started.Value?.Invoke(arguments);
        var began = Stopwatch.GetTimestamp();
        // The gate writes its wait and its creation here (ProcessStarts.Timed), in this start's own flow alone.
        using (ProcessStarts.Timing((waited, created) => launched.Gate(waited, created)))
        {
            try
            {
                return await work().ConfigureAwait(false);
            }
            finally
            {
                launched.End(Stopwatch.GetTimestamp() - began);
            }
        }
    }

    /// <summary>The arguments past git's own options (<c>-c name=value</c>, <c>-C dir</c>, <c>--no-optional-locks</c> and the like): the subcommand first.</summary>
    internal static IReadOnlyList<string> Command(IReadOnlyList<string> arguments)
    {
        var at = 0;
        while (at < arguments.Count)
        {
            var word = arguments[at];
            if (word is "-c" or "-C") at += 2;
            else if (word.StartsWith('-')) at++;
            else break;
        }

        return at == 0 ? arguments : [.. arguments.Skip(at)];
    }

    /// <summary>The read kind of a start by its command (§1.1's table).</summary>
    internal static GitKind KindOf(IReadOnlyList<string> arguments)
    {
        var command = Command(arguments);
        if (command.Count == 0) return GitKind.Other;
        var rest = command.Skip(1).ToArray();
        return command[0] switch
        {
            "rev-parse" or "symbolic-ref" or "show-ref" or "for-each-ref" or "config" or "var" or "ls-remote" => GitKind.Layout,
            "worktree" when rest is ["list", ..] => GitKind.Layout,
            "merge-base" or "rev-list" or "cherry" or "name-rev" or "describe" => GitKind.Walk,
            "log" => rest.Any(word => word is "-p" or "--patch" or "-u") ? GitKind.ObjectDiff : GitKind.Walk,
            "status" => GitKind.Status,
            "show" => GitKind.ObjectDiff,
            "diff" => DiffKind(rest),
            "merge-tree" => GitKind.MergeTree,
            "blame" => GitKind.Blame,
            "cat-file" or "ls-tree" => GitKind.Object,
            _ => GitKind.Other,
        };
    }

    private static GitKind DiffKind(string[] rest)
    {
        if (rest.Contains("--no-index")) return GitKind.UntrackedDiff;
        if (rest.Contains("--cached") || rest.Contains("--staged")) return GitKind.IndexDiff;
        var paths = Array.IndexOf(rest, "--");
        var revisions = (paths < 0 ? rest : rest[..paths]).Where(word => !word.StartsWith('-')).ToArray();
        return revisions.Any(word => word.Contains("..", StringComparison.Ordinal)) || revisions.Length >= 2
            ? GitKind.ObjectDiff
            : GitKind.WorktreeDiff;
    }

    /// <summary>
    /// The start's node (the design's P1.2) where it is the review's, by today's command for it; else its subcommand. Only the
    /// review's budgets are read as nodes, since the offer and the landed review ask some of the same commands as their own.
    /// </summary>
    internal static string NodeOf(IReadOnlyList<string> arguments, GitBudget budget)
    {
        var command = Command(arguments);
        if (command.Count == 0) return "git";
        if (budget is not (GitBudget.Review or GitBudget.Untracked)) return command[0];
        return command switch
        {
            ["rev-parse", "--show-toplevel"] => "T",
            ["rev-parse", "--path-format=absolute", "--git-common-dir"] => "P",
            ["rev-parse", "--abbrev-ref", "HEAD"] => "E",
            ["merge-base", "--is-ancestor", ..] => "K",
            ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"] => "G1",
            ["rev-parse", "--verify", "--quiet", "refs/heads/main^{commit}"] => "G2",
            ["rev-parse", "--verify", "--quiet", "refs/heads/master^{commit}"] => "G3",
            ["merge-base", "--independent", ..] => "I",
            ["merge-base", "HEAD", var reference] when reference.StartsWith("refs/heads/", StringComparison.Ordinal) => "B1",
            ["merge-base", "HEAD", var reference] when reference.StartsWith("refs/remotes/origin/", StringComparison.Ordinal) => "B2",
            ["rev-list", "--count", "HEAD", "--not", ..] => "N2",
            ["rev-list", "--count", ..] => "N1",
            ["merge-tree", "--write-tree", ..] => "M",
            ["status", ..] => "S",
            ["symbolic-ref", "--short", "-q", "HEAD"] => "H",
            ["diff", ..] => KindOf(arguments) switch
            {
                GitKind.UntrackedDiff => "U",
                GitKind.WorktreeDiff => "W",
                _ => "D",
            },
            _ => command[0],
        };
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

/// <summary>The starts a flow made while a <see cref="GitLaunch.Count"/> was open: thread-safe, as a flow's reads may run side by side.</summary>
internal sealed class GitCount : IDisposable
{
    private readonly GitCount? _outer;
    private readonly ConcurrentQueue<GitLaunched> _starts = new();
    private readonly long _began = Stopwatch.GetTimestamp();

    internal GitCount(GitCount? outer) => _outer = outer;

    /// <summary>Every start so far, in the order the seam saw them.</summary>
    public IReadOnlyList<GitLaunched> Starts => [.. _starts];

    /// <summary>How long since the count began.</summary>
    public long ElapsedTicks => Stopwatch.GetTimestamp() - _began;

    internal void Add(GitLaunched launched)
    {
        _starts.Enqueue(launched);
        _outer?.Add(launched);
    }

    /// <summary>The starts in <paramref name="budget"/>.</summary>
    public int In(GitBudget budget) => _starts.Count(start => start.Budget == budget);

    /// <summary>The count ended: the flow's count is the one it was inside again. Disposed in the flow that opened it, as a <c>using</c> is.</summary>
    public void Dispose() => GitLaunch.Ended(this, _outer);
}
