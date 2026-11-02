namespace Daoris.Driver;

/// <summary>The machine log's line for a line that went on with another choice of its role (STAFFPOLICY1c2; the staff policy design §5).</summary>
public static class ActorMoves
{
    /// <summary>What <paramref name="action"/> is when no policy names it: the role's own order walked on.</summary>
    public const string Next = "next";

    /// <summary>
    /// <c>actor.moved</c>: the <paramref name="role"/>'s <paramref name="line"/> (a quest or a conversation) left choice <paramref name="from"/> for
    /// <paramref name="to"/> on <paramref name="condition"/>, by <paramref name="action"/>; <paramref name="n"/> counts the moves it has made.
    /// </summary>
    public static void Write(MachineLog? log, string role, string line, string? from, string? to, string condition, string action = Next, int n = 1) =>
        log?.Write("info", "actor.moved", [("role", role), ("line", line), ("from", from), ("to", to), ("condition", condition), ("action", action), ("n", n)]);
}
