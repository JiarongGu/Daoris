namespace Daoris.Driver;

/// <summary>
/// One turn of a brainstorm's pass (BRAINSTORM1a, D176 points 1-4): a generator's or the synthesis's, or its one repair, in a room
/// that holds the packet and the instruction and nothing else.
/// </summary>
/// <param name="Turn">The turn's id: <c>g1</c>… for a generator, <c>s1</c> for the synthesis, each with <c>r</c> for its repair.</param>
/// <param name="Agent">The adapter it runs on.</param>
/// <param name="Room">Its room, under the home's work.</param>
/// <param name="Answer">The file its result tool writes its answer to, outside the room.</param>
/// <param name="Minutes">How long it may take.</param>
/// <param name="Entry">
/// The voice's entry of the Brainstorm actor, frozen with its plan (D205 §4.4, MODELROLE1e): the model and effort its turn asks and the
/// choice its record keeps; null for a turn of a meeting, which asks account default as it always did.
/// </param>
/// <param name="Passed">The accounts of its agent this voice already passed for its model: the one bounded restart leaves them out.</param>
public sealed record BrainstormTurn(
    string Id, string Turn, string Agent, string Room, string Instruction, string Answer, int Minutes,
    ActorEffective? Entry = null, IReadOnlyCollection<string>? Passed = null)
{
    /// <summary>
    /// The workspace of the repository its brainstorm asks about (FOUNDATION1j part ii, the foundation design §2.4's table), for the version it
    /// pins; null for a brainstorm on no repository, and for a meeting's turn, which asks the machine's.
    /// </summary>
    public string? Workspace { get; init; }
}

/// <summary>How a turn went: whether it ran, the account it ran as (this machine only), why it did not run, and the look's line.</summary>
/// <param name="Code">Why it did not run, or how it failed: a fixed code; null for a turn that ran and ended.</param>
public sealed record BrainstormTurnRun(bool Ran, string? Account, string? Code, string Line);

/// <summary>
/// The variables a brainstorm turn's spawn carries (BRAINSTORM1a): the brainstorm, the turn, and the file its result tool writes,
/// which a stub agent may write in place of calling the tool. Its connector carries them too, with the room (BRAINSTORM1a2).
/// </summary>
/// <remarks>A twin of the service's <c>BrainstormScope</c>: the names are duplicated deliberately, never imported across.</remarks>
public static class BrainstormVariables
{
    public const string Id = AgentWorkVariables.Brainstorm;
    public const string Turn = AgentWorkVariables.Turn;
    public const string Answer = AgentWorkVariables.Answer;

    /// <summary>The turn's room, on its connector alone: what <c>brainstorm_give</c> and <c>brainstorm_synthesize</c> judge against.</summary>
    public const string Room = AgentWorkVariables.Room;

    /// <summary>What a turn's connector carries beyond a session's (<see cref="KnowledgeConnector.SessionScope"/>): its brainstorm whole.</summary>
    public static IReadOnlyDictionary<string, string> Scope(BrainstormTurn turn) => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Id] = turn.Id,
        [Turn] = turn.Turn,
        [Answer] = turn.Answer,
        [Room] = turn.Room,
    };
}
