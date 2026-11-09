namespace Daoris.Driver;

/// <summary>
/// The ports and outcomes a kind already offered declares before the change that wires them, as an arriving kind is declared before it is
/// offered: the chart and the run view draw and word them, the palette leaves them out, and a person's save refuses a graph that names one
/// (<see cref="ArrivingKinds"/>), so the contracts the page reads are generated once.
/// </summary>
public sealed record NodeArrivals(IReadOnlyDictionary<string, string> Inputs, IReadOnlyDictionary<string, string> Outputs, IReadOnlyList<string> Outcomes)
{
    public static readonly NodeArrivals None = new(NodePorts.None, NodePorts.None, []);

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoneOn = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>The inputs named, each with its schema, and nothing else arriving.</summary>
    public static NodeArrivals Taking(params (string Port, string Schema)[] inputs) =>
        None with { Inputs = inputs.ToDictionary(each => each.Port, each => each.Schema, StringComparer.Ordinal) };
}
