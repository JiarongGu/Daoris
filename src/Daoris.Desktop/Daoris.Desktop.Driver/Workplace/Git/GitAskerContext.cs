using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a git asked in one folder reads beyond its repository's shared files: the asking tree's own configuration. Two working trees
/// of one repository share a memo's answer (a question by ids, a listing's derived tree) only where their contexts are equal, so tree
/// B with <c>core.useReplaceRefs=false</c> in its <c>config.worktree</c> is never answered from tree A's replaced commit.
/// </summary>
/// <remarks>
/// The context is the repository (its common directory), the asking tree's <c>config.worktree</c> by its bytes (read whether
/// <c>extensions.worktreeConfig</c> is set or not, which only parts more finely), and, where the shared configuration or Daoris's
/// global file holds an <c>includeIf</c> (evaluated per folder by <c>gitdir:</c> and per branch by <c>onbranch:</c>), the folder itself.
/// Nothing is started.
/// </remarks>
internal static class GitAskerContext
{
    /// <summary>The context of <paramref name="folder"/>; null where it is no repository's top.</summary>
    internal static string? Of(string folder)
    {
        if (RefFiles.CommonDirOf(folder) is not { } common) return null;
        var key = new StringBuilder(GitFacts.Folder(common));
        if (RefFiles.GitDirOf(folder) is { } own) key.Append('\n').Append(Stamp(Path.Combine(own, "config.worktree")));
        if (Conditional(Path.Combine(common, "config")) || (ProgramHome.Named is { } home && Conditional(GitGlobalFile.PathOf(home))))
        {
            key.Append('\n').Append(GitFacts.Folder(folder));
        }

        return key.ToString();
    }

    /// <summary>Whether a git asked in <paramref name="one"/> and one asked in <paramref name="other"/> read the same configuration.</summary>
    internal static bool Same(string one, string other) => Of(one) is { } context && string.Equals(context, Of(other), StringComparison.Ordinal);

    private static string Stamp(string file)
    {
        try
        {
            return File.Exists(file) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))) : "-";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Unreadable: a context of its own, shared with nothing.
            return Guid.NewGuid().ToString("N");
        }
    }

    private static bool Conditional(string file)
    {
        try
        {
            return File.Exists(file) && File.ReadAllText(file).Contains("includeif", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
