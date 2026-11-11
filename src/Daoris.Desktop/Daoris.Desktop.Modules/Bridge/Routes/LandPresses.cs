using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What an Accept that waits for its run's landing says where nothing landed yet, and how it reads the gate that judged it: the node door's
/// (<see cref="AcceptWaits"/>) and a conversation's (<see cref="ChatAccepts"/>) alike.
/// </summary>
public static class LandPresses
{
    /// <summary>What a press answers that kept the person's Accept, where the pass has not made the landing yet.</summary>
    public const string Landing = "Accepted; landing. Daoris lands it at its pass, and this window shows it when it has.";

    /// <summary>What it answers where the Accept carried the session on to clear its tree.</summary>
    public const string Carried = "Accepted; its session was carried on to clear its tree, and it lands when the tree is clean.";

    /// <summary>A press kept as the review's Accept (<see cref="EarlyAccepts"/>) answered accepted; null where the graph does not hold it.</summary>
    public static TreeLanding? Kept(LandingGate gate) =>
        gate.GraphHolds && gate.GraphPart() is { State: GraphGateStates.NotJudged or GraphGateStates.Waits } graph
            ? new TreeLanding(false, EarlyAccepts.Said(graph))
            : null;

    /// <summary>
    /// Whether what holds <paramref name="gate"/> is only the review the press itself just accepted: the pass it asked had not come within
    /// its bound, so the gate is the one read before its Accept. That is no refusal, and the press goes on to the landing's wait.
    /// </summary>
    public static bool HeldByOwnAccept(LandingGate gate, bool accepted) =>
        accepted && gate.GraphHolds && gate.GraphPart() is { State: GraphGateStates.Waits, Kind: NodeKinds.Review };
}
