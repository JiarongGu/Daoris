using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What a lineage line of an agent's says of the child a call came from, as its <see cref="IChildVocabulary"/> reads it.</summary>
public enum ChildCallKind
{
    /// <summary>The line names no call this reading counts.</summary>
    None,

    /// <summary>A call from the child it names.</summary>
    Child,

    /// <summary>A call from the part's own agent, its root.</summary>
    Root,

    /// <summary>A call whose agent was sent and could not be read, or that names no id: never the root's.</summary>
    Unknown,

    /// <summary>A call no line of the agent's joins to an agent.</summary>
    Unjoined,
}

/// <summary>One line's reading (the design §2.3, row l): what it is, and the child it names where it names one.</summary>
public sealed record ChildCall(ChildCallKind Kind, string? Agent = null)
{
    public static ChildCall None { get; } = new(ChildCallKind.None);

    public static ChildCall Root { get; } = new(ChildCallKind.Root);

    public static ChildCall Unknown { get; } = new(ChildCallKind.Unknown);

    public static ChildCall Unjoined { get; } = new(ChildCallKind.Unjoined);

    public static ChildCall Of(string agent) => new(ChildCallKind.Child, agent);
}

/// <summary>
/// One record's lineage lines read in an agent's words, new per reading (the design §2.3, row l): every line of the agent's learnt first,
/// in no order, for who an id is, then each call read in the file's order as the child it came from.
/// </summary>
public interface IChildLines
{
    /// <summary>A line of this agent's, learnt before any call is read.</summary>
    void Learn(JsonObject line);

    /// <summary>The part's own agent as its lines name it; null where none said.</summary>
    string? Root { get; }

    /// <summary>Every id its lines name as the root's.</summary>
    IReadOnlyCollection<string> Roots { get; }

    /// <summary>What a call line of this agent's says, once every line is learnt.</summary>
    ChildCall Call(JsonObject line);
}

/// <summary>A lineage line's fields as the connector and the hook write them: text, and an id of the connector's shape.</summary>
public static class LineageText
{
    /// <summary>🔴 The connector's <c>CallLineage</c> id shape, copied (twins).</summary>
    public static readonly Regex IdShape = new("^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$", RegexOptions.CultureInvariant);

    public static string? Text(JsonObject line, string name) =>
        line[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    public static string? Id(JsonObject line, string name) => Text(line, name) is { } text && IdShape.IsMatch(text) ? text : null;

    /// <summary>Whether the connector named a field its agent sent that is not an id (FABRIC-C2a): the call is unknown, never the root's.</summary>
    public static bool Malformed(JsonObject line) => line["malformed"] is not null;
}
