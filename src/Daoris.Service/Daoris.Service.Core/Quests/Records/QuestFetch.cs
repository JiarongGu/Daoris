namespace Daoris.Knowledge;

/// <param name="Operations">A page of what a remote accepted, in its order, each carrying its number.</param>
/// <param name="Through">The last number the page covers — where the fetching machine's cursor moves.</param>
/// <param name="More">Whether another page follows.</param>
public sealed record QuestFetch(IReadOnlyList<QuestOperation> Operations, long Through, bool More)
{
    /// <summary>The remote's handshake at this fetch; null from a remote before it.</summary>
    public QuestClosure? Closure { get; init; }
}
