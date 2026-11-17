using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where one review of <see cref="ReviewGraphCensusTests"/> is read: its scratch, the Daoris home under it and the session's tree, with
/// the git directory the untracked read's private index is named in and the untracked files the worlds list. A case runs a line's
/// thirty combinations at once, each at a place of its own, so the config and the grown record one world writes are never another's:
/// one after another, the class took ten seconds alone.
/// </summary>
internal sealed record ReviewGraphPlace(string Scratch, string Home, string Tree)
{
    public static ReviewGraphPlace Make(string scratch, string session)
    {
        var home = Path.Combine(scratch, "daoris-home");
        Directory.CreateDirectory(home);
        var tree = Path.Combine(DaorisHome.Entry(home, HomeEntry.Trees), "games", "engine", session);
        Directory.CreateDirectory(Path.Combine(tree, ".git"));
        foreach (var name in new[] { "own.txt", "u0.txt", "u1.txt" }) File.WriteAllText(Path.Combine(tree, name), "mine\n");
        return new(scratch, home, tree);
    }
}
