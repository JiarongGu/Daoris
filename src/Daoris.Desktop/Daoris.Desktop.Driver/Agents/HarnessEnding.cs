namespace Daoris.Driver;

/// <summary>
/// What a harness itself said about how its run ended, apart from its agent's words: the process's exit, its door's
/// failure, and the lines it wrote that its agent could not. What a refused credential is read from, as a limit and a
/// refused sign-in are read from the door's failure.
/// </summary>
/// <param name="Exit">The process's exit code; null where the timeout killed it.</param>
/// <param name="Failure">
/// The door's failure in the harness's words: the protocol door's refused call, or the native door's failed
/// <c>result</c>. Null where the door said the turn ended.
/// </param>
/// <param name="Lines">
/// The harness's own last lines: on the native door its stderr and what it printed outside its protocol. A door that carries
/// only text gives nothing but its transcript, so its transcript's last lines are these, the agent's words among them: there,
/// only the exit, which the harness sets, tells a failure (<see cref="Transcript"/>). None on the protocol door, whose failure
/// is its door's.
/// </param>
public sealed record HarnessEnding(int? Exit, string? Failure, IReadOnlyList<string> Lines) : IHarnessEnding
{
    /// <summary>How many of the harness's last lines are kept: where a tool says why it gave up.</summary>
    public const int LinesKept = 40;

    /// <summary>Whether the harness itself said the run failed: its door's failure, or an exit other than 0.</summary>
    public bool Failed => Failure is { Length: > 0 } || Exit is { } exit && exit != 0;

    /// <summary>
    /// The session's own background work its harness ended with its turn, by name: on the native door, a command still
    /// running at the turn's <c>result</c> and ended after it, which only the harness does there. Empty on every other door.
    /// </summary>
    public IReadOnlyList<string> BackgroundCut { get; init; } = [];

    /// <summary>
    /// Not the harness's word: Daoris ended the run at a turn's end because its repository's permission prompts changed while it worked,
    /// handing back the words held for it (<see cref="Observation.SetupChanged"/>). Null on every other run.
    /// </summary>
    public SetupSwap? SetupChanged { get; init; }

    /// <summary>
    /// Whether its last turn ended on its model's provider refusing a call: the protocol door's own stop reason,
    /// <see cref="AcpOutcome.Refusal"/>, never words its agent wrote. False on every other door.
    /// </summary>
    public bool ProviderRefused { get; init; }

    /// <summary>
    /// How its last turn is cut (<see cref="AcpOutcome.Cut"/>): the protocol door's own word that its agent ended the turn
    /// <c>cancelled</c> though nobody stopped it, so a clean end with the quest taken asked the person nothing. Null on every other run.
    /// </summary>
    public TurnCut? Cut { get; init; }

    /// <summary>
    /// The transient retry the run is handed, whose episode, dispositions and last reading the conclusion
    /// reads; null for a run handed none.
    /// </summary>
    public TransientRetry? Transient { get; init; }

    /// <summary>
    /// The refusal the protocol door's turn ended on, as the door carried it with its code: what a run handed no
    /// transient retry, a brainstorm's pass, reads through <see cref="AgentErrors.Read"/> for a limit by its code, where
    /// <see cref="Transient"/> keeps no reading. Null where the turn is not refused, on every other door, and where another failure ended it.
    /// </summary>
    public AcpRefusal? Refusal { get; init; }

    /// <summary>
    /// Its agent did not offer, or refused, the choice its actor asked at the open, and nothing else could run on that session, so it
    /// ended before any prompt: its record ends <c>choice-not-offered</c>. Null on every other run.
    /// </summary>
    public ModelRefusal? NotOffered { get; init; }

    /// <summary>
    /// The pipe door refused the model it is handed: the measured words, the
    /// <c>assistant</c> frame's <c>error: "model_not_found"</c>, on a turn with no output and no tool use, ended failed. That door has no
    /// offer list and its prompt rides its arguments, so this is the one refusal read after a prompt is handed. False on every other run.
    /// </summary>
    public bool ModelRefused { get; init; }

    /// <summary>
    /// A door that carries only text: its transcript's last lines, both its streams, beside the process's exit. Its
    /// agent's words are among them and cannot be told apart, so only an exit other than 0 lets them be read. Unreadable
    /// is none.
    /// </summary>
    public static HarnessEnding Transcript(int? exit, string transcript)
    {
        try
        {
            return new(exit, null, [.. File.ReadLines(transcript).TakeLast(LinesKept)]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new(exit, null, []);
        }
    }
}
