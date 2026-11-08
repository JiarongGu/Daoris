using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The questions the driver asks a working tree: is it clean enough to spawn into, what landed while
/// a session ran, and where in the repository's history this checkout stands. Asked
/// of git itself, because git's answer is the one that matters.
/// </summary>
/// <remarks>
/// <para>A session spawns only onto a clean tree: uncommitted changes are somebody's work in
/// flight — the lesson `reaching-in` comes from — and a tree that is not a git repository at
/// all cannot make that promise, so it refuses too.</para>
///
/// <para>The provenance half exists because two checkouts of one repository are two points in its
/// history, and a deployment fed by both must decide which one speaks. Only this side can answer it:
/// the deployment has no checkout and cannot ask git anything.</para>
/// </remarks>
public static class WorkingTree
{
    /// <summary>
    /// Where this checkout stands — or null when git cannot say, which is a tree that feeds nothing.
    /// </summary>
    public static async Task<TreeProvenance?> ProvenanceAsync(string root, CancellationToken ct = default)
    {
        // One call for both fields, so the commit and its time can never come from different commits —
        // a session landing work between two calls would otherwise produce a provenance describing
        // neither. %H is the SHA and %cI the committer date; %n separates them, because neither field
        // can contain a newline (unlike a commit message, which is deliberately not asked for here).
        var (code, stdout, _) = await GitAsync(root, ["log", "-1", "--format=%H%n%cI"], ct).ConfigureAwait(false);
        if (code != 0) return null;

        var lines = stdout.Trim().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != 2
            || Moments.Read(lines[1]) is not { } committedAt)
        {
            return null;
        }

        var (branchCode, branchOut, _) = await GitAsync(root, ["rev-parse", "--abbrev-ref", "HEAD"], ct)
            .ConfigureAwait(false);
        var branch = branchCode == 0 ? branchOut.Trim() : "";

        return new(
            lines[0],
            committedAt,
            // A detached HEAD is on no line at all. Named rather than blank, because the deployment's
            // refusal quotes it back — and "fed from ``" is a sentence nobody can act on.
            branch is "" or "HEAD" ? "(detached)" : branch);
    }

    /// <summary>
    /// The branch this checkout stands on, by its short name: what the composer's tree chip says. Null where HEAD is
    /// detached, which is no branch at all; a branch with no commit yet answers its name. A git that fails is thrown, because a
    /// tree git cannot read has no head to name, and "detached" would be a claim about it.
    /// </summary>
    public static async Task<string?> BranchAsync(string root, CancellationToken ct = default)
    {
        // `-q` makes a detached HEAD exit 1 with nothing said, which is told apart from a real failure by its silence.
        var (code, stdout, stderr) = await GitAsync(root, BranchAsk, ct).ConfigureAwait(false);
        return ReviewChoices.Branch(code, stdout, stderr);
    }

    /// <summary><see cref="BranchAsync"/>'s question: H, in Review's graph.</summary>
    internal static readonly string[] BranchAsk = ["symbolic-ref", "--short", "-q", "HEAD"];

    /// <summary><see cref="DefaultBranchAsync"/>'s first question, origin's <c>HEAD</c>: G1, in Review's graph.</summary>
    internal static readonly string[] OriginHeadAsk = ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"];

    /// <summary>
    /// Which line this repository treats as canonical, as far as this checkout knows.
    /// </summary>
    /// <remarks>
    /// <para>Asked of the remote's own HEAD first, which is what a clone records and therefore the
    /// repository's own answer rather than this machine's guess. A checkout made with
    /// `git remote add` has no such ref, so the conventional names are tried in turn.</para>
    ///
    /// <para>Null when none of them exists — and null is PERMISSIVE at the deployment: a repository
    /// that never said which line is canonical has not asked for its branches to be judged, and
    /// refusing every feed from it would silence a real repository over a missing ref.</para>
    /// </remarks>
    public static async Task<string?> DefaultBranchAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout) = OriginHeadFile(root) ?? await OriginHeadGitAsync(root, ct).ConfigureAwait(false);
        if (ReviewChoices.Guess(code, stdout, null, null) is { } pointed) return pointed;

        // The order is ReviewChoices.Guess's, which Review's graph asks all three together and chooses by.
        int? main = null, master = null;
        foreach (var candidate in ReviewChoices.Conventional)
        {
            // Spelled as every read of a branch's commit is, so one ref is one command wherever it is asked.
            var (exists, _, _) = await GitAsync(root, ReviewChoices.ConventionalAsk(candidate), ct).ConfigureAwait(false);
            if (candidate == "main") main = exists;
            else master = exists;
            if (ReviewChoices.Guess(code, stdout, main, master) is { } guessed) return guessed;
        }

        return null;
    }

    private static async Task<(int Code, string Stdout)> OriginHeadGitAsync(string root, CancellationToken ct)
    {
        var (code, stdout, _) = await GitAsync(root, OriginHeadAsk, ct).ConfigureAwait(false);
        return (code, stdout);
    }

    /// <summary>
    /// <c>symbolic-ref --short refs/remotes/origin/HEAD</c> as git's own files answer it, no process: <c>origin/main</c> where the ref is a
    /// symbolic one, git's failure where the repository holds no such ref; null where the files cannot say (a reftable repository, a ref
    /// that is no symbolic one), and git is asked.
    /// </summary>
    private static (int Code, string Stdout)? OriginHeadFile(string root)
    {
        const string Remotes = "refs/remotes/";
        const string Symbolic = "ref: " + Remotes;
        if (RefFiles.CommonDirOf(root) is not { } common) return null;
        try
        {
            var file = Path.Combine(common, "refs", "remotes", "origin", "HEAD");
            if (File.Exists(file))
            {
                var text = File.ReadAllText(file).Trim();
                return text.StartsWith(Symbolic, StringComparison.Ordinal) ? (0, text[Symbolic.Length..] + "\n") : null;
            }

            return RefFiles.Absent(common, "refs/remotes/origin/HEAD") ? (128, "") : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// How <paramref name="head"/> stands to <paramref name="held"/> — the question a deployment
    /// cannot ask, because it has no checkout.
    /// </summary>
    /// <remarks>
    /// Whether the commit is here is asked first and on its own: `merge-base` fails the same way for a
    /// commit it does not have as for anything else, and an unknown commit reported as diverged would
    /// let commit time replace a history this machine has never seen.
    /// </remarks>
    public static async Task<TreeRelation> RelationAsync(
        string root, string held, string head, CancellationToken ct = default)
    {
        // The held commit is the REMOTE's answer and becomes a git argument, so it must be a commit id
        // and nothing else — a value starting with `-` would be read as an option.
        if (!IsCommitId(held) || !IsCommitId(head)) return TreeRelation.Unknown;

        var (known, _, _) = await GitAsync(root, ["cat-file", "-e", $"{held}^{{commit}}"], ct).ConfigureAwait(false);
        if (known != 0) return TreeRelation.Unknown;

        // `--is-ancestor` answers 0 for yes and 1 for no; anything else is git failing to answer.
        var (descends, _, _) = await GitAsync(root, ["merge-base", "--is-ancestor", held, head], ct).ConfigureAwait(false);
        if (descends == 0) return TreeRelation.Descends;
        if (descends != 1) return TreeRelation.Unknown;

        var (behind, _, _) = await GitAsync(root, ["merge-base", "--is-ancestor", head, held], ct).ConfigureAwait(false);
        return behind switch
        {
            0 => TreeRelation.Behind,
            1 => TreeRelation.Diverged,
            _ => TreeRelation.Unknown,
        };
    }

    /// <summary>A full or abbreviated commit id: hex, and long enough to mean one commit.</summary>
    internal static bool IsCommitId(string text) =>
        text.Length is >= 7 and <= 64 && text.All(char.IsAsciiHexDigit);

    /// <summary>Clean means: a git repository, with nothing uncommitted.</summary>
    public static async Task<(bool Clean, string Detail)> CleanAsync(string root, CancellationToken ct = default)
    {
        var read = await ReadCleanAsync(root, ct).ConfigureAwait(false);
        return (read.Clean, read.Detail);
    }

    /// <summary><see cref="CleanAsync"/>'s answer with the fingerprint of the status git listed, null where clean or unread.</summary>
    internal readonly record struct CleanRead(bool Clean, string Detail, string? Fingerprint);

    /// <summary><see cref="CleanAsync"/> with the status's fingerprint.</summary>
    internal static async Task<CleanRead> ReadCleanAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, stderr) = await GitAsync(root, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        if (code != 0)
        {
            return new(false, $"not a spawnable tree — git status failed: {FirstLine(stderr)}", null);
        }

        return string.IsNullOrWhiteSpace(stdout)
            ? new(true, "clean", null)
            : new(false, $"the working tree has uncommitted changes ({stdout.Trim().Split('\n').Length} paths) — "
                         + "somebody's work in flight; the driver holds rather than entangling a session with it.", LandTries.Fingerprint(stdout));
    }

    /// <summary>
    /// What a tree holds uncommitted, as git's own short lines (<c>M path</c>, <c>?? path</c>), at most
    /// <paramref name="limit"/> of them with a last line counting the rest. Empty when the tree is clean
    /// or git cannot say. What a session carrying on after a cut-off is handed: it may not run
    /// <c>git status</c> itself, and a timeout lands mid-change.
    /// </summary>
    public static async Task<IReadOnlyList<string>> UncommittedAsync(
        string root, int limit = 60, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(root, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        if (code != 0) return [];

        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Trim().Length > 0)
            .ToList();
        return lines.Count <= limit
            ? lines
            : [.. lines.Take(limit), $"… and {lines.Count - limit} more"];
    }

    /// <summary>
    /// Whether one file is exactly as HEAD has it — tracked, and nothing staged, changed or untracked
    /// about it. What a registration asks of the manifest before it names the commit.
    /// </summary>
    public static async Task<bool> UnmodifiedAsync(string root, string path, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(root, ["--no-optional-locks", "status", "--porcelain", "--", path], ct).ConfigureAwait(false);
        return code == 0 && string.IsNullOrWhiteSpace(stdout);
    }

    /// <summary>Where HEAD is now — the "before" a session's evidence is measured from.</summary>
    public static async Task<string?> HeadAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(root, ["rev-parse", "HEAD"], ct).ConfigureAwait(false);
        return code == 0 ? stdout.Trim() : null;
    }

    /// <summary>
    /// The commit <paramref name="name"/> points at in <paramref name="root"/>, by its full id; null where it names no commit there
    /// (a press's <c>branch</c> ground, read at its ask and again at its Confirm).
    /// </summary>
    public static async Task<string?> TipAsync(string root, string name, CancellationToken ct = default)
    {
        // A name git would read as an option names no commit here.
        if (name.Length == 0 || name.StartsWith('-')) return null;
        var (code, stdout, _) = await GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{name}^{{commit}}"], ct).ConfigureAwait(false);
        return code == 0 && stdout.Trim() is { Length: > 0 } sha ? sha : null;
    }

    /// <summary>
    /// Every branch tip in <paramref name="root"/>, read by one <c>git for-each-ref</c> (a plan's branches bound by their full
    /// commits, not their count): each local branch by its name, each remote's as
    /// <c>&lt;remote&gt;/&lt;name&gt;</c>, each by its full commit id. Null where git cannot say.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>?> TipsAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(
            root, ["for-each-ref", "--format=%(objectname) %(refname)", "refs/heads", "refs/remotes"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var at = line.IndexOf(' ', StringComparison.Ordinal);
            if (at <= 0) continue;
            var name = line[(at + 1)..].TrimEnd('\r');
            var shortName = name.StartsWith("refs/heads/", StringComparison.Ordinal) ? name["refs/heads/".Length..]
                : name.StartsWith("refs/remotes/", StringComparison.Ordinal) ? name["refs/remotes/".Length..]
                : null;
            if (shortName is { Length: > 0 } && !shortName.EndsWith("/HEAD", StringComparison.Ordinal)) tips[shortName] = line[..at];
        }

        return tips;
    }

    /// <summary>
    /// What <paramref name="root"/>'s index holds staged against its head, by each entry's modes and full blob ids
    /// (a discard binds its index, whose staged bytes can change while the working file stays the same). Empty where nothing is
    /// staged; null where git cannot say.
    /// </summary>
    public static async Task<string?> StagedAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(
            root, ["--no-optional-locks", "diff", "--cached", "--raw", "--no-abbrev", "-z", "--no-renames"], ct).ConfigureAwait(false);
        return code == 0 ? stdout : null;
    }

    /// <summary>
    /// The paths <paramref name="root"/> holds uncommitted, untracked files each by name, relative to it with <c>/</c>; null where git
    /// cannot say (a forced discard's ground, what it would lose).
    /// </summary>
    public static async Task<IReadOnlyList<string>?> UncommittedAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(
            root, ["--no-optional-locks", "status", "--porcelain=v1", "-z", "--untracked-files=all"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var paths = new List<string>();
        var entries = stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var at = 0; at < entries.Length; at++)
        {
            if (entries[at].Length < 4) continue;
            paths.Add(entries[at][3..]);
            // A rename or a copy names its source next, which is no path of the tree's own.
            if (entries[at][0] is 'R' or 'C') at++;
        }

        return paths;
    }

    /// <summary>What landed since — the reviewable half of the record.</summary>
    public static async Task<string> CommitsSinceAsync(string root, string? before, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(root, ["log", "--oneline", Since(before)], ct).ConfigureAwait(false);
        return Landed(code, stdout);
    }

    /// <inheritdoc cref="CommitsSinceAsync(string, string?, CancellationToken)"/>
    /// <param name="git">How git is read: the review's seam, through which the sweep reads a lost session's.</param>
    internal static async Task<string> CommitsSinceAsync(string root, string? before, GitRead git, CancellationToken ct)
    {
        var stdout = new StringBuilder();
        var code = await git(root, ["log", "--oneline", Since(before)], piece =>
        {
            stdout.Append(piece.Span);
            return true;
        }, ct).ConfigureAwait(false);
        return Landed(code, stdout.ToString());
    }

    private static string Since(string? before) => before is null ? "HEAD" : $"{before}..HEAD";

    private static string Landed(int code, string stdout)
    {
        if (code != 0) return "no commits readable";

        var commits = stdout.Trim();
        return commits.Length == 0 ? "no commits landed" : $"commits landed:\n{commits}";
    }

    public const int PatchBudget = TreeDiffs.PatchBudget;
    public const int PatchCap = TreeDiffs.PatchCap;
    public const int UntrackedPatchLimit = TreeDiffs.UntrackedPatchLimit;
    internal const string UncommittedRest = TreeDiffs.UncommittedRest;
    internal const string CommittedRest = TreeDiffs.CommittedRest;
    internal static readonly string[] UncommittedStatus = TreeDiffs.UncommittedStatus;

    public static Task<TreeDiff?> DiffAsync(string root, string before, CancellationToken ct = default) => TreeDiffs.DiffAsync(root, before, ct);

    internal static Task<TreeDiff?> DiffAsync(string root, string before, GitRead git, CancellationToken ct) => TreeDiffs.DiffAsync(root, before, git, ct);

    public static Task<TreeDiff?> UncommittedDiffAsync(string root, CancellationToken ct = default) => TreeDiffs.UncommittedDiffAsync(root, ct);

    internal static Task<TreeDiff?> UncommittedDiffAsync(string root, GitRead git, CancellationToken ct) => TreeDiffs.UncommittedDiffAsync(root, git, ct);

    internal static Task<(string? Patch, int? Added)> UntrackedReadAsync(string root, string path, GitRead git, CancellationToken ct) =>
        TreeDiffs.UntrackedReadAsync(root, path, git, ct);

    public static Task<TreeDiff?> DiffBetweenAsync(string root, string from, string to, CancellationToken ct = default) => TreeDiffs.DiffBetweenAsync(root, from, to, ct);

    internal static Task<TreeDiff?> DiffBetweenAsync(string root, string from, string to, GitRead git, CancellationToken ct) =>
        TreeDiffs.DiffBetweenAsync(root, from, to, git, ct);

    internal static string BetweenRest(string from, string to) => TreeDiffs.BetweenRest(from, to);

    internal static Task<TreeDiff?> RangeAsync(string root, string before, string range, string rest, GitRead git, CancellationToken ct) =>
        TreeDiffs.RangeAsync(root, before, range, rest, git, ct);

    /// <summary>The files a tree holds, for a person to `@` one.</summary>
    /// <param name="Files">Tree-relative, with forward slashes, in ordinal order.</param>
    /// <param name="Unlisted">How many more the bound left out — counted, so the page can say so.</param>
    public sealed record TreeFiles(IReadOnlyList<string> Files, int Unlisted);

    /// <summary>How many paths a completion list is handed before the rest are only counted.</summary>
    /// <remarks>
    /// The console's rule again: a tree has no upper size, so the bound is stated. A path
    /// the list leaves out can still be typed, and both doors expand it as typed.
    /// </remarks>
    public const int FileLimit = 20_000;

    /// <summary>
    /// What a person may mention in this tree: every file git tracks that is still there, and every new
    /// one it does not ignore — or null when git cannot say.
    /// </summary>
    /// <remarks>
    /// <para><b>Untracked files are in.</b> A file the agent wrote a minute ago is as mentionable as one
    /// from last year, and the harness expands either. What git ignores is out, which is what keeps a
    /// dependency folder from burying the tree's own files.</para>
    ///
    /// <para><b>A tracked file deleted from the tree is out</b>: it is still in the index, and no
    /// harness can expand a file that is not there.</para>
    ///
    /// <para>`-z` rather than lines, because git quotes a name outside ASCII as octal escapes
    /// otherwise, and a completion would then offer a file nobody has.</para>
    /// </remarks>
    public static async Task<TreeFiles?> FilesAsync(string root, CancellationToken ct = default) =>
        await FilesAsync(root, FileLimit, ct).ConfigureAwait(false);

    /// <inheritdoc cref="FilesAsync(string, CancellationToken)"/>
    public static async Task<TreeFiles?> FilesAsync(string root, int limit, CancellationToken ct = default)
    {
        if (!await IsTopLevelAsync(root, ct).ConfigureAwait(false)) return null;

        var (code, stdout, _) = await GitAsync(
            root, ["ls-files", "-z", "--cached", "--others", "--exclude-standard"], ct).ConfigureAwait(false);
        if (code != 0) return null;

        var (deletedCode, deletedOut, _) = await GitAsync(root, ["ls-files", "-z", "--deleted"], ct).ConfigureAwait(false);
        var deleted = deletedCode == 0
            ? new HashSet<string>(deletedOut.Split('\0', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal)
            : [];

        // A conflicted file is in the index once per stage, so the set is taken before the order.
        var files = stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => !deleted.Contains(path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return files.Count <= limit
            ? new TreeFiles(files, 0)
            : new TreeFiles(files[..limit], files.Count - limit);
    }

    /// <summary>
    /// 🔴 Whether <paramref name="root"/> is the top of a repository of its own, which is what every
    /// question about a session's tree has to confirm before git's answer means anything.
    /// </summary>
    /// <remarks>
    /// git WALKS UP. Run it in a directory that is not a repository and it answers for whatever
    /// repository encloses it — so a session tree that has since been deleted would quietly return the
    /// diff of the parent checkout, presented as that session's work. Found by a test that pointed at
    /// an empty directory inside this repository and got a clean exit code back. Requiring the top level
    /// to BE this path also rejects a subdirectory, which the driver never passes and which would
    /// silently narrow the answer if it ever did.
    /// </remarks>
    internal static Task<bool> IsTopLevelAsync(string root, CancellationToken ct) => IsTopLevelAsync(root, WholeAsync, ct);

    /// <inheritdoc cref="IsTopLevelAsync(string, CancellationToken)"/>
    /// <param name="git">How git is read: the review's seam, so its tests count this start with the range's, and
    /// evidence's.</param>
    internal static async Task<bool> IsTopLevelAsync(string root, GitRead git, CancellationToken ct)
    {
        // A folder that is not there: git would refuse to start in it, and nothing here is asked of the one above.
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;
        var stdout = new StringBuilder();
        var code = await git(
            root,
            ["rev-parse", "--show-toplevel"],
            piece =>
            {
                stdout.Append(piece.Span);
                return true;
            },
            ct).ConfigureAwait(false);
        return code == 0 && SamePath(stdout.ToString().Trim(), root);
    }

    /// <summary>git's whole answer handed on at once: how every caller but the review asks, through <see cref="GitAsync(string, IReadOnlyList{string}, CancellationToken)"/> as before.</summary>
    private static async Task<int> WholeAsync(
        string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        var (code, stdout, _) = await GitAsync(root, arguments, ct).ConfigureAwait(false);
        take(stdout.AsMemory());
        return code;
    }

    /// <summary>
    /// Whether two paths name the same directory. git answers in forward slashes even on Windows,
    /// and a trailing separator or a differently-cased drive letter is the same place.
    /// </summary>
    private static bool SamePath(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0) return false;

        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            // An unrooted or malformed path is not the one we are standing in.
            return false;
        }
    }

    internal static (ProcessStartInfo? Info, string? Refusal) GitStart(string root, IReadOnlyList<string> arguments, string? home) => GitRuns.GitStart(root, arguments, home);

    internal static string? GitInUse(string? home) => GitRuns.GitInUse(home);

    internal static AsyncLocal<Action<IReadOnlyList<string>>?> Started => GitRuns.Started;

    internal static AsyncLocal<Func<IReadOnlyList<string>, (int Code, string Stdout, string Stderr)?>?> Answered => GitRuns.Answered;

    internal static AsyncLocal<Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ChildRun>>?> Runs => GitRuns.Runs;

    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(string root, IReadOnlyList<string> arguments, CancellationToken ct) =>
        GitRuns.GitAsync(root, arguments, ct);

    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout, CancellationToken ct) =>
        GitRuns.GitAsync(root, arguments, environment, timeout, ct);

    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(string root, IReadOnlyList<string> arguments, string input, CancellationToken ct) =>
        GitRuns.GitAsync(root, arguments, input, ct);

    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string input, CancellationToken ct) =>
        GitRuns.GitAsync(root, arguments, environment, input, ct);

    internal static Task<(byte[] Bytes, int Count)?> GitBytesAsync(string root, IReadOnlyList<string> arguments, int limit, CancellationToken ct) =>
        GitRuns.GitBytesAsync(root, arguments, limit, ct);

    internal static Task<int> ReadGitAsync(string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct) =>
        GitRuns.ReadGitAsync(root, arguments, take, ct);

    /// <summary>
    /// How a review reads git: <c>take</c> is handed git's answer as it comes and answers false once it has what
    /// it needs, and git is stopped there. The answer is git's exit code, 0 where the reader stopped it, -1 where git did
    /// not start or could not be read.
    /// </summary>
    /// <remarks>The seam a review's tests count its git processes through; production passes <see cref="ReadGitAsync"/>.</remarks>
    internal delegate Task<int> GitRead(
        string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct);

    private static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var newline = trimmed.IndexOf('\n');
        return newline < 0 ? trimmed : trimmed[..newline].Trim();
    }
}
