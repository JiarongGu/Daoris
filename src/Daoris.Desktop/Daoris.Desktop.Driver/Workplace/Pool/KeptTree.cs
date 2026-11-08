namespace Daoris.Driver;

/// <summary>What a kept slot is doing between lines.</summary>
public static class KeptStates
{
    /// <summary>Serves lines.</summary>
    public const string Kept = "kept";

    /// <summary>Takes no new line and is let go when it is free.</summary>
    public const string Retiring = "retiring";

    /// <summary>Left where a refusal found it, until the person reads why.</summary>
    public const string Stuck = "stuck";
}

/// <summary>
/// One kept slot of a repository's pool: a linked worktree at a fixed folder, numbered, serving at most one line. The folder's name is
/// the number and no record names it; a line is found in its slot through the store alone (<see cref="TreePool.WhereIs"/>).
/// </summary>
/// <param name="Registration">The name git registers the worktree under.</param>
/// <param name="Path">The slot's fixed folder.</param>
/// <param name="Line">The line's path whose branch the slot is on; empty when it is detached.</param>
/// <param name="Tip">The commit HEAD stands at while the slot is free; empty while a line holds it.</param>
/// <param name="Said">Why the slot is held or stuck; empty otherwise.</param>
/// <param name="Since">The moment its present line took it.</param>
/// <param name="Bytes">What it holds ignored outside <c>node_modules</c>, read at the last let-go.</param>
/// <param name="Toolchain">The key of its rules' <c>toolchain</c> outputs at the last take.</param>
public sealed record KeptTree(
    string Workspace, string Repository, int Number, string Registration, string Path, string Line, string Tip, string State, string Said,
    DateTimeOffset Since, long Bytes, string Toolchain)
{
    /// <summary>What a slot's folder starts with, beside a fresh tree's <c>s-</c>.</summary>
    public const string Prefix = "k-";

    /// <summary>The folder of slot <paramref name="number"/> under a repository's trees folder.</summary>
    public static string FolderIn(string repositoryTrees, int number) => System.IO.Path.Combine(repositoryTrees, $"{Prefix}{number}");

    /// <summary>Whether <paramref name="folder"/> is named as a slot's folder is.</summary>
    public static bool IsSlotFolder(string folder) => System.IO.Path.GetFileName(folder).StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Whether a line has the slot.</summary>
    public bool Held => Line.Length > 0;
}
