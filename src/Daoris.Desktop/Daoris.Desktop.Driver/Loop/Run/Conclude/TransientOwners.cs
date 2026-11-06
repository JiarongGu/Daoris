namespace Daoris.Driver;

/// <summary>
/// Each kind of pass's own authority as the check its retry is handed (AGENTERRORS1d, the agent-errors design §5.5): another try is wanted
/// only while the person has not stopped the run and the thing the pass exists to produce is not already there: a helper's stored answer, an
/// intake's ask settled, an opinion recorded, a partner's piece ending. A check that cannot be read answers yes (§5.1), so a read that failed
/// costs a try, never a missed one.
/// </summary>
internal static class TransientOwners
{
    /// <summary>A helper's: its answer is stored.</summary>
    public static Func<CancellationToken, Task<bool>> Helper(SessionProcesses processes, string sessionId, HelperFiles files, string ask) =>
        _ => Task.FromResult(!processes.WasStopRequested(sessionId) && !Stored(() => files.ReadAnswer(ask) is not null));

    /// <summary>A partner's: its piece is ending or already ended.</summary>
    public static Func<CancellationToken, Task<bool>> Partner(SessionProcesses processes, string sessionId, PartnerFiles files, string piece) =>
        _ => Task.FromResult(!processes.WasStopRequested(sessionId) && !Stored(() => files.ReadState(piece) is { } state && Finished(state.State)));

    /// <summary>An opinion's: the reviewer has said it, so the host holds it given.</summary>
    public static Func<CancellationToken, Task<bool>> Opinion(
        SessionProcesses processes, string sessionId, Func<CancellationToken, Task<OpinionView?>> read) =>
        async retryCt =>
        {
            if (processes.WasStopRequested(sessionId)) return false;
            try
            {
                return await read(retryCt).ConfigureAwait(false) is not { Given: true };
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or TaskCanceledException && !retryCt.IsCancellationRequested)
            {
                return true;
            }
        };

    /// <summary>An intake's: its ask is settled meanwhile, as <see cref="IntakeObservation.Conclude"/> reads it before any failed turn.</summary>
    public static Func<CancellationToken, Task<bool>> Intake(
        SessionProcesses processes, string sessionId, Func<CancellationToken, Task<AskView?>> read, int before) =>
        async retryCt => !processes.WasStopRequested(sessionId) && !Settled(before, await read(retryCt).ConfigureAwait(false));

    /// <summary>The ask is deleted, closed, or has gained quests since the intake began: <c>Conclude</c>'s authoritative endings.</summary>
    public static bool Settled(int before, AskView? after) => after is null || after.State == "Closed" || after.Quests.Count > before;

    private static bool Finished(string state) =>
        state is PartnerStates.Ending or PartnerStates.Held or PartnerStates.Answered or PartnerStates.Handed or PartnerStates.TakenIn
            or PartnerStates.NotTakenIn or PartnerStates.Discarded or PartnerStates.Imported;

    private static bool Stored(Func<bool> stored)
    {
        try
        {
            return stored();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
