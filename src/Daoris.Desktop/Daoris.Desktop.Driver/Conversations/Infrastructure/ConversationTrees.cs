namespace Daoris.Driver;

/// <summary>
/// What the disk says of the tree a conversation ran in (STRUCT3h, D166's note): whether it still stands, and what git says it
/// holds uncommitted. Conversations' Infrastructure: the resume's judgement (<see cref="Continuations"/>) takes the first as a
/// value, and the hand-over's judge and its take ask here from the Application.
/// </summary>
internal static class ConversationTrees
{
    /// <summary>Whether <paramref name="tree"/> is still there.</summary>
    public static bool Stands(string tree) => Directory.Exists(tree);

    /// <summary>What the tree holds uncommitted, as git says it, at most <paramref name="limit"/>; none where git cannot say.</summary>
    public static async Task<IReadOnlyList<string>> UncommittedAsync(string tree, int limit, CancellationToken ct)
    {
        try
        {
            return await WorkingTree.UncommittedAsync(tree, limit, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return [];
        }
    }
}
