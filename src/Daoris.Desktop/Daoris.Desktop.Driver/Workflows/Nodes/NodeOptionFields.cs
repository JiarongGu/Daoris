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

/// <summary>The fields a kind's options are written with (<see cref="INodeKind.Options"/>), and the one option two kinds share.</summary>
public static class NodeOptionFields
{
    /// <summary>
    /// The option an agent step names the role that takes it by: a role's id, on <c>claimed</c> and <c>opinion</c> alone, since
    /// no agent session takes Daoris's or a plugin's performed steps.
    /// </summary>
    public const string Role = "role";

    public static NodeOptionField Choice(string name, IReadOnlyList<string> choices, bool required = false) => new(name, NodeOptionTypes.Choice, choices, required);

    public static NodeOptionField Text(string name, bool required = false) => new(name, NodeOptionTypes.Text, [], required);

    public static NodeOptionField Switch(string name) => new(name, NodeOptionTypes.Switch, [], false);

    public static NodeOptionField Names(string name) => new(name, NodeOptionTypes.Names, [], false);
}
