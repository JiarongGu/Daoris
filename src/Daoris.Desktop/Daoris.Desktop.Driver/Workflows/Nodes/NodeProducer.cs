namespace Daoris.Driver;

/// <summary>The node and attempt that made a value on a port: what a reader of a <see cref="WireValues.Ref"/> or a problem is bound to.</summary>
/// <param name="Node">The producing node's id in its graph.</param>
/// <param name="Attempt">The attempt that made it, from 1.</param>
public sealed record NodeProducer(string Node, int Attempt);
