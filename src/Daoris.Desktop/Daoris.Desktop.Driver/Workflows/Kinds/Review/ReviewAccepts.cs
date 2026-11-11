namespace Daoris.Driver;

/// <summary>
/// The person's Accept bound to the review record it names: checked when it is kept, against the record the review stands on, and when the
/// pass applies it, against the run's work read afresh, since a page can be stale before the runtime has seen the work move. A word on
/// another record is refused or dropped <see cref="ReviewMoved.Word"/>, saying what it moved to.
/// </summary>
public static class ReviewAccepts
{
    /// <summary>The fewest characters of a revision that name it, as the terminal prints it short.</summary>
    private const int Shortest = 7;

    private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="named"/>, whole or a prefix of at least seven characters, names <paramref name="revision"/>.</summary>
    public static bool Names(string named, string revision) =>
        named.Length >= Shortest && revision.StartsWith(named.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The first check, as the word is kept on an active review at <paramref name="status"/>: the record of its attempt, or its binding where
    /// none is kept. Refused <see cref="ReviewMoved.Word"/> where the word names another attempt or another revision; the revision standing is
    /// returned either way, which an unnamed word is bound to.
    /// </summary>
    public static (string? Refused, string? Standing) AtKeep(string home, string run, string node, NodeStatus status, int? attempt, string? revision)
    {
        var standing = ReviewRecords.Read(home, run, node, status.Attempt)?.Revision ?? status.Binding?.SubjectRevision;
        if (attempt is { } named && named != status.Attempt) return (ReviewMoved.Word, standing);
        if (revision is { } drawn && standing is { } at && !Names(drawn, at)) return (ReviewMoved.Word, standing);
        return (null, standing);
    }

    /// <summary>
    /// The second check, at the pass: whether <paramref name="word"/>, the person's or a supervisor's, names a revision the work read afresh no
    /// longer holds (<paramref name="present"/>, by review), or the person's names an attempt its review has left.
    /// </summary>
    public static bool MovedFrom(NodeWord word, NodeStatus standing, IReadOnlyDictionary<string, string>? present) =>
        (word.Revision is { } named && present?.GetValueOrDefault(word.Review ?? word.Node) is { } now && !string.Equals(named, now, StringComparison.OrdinalIgnoreCase))
        || (word is { By: null, Act: NodeWordActs.Accept, Kept: false, Review: null, Attempt: { } attempt } && attempt != standing.Attempt);

    /// <summary>
    /// The revision each review a word on <paramref name="record"/> is bound to reads now: the run's work read as its record is
    /// (<see cref="ReviewWork.Read"/>), a branch at the tip <paramref name="tipOf"/> reads this pass. Empty where no word names one, or where a
    /// branch's tip is not read, so nothing is dropped on a guess.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> PresentAsync(
        string home, GovernedUnit governed, NodeRecord record, Func<RunScope, Task<string?>> tipOf)
    {
        var bound = record.Pending.Where(word => word.Revision is not null).Select(word => word.Review ?? word.Node)
            .Where(node => governed.Declared.GetValueOrDefault(node)?.Kind == NodeKinds.Review).ToHashSet(StringComparer.Ordinal);
        if (bound.Count == 0 || governed.Scope is not { } scope) return None;
        string? tip = null;
        if (ReviewWork.OnBranch(scope))
        {
            if (scope.Branch is not { } branch || await tipOf(scope).ConfigureAwait(false) is not { } read) return None;
            tip = RunMoves.Revision(record.Moves, branch.Id, read);
        }

        var present = ReviewWork.Read(home, scope, tip).Revision;
        return bound.ToDictionary(node => node, _ => present, StringComparer.Ordinal);
    }

    /// <summary>What the pass says of a word it dropped because the work moved from the record it named.</summary>
    public static string DroppedSaid(string unit, NodeWord word, IReadOnlyDictionary<string, string> present) =>
        $"work  {unit}: your {word.Act} of `{word.Node}` was not applied (moved): the work moved from the record you pressed on ({ReviewMoved.Short(word.Revision)})"
        + (present.GetValueOrDefault(word.Review ?? word.Node) is { } now ? $" and now reads {ReviewMoved.Short(now)}" : "")
        + ", so read it again and say what it should do.";
}
