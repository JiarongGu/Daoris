using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// One typed transition receipt (FABRIC-WIRE1b; §E3), scoped to its graph, run and binding: what a downstream binding's
/// <see cref="NodeAcceptance.PrerequisiteReceipts"/> names, so losing it invalidates what rested on it.
/// </summary>
/// <param name="Id">
/// A reach's acceptance id; else a digest of its kind, graph, run and binding (<c>skp-</c>, <c>fld-</c>, <c>rej-</c>), worked out from its status's
/// own scope (FABRIC-RUN1f): its node's place where that names one, else the graph and the run as the scope names them.
/// </param>
/// <param name="Kind">One of <see cref="WireReceiptKinds"/>.</param>
/// <param name="Node">The node whose transition it is.</param>
public sealed record WireReceipt(string Id, string Kind, string Node)
{
    /// <summary>A reach's acceptance, whose evidence and outputs a <c>data</c> wire passes on; null on every other kind.</summary>
    public NodeAcceptance? Acceptance { get; init; }

    /// <summary>A reached decision's outcome, which a <c>when</c> wire follows (DECIDE1a); null on every other receipt.</summary>
    public string? Outcome { get; init; }
}
