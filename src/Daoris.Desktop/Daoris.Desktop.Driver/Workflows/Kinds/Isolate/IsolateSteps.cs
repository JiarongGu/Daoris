using System.Globalization;

namespace Daoris.Driver;

/// <summary>An <c>isolate</c> node read as its <see cref="IsolateStep"/>, its options checked, and a step written back as a node.</summary>
public static class IsolateSteps
{
    /// <summary>The first option of <paramref name="node"/> its choices do not hold, or null.</summary>
    public static string? Problem(NodeDeclaration node)
    {
        foreach (var (name, choices) in new (string, IReadOnlyList<string>)[]
                 {
                     (IsolateStep.KindOption, IsolateStep.Kinds), (IsolateStep.AtOnceOption, IsolateStep.Counts), (IsolateStep.OverlapOption, IsolateStep.Overlaps),
                 })
        {
            if (node.Options.ContainsKey(name) && !(NodeOptions.Text(node, name) is { } text && choices.Contains(text, StringComparer.Ordinal))) return name;
        }

        return null;
    }

    public static IsolateStep Of(NodeDeclaration node) => new(
        NodeOptions.Text(node, IsolateStep.KindOption) ?? IsolateStep.Auto,
        int.TryParse(NodeOptions.Text(node, IsolateStep.AtOnceOption), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null,
        NodeOptions.Text(node, IsolateStep.OverlapOption) == IsolateStep.Wait);

    /// <summary>The graph's isolate step, or null where it has none.</summary>
    public static IsolateStep? In(NodeGraph? graph) =>
        graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Isolate) is { } step ? Of(step) : null;

    /// <summary>The step written as a node, its options those that differ from an unwritten one's.</summary>
    public static NodeDeclaration Declaration(IsolateStep step, string id)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        if (step.Kind != IsolateStep.Auto) options[IsolateStep.KindOption] = $"\"{step.Kind}\"";
        if (step.AtOnce is { } count) options[IsolateStep.AtOnceOption] = $"\"{count.ToString(CultureInfo.InvariantCulture)}\"";
        if (step.OverlapWaits) options[IsolateStep.OverlapOption] = $"\"{IsolateStep.Wait}\"";
        return new NodeDeclaration(id, NodeKinds.Isolate, 1, NodeReach.Observed) { Options = options };
    }
}
