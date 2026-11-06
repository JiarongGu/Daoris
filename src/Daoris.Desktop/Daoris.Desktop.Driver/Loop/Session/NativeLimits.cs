namespace Daoris.Driver;

/// <summary>
/// The account's part of a run's sink for the native door's limit (the agent-provider design §11 row l; D210 row 1c, TOOL4n, LIMITRESUME1f):
/// a <see cref="WireReading.Failed"/> the pipe door's failed <c>result</c> carried is read by the one reader (<see cref="AgentErrors.Read"/>),
/// and one it reads as a limit is the limit the turn ended on, held until that turn's end is said. The run concludes on it as on the protocol
/// door's refused turn: a driven run's conclusion reads <see cref="Ended"/> as its turn's failure, so the account cools there once, the record
/// says <c>limit</c> and parks or waits, and its line moves by its rules; a conversation is told at the turn's end (<c>met</c>), before the
/// record keeps that end, so its cooling line stands before it and the turn's end moves it to the next account as a protocol conversation's
/// does. A protocol door's failure is its own transport's to read, never this part's; a harness's own line beside a result is read for access
/// alone.
/// </summary>
/// <remarks>Read from the structure the door carried, never from the agent's own streamed words (D125 point 1.4).</remarks>
internal sealed class NativeLimits(Func<DoorFailure, AgentError> read, Action<AgentError>? met = null) : IReadingSink
{
    private AgentError? _turn;

    /// <summary>The limit the run's last turn ended on, read from its failed result; null where its last turn ended otherwise.</summary>
    public AgentError? Ended { get; private set; }

    public bool Watched => false;

    /// <summary>
    /// The turn's failure a run's conclusion reads: the protocol door's refused turn, or the native door's last turn ended on its account's
    /// limit, in its result's words; any other failed native result stays the harness's own words, read for access alone, and is null here.
    /// </summary>
    public static string? TurnFailed(bool protocol, HarnessEnding ending, NativeLimits? limits) =>
        protocol ? ending.Failure : limits?.Ended is { } limit ? limit.Failure.Words ?? ending.Failure : null;

    public void Read(WireReading reading)
    {
        switch (reading)
        {
            case WireReading.Failed { Replayed: false, Failure: { Door: "native", Where: DoorWhere.Result } failure }:
                var error = read(failure);
                if (error.Kind == AgentErrorKind.Limit) _turn = error;
                return;

            // The result's failure is read before its turn's end on the same frame: that end is whose limit it was, and a later turn that
            // ended otherwise leaves the run on no limit.
            case WireReading.Said { Replayed: false, Event.Kind: SessionEventKind.Turn }:
                Ended = _turn;
                _turn = null;
                if (Ended is { } limit) met?.Invoke(limit);
                return;
        }
    }
}
