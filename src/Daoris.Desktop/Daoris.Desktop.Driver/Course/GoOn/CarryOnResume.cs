using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// A driven line Daoris cut, by its close (an update's or any other) or by the sweep of a run nothing here runs any more, goes on in its own
/// record as that record's next turn: its own conversation resumes on its own agent and account with Daoris's restart word, as the limit's
/// resume goes on (<see cref="LimitResume"/>), and nothing opens a part linked by <c>from</c>. Where the same agent, account, tree, kept
/// conversation and a door that fails closed do not all hold, or its own account is not ready, the line is carried on to a new part as before.
/// </summary>
internal static class CarryOnResume
{
    /// <summary>
    /// The cut record a start of its quest resumes in its own conversation: the last part of an open or taken quest that waits on nothing, cut
    /// by Daoris, and not the record its quest is handed over from. Null for every other start, and for a record the limit resume takes.
    /// </summary>
    public static PriorSession? Line(QuestView quest, PriorSession? resumes, bool handedFrom) =>
        !handedFrom
        && quest is { Status: "Open" or "Taken", Awaits: null or "" }
        && resumes is { Parked: false } cut
        && Close(cut) is not null
            ? cut
            : null;

    /// <summary>
    /// The key Daoris's restart word on <paramref name="record"/> is kept under (<see cref="DaorisKeys.Restart"/>): an update's build, else
    /// the moment the cut ended it, so a start tried again for the same cut keeps one word. Null for an end that is no cut of Daoris's: the
    /// person's stop, a failure, a limit.
    /// </summary>
    public static string? Close(PriorSession record) =>
        string.Equals(record.State, "stopped", StringComparison.OrdinalIgnoreCase) && record.Interrupted
            ? LimitResume.UpdateBuild(record)
              ?? (record.Updated ?? DateTimeOffset.UnixEpoch).UtcDateTime.ToString("yyyy-MM-dd'T'HH-mm-ss.fffffff'Z'", CultureInfo.InvariantCulture)
            : null;
}
