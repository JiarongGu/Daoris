namespace Daoris.Driver;

/// <summary>
/// A kind whose step reads a target's tip: the ref a node of it moves onto or compares with. The pass reads that tip and the step is bound to
/// it, under the bound on a target's move (<see cref="WireTargets"/>).
/// </summary>
internal interface ITargets
{
    /// <summary>The full ref <paramref name="node"/> reads the tip of, or null where its options name none this build can read.</summary>
    string? Target(NodeDeclaration node);
}
