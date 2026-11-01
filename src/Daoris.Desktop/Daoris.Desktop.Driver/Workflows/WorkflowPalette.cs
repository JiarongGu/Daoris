namespace Daoris.Driver;

/// <summary>A built-in kind a step graph may hold: who reaches and fails it, what the person may do to it, and its options.</summary>
public sealed record PaletteKind(
    string Kind, string Reach, string ReachBy, string? FailureBy, bool PersonJudges, bool PersonSkips, bool MayEnd, bool Evidenced,
    string? SkipBy, IReadOnlyList<NodeOptionField> Options);

/// <summary>
/// A wire kind: whether it makes its target eligible (and so may be grouped), and the kinds it may leave. <see cref="From"/> is null where any
/// node may be its source; otherwise the built-in kinds that may, and a plugin's step beside them where its declaration allows.
/// </summary>
public sealed record PaletteWire(string Kind, bool Eligibility, IReadOnlyList<string>? From, string? To);

/// <summary>One step a plugin declares, as its manifest holds it.</summary>
public sealed record PaletteStep(string Kind, string Reach, bool Rejectable, bool Skippable, IReadOnlyDictionary<string, string> Inputs, IReadOnlyDictionary<string, string> Outputs);

/// <summary>An installed plugin and the steps it declares.</summary>
public sealed record PalettePlugin(string Plugin, IReadOnlyList<PaletteStep> Steps);

/// <summary>What a step graph may hold.</summary>
public sealed record WorkflowPalette(IReadOnlyList<PaletteKind> Kinds, IReadOnlyList<PaletteWire> Wires, IReadOnlyList<PalettePlugin> Plugins)
{
    /// <summary>
    /// The palette, built from the code's own tables: each built-in kind's authority and options, each wire kind with the kinds that may leave
    /// it (a failed wire only a kind something can fail, a rejected wire only a kind a person may reject), and the plugins' declared steps.
    /// </summary>
    public static WorkflowPalette Build(IEnumerable<(string Plugin, IReadOnlyList<NodeDeclaration> Steps)> plugins)
    {
        var kinds = NodeKinds.BuiltIn
            .Select(kind => NodeAuthorities.BuiltIn[kind])
            .Select(each => new PaletteKind(each.Kind, each.Reach, each.ReachBy, each.FailureBy, each.PersonJudges, each.PersonSkips, each.MayEnd,
                each.Evidenced, each.SkipBy, NodeOptionFields.Of(each.Kind)))
            .ToArray();
        IReadOnlyList<string> Leaving(Func<NodeAuthority, bool> may) =>
            [.. NodeKinds.BuiltIn.Where(kind => may(NodeAuthorities.BuiltIn[kind]))];
        var wires = WireKinds.All.Select(kind => new PaletteWire(
            kind,
            WireKinds.Eligibility.Contains(kind),
            kind switch
            {
                WireKinds.Failed => Leaving(each => each.FailureBy is not null),
                WireKinds.Rejected => Leaving(each => each.PersonJudges),
                _ => null,
            },
            kind == WireKinds.Wait ? NodeKinds.WaitUnit : null)).ToArray();
        var declared = plugins.Select(each => new PalettePlugin(each.Plugin,
            [.. each.Steps.Select(step => new PaletteStep(step.Kind, step.Reach, step.Rejectable, step.Skippable, step.Inputs, step.Outputs))])).ToArray();
        return new WorkflowPalette(kinds, wires, declared);
    }
}
