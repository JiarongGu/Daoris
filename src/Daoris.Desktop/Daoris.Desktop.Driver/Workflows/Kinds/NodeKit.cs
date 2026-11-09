using System.Reflection;
using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// The node kinds this build knows, built once from the driver's <see cref="NodeKindAttribute"/> marks with no runtime, so the save, the
/// palette, a run's binding and the run view read the one declaration each kind gives. <see cref="Find"/> is the one place a kind's name is split:
/// a built-in's unit, or a plugin's step read as <see cref="PluginStep"/>, with Daoris's half where its step name has one.
/// </summary>
public sealed class NodeKit
{
    /// <summary>The prefix of a half's name: <c>*/&lt;step&gt;</c> is Daoris's half of every plugin's step of that name.</summary>
    public const string AnyPlugin = "*/";

    private static readonly Lazy<NodeKit> Registered = new(() => From(Parts.Declared<NodeKindAttribute>(typeof(NodeKit).Assembly)));

    private static readonly PluginStep Plain = new();

    private readonly IReadOnlyList<(PartDeclaration Declared, INodeKind Unit)> _built;
    private readonly Dictionary<string, BuiltInNode> _units;
    private readonly Dictionary<string, PluginStep> _halves;
    private readonly Dictionary<string, BuiltInNode> _drawn;
    private readonly HashSet<string> _arriving;

    private NodeKit(IReadOnlyList<PartDeclaration> order, IReadOnlyList<(PartDeclaration Declared, INodeKind Unit)> built, IEnumerable<string>? arriving = null)
    {
        Order = order;
        _built = built;
        _arriving = arriving?.ToHashSet(StringComparer.Ordinal)
            ?? [.. built.Where(each => each.Declared.Type.GetCustomAttribute<NodeKindAttribute>()?.Arriving == true).Select(each => each.Declared.Name)];
        Units = [.. built.Select(each => each.Unit).OfType<BuiltInNode>()];
        _units = Units.ToDictionary(unit => unit.Row.Kind, StringComparer.Ordinal);
        _halves = built.Where(each => each.Unit is PluginStep)
            .ToDictionary(each => each.Declared.Name[AnyPlugin.Length..], each => (PluginStep)each.Unit, StringComparer.Ordinal);
        _drawn = new Dictionary<string, BuiltInNode>(StringComparer.Ordinal);
        foreach (var unit in Units) _drawn.TryAdd(unit.Part(new NodeDeclaration(unit.Row.Kind, unit.Row.Kind, 1, unit.Row.Reach)).Step, unit);
    }

    /// <summary>The kit this build registers.</summary>
    public static NodeKit Built => Registered.Value;

    /// <summary>Every built-in kind's id, in the editor's groups and their order.</summary>
    public static IReadOnlyList<string> Kinds => Built.Ids;

    /// <summary>The unit <paramref name="kind"/> reads as in this build, or null for a kind it does not know.</summary>
    public static INodeKind? Of(string kind) => Built.Find(kind);

    /// <summary>
    /// What the save refuses by the kinds: the protected order the gate kinds share (<see cref="ProtectedOrder"/>), then each node's kind's own
    /// rules of where it stands, each said against the node it is about, then, for a graph a person saves now, what the landing's readers need
    /// (<see cref="LandingRules"/>). The rules every graph is held to are <see cref="NodeGraphs.Problems"/>'.
    /// </summary>
    internal static IReadOnlyList<WireProblem> Problems(GraphStanding standing, NodeGraph graph, bool savedNow = false, NodeKit? kit = null) =>
    [
        .. ProtectedOrder.Problems(standing),
        .. standing.ById.Values.SelectMany(node => Of(node.Kind)?.Problems(graph, node) ?? []),
        .. savedNow ? LandingRules.Problems(standing) : [],
        .. savedNow ? ArrivingKinds.Problems(graph, kit ?? Built) : [],
    ];

    /// <summary>Whether <paramref name="kind"/> is declared before it is offered (<see cref="NodeKindAttribute.Arriving"/>).</summary>
    public bool IsArriving(string kind) => _arriving.Contains(kind);

    /// <summary>This kit with <paramref name="kind"/> marked arriving, as a build that declares it before offering it reads it.</summary>
    internal NodeKit Arriving(string kind) => new(Order, _built, [.. _arriving, kind]);

    /// <summary>A built-in kind's contract row, or null for any other kind.</summary>
    public static NodeAuthority? Row(string kind) => Built.Find(kind) is BuiltInNode unit ? unit.Row : null;

    /// <summary>
    /// The unit a run view's step of kind <paramref name="step"/> is drawn from: the built-in whose cell is drawn as that step (the work is a
    /// <c>claimed</c>'s, the landing a <c>land</c>'s), else the kind of that name.
    /// </summary>
    public static INodeKind? Drawn(string step) => Built._drawn.GetValueOrDefault(step) ?? Of(step);

    /// <summary>Whether a kind is a plugin's own step: <c>&lt;plugin&gt;/&lt;step&gt;</c>, both parts named.</summary>
    public static bool IsPlugin(string kind)
    {
        var slash = kind.IndexOf('/');
        return slash > 0 && slash < kind.Length - 1 && kind.IndexOf('/', slash + 1) < 0 && !kind.Any(char.IsWhiteSpace);
    }

    /// <summary>The marks in their order, the halves among them: the editor's groups are their phases.</summary>
    public IReadOnlyList<PartDeclaration> Order { get; }

    /// <summary>The built-in kinds' units, in order.</summary>
    public IReadOnlyList<BuiltInNode> Units { get; }

    /// <summary>The built-in kinds' ids, in order.</summary>
    public IReadOnlyList<string> Ids => [.. Units.Select(unit => unit.Row.Kind)];

    /// <summary>The unit <paramref name="kind"/> reads as in this kit, or null for a kind it does not know.</summary>
    public INodeKind? Find(string kind) =>
        _units.TryGetValue(kind, out var unit) ? unit
        : IsPlugin(kind) ? (_halves.GetValueOrDefault(kind[(kind.IndexOf('/') + 1)..]) ?? Plain) with { Kind = kind }
        : null;

    /// <summary>
    /// This kit as an older build's reads, one that lacks <paramref name="kind"/>: a declaration naming it is
    /// no kind there.
    /// </summary>
    public NodeKit Without(string kind) =>
        new(Order.Where(part => part.Name != kind).ToList(), [.. _built.Where(each => each.Declared.Name != kind)], _arriving.Where(each => each != kind));

    /// <exception cref="PartOrderException">
    /// A mark on a class that is no unit of its name's sort (a kind's <see cref="BuiltInNode"/>, a half's <see cref="PluginStep"/>), or a kind whose
    /// unit declares another kind's row.
    /// </exception>
    internal static NodeKit From(IReadOnlyList<PartDeclaration> order)
    {
        var set = Parts.Build<INodeKind>(order, type => (INodeKind)Activator.CreateInstance(type)!);
        var built = set.Order.Zip(Enum.GetValues<NodeGroup>().SelectMany(group => set.In(group))).ToList();
        foreach (var (part, unit) in built)
        {
            var half = part.Name.StartsWith(AnyPlugin, StringComparison.Ordinal);
            if (half ? unit is not PluginStep : unit is not BuiltInNode)
            {
                throw new PartOrderException($"the part {part.Name} ({part.Type.Name}) is no {(half ? nameof(PluginStep) : nameof(BuiltInNode))}");
            }

            if (unit is BuiltInNode { Row.Kind: var row } && row != part.Name)
            {
                throw new PartOrderException($"the part {part.Name} ({part.Type.Name}) declares the row of {row}");
            }
        }

        return new NodeKit(set.Order, built);
    }
}
