using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// The driver's doors on a workflow run's quests: the bind at a session's first start, which makes its agent's done a submission, and the
/// run's end, which writes the quests' Done and ends the sessions waiting on the run in one write. What the end moved is told to the
/// client's <see cref="ServiceClient.Moved"/> watchers.
/// </summary>
public static class RunEndDoors
{
    /// <summary>Binds <paramref name="quest"/> to <paramref name="run"/>; a quest bound already, or closed, is answered not bound.</summary>
    public static async Task<RunBindResponse> BindToRunAsync(this ServiceClient service, string quest, string run, CancellationToken ct) =>
        (await service.SendAsync(new BindQuestToRun(quest.TrimStart('#'), run), ct).ConfigureAwait(false)).Answered();

    /// <summary>Ends <paramref name="run"/>: its <paramref name="quests"/> Done where <paramref name="state"/> is <c>completed</c>, then its sessions.</summary>
    public static async Task<RunEndResponse> EndRunAsync(
        this ServiceClient service, string run, IReadOnlyList<RunEndQuest> quests, IReadOnlyList<string> sessions, string state, Noted noted,
        CancellationToken ct)
    {
        var ended = (await service.SendAsync(
            new EndRun(run, [.. quests], [.. sessions], state, noted.Note, noted.Parts is { Count: > 0 } parts ? SessionMoveDoors.PartsElement(parts) : null),
            ct).ConfigureAwait(false)).Answered();
        foreach (var session in ended.Ended) service.RecordMoved(new SessionMoved(session, state));
        return ended;
    }
}
