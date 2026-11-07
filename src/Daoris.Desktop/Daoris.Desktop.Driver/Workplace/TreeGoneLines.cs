namespace Daoris.Driver;

/// <summary>
/// The line a merge would land into when its tree is gone (a tidy after a merge made by hand): no checkout is left to ask git, so the line is
/// the one the choices set, else the one the session branch grew from as its record keeps it. Never a guess in another folder: git asked with
/// no tree would read whatever repository the asking process stands in.
/// </summary>
internal static class TreeGoneLines
{
    /// <summary>What a plan names as its target where neither says a line, so no empty name is ever printed.</summary>
    public const string Unnamed = "its repository's line";

    /// <summary>The line for the gone tree <paramref name="full"/>, or null where neither the choices nor its branch's record names one.</summary>
    public static string? Of(TreeContext context, string full, string repository, string workspace)
    {
        if (CanonicalLine.Judged(context.Config(), repository, workspace, root: null, guessed: null)?.Branch is { } chosen) return chosen;
        var name = TreeFollows.SessionPrefix + Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return context.Grown.Following(repository, name)?.Line;
    }

    /// <summary>Why the plan names no line.</summary>
    public static string Problem(string repository) =>
        $"its tree is gone, and neither `{repository}`'s settings nor its session branch's record names the line it would merge into.";
}
