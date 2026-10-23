using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// Today's choices of Review's read, lifted out of its chain into pure functions (GITAPP1a-ii, the git-app design P1.1): what each
/// git answer means, and which answer is taken where several were asked. Today's chain (<see cref="ReviewRanges"/>,
/// <see cref="WorkingTree"/>) and the graph (<see cref="ReviewGraphReads"/>) both call them, so the two cannot drift.
/// </summary>
internal static class ReviewChoices
{
    /// <summary>Whether a word may be a git argument as a commit id: hex digits and nothing else.</summary>
    internal static bool Id(string? word) => word is { Length: > 0 } && word.All(char.IsAsciiHexDigit);

    /// <summary>
    /// The record's base where the tree's history holds it; else the branch's grown base where that is; else null
    /// (<c>RecordedBaseAsync</c>'s order). Each held flag is null where it was not asked.
    /// </summary>
    internal static string? RecordedBase(string recorded, bool? recordHeld, string? grown, bool? grownHeld) =>
        Id(recorded) && recordHeld == true ? recorded
        : grown is not null && grownHeld == true ? grown
        : null;

    /// <summary>The grown base the branch's record names for the branch <c>rev-parse --abbrev-ref HEAD</c> answered, where it is an id.</summary>
    internal static string? Grown(SessionBranches grown, string repository, string abbrevRef) =>
        grown.Of(repository, abbrevRef.Trim()) is { } branch && Id(branch.From) ? branch.From : null;

    /// <summary>A line's candidate from <c>merge-base HEAD &lt;ref&gt;</c>: its id, or null where git named none.</summary>
    internal static string? Shared(int code, string shared) => code != 0 || shared.Trim().Length == 0 ? null : shared.Trim();

    /// <summary><c>merge-base --independent</c>'s answer: those no other reaches, as git names them; none where git cannot say.</summary>
    internal static IReadOnlyList<string> Independent(int code, string named) =>
        code != 0 ? [] : named.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary><c>merge-tree --write-tree</c>'s tree (REVIEWBASE3): null where they conflict or git cannot say.</summary>
    internal static string? Merged(int code, string output)
    {
        var first = output.Split('\n', 2)[0].Trim();
        return code == 0 && first.Length > 0 && first.All(char.IsAsciiHexDigit) ? first : null;
    }

    /// <summary>The two counts of <c>rev-list --count</c> as how the tree stands against its line; null where git could not count.</summary>
    internal static LineBehind? Behind(string line, int behindCode, string behind, int aheadCode, string ahead) =>
        behindCode == 0 && aheadCode == 0
        && int.TryParse(behind.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var commits)
        && int.TryParse(ahead.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var beyond)
            ? new(line, commits, beyond)
            : null;

    /// <summary>
    /// The base and the merge the selection asks for (<c>ReviewRangeAsync</c>'s Σ): with fewer than two candidates, the one or the
    /// record's word; else <see cref="ReviewBase.Newest"/> over git's maximal ones, and the incomparable pair where REVIEWBASE3 applies.
    /// </summary>
    internal static (string From, (string Line, string Record)? Merge) Choose(
        IReadOnlyList<string> candidates, IReadOnlyList<string>? maximal, string? record, string recorded)
    {
        if (candidates.Count < 2) return (candidates.Count == 1 ? candidates[0] : recorded, null);
        var named = maximal ?? [];
        return (ReviewBase.Newest(candidates, named) ?? record ?? recorded, ReviewBase.Incomparable(candidates, named, record));
    }

    /// <summary>
    /// git's guess at an unset line in <c>DefaultBranchAsync</c>'s order: origin's <c>HEAD</c> where <c>symbolic-ref</c> named it,
    /// else <c>main</c>, else <c>master</c>, where each answered 0; null where none did. A code is null where it was not asked.
    /// </summary>
    internal static string? Guess(int originCode, string originOut, int? mainCode, int? masterCode)
    {
        if (originCode == 0 && originOut.Trim() is { Length: > 0 } pointed)
        {
            var slash = pointed.IndexOf('/');
            return slash < 0 ? pointed : pointed[(slash + 1)..];
        }

        if (mainCode == 0) return "main";
        return masterCode == 0 ? "master" : null;
    }

    /// <summary><c>DefaultBranchAsync</c>'s questions after origin's <c>HEAD</c>, in its order.</summary>
    internal static readonly string[] Conventional = ["main", "master"];

    /// <summary>The argument list of a conventional line's question, spelled as every read of a branch's commit is (LANDGIT1c).</summary>
    internal static string[] ConventionalAsk(string candidate) => ["rev-parse", "--verify", "--quiet", $"refs/heads/{candidate}^{{commit}}"];

    /// <summary>
    /// <c>BranchAsync</c>'s reading of <c>symbolic-ref --short -q HEAD</c>: the branch; null where detached (exit 1, nothing said);
    /// a git that failed otherwise is thrown, since a tree git cannot read has no head to name.
    /// </summary>
    internal static string? Branch(int code, string stdout, string stderr)
    {
        if (code == 0 && stdout.Trim() is { Length: > 0 } branch) return branch;
        if (code == 1 && string.IsNullOrWhiteSpace(stderr)) return null;
        throw new DriverException($"git could not name this checkout's branch: {stderr.Trim()}");
    }

    /// <summary>
    /// <c>LandedFromAsync</c>'s first choice: the recorded start, as git's full id, where the branch holds it (<c>--is-ancestor</c>
    /// answered 0) and git named it; else null, and the branch's fork from the line is read instead.
    /// </summary>
    internal static string? LandedFrom(int heldCode, string full) => heldCode == 0 && full.Trim() is { Length: > 0 } commit ? commit : null;

    /// <summary>The untracked paths of <c>status --porcelain -z</c>, in its order: a rename's or a copy's source skipped.</summary>
    internal static IReadOnlyList<string> UntrackedPaths(string status)
    {
        var paths = new List<string>();
        var entries = status.Split('\0');
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            // A rename's or a copy's source follows its entry, and is no path of its own.
            if (entry[0] is 'R' or 'C' || entry[1] is 'R' or 'C') at++;
            if (!entry.StartsWith("?? ", StringComparison.Ordinal)) continue;
            paths.Add(entry[3..]);
        }

        return paths;
    }
}

/// <summary>
/// The untracked files' fold (REVIEWDIRTY1-a; GITAPP1a-ii, the git-app design P1.3): each answer taken in <c>status</c>'s order, the
/// patch budget spent by W's patches and each read patch, a file past it listed with no patch and counted in the truncation's words.
/// Today's loop and the graph's window both fold through it.
/// </summary>
internal sealed class UntrackedFold(WorkingTree.TreeDiff tracked)
{
    private readonly List<WorkingTree.DiffFile> _files = [.. tracked.Files];
    private int _spent = tracked.Files.Sum(file => file.Patch?.Length ?? 0);
    private int _dropped;

    /// <summary>Whether the budget is spent: every later file is listed with no read.</summary>
    internal bool Spent => _spent >= WorkingTree.PatchBudget;

    /// <summary>A file past the budget, listed with no patch.</summary>
    internal void Drop(string path)
    {
        _dropped++;
        _files.Add(new WorkingTree.DiffFile(path, "added", null, null, null));
    }

    /// <summary>A file read: its patch spends the budget.</summary>
    internal void Take(string path, string? patch, int? added)
    {
        _spent += patch?.Length ?? 0;
        // The word the committed list uses for an addition, so the page renders both alike.
        _files.Add(new WorkingTree.DiffFile(path, "added", added, added is null ? null : 0, patch));
    }

    /// <summary>The uncommitted answer: the files in order, and the truncation in today's words.</summary>
    internal WorkingTree.TreeDiff End(string rest)
    {
        var truncated = tracked.Truncated ?? (_dropped == 0
            ? null
            : $"{_dropped} more untracked file{(_dropped == 1 ? "" : "s")}; their patches are not shown here. {rest}");
        return new WorkingTree.TreeDiff(_files, truncated, "HEAD");
    }
}
