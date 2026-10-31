namespace Daoris.Driver;

/// <summary>What a restart settles an operation at <i>landing</i> as.</summary>
public enum TogetherSettled
{
    /// <summary>Landed: its members are brought in, any missed record written.</summary>
    Landed,

    /// <summary>Still under way: a merge's request waits on its line's run.</summary>
    Open,

    /// <summary>Proved unmade: back to <i>ready to accept</i>.</summary>
    Unmade,

    /// <summary>Not proved either way: <i>uncertain</i>, its members bound.</summary>
    Uncertain,
}

/// <summary>A ref as git read it: missing only on git's own word (<c>show-ref --exists</c> exiting 2).</summary>
internal sealed record RefRead(string? Tip, bool Missing)
{
    public static RefRead Unread { get; } = new(null, false);

    public bool Read => Tip is not null || Missing;
}

/// <summary>A request correlated with an attempt: its id and whether it settled, closed unmerged or is open.</summary>
internal sealed record CorrelatedRequest(string Id, bool Settled, bool Closed);

/// <summary>What a restart reads of an attempt's effects.</summary>
/// <param name="Recorded">A landing record or advance correlated with the attempt was found.</param>
/// <param name="Request">The request correlated with it, by its id or at the replayed tip; null where none was found.</param>
/// <param name="RequestUnread">The integration store could not be read, so a request may stand unseen.</param>
/// <param name="Target">The target ref as it stands.</param>
/// <param name="HoldsPinned">Whether the target's tip holds the pinned tip's work; null where unasked or unread.</param>
internal sealed record LandingEvidence(bool Recorded, CorrelatedRequest? Request, bool RequestUnread, RefRead Target, bool? HoldsPinned);

/// <summary>
/// The restart's settlement of a landing attempt (the review-flow design §4, <i>Restart</i>): first by a record, advance or request
/// correlated with it, else by its git effects, the target ref moved from the tip expected to one holding the pinned tip's work. Only a
/// proved unmade attempt goes back to <i>ready to accept</i>; anything not proved stays <i>uncertain</i>.
/// </summary>
internal static class TogetherSettle
{
    public static TogetherSettled Decide(TogetherAttempt attempt, LandingEvidence evidence)
    {
        if (evidence.Recorded || evidence.Request is { Settled: true }) return TogetherSettled.Landed;
        if (evidence.Request is { Closed: false }) return TogetherSettled.Open;
        if (!evidence.Target.Read) return TogetherSettled.Uncertain;
        var unmoved = evidence.Target.Missing
            ? attempt.Expected is null
            : string.Equals(evidence.Target.Tip, attempt.Expected, StringComparison.OrdinalIgnoreCase);
        if (unmoved) return evidence.RequestUnread ? TogetherSettled.Uncertain : TogetherSettled.Unmade;
        return !evidence.Target.Missing && evidence.HoldsPinned == true ? TogetherSettled.Landed : TogetherSettled.Uncertain;
    }

    /// <summary>
    /// Read an attempt's effects in <paramref name="root"/>: the target ref, whether its tip holds the pinned tip's work, the request by
    /// its id or, for a merge whose correlation was never written, the head branch's request at the replayed tip.
    /// </summary>
    /// <param name="recorded">Whether a landing record or advance names the attempt's target at its tip; null where it does not read.</param>
    /// <param name="requestById">The integration store's request by id.</param>
    /// <param name="requestsOf">The integration store's requests of the head's branch.</param>
    public static async Task<LandingEvidence> ReadAsync(
        string root, TogetherOperation operation, Func<TogetherAttempt, bool?> recorded,
        Func<string, IntegrationRequest?> requestById, Func<string, string, IReadOnlyList<IntegrationRequest>> requestsOf, CancellationToken ct)
    {
        var attempt = operation.Attempt ?? throw new ArgumentException("the operation holds no attempt", nameof(operation));
        var record = recorded(attempt);
        CorrelatedRequest? request = null;
        var unread = record is null;
        try
        {
            var found = attempt.Request is { } id ? requestById(id)
                : attempt.Form == TogetherForms.Merge
                    ? requestsOf(operation.Repository, operation.Head.Branch).LastOrDefault(each =>
                        string.Equals(each.Tip, attempt.Replayed ?? attempt.Pinned, StringComparison.OrdinalIgnoreCase) && each.At >= attempt.At.AddMinutes(-1))
                    : null;
            if (found is not null)
            {
                request = new CorrelatedRequest(found.Id, found.ClosedWhy == IntegrationRequestCloses.Settled, found.ClosedAt is not null);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
            or Microsoft.Data.Sqlite.SqliteException)
        {
            unread = true;
        }

        var target = await RefAsync(root, attempt.Target, ct).ConfigureAwait(false);
        bool? holds = null;
        if (target.Tip is { } tip && !string.Equals(tip, attempt.Expected, StringComparison.OrdinalIgnoreCase))
        {
            holds = await HoldsAsync(root, tip, attempt.Replayed ?? attempt.Pinned, ct).ConfigureAwait(false);
            if (holds is false && attempt.Replayed is not null) holds = await HoldsAsync(root, tip, attempt.Pinned, ct).ConfigureAwait(false);
        }

        return new LandingEvidence(record == true, request, unread && request is null, target, holds);
    }

    /// <summary>A branch's tip, or git's own word that it is missing; unread otherwise.</summary>
    internal static async Task<RefRead> RefAsync(string root, string branch, CancellationToken ct)
    {
        var name = branch.StartsWith("refs/", StringComparison.Ordinal) ? branch : $"refs/heads/{branch}";
        var (code, stdout, _) = await WorkingTree.GitAsync(
            root, ["--no-optional-locks", "rev-parse", "--verify", "--quiet", $"{name}^{{commit}}"], ct).ConfigureAwait(false);
        if (code == 0 && stdout.Trim() is { Length: > 0 } tip) return new RefRead(tip, false);
        var (exists, _, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "show-ref", "--exists", name], ct).ConfigureAwait(false);
        return exists == 2 ? new RefRead(null, true) : RefRead.Unread;
    }

    /// <summary>
    /// Whether <paramref name="tip"/> holds <paramref name="work"/>'s: descends from it, or stands at its tree (a squash of it); null
    /// where git could not say.
    /// </summary>
    internal static async Task<bool?> HoldsAsync(string root, string tip, string work, CancellationToken ct)
    {
        var (code, _, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "merge-base", "--is-ancestor", work, tip], ct).ConfigureAwait(false);
        if (code == 0) return true;
        if (code != 1) return null;
        var (treeCode, trees, _) = await WorkingTree.GitAsync(
            root, ["--no-optional-locks", "rev-parse", $"{tip}^{{tree}}", $"{work}^{{tree}}"], ct).ConfigureAwait(false);
        if (treeCode != 0) return null;
        var both = trees.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return both.Length == 2 && string.Equals(both[0], both[1], StringComparison.OrdinalIgnoreCase);
    }
}
