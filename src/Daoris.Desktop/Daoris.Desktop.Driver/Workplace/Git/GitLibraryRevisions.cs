using LibGit2Sharp;

namespace Daoris.Driver;

/// <summary>
/// A revision a walk or a names question names, read in one call as git reads it, or null where git must read it: a full id,
/// <c>HEAD</c>, a full ref name, or a short name by git's rules, the first rule a ref answers winning as git's does, before git
/// would read it as a short id.
/// </summary>
/// <remarks>A name holding an operator (<c>^</c>, <c>~</c>, <c>:</c>, <c>@</c>, <c>..</c>), one no ref answers (which git could read as
/// a short id), or one a file in the git directory could answer before any ref is git's to read; so is one whose object is not here.</remarks>
internal static class GitLibraryRevisions
{
    private static readonly string[] Rules = ["refs/{0}", "refs/tags/{0}", "refs/heads/{0}", "refs/remotes/{0}", "refs/remotes/{0}/HEAD"];

    /// <summary>The commit <paramref name="name"/> names, a tag peeled to it; null where git must say.</summary>
    internal static Commit? CommitOf(GitLibraryCall call, string name) => Peeled(Named(call, name)) as Commit;

    /// <summary>The tree <paramref name="name"/> names, a commit's or a tag's peeled to it; null where git must say.</summary>
    internal static Tree? TreeOf(GitLibraryCall call, string name) => Peeled(Named(call, name)) switch
    {
        Commit commit => commit.Tree,
        Tree tree => tree,
        _ => null,
    };

    private static GitObject? Named(GitLibraryCall call, string name)
    {
        var repository = call.Repository;
        if (name.Length == 40 && name.All(char.IsAsciiHexDigit)) return repository.Lookup(new ObjectId(name));
        if (name.Length == 0 || name.StartsWith('-') || name.Contains("..", StringComparison.Ordinal)
            || name.IndexOfAny(['^', '~', ':', '@', '*', '?', '[', '\\', ' ']) >= 0)
        {
            return null;
        }

        if (name == "HEAD") return repository.Info.IsHeadUnborn ? null : repository.Head.Tip;
        if (name.StartsWith("refs/", StringComparison.Ordinal)) return Target(repository.Refs[name]);
        if (File.Exists(Path.Combine(call.GitDir, name)) || File.Exists(Path.Combine(call.CommonDir, name))) return null;
        foreach (var rule in Rules)
        {
            if (repository.Refs[string.Format(rule, name)] is { } reference) return Target(reference);
        }

        return null;
    }

    private static GitObject? Target(Reference? reference) => reference?.ResolveToDirectReference()?.Target;

    private static GitObject? Peeled(GitObject? found) => found is TagAnnotation tag ? Peeled(tag.Target) : found;
}
