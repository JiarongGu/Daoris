namespace Daoris.Driver;

/// <summary>
/// A kind the person acts on beyond the lifecycle's skip and stop (D247 §3.3): the acts it takes, which no other kind does, and those its node
/// offers where it stands.
/// </summary>
public interface IPressed
{
    /// <summary>The acts of its own: a word of one on a node of another kind is refused with <see cref="Refusal"/>.</summary>
    IReadOnlyList<string> Takes { get; }

    /// <summary>The refusal of a word of its acts on a node of another kind.</summary>
    string Refusal { get; }

    /// <summary>Its acts <paramref name="node"/> offers standing at <paramref name="status"/>, which its view lists before the skip and stop.</summary>
    IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status);
}
