namespace Daoris.Driver;

/// <summary>
/// The step of its run's workflow a session performs: the graph's work (<c>claimed</c>) node and the attempt of it. A session's first turn
/// names it at its open, and its wait on the next step names it when its turn ends.
/// </summary>
/// <param name="Node">The work node's id in the graph the run is bound to.</param>
/// <param name="Attempt">The attempt of it this session's first turn is.</param>
public sealed record WorkStep(string Node, int Attempt = 1);
