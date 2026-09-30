using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The forms a landing rule takes (WSR1, D87). Pushing and opening a pull request is not a form of its
/// own: a branch rule names the plugin that does it (WSR4, D100).
/// </summary>
public static class LandingForm
{
    /// <summary>Into the repository's line (D86), in its own checkout — the merge door as it always was.</summary>
    public const string Merge = "merge";

    /// <summary>Onto a new branch named by a pattern, from the session's branch, for the person — or the rule's plugin — to push.</summary>
    public const string Branch = "branch";
}

/// <summary>
/// How a session's work lands: a form, for <see cref="LandingForm.Branch"/> the pattern that names the
/// branch, and whether the tree and its branch go once a press lands the work (<paramref name="Tidy"/>,
/// D88) — only where git proves the work is on a branch of the person's.
/// </summary>
/// <param name="Plugin">
/// The plugin a branch rule hands the new branch to (WSR4, D100): spoken to on <see cref="HookPoints.Land"/>
/// once the branch exists, it pushes and opens the pull request for its platform. Null — the default — is
/// the branch left for the person to push, as it always was.
/// </param>
public sealed record LandingRule(string Form, string? Pattern = null, bool Tidy = false, string? Plugin = null)
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
/// <param name="Plugin">The plugin that pushes the branch once it is made (D100), or null for the person.</param>
/// <param name="Problem">Why that plugin cannot land work here now — the sentence a press would be refused with — or null.</param>
public sealed record LandingPlan(string Form, string Target, string Source, string? Plugin = null, string? Problem = null);

/// <summary>What came of a press. A refusal is an answer, as the merge door's are, and names what the person would do.</summary>
/// <param name="Plugin">What the rule's plugin answered once the branch was made (D100) — null where no plugin was spoken to.</param>
public sealed record TreeLanding(bool Landed, string Message, string? Branch = null, PluginLanding? Plugin = null);

/// <summary>
/// What a plugin answered at a landing (WSR4, D100): whether it pushed the branch, the pull request it
/// opened where it opened one, and its own sentence.
/// </summary>
/// <param name="Failed">
/// The plugin's step did not complete — it could not start, answered late or wrongly, or refused the
/// call — and <paramref name="Message"/> is Daoris's sentence saying which. A plugin that answered it
/// did not push is not a failure of the wire: its own sentence says why.
/// </param>
public sealed record PluginLanding(string Plugin, bool Pushed, string? PullRequest, string Message, bool Failed = false);

/// <summary>One commit a landing carries, oldest first: its full id and its subject line.</summary>
public sealed record LandingCommit(string Sha, string Subject);

/// <summary>
/// What a plugin is told at a landing (D100) — the one frame on <see cref="HookPoints.Land"/>, sent once
/// the branch exists.
/// </summary>
/// <param name="Root">The repository's own checkout on this machine, where the plugin runs its push.</param>
/// <param name="Branch">The branch Daoris just made, holding the session's work.</param>
/// <param name="Base">The line the work grew from, which a pull request goes into — null where git can name none.</param>
/// <param name="Title">What the work is called: the quest's title, else what the person first said, else null.</param>
/// <param name="Quest">The chain's first quest (WSR5), or null for a conversation.</param>
public sealed record LandingFrame(
    string Repository, string Workspace, string Root, string Branch, string? Base, string? Title,
    string? Quest, string Session, IReadOnlyList<LandingCommit> Commits);

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

    /// <summary>What a plugin's id may be — the catalogue's own shape, so a rule never names a path.</summary>
    private static readonly Regex PluginId = new("^[a-z0-9][a-z0-9.-]*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// What is wrong with a rule's shape, in a sentence, or null when it can land work. Whether its plugin
    /// is on this machine is <see cref="PluginProblem"/>'s question: a file stays readable wherever it is.
    /// </summary>
    public static string? Problem(LandingRule rule) => rule.Form switch
    {
        // A merge makes no branch, and the plugin starts from the branch Daoris made (D100).
        LandingForm.Merge when rule.Plugin is not null =>
            "only a branch rule hands its work to a plugin — the plugin pushes the branch Daoris made, and a merge "
            + "makes none. `branch <pattern> --plugin <id>` is the form that does.",
        LandingForm.Merge => null,
        LandingForm.Branch => Problem(rule.Pattern ?? "") ?? (rule.Plugin is { } plugin && !PluginId.IsMatch(plugin)
            ? $"`{plugin}` is not a plugin id — one is lowercase letters, digits, dots and dashes, like `example.github-pull-request`."
            : null),
        _ => $"`{rule.Form}` is not a way work lands here — `{LandingForm.Merge}` or `{LandingForm.Branch}`. "
             + "Pushing and opening a pull request is a plugin's to do, named on the branch form: "
             + "`branch <pattern> --plugin <id>`.",
    };

    /// <summary>
    /// Why the plugin a rule names cannot land work on this machine, in a sentence, or null when it can
    /// (D100): not installed, switched off, refused by the catalogue, or speaking on no
    /// <see cref="HookPoints.Land"/> point. Asked when the rule is set and again at the press, so a plugin
    /// gone since is refused before anything is made. The CLI's <c>driverconfig.ts</c> is the twin.
    /// </summary>
    public static string? PluginProblem(string plugin, PluginCatalog catalog)
    {
        var entry = catalog.Plugins.FirstOrDefault(p => string.Equals(p.Manifest.Id, plugin, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return $"the landing rule names plugin `{plugin}`, which is not installed on this machine — "
                + "`daoris plugin add <folder>` installs one, and `daoris plugin list` shows what there is.";
        }

        if (!entry.Enabled)
        {
            return $"plugin `{plugin}` is switched off on this machine — `daoris plugin enable {entry.Manifest.Id}` switches it on.";
        }

        if (entry.Problem is { } problem) return $"plugin `{plugin}` contributes nothing: {problem}";

        return entry.Manifest.Hooks?.Points.Contains(HookPoints.Land, StringComparer.Ordinal) == true
            ? null
            : $"plugin `{plugin}` does not land work — its manifest speaks on no `{HookPoints.Land}` point.";
    }

    /// <summary>
    /// What the conversation's record keeps of a landing (D100): the whole sentence the press said, the
    /// plugin's part in it. Machine-local, like every note there — a plugin's word never rides the
    /// session record, which travels (D64 §4).
    /// </summary>
    public static SessionEvent Note(TreeLanding landed) =>
        new() { Kind = SessionEventKind.Note, Text = $"the person accepted this work: {landed.Message}" };

    /// <summary>
    /// What the conversation's record keeps of a hand-off after the landing (WSR5b): the whole sentence, the
    /// plugin's part in it — kept where the landing's own note is, and for the same reason (D100).
    /// </summary>
    public static SessionEvent HandNote(TreeHand handed) =>
        new() { Kind = SessionEventKind.Note, Text = $"the person handed this work on: {handed.Message}" };

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

    /// <summary>
    /// What a landing is named for (WSR5): the session, and the chain's FIRST quest with its title — the
    /// one the ask became — walking up each step's parent; a chat with no quest has its opening line.
    /// </summary>
    /// <remarks>
    /// 🔴 A chain lands from its last step (WSR1), so naming it for that step's quest named AR-2202's work
    /// `feature/verify-in-prod-…-381807d1f7bd`, after its verify step. The walk stops where the service no
    /// longer answers for a parent, and after twenty steps, which no chain reaches.
    /// </remarks>
    public static async Task<LandingSubject> SubjectAsync(
        string session, string? quest, Func<string, Task<QuestView?>> find, string? opening)
    {
        if (quest is null) return new LandingSubject(session, null, opening);

        var current = await find(quest).ConfigureAwait(false);
        if (current is null) return new LandingSubject(session, quest, opening);
        for (var steps = 0; steps < 20 && current.Parent is { Length: > 0 } parent; steps++)
        {
            if (await find(parent).ConfigureAwait(false) is not { } above) break;
            current = above;
        }

        return new LandingSubject(session, current.Id, current.Title);
    }

    /// <summary>The repository's rule, else its workspace's (a repository in no workspace is in `default`'s), else merge.</summary>
    public static Landing Choose(DriverConfig config, string repository, string? workspace)
    {
        if (config.Landings.TryGetValue(repository, out var own)) return new(own, LandingSource.Repository);
        if (config.WorkspaceLandings.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)) return new(shared, LandingSource.Workspace);
        return new(LandingRule.Merge, LandingSource.Default);
    }
}
