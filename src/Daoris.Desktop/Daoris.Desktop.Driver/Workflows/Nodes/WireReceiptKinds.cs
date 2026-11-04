using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>The kinds of a typed transition receipt (§E3): the one transition of its source that an eligibility wire reads.</summary>
public static class WireReceiptKinds
{
    /// <summary>
    /// A current <c>reached</c> acceptance: read by <c>after</c>, <c>data</c> and <c>trigger</c> wires, its id the acceptance's; and by a
    /// <c>when</c> wire on its outcome alone, through <see cref="WireEvaluation.OutcomeReceipt"/> (DECIDE1a).
    /// </summary>
    public const string Reached = "reached";

    /// <summary>The person's skip for this graph and run: read by <c>after</c> wires alone, since a skipped node produces no outputs (§E2).</summary>
    public const string Skipped = "skipped";

    /// <summary>A failure with its declared retries exhausted, on this graph's binding: read by <c>failed</c> wires alone.</summary>
    public const string Failed = "failed";

    /// <summary>The person's recorded rejection, on this graph's binding: read by <c>rejected</c> wires alone.</summary>
    public const string Rejected = "rejected";
}
