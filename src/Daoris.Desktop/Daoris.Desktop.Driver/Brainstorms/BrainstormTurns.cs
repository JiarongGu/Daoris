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
public sealed record BrainstormTurn(string Id, string Turn, string Agent, string Room, string Instruction, string Answer, int Minutes);

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
    public const string Id = "DAORIS_BRAINSTORM_ID";
    public const string Turn = "DAORIS_BRAINSTORM_TURN";
    public const string Answer = "DAORIS_BRAINSTORM_ANSWER";

    /// <summary>The turn's room, on its connector alone: what <c>brainstorm_give</c> and <c>brainstorm_synthesize</c> judge against.</summary>
    public const string Room = "DAORIS_BRAINSTORM_ROOM";

    /// <summary>What a turn's connector carries beyond a session's (<see cref="KnowledgeConnector.SessionScope"/>): its brainstorm whole.</summary>
    public static IReadOnlyDictionary<string, string> Scope(BrainstormTurn turn) => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Id] = turn.Id,
        [Turn] = turn.Turn,
        [Answer] = turn.Answer,
        [Room] = turn.Room,
    };
}

/// <summary>What one wording's search found, and the tier that answered: <c>meaning</c>, <c>words</c> or <c>files</c>.</summary>
public sealed record BrainstormSearch(IReadOnlyList<BrainstormFound> Found, string Tier);
