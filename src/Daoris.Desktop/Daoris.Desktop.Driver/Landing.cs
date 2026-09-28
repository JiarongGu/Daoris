using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The forms a landing rule takes (WSR1, D87). Pushing and opening a pull request is a plugin's form, not yet built (WSR4).</summary>
public static class LandingForm
{
    /// <summary>Into the repository's line (D86), in its own checkout — the merge door as it always was.</summary>
    public const string Merge = "merge";

    /// <summary>Onto a new branch named by a pattern, from the session's branch, for the person to push.</summary>
    public const string Branch = "branch";
}

/// <summary>
/// How a session's work lands: a form, for <see cref="LandingForm.Branch"/> the pattern that names the
/// branch, and whether the tree and its branch go once a press lands the work (<paramref name="Tidy"/>,
/// D88) — only where git proves the work is on a branch of the person's.
/// </summary>
public sealed record LandingRule(string Form, string? Pattern = null, bool Tidy = false)
{
    public static readonly LandingRule Merge = new(LandingForm.Merge);
}

/// <summary>Where a landing rule came from, for the screen and the sentences that name it.</summary>
public static class LandingSource
{
    public const string Repository = "repository";
    public const string Workspace = "workspace";
    public const string Default = "default";
}

/// <summary>A repository's landing rule and what said so.</summary>
public sealed record Landing(LandingRule Rule, string Source);

/// <summary>What a pattern is expanded from: the session's quest (or the session itself), its title's words, the repository.</summary>
public sealed record LandingNames(string Quest, string Slug, string Repository, string Session);

/// <summary>The session a press lands: its id, and its quest and that quest's title where it has one.</summary>
public sealed record LandingSubject(string Session, string? Quest, string? Title);

/// <summary>What a press would do: the form, where the work would go (the line, or the branch it would make), and what said so.</summary>
public sealed record LandingPlan(string Form, string Target, string Source);

/// <summary>What came of a press. A refusal is an answer, as the merge door's are, and names what the person would do.</summary>
public sealed record TreeLanding(bool Landed, string Message, string? Branch = null);

/// <summary>What a session branch holds, for the clean-up's list (D88). Only the first two go.</summary>
public static class SweepKind
{
    /// <summary>Nothing beyond the line.</summary>
    public const string Empty = "empty";

    /// <summary>Every commit on a branch of the person's.</summary>
    public const string Landed = "landed";

    /// <summary>Commits only Daoris's branches hold — kept, and named.</summary>
    public const string Unlanded = "unlanded";

    /// <summary>Its tree holds uncommitted work — kept.</summary>
    public const string Dirty = "dirty";

    /// <summary>A session still running or waiting names its tree — kept.</summary>
    public const string InUse = "in-use";
}

/// <param name="Commits">Unlanded: how many only Daoris holds. Landed: how many it carried.</param>
/// <param name="Where">Landed: the first branch of the person's that holds it. Empty: the line.</param>
/// <param name="Detail">Git's own lines where they say more: the unlanded commits, the uncommitted count.</param>
public sealed record SweepItem(
    string Repository, string Workspace, string Branch, string? Tree, string Kind, int Commits, string? Where, string? Detail)
{
    public bool Removable => Kind is SweepKind.Empty or SweepKind.Landed;
}

/// <summary>What the clean-up did with one branch, in the driver's words.</summary>
public sealed record SweepResult(SweepItem Item, bool Removed, string Message);

/// <summary>
/// The landing rules (WSR1, D87): which applies to a repository, whether a pattern can name a branch,
/// and what it names for one session. The CLI's <c>driverconfig.ts</c> holds the same pattern rule.
/// </summary>
public static class LandingRules
{
    /// <summary>What a pattern may say. One of the first two is required, or every session's work would go to one branch.</summary>
    public static readonly IReadOnlyList<string> Placeholders = ["quest", "session", "slug", "repository"];

    /// <summary>What a pattern is tried against before it is kept — the CLI's twin tries the same names.</summary>
    private static readonly LandingNames Sample = new("0fda18", "sample-work", "engine", "s1a2b3c4");

    private static readonly Regex Placeholder = new(@"\{([^{}]*)\}", RegexOptions.CultureInvariant);

    /// <summary>What is wrong with a rule, in a sentence, or null when it can land work.</summary>
    public static string? Problem(LandingRule rule) => rule.Form switch
    {
        LandingForm.Merge => null,
        LandingForm.Branch => Problem(rule.Pattern ?? ""),
        _ => $"`{rule.Form}` is not a way work lands here — `{LandingForm.Merge}` or `{LandingForm.Branch}`. "
             + "Pushing and opening a pull request is a plugin's to do, and none can yet.",
    };

    /// <summary>What is wrong with a branch pattern, in a sentence, or null when it names one branch per session.</summary>
    public static string? Problem(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "a branch pattern needs a name — e.g. `feature/{quest}-{slug}`.";
        }

        foreach (Match used in Placeholder.Matches(pattern))
        {
            if (!Placeholders.Contains(used.Groups[1].Value))
            {
                return $"`{used.Value}` is not something a pattern can say — "
                    + string.Join(", ", Placeholders.Select(name => $"`{{{name}}}`")) + ".";
            }
        }

        if (!pattern.Contains("{quest}", StringComparison.Ordinal) && !pattern.Contains("{session}", StringComparison.Ordinal))
        {
            return "a pattern needs `{quest}` or `{session}`, or every session's work would be put on one branch.";
        }

        var sample = Expand(pattern, Sample);
        return sample.Contains('{') || sample.Contains('}') || !BranchName.IsValid(sample)
            ? $"`{pattern}` does not make a branch name git would take — it gives `{sample}`."
            : null;
    }

    public static string Expand(string pattern, LandingNames names) => pattern
        .Replace("{quest}", names.Quest, StringComparison.Ordinal)
        .Replace("{session}", names.Session, StringComparison.Ordinal)
        .Replace("{slug}", names.Slug, StringComparison.Ordinal)
        .Replace("{repository}", names.Repository, StringComparison.Ordinal);

    /// <summary>
    /// A title in the words git takes: lower-case letters and digits, joined by hyphens, whole words up to
    /// 40 characters. A title with none of those — one written in another script — is `work`.
    /// </summary>
    public static string Slug(string? title)
    {
        var slug = new StringBuilder();
        foreach (var word in Regex.Split((title ?? "").ToLowerInvariant(), "[^a-z0-9]+").Where(word => word.Length > 0))
        {
            if (slug.Length + (slug.Length > 0 ? 1 : 0) + word.Length > 40) break;
            if (slug.Length > 0) slug.Append('-');
            slug.Append(word);
        }

        return slug.Length > 0 ? slug.ToString() : "work";
    }

    /// <summary>The repository's rule, else its workspace's (a repository in no workspace is in `default`'s), else merge.</summary>
    public static Landing Choose(DriverConfig config, string repository, string? workspace)
    {
        if (config.Landings.TryGetValue(repository, out var own)) return new(own, LandingSource.Repository);
        if (config.WorkspaceLandings.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)) return new(shared, LandingSource.Workspace);
        return new(LandingRule.Merge, LandingSource.Default);
    }
}
