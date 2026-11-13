namespace Daoris.Driver;

/// <summary>
/// The git reads the library answers in git's words, told by their arguments: <c>merge-base</c> of two commits, with
/// <c>--is-ancestor</c> or <c>--independent</c>; <c>rev-list --count</c> over tips, exclusions and ranges, and
/// <c>--left-right --count A...B</c>; and two commits' <c>diff --name-only --no-renames</c>. Every other read, and each of these the
/// admission or the call refuses, is git's.
/// </summary>
/// <remarks>
/// <para><b>One door.</b> <see cref="GitRuns"/> asks <see cref="Before"/> for every plain read beneath its flow memos, so a caller
/// asks the question as git's and is answered by whichever may; a test's stand-in runner keeps every read git's.</para>
/// <para><b>Shapes held exactly.</b> git's own options before the command may be <c>--no-optional-locks</c> and
/// <c>-c core.quotePath=false</c>, which change none of these answers; any other option, a path after <c>--</c>, a filter or rename
/// detection sends the read to git.</para>
/// </remarks>
internal static class GitLibraryReads
{
    private static readonly string[] NamesOptions = ["--name-only", "-z", "--no-renames", "--no-ext-diff", "--ignore-submodules=none"];

    /// <summary>The library's answer to the read <paramref name="arguments"/> in <paramref name="folder"/>; null where git answers it.</summary>
    internal static Task<GitLibraryAnswer?> AnswerAsync(string folder, IReadOnlyList<string> arguments, CancellationToken ct) =>
        Asked(arguments) is { } asked ? GitLibrary.AskAsync(folder, asked.Question, asked.Answer, ct) : Task.FromResult<GitLibraryAnswer?>(null);

    internal static (GitLibraryQuestion Question, Func<GitLibraryCall, GitLibraryAnswer?> Answer)? Asked(IReadOnlyList<string> arguments)
    {
        var at = 0;
        while (at < arguments.Count && arguments[at].StartsWith('-'))
        {
            if (arguments[at] == "--no-optional-locks") at++;
            else if (arguments[at] == "-c" && at + 1 < arguments.Count && arguments[at + 1].Equals("core.quotePath=false", StringComparison.OrdinalIgnoreCase)) at += 2;
            else return null;
        }

        if (at >= arguments.Count) return null;
        var rest = arguments.Skip(at + 1).ToList();
        return arguments[at] switch
        {
            "merge-base" => MergeBase(rest),
            "rev-list" => Counted(rest),
            "rev-parse" => GitLibraryVerify.Asked(rest),
            "diff" => Changed(rest),
            "for-each-ref" when rest.SequenceEqual(GitLibraryRefs.Listed) =>
                (GitLibraryQuestion.Refs, call => GitLibraryRefs.Listing(call) is { } listed ? new GitLibraryAnswer(0, listed) : null),
            _ => null,
        };
    }

    private static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>)? MergeBase(List<string> rest)
    {
        var names = rest.Where(each => !each.StartsWith('-')).ToList();
        var options = rest.Where(each => each.StartsWith('-')).ToList();
        return options switch
        {
            [] when names.Count == 2 => Walk(call => Commits(call, names) is [var one, var other] ? GitLibraryWalks.MergeBase(call, one, other) : null),
            ["--is-ancestor"] when names.Count == 2 && rest[0] == "--is-ancestor" =>
                Walk(call => Commits(call, names) is [var ancestor, var of] ? GitLibraryWalks.Ancestry(call, ancestor, of) : null),
            ["--independent"] when names.Count > 0 && rest[0] == "--independent" =>
                Walk(call => Commits(call, names) is { } commits ? GitLibraryWalks.Independent(call, commits) : null),
            _ => null,
        };
    }

    private static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>)? Counted(List<string> rest)
    {
        if (!rest.Contains("--count") || rest.Any(each => each.StartsWith('-') && each is not ("--count" or "--not" or "--left-right"))) return null;
        if (rest.Contains("--left-right"))
        {
            var sides = rest.Where(each => !each.StartsWith('-')).ToList();
            if (rest.Contains("--not") || sides is not [var range] || range.Split("...") is not [var left, var right]) return null;
            return Walk(call => Commits(call, [left, right]) is [var one, var other] ? GitLibraryWalks.Sides(call, one, other) : null);
        }

        var (tips, not, negated) = (new List<string>(), new List<string>(), false);
        foreach (var each in rest)
        {
            if (each == "--not") negated = !negated;
            else if (each == "--count") continue;
            else if (each.Contains("...", StringComparison.Ordinal)) return null;
            else if (each.Split("..") is [var from, var to])
            {
                if (negated) return null;
                not.Add(from);
                tips.Add(to);
            }
            else if (each.StartsWith('^')) (negated ? tips : not).Add(each[1..]);
            else (negated ? not : tips).Add(each);
        }

        if (tips.Count == 0) return null;
        return Walk(call => Commits(call, tips) is { } included && Commits(call, not) is { } excluded
            ? GitLibraryWalks.Count(call, included, excluded)
            : null);
    }

    private static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>)? Changed(List<string> rest)
    {
        var ended = rest.Count > 0 && rest[^1] == "--" ? rest.Take(rest.Count - 1).ToList() : rest;
        var names = ended.Where(each => !each.StartsWith('-')).ToList();
        var options = ended.Where(each => each.StartsWith('-')).ToList();
        if (names.Count != 2 || !options.Contains("--name-only") || !options.Contains("--no-renames") || options.Any(each => !NamesOptions.Contains(each))) return null;
        var (nul, submodules) = (options.Contains("-z"), options.Contains("--ignore-submodules=none"));
        return (GitLibraryQuestion.Names, call =>
            GitLibraryRevisions.TreeOf(call, names[0]) is { } from && GitLibraryRevisions.TreeOf(call, names[1]) is { } to
                ? GitLibraryNames.Changed(call, from, to, nul, submodules)
                : null);
    }

    private static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>) Walk(Func<GitLibraryCall, GitLibraryAnswer?> answer) =>
        (GitLibraryQuestion.Walks, answer);

    /// <summary>Each name's commit, in order; null where any is git's to read.</summary>
    private static List<LibGit2Sharp.Commit>? Commits(GitLibraryCall call, IReadOnlyList<string> names)
    {
        var commits = new List<LibGit2Sharp.Commit>();
        foreach (var name in names)
        {
            if (name.Length == 0 || GitLibraryRevisions.CommitOf(call, name) is not { } commit) return null;
            commits.Add(commit);
        }

        return commits;
    }
}
