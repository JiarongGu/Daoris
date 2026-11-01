namespace Daoris.Driver;

/// <summary>The type of a node's option, as an editor draws it.</summary>
public static class NodeOptionTypes
{
    public const string Choice = "choice";
    public const string Text = "text";
    public const string Switch = "switch";
    public const string Names = "names";
}

/// <summary>One option a kind's node takes: its name, its type, the choices of a <c>choice</c>, and whether the node needs it written.</summary>
public sealed record NodeOptionField(string Name, string Type, IReadOnlyList<string> Choices, bool Required);

/// <summary>
/// The options each built-in kind reads, with the choices the code holds for them. A kind not listed takes none. Land's
/// <c>accept</c> is not listed: the person's Accept completes the review step, and the landing step has none.
/// </summary>
public static class NodeOptionFields
{
    private static NodeOptionField Choice(string name, IReadOnlyList<string> choices, bool required = false) => new(name, NodeOptionTypes.Choice, choices, required);

    private static NodeOptionField Text(string name, bool required = false) => new(name, NodeOptionTypes.Text, [], required);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<NodeOptionField>> Table = new Dictionary<string, IReadOnlyList<NodeOptionField>>(StringComparer.Ordinal)
    {
        [NodeKinds.Land] =
        [
            Choice("method", LandNodes.Methods),
            Text("target"),
            Text("pattern"),
            Text("plugin"),
            new("squash", NodeOptionTypes.Switch, [], false),
            Text("message"),
        ],
        [NodeKinds.Integrate] = [Choice("request", LandingIntegrate.Requests), Choice("conflicts", LandingIntegrate.ConflictChoices)],
        [NodeKinds.Request] =
        [
            Text("to", required: true),
            Text("title", required: true),
            Text("body", required: true),
            Choice("start", RequestOptions.Starts),
            Choice("until", RequestOptions.Untils),
            Choice("declined", RequestOptions.Declines),
        ],
        [NodeKinds.Check] = [new("gates", NodeOptionTypes.Names, [], false)],
        [NodeKinds.GoAhead] = [Text("act")],
    };

    /// <summary>The options a built-in kind takes, in the order an editor lists them.</summary>
    public static IReadOnlyList<NodeOptionField> Of(string kind) => Table.TryGetValue(kind, out var fields) ? fields : [];
}
