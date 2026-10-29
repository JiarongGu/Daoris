namespace Daoris.Driver;

/// <summary>
/// What the disk says of the tree a conversation ran in (STRUCT3h, D166's note): whether it still stands. The resume's judgement
/// (<see cref="Continuations"/>), pure in the Domain, takes it as a value; the hand-over's judge and its take ask here. What the tree
/// holds uncommitted is the brief's own read (<see cref="ContextBriefs"/>).
/// </summary>
internal static class ConversationTrees
{
    /// <summary>Whether <paramref name="tree"/> is still there.</summary>
    public static bool Stands(string tree) => Directory.Exists(tree);
}
