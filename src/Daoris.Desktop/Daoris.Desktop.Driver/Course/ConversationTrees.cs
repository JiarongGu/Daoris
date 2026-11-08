namespace Daoris.Driver;

/// <summary>What the disk and the home's pool say of the tree a conversation ran in, which the resume's judgement and the hand-over ask.</summary>
internal static class ConversationTrees
{
    /// <summary>Whether <paramref name="tree"/> stands, or a slot can be switched back to its line; with no home the disk alone answers.</summary>
    public static bool Stands(string? home, string tree) =>
        home is null ? Directory.Exists(tree) : new LinePlaces(home).Of(tree).StandsOrCanBeHad;
}
