namespace Daoris.Driver;

/// <summary>A built-in kind a step graph may hold: who reaches and fails it, what the person may do to it, and its options.</summary>
/// <param name="Leaves">The wire kinds that may leave a node of this kind (WORKFLOWEDIT1b), which the step editor offers from it.</param>
/// <param name="RoleTakes">The works a role takes a node of this kind by, the step's own first; none where no agent session takes it.</param>
public sealed record PaletteKind(
    string Kind, string Reach, string ReachBy, string? FailureBy, bool PersonJudges, bool PersonSkips, bool MayEnd, bool Evidenced,
    string? SkipBy, IReadOnlyList<NodeOptionField> Options, IReadOnlyList<string> Leaves, IReadOnlyList<string> RoleTakes);

/// <summary>
/// A wire kind: whether it makes its target eligible (and so may be grouped), and the kinds it may leave. <see cref="From"/> is null where any
/// node may be its source; otherwise the built-in kinds that may, and a plugin's step beside them where its declaration allows.
/// </summary>
public sealed record PaletteWire(string Kind, bool Eligibility, IReadOnlyList<string>? From, string? To);

/// <summary>One step a plugin declares, as its manifest holds it, with the wire kinds that may leave it.</summary>
/// <param name="OutputsOn">DECIDE1d: each output a step carries on some of its outcomes alone, with the outcomes that carry it; an output left out is carried on every reach.</param>
/// <param name="Title">SETUPUX1f: the step's name in en and zh as its manifest wrote it, or null where it declares none.</param>
/// <param name="About">SETUPUX1f: a sentence of what the step does, in en and zh, or null.</param>
public sealed record PaletteStep(
    string Kind, int Version, string Reach, bool Rejectable, bool Skippable, IReadOnlyDictionary<string, string> Inputs,
    IReadOnlyDictionary<string, string> Outputs, string? Evidence, IReadOnlyList<string> Outcomes,
    IReadOnlyDictionary<string, IReadOnlyList<string>> OutputsOn, IReadOnlyList<string> Leaves, StepWords? Title = null, StepWords? About = null);

/// <summary>An installed plugin and the steps it declares.</summary>
public sealed record PalettePlugin(string Plugin, IReadOnlyList<PaletteStep> Steps);

/// <summary>What a step graph may hold.</summary>
public sealed record WorkflowPalette(IReadOnlyList<PaletteKind> Kinds, IReadOnlyList<PaletteWire> Wires, IReadOnlyList<PalettePlugin> Plugins)
{
    /// <summary>
    /// The works a role must take to take a step of <paramref name="kind"/> (ROLESTEP1b): the work step as design work, which a build role takes
    /// too, and the second opinion as a review. Empty for every other kind, which no agent session takes.
    /// </summary>
    public static IReadOnlyList<string> RoleTakesOf(string kind) => kind switch
    {
        NodeKinds.Claimed => [RoleWork.Design, RoleWork.Build],
        NodeKinds.Opinion => [RoleWork.Review],
        _ => [],
    };

    /// <summary>
    /// The palette, built from the code's own tables: each built-in kind's authority and options, each wire kind with the kinds that may leave
    /// it (a failed wire only a kind something can fail, a rejected wire only a kind a person may reject, a decision's route none, since no
    /// built-in is one, and a wait none, since its source is another unit's), and the plugins' declared steps.
    /// </summary>
    public static WorkflowPalette Build(IEnumerable<(string Plugin, IReadOnlyList<NodeDeclaration> Steps)> plugins)
    {
        var kinds = NodeKinds.BuiltIn
            .Select(kind => NodeAuthorities.BuiltIn[kind])
            .Select(each => new PaletteKind(each.Kind, each.Reach, each.ReachBy, each.FailureBy, each.PersonJudges, each.PersonSkips, each.MayEnd,
                each.Evidenced, each.SkipBy, NodeOptionFields.Of(each.Kind), Leaving(each, decision: false), RoleTakesOf(each.Kind)))
            .ToArray();
        IReadOnlyList<string> Leaves(Func<NodeAuthority, bool> may) =>
            [.. NodeKinds.BuiltIn.Where(kind => may(NodeAuthorities.BuiltIn[kind]))];
        var wires = WireKinds.All.Select(kind => new PaletteWire(
            kind,
            WireKinds.Eligibility.Contains(kind),
            kind switch
            {
                WireKinds.Failed => Leaves(each => each.FailureBy is not null),
                WireKinds.Rejected => Leaves(each => each.PersonJudges),
                WireKinds.When or WireKinds.Wait => [],
                _ => null,
            },
            kind == WireKinds.Wait ? NodeKinds.WaitUnit : null)).ToArray();
        var declared = plugins.Select(each => new PalettePlugin(each.Plugin,
            [.. each.Steps.Select(step => new PaletteStep(step.Kind, step.Version, step.Reach, step.Rejectable, step.Skippable, step.Inputs, step.Outputs,
                step.Evidence, step.Outcomes, step.OutputsOn, NodeAuthorities.Of(step) is { } authority ? Leaving(authority, step.Outcomes.Count > 0) : [],
                step.Title, step.About))])).ToArray();
        return new WorkflowPalette(kinds, wires, declared);
    }

    /// <summary>
    /// The wire kinds a node with <paramref name="authority"/> may be the source of, as the graph rules judge a wire's source: a failure's
    /// and a rejection's path where something can fail or reject it, a decision's route from a decision, and a way back where it has any of these.
    /// </summary>
    private static IReadOnlyList<string> Leaving(NodeAuthority authority, bool decision)
    {
        var fails = authority.FailureBy is not null;
        return [.. WireKinds.All.Where(kind => kind switch
        {
            WireKinds.Wait => false,
            WireKinds.Failed => fails,
            WireKinds.Rejected => authority.PersonJudges,
            WireKinds.When => decision,
            WireKinds.Back => fails || authority.PersonJudges || decision,
            _ => true,
        })];
    }
}
