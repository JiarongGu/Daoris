using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>One trigger's firing (§E3): its wire started its target on one receipt. Its key is what the record writer keeps.</summary>
/// <param name="Key">A digest of the graph, run, wire and receipt: the same firing is the same key, so a replay fires nothing.</param>
public sealed record WireFiring(string Key, string Wire, string Receipt, string Node);
