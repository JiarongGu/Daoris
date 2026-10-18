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
/// which the stub agents read in place of a connector's tool.
/// </summary>
public static class BrainstormVariables
{
    public const string Id = "DAORIS_BRAINSTORM_ID";
    public const string Turn = "DAORIS_BRAINSTORM_TURN";
    public const string Answer = "DAORIS_BRAINSTORM_ANSWER";
}

/// <summary>What one wording's search found, and the tier that answered: <c>meaning</c>, <c>words</c> or <c>files</c>.</summary>
public sealed record BrainstormSearch(IReadOnlyList<BrainstormFound> Found, string Tier);
