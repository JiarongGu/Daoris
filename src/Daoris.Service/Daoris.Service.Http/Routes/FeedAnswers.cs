using Daoris.Contracts.Feed;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>What a feed's outcome answers, and the commit a feed speaks for.</summary>
public static class FeedAnswers
{
    /// <summary>
    /// The commit a feed speaks for. Whole or nothing, like every pair in this system: half a provenance cannot be compared
    /// with what is held, and the door must not assemble a plausible one out of the half it gets. The origin is the key's own identity,
    /// recorded because the write carried it.
    /// </summary>
    public static FeedProvenance? FedFrom(string? commit, DateTimeOffset? committedAt, string? branch, string? checkedAgainst, HttpContext http) =>
        commit is { Length: > 0 } && committedAt is { } at && branch is { Length: > 0 }
            ? new FeedProvenance(commit, at, branch, http.Items["daoris.principal"] as string)
            {
                Base = string.IsNullOrWhiteSpace(checkedAgainst) ? null : checkedAgainst.Trim(),
            }
            : null;

    /// <summary>
    /// A feed with no commit or a malformed map is a bad request; everything else is a state conflict, as this deployment holds
    /// something the feed cannot replace. <c>Information</c> lets a sync report a stale, moved or branch feed as news (§6).
    /// </summary>
    public static Answer<T> Of<T>(FeedOutcome outcome) =>
        outcome.Accepted
            ? Answer<T>.Else(StatusCodes.Status200OK, new FeedResponse(outcome.Entries, outcome.Message))
            : Answer<T>.Else(
                outcome.Refusal is FeedRefusal.NoProvenance or FeedRefusal.Malformed ? StatusCodes.Status400BadRequest : StatusCodes.Status409Conflict,
                new FeedRefusalResponse(outcome.Message, outcome.Information));
}
