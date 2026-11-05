namespace Daoris.Driver;

/// <summary>
/// The account's part of a run's sink for the native door's limit (the agent-provider design §11 row l; D210 row 1c, TOOL4n, LIMITRESUME1f):
/// a <see cref="WireReading.Failed"/> the pipe door's failed <c>result</c> carried is read by the one reader (<see cref="AgentErrors.Read"/>),
/// and one it reads as a limit cools the account the run ran as, so the next start's walk passes it. A protocol door's failure is the run's
/// conclusion's or its conversation's to read, never this part's; a harness's own line beside a result is read for access alone.
/// </summary>
/// <remarks>Read from the structure the door carried, never from the agent's own streamed words (D125 point 1.4).</remarks>
internal sealed class NativeLimits(Func<DoorFailure, AgentError> read, Func<DoorFailure, AgentError, bool> cool) : IReadingSink
{
    /// <summary>The limit the run's native result carried and the account it cooled, or null where it met none.</summary>
    public AgentError? Cooled { get; private set; }

    public bool Watched => false;

    public void Read(WireReading reading)
    {
        if (reading is not WireReading.Failed { Replayed: false, Failure: { Door: "pipe", Where: DoorWhere.Result } failure }) return;

        var error = read(failure);
        if (error.Kind != AgentErrorKind.Limit) return;
        if (cool(failure, error)) Cooled = error;
    }
}
