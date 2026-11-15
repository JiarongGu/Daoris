namespace Daoris.Testing;

/// <summary>What a test needs of a git checkout when no git runs: the folder that marks a place as one.</summary>
public static class CheckoutMarks
{
    /// <summary>Makes <paramref name="folder"/> read as a git checkout, the way the in-memory repositories mark theirs.</summary>
    public static void Mark(string folder) => Directory.CreateDirectory(Path.Combine(folder, ".git"));
}
