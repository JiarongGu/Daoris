namespace Daoris.Driver;

/// <summary>A built-in kind a step graph may hold: who reaches and fails it, what the person may do to it, and its options.</summary>
/// <param name="Leaves">The wire kinds that may leave a node of this kind (WORKFLOWEDIT1b), which the step editor offers from it.</param>
/// <param name="RoleTakes">The works a role takes a node of this kind by, the step's own first; none where no agent session takes it.</param>
/// <param name="Offered">
/// How the editor offers the kind (WORKFLOWCLEAN1 1b): <c>alone</c>, a step to add; <c>tail:&lt;method&gt;</c>, a step a <c>land</c> of that method brings
/// with it. The page reads it and keeps no list of its own.
/// </param>
public sealed record PaletteKind(
    string Kind, string Reach, string ReachBy, string? FailureBy, bool PersonJudges, bool PersonSkips, bool MayEnd, bool Evidenced,
    string? SkipBy, IReadOnlyList<NodeOptionField> Options, IReadOnlyList<string> Leaves, IReadOnlyList<string> RoleTakes, string Offered = PaletteKind.Alone)
{
    public const string Alone = "alone";

    /// <summary>The outcomes a node of the kind is reached with, which a step added in the editor declares: a monitor's; none on the rest.</summary>
    public IReadOnlyList<string> Outcomes { get; init; } = [];
}

/// <summary>
/// A wire kind: whether it makes its target eligible (and so may be grouped), and the kinds it may leave. <see cref="From"/> is null where any
/// node may be its source; otherwise the built-in kinds that may, and a plugin's step beside them where its declaration allows.
/// </summary>
public sealed record PaletteWire(string Kind, bool Eligibility, IReadOnlyList<string>? From, string? To);

/// <summary>One step a plugin declares, as its manifest holds it, with the wire kinds that may leave it.</summary>
/// <param name="OutputsOn">DECIDE1d: each output a step carries on some of its outcomes alone, with the outcomes that carry it; an output left out is carried on every reach.</param>
/// <param name="Title">SETUPUX1f: the step's name as its manifest wrote it (authored text), or null where it declares none.</param>
/// <param name="About">SETUPUX1f: a sentence of what the step does (authored text), or null.</param>
public sealed record PaletteStep(
    string Kind, int Version, string Reach, bool Rejectable, bool Skippable, IReadOnlyDictionary<string, string> Inputs,
    IReadOnlyDictionary<string, string> Outputs, string? Evidence, IReadOnlyList<string> Outcomes,
    IReadOnlyDictionary<string, IReadOnlyList<string>> OutputsOn, IReadOnlyList<string> Leaves, AuthoredText? Title = null, AuthoredText? About = null);

/// <summary>An installed plugin and the steps it declares.</summary>
public sealed record PalettePlugin(string Plugin, IReadOnlyList<PaletteStep> Steps);

/// <summary>What a step graph may hold.</summary>
public sealed record WorkflowPalette(IReadOnlyList<PaletteKind> Kinds, IReadOnlyList<PaletteWire> Wires, IReadOnlyList<PalettePlugin> Plugins)
{
    /// <summary>
    /// The palette, built from the kinds' own units (<see cref="NodeKit"/>), in the kit's order: each built-in kind's authority, options, role and
    /// offer, each wire kind with the kinds that may leave it (a failed wire only a kind something can fail, a rejected wire only a kind a person
    /// may reject, a decision's route only a kind reached with outcomes (a monitor), and a wait none, since its source is another unit's), and the
    /// plugins' declared steps.
    /// </summary>
    public static WorkflowPalette Build(IEnumerable<(string Plugin, IReadOnlyList<NodeDeclaration> Steps)> plugins)
    {
        var units = NodeKit.Built.Units;
        var kinds = units
            .Select(unit => (Row: unit.Row, Unit: unit))
            .Select(each => new PaletteKind(each.Row.Kind, each.Row.Reach, each.Row.ReachBy, each.Row.FailureBy, each.Row.PersonJudges, each.Row.PersonSkips,
                each.Row.MayEnd, each.Row.Evidenced, each.Row.SkipBy, each.Unit.Options, Leaving(each.Row, decision: each.Unit.Outcomes.Count > 0), each.Unit.RoleTakes,
                each.Unit.Offered) { Outcomes = each.Unit.Outcomes })
            .ToArray();
        IReadOnlyList<string> Leaves(Func<NodeAuthority, bool> may) => [.. units.Where(unit => may(unit.Row)).Select(unit => unit.Row.Kind)];
        var wires = WireKinds.All.Select(kind => new PaletteWire(
            kind,
            WireKinds.Eligibility.Contains(kind),
            kind switch
            {
                WireKinds.Failed => Leaves(each => each.FailureBy is not null),
                WireKinds.Rejected => Leaves(each => each.PersonJudges),
                WireKinds.When => [.. units.Where(unit => unit.Outcomes.Count > 0).Select(unit => unit.Row.Kind)],
                WireKinds.Wait => [],
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
    /// The palette as Ask Daoris is handed it beside an open draft (FLOWCHART2d; the flowchart design §8.3), one line a kind: each built-in
    /// kind's reach, its options with their choices and the ways that may leave it, then each plugin step's version, reach, outcomes,
    /// inputs, outputs (and the outcomes that carry each), evidence and ways. Agent-facing English, never shown.
    /// </summary>
    public string Compact()
    {
        static string Names(IEnumerable<string> names) => string.Join(", ", names.Select(name => $"`{name}`"));
        static string Ports(IReadOnlyDictionary<string, string> ports) => string.Join(", ", ports.Select(port => $"`{port.Key}` ({port.Value})"));
        var text = new System.Text.StringBuilder("Built-in kinds:\n");
        foreach (var kind in Kinds)
        {
            var options = kind.Options.Select(option =>
                $"`{option.Name}` ({option.Type}{(option.Choices.Count > 0 ? $": {string.Join("|", option.Choices)}" : "")}{(option.Required ? ", required" : "")})");
            text.Append($"- `{kind.Kind}`: {kind.Reach}");
            if (kind.Outcomes.Count > 0) text.Append($"; outcomes {Names(kind.Outcomes)}");
            if (kind.Options.Count > 0) text.Append($"; options {string.Join(", ", options)}");
            text.Append($"; ways on {string.Join(", ", kind.Leaves)}{(kind.MayEnd ? "; may end the work" : "")}\n");
        }

        if (Plugins.Count > 0) text.Append("Plugins' steps:\n");
        foreach (var step in Plugins.SelectMany(plugin => plugin.Steps))
        {
            text.Append($"- `{step.Kind}` v{step.Version}: {step.Reach}");
            if (step.Outcomes.Count > 0) text.Append($"; outcomes {Names(step.Outcomes)}");
            if (step.Inputs.Count > 0) text.Append($"; inputs {Ports(step.Inputs)}");
            if (step.Outputs.Count > 0)
            {
                text.Append("; outputs ").Append(string.Join(", ", step.Outputs.Select(port => step.OutputsOn.TryGetValue(port.Key, out var on)
                    ? $"`{port.Key}` ({port.Value}, on {Names(on)})"
                    : $"`{port.Key}` ({port.Value})")));
            }

            if (step.Evidence is { } evidence) text.Append($"; evidence {evidence}");
            text.Append($"; ways on {string.Join(", ", step.Leaves)}\n");
        }

        return text.ToString().TrimEnd('\n');
    }

    /// <summary>The digest a compact palette is told under: its first 16 hex digits of SHA-256, prefixed <c>p1-</c>.</summary>
    public static string DigestOf(string compact) =>
        "p1-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(compact)))[..16].ToLowerInvariant();

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
