namespace Daoris.Driver;

/// <summary>
/// <c>claimed</c>'s <c>work</c> output: the run's work, carried on its reach, which a node fed it binds on the run's branch at the tip the
/// pass reads (<see cref="RunInputs"/>). Its producer and its acceptance are the claim's, so what rests on it lapses with the claim.
/// </summary>
internal static class ClaimedWork
{
    /// <summary>The output's port.</summary>
    public const string Port = "work";

    /// <summary>The kind's outputs, each with its schema.</summary>
    public static readonly IReadOnlyDictionary<string, string> Outputs = new Dictionary<string, string>(StringComparer.Ordinal) { [Port] = WireValues.Work };

    /// <summary><paramref name="seen"/> with the work on its reach; any other observation as it is.</summary>
    public static NodeObservation Carried(NodeObservation seen) =>
        seen is { Outcome: NodeStates.Reached, Acceptance: { } accepted }
            ? seen with
            {
                Acceptance = accepted with
                {
                    Outputs = new Dictionary<string, NodePort>(StringComparer.Ordinal)
                    {
                        [Port] = new(WireValues.Work, accepted.Unit, null, accepted.Id) { Producer = new NodeProducer(accepted.Node, accepted.Attempt) },
                    },
                },
            }
            : seen;
}
