using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Which git reads are pure functions of object ids, and so may be kept for good (<see cref="GitObjectMemo"/>): <c>merge-base</c>,
/// <c>rev-list</c>, <c>cherry</c>, a two-commit <c>diff</c> of names and a <c>cat-file</c> of one object, each naming every commit by its full id
/// and reading no ref, <c>HEAD</c>, index, working tree or reflog; with <see cref="AddressedReads"/>' listings and searches of one commit.
/// Anything not named here is not kept: a lost qualification is a read asked again, never a stale answer.
/// </summary>
internal static class GitObjectArguments
{
    private static readonly Regex Id = new(@"^([0-9a-f]{40}|[0-9a-f]{64})$", RegexOptions.CultureInvariant);

    private static readonly Regex Revision = new(@"^\^?([0-9a-f]{40}|[0-9a-f]{64})(\.\.\.?([0-9a-f]{40}|[0-9a-f]{64}))?$", RegexOptions.CultureInvariant);

    private static readonly string[] MergeBases = ["--is-ancestor", "--independent", "--octopus", "--all"];

    private static readonly string[] Walks =
        ["--count", "--not", "--left-right", "--cherry-mark", "--cherry-pick", "--right-only", "--left-only", "--no-merges", "--merges", "--first-parent", "--reverse", "--topo-order", "--date-order"];

    private static readonly string[] Names = ["--name-only", "--name-status", "--numstat", "--shortstat", "-z", "--no-renames", "--no-ext-diff", "--no-color", "--ignore-submodules=none"];

    private static readonly string[] Objects = ["-t", "-s", "-p"];

    /// <summary>Whether <paramref name="arguments"/> ask a read an answer to which only ids decide.</summary>
    internal static bool Qualifies(IReadOnlyList<string> arguments)
    {
        var at = 0;
        while (at < arguments.Count && arguments[at].StartsWith('-'))
        {
            if (arguments[at] == "--no-optional-locks") at++;
            else if (arguments[at] == "-c" && at + 1 < arguments.Count && arguments[at + 1].Equals("core.quotePath=false", StringComparison.OrdinalIgnoreCase)) at += 2;
            else return false;
        }

        if (at >= arguments.Count) return false;
        var rest = arguments.Skip(at + 1).ToList();
        var ended = rest.IndexOf("--");
        var before = ended < 0 ? rest : rest[..ended];
        var options = before.Where(word => word.StartsWith('-')).ToList();
        var words = before.Where(word => !word.StartsWith('-')).ToList();
        return arguments[at] switch
        {
            "merge-base" => ended < 0 && words.Count >= 2 && words.All(Id.IsMatch) && options.All(MergeBases.Contains),
            "cherry" => ended < 0 && words.Count == 2 && words.All(Id.IsMatch) && options.All(option => option == "-v"),
            "rev-list" => words.Count > 0 && words.All(Revision.IsMatch) && options.All(Walks.Contains),
            "diff" => words.Count is 1 or 2 && words.All(Revision.IsMatch) && NamedPair(words) && options.All(Names.Contains)
                      && options.Contains("--no-renames") && options.Any(option => option is "--name-only" or "--name-status" or "--numstat" or "--shortstat"),
            "cat-file" => ended < 0 && words.Count == 1 && Id.IsMatch(words[0]) && options is [var one] && Objects.Contains(one),
            "ls-tree" or "grep" => AddressedReads.Addressed(arguments),
            _ => false,
        };
    }

    // A diff of one id alone compares it with the working tree.
    private static bool NamedPair(List<string> words) => words.Count == 2 || words[0].Contains("..", StringComparison.Ordinal);
}
