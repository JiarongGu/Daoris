namespace Daoris.Driver;

/// <summary>Who a line's state is read from: one of its parts, or a co-worker serving one in its role.</summary>
public static class LineRole
{
    public const string Part = "part";
    public const string Reviewer = "reviewer";
    public const string Partner = "partner";
    public const string Helper = "helper";

    /// <summary>A record's role as a co-worker by its marks, or null where it serves no session.</summary>
    internal static string? Of(SessionRecord record) =>
        record.Serves is not { Length: > 0 } ? null
        : record.Opinion is not null ? Reviewer
        : record.Partner ? Partner
        : Helper;
}

/// <summary>
/// A line of work's state, as every part of it shows it (<see cref="LineStandings"/>): the first of needs you, working and pending
/// any part is in, or needs you and working where a co-worker is, else as its newest part ended.
/// </summary>
/// <param name="State">One of the five states (<see cref="SessionGroup"/>), never Archived.</param>
/// <param name="Group">Its state, or Archived where the part it is read from rests in that fold.</param>
/// <param name="Reason">The finer reason of the row it is read from (<see cref="SessionGrouping.Reason"/>).</param>
/// <param name="By">The session it is read from: the newest part in that state, else the co-worker in it.</param>
/// <param name="Role">One of <see cref="LineRole"/>: whether <paramref name="By"/> is a part or which co-worker.</param>
public sealed record LineStanding(string State, string Group, string Reason, string By, string Role);
