namespace Daoris.Driver;

/// <summary>
/// A kind the person acts on beyond the lifecycle's skip and stop (D247 §3.3): the acts it takes, which no other kind does, those its node
/// offers where it stands, and the move each of them asks of the one table.
/// </summary>
public interface IPressed
{
    /// <summary>The acts of its own: a word of one on a node of another kind is refused with <see cref="Refusal"/>.</summary>
    IReadOnlyList<string> Takes { get; }

    /// <summary>The refusal of a word of its acts on a node of another kind.</summary>
    string Refusal { get; }

    /// <summary>Its acts <paramref name="node"/> offers standing at <paramref name="status"/>, which its view lists before the skip and stop.</summary>
    IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status);

    /// <summary>
    /// The move the person's <paramref name="word"/>, one of <see cref="Takes"/>, asks of the one table on <paramref name="node"/> standing at
    /// <paramref name="status"/> in the run's <paramref name="scope"/>; null for an act that moves no state, kept on the record as a mark.
    /// </summary>
    NodeMove? Move(NodeDeclaration node, NodeWord word, NodeStatus status, NodeScope scope);
}
