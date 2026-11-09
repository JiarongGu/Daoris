using System.Text;
using LibGit2Sharp;

namespace Daoris.Driver;

/// <summary>
/// The paths two trees differ in, in one library call, printed as git's <c>diff --name-only --no-renames</c> prints them between two
/// commits: every changed file's path once, a rename as its two paths, sorted as git's tree order sorts them (a full path's bytes),
/// each ended by a newline or with <c>-z</c> a NUL. Null where git must answer.
/// </summary>
/// <remarks>
/// <para><b>Git's where it could differ.</b> A gitlink that changed, unless <c>--ignore-submodules=none</c> is asked, since a submodule's
/// <c>ignore</c> setting in <c>.gitmodules</c> or the configuration can hide it from git; without <c>-z</c>, a path git would quote
/// (a control character, a quote, a backslash or a byte past ASCII); and a path that is not UTF-8.</para>
/// </remarks>
internal static class GitLibraryNames
{
    internal static GitLibraryAnswer? Changed(GitLibraryCall call, Tree from, Tree to, bool nul, bool submodules)
    {
        var options = new CompareOptions { Similarity = SimilarityOptions.None, IncludeUnmodified = false };
        var paths = new List<string>();
        foreach (var change in call.Repository.Diff.Compare<TreeChanges>(from, to, options))
        {
            if (!submodules && (change.Mode == Mode.GitLink || change.OldMode == Mode.GitLink)) return null;
            paths.Add(change.Path);
        }

        var sorted = paths.Distinct(StringComparer.Ordinal).Select(path => (Path: path, Bytes: Encoding.UTF8.GetBytes(path))).ToList();
        sorted.Sort((one, other) => one.Bytes.AsSpan().SequenceCompareTo(other.Bytes));
        if (sorted.Any(each => each.Path.Contains('�') || (!nul && each.Path.Any(Quoted)))) return null;
        return new(0, string.Concat(sorted.Select(each => each.Path + (nul ? "\0" : "\n"))));

        static bool Quoted(char each) => each < ' ' || each > '~' || each is '"' or '\\';
    }
}
