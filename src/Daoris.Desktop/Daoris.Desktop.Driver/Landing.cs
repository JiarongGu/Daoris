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
/// <param name="AutoAccept">
/// <i>Accept automatically</i> (LAND2a, D145): a quest's done lands its work as the person's Accept would, and the rule's
/// plugin pushes it and opens the pull request with no press, the pull request being the last human step. Only a branch
/// rule takes it; off, the default, is the press as it always was. It rides the rule, so a repository's own rule replaces
/// its workspace's switch with the rest of it. LAND2b acts on it: a session concluding on a done quest becomes due
/// (<see cref="AutoLandings"/>), and the next look lands it (<see cref="AutoLander"/>).
/// </param>
public sealed record LandingRule(string Form, string? Pattern = null, bool Tidy = false, string? Plugin = null, bool AutoAccept = false)
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
public sealed record LandingSubject(string Session, string? Quest, string? Title)
{
    /// <summary>
    /// The quest's name where it is not its title (SESSUX1j, LANDNAME1): its short title, which the branch is named for. Null
    /// where the name is the title, or there is no quest: the title names the branch.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>The branch pattern's <c>{slug}</c>: the quest's name in the words git takes, its title where it has no other.</summary>
    public string Slug => LandingRules.Slug(Name ?? Title);

    /// <summary>The subject a quest is landed as: its title for the record, and its name for the branch where they differ.</summary>
    public static LandingSubject Of(string session, QuestView quest) =>
        new(session, quest.Id, quest.Title) { Name = quest.Name == quest.Title ? null : quest.Name };
}

/// <summary>What a press would do: the form, where the work would go (the line, or the branch it would make), and what said so.</summary>
/// <param name="Plugin">The plugin that pushes the branch once it is made (D100), or null for the person.</param>
/// <param name="Problem">Why that plugin cannot land work here now — the sentence a press would be refused with — or null.</param>
/// <param name="AutoAccept">
/// Whether the rule accepts automatically (LAND2b, D145): the quest's done lands the work, with no press, and the session's
/// instruction says so.
/// </param>
public sealed record LandingPlan(string Form, string Target, string Source, string? Plugin = null, string? Problem = null, bool AutoAccept = false);

/// <summary>What came of a press. A refusal is an answer, as the merge door's are, and names what the person would do.</summary>
/// <param name="Plugin">What the rule's plugin answered once the branch was made (D100) — null where no plugin was spoken to.</param>
/// <param name="Tidied">
/// What the rule's tidy did with the other session branches the landed work holds (LAND3), each removed or kept and why —
/// null where no tidy ran. The message says the same, so the landing's note keeps it.
/// </param>
public sealed record TreeLanding(
    bool Landed, string Message, string? Branch = null, PluginLanding? Plugin = null, IReadOnlyList<TidiedBranch>? Tidied = null)
{
    /// <summary>
    /// A refusal's code (LAND2b), one of <see cref="AutoLandingCode"/>: <c>uncommitted</c>, <c>nothing</c>, <c>exists</c> or, where a
    /// chain's branch was merged, <c>completed</c> (LAND2c) where the branch form said so, else null, which a landing at done keeps
    /// as <c>refused</c>. The message says it to a person.
    /// </summary>
    public string? Refusal { get; init; }

    /// <summary>
    /// Why the rule's plugin could not land work here, where a landing at done made the branch without it (LAND2b, D145
    /// point 2): nobody is there to fix the plugin, so the branch is made and the push is not tried. Null at a press, which
    /// refuses before anything is made instead (D100).
    /// </summary>
    public string? Unready { get; init; }

    /// <summary>
    /// Where the landing moved a branch Daoris made on from (LAND2c, D149 point 2): the commit it stood at, recorded, before this
    /// session's work fast-forwarded it. Null for a branch made new, and for every merge.
    /// </summary>
    public string? AdvancedFrom { get; init; }
}

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
/// <param name="PullRequest">
/// The pull request the landing record holds for this branch (LAND2c, D149 point 4): one a plugin opened at the chain's first
/// landing, which this push grows, so the plugin pushes and opens no second one. Null where none is open from it yet.
/// </param>
/// <param name="AcceptedBy">
/// Who accepted the work (LAND2c, D145 point 3), one of <see cref="Daoris.Driver.AcceptedBy"/>: the description a plugin
/// writes says it, and it is not always the person who reviewed it.
/// </param>
public sealed record LandingFrame(
    string Repository, string Workspace, string Root, string Branch, string? Base, string? Title,
    string? Quest, string Session, IReadOnlyList<LandingCommit> Commits, string? PullRequest = null,
    string AcceptedBy = Daoris.Driver.AcceptedBy.Person);

/// <summary>
/// What an advance of a standing branch is judged by (LAND2c, D149 points 2–3), read from git at the press or the look: the
/// record's entry for it, where it stands, where it is checked out, whether the session's work grows from it, and whether its
/// work already reads on the line.
/// </summary>
/// <param name="Recorded">The landing record's standing entry for this branch in this repository, or null: not Daoris's.</param>
/// <param name="Tip">The commit the branch stands at now.</param>
/// <param name="CheckedOutAt">The working tree that has it checked out, or null.</param>
/// <param name="Descends">Whether the session's work grows from the recorded tip: its tip has that commit in its history.</param>
/// <param name="Ahead">How many commits the session's work holds beyond the recorded tip.</param>
/// <param name="Completed">
/// Where D102's proof found the recorded tip's work on the line — a form of it, such as <c>origin/main</c> — so its pull
/// request was merged; null where it does not read there.
/// </param>
public sealed record AdvanceFacts(LandedBranch? Recorded, string Tip, string? CheckedOutAt, bool Descends, int Ahead, string? Completed);

/// <summary>
/// Whether a landing may move a standing branch on (LAND2c, D145 §3, D149): pure, so its table holds every refusal without
/// git. Only a branch Daoris made and recorded, standing at the recorded tip, checked out nowhere, whose work does not
/// already read on the line, to a commit that grows from that tip with something new.
/// </summary>
public static class LandingAdvance
{
    /// <summary>The refusal, in its code and its sentence, or null where the branch may move on.</summary>
    /// <param name="name">The branch the pattern names.</param>
    /// <param name="sessionBranch">The session's own branch, whose work would move it.</param>
    public static TreeLanding? Refusal(string name, string repository, string sessionBranch, AdvanceFacts facts)
    {
        TreeLanding Exists(string why) => new(false, why) { Refusal = AutoLandingCode.Exists };

        if (facts.Recorded is not { } recorded)
        {
            return Exists($"`{name}` is already a branch in `{repository}`, and Daoris does not move a branch it did not make. "
                + "Rename or delete it there, or change the pattern with `daoris driver landing`.");
        }

        if (!string.Equals(facts.Tip, recorded.Tip, StringComparison.OrdinalIgnoreCase))
        {
            return Exists($"`{name}` moved since Daoris landed on it (it stood at {Short(recorded.Tip)}, and stands at {Short(facts.Tip)} "
                + "now), so Daoris does not move it on: what moved it may be somebody's. Land this work by hand, or rename "
                + "that branch and accept again.");
        }

        if (facts.CheckedOutAt is { } at)
        {
            return Exists($"`{name}` is checked out at {at}, and Daoris does not move a branch a working tree stands on. "
                + "Switch that tree to another branch, then accept again.");
        }

        // PLUGHOOK1a (D149 point 3): the platform's kept word refuses as git's own proof does, since git cannot see a squash once
        // the line has moved on, and a closed pull request takes no new commits.
        if (recorded.PullRequestState is { State: PullRequestStates.Completed or PullRequestStates.Abandoned } kept)
        {
            var completed = kept.State == PullRequestStates.Completed;
            var answered = (kept.Plugin is { } plugin ? $", as `{plugin}` answered" : "")
                + (kept.AskedAt > DateTimeOffset.MinValue ? $" at {kept.AskedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC" : "");
            return new TreeLanding(false,
                $"`{name}`'s pull request {(completed ? "completed" : "was abandoned")}{answered}, so Daoris does not move it on: "
                + "commits added to it now would ride no pull request. "
                + (completed
                    ? "Bring the repository up to date (`daoris-driver trees sync`, or Bring up to date on the page), which replays "
                      + "this session's own commits onto the line and removes that branch; "
                    : $"Delete or rename `{name}` once you no longer want its work; ")
                + $"then Accept lands this work on a fresh `{name}`, and its plugin opens a new pull request.")
            { Refusal = AutoLandingCode.Completed };
        }

        if (facts.Completed is { } where)
        {
            return new TreeLanding(false,
                $"`{name}`'s work already reads on `{where}` — its pull request was merged — so Daoris does not move it on: commits "
                + "added to it now would ride no pull request. Bring the repository up to date (`daoris-driver trees sync`, or "
                + "Bring up to date on the page), which replays this session's own commits onto the line and removes that branch; "
                + $"then Accept lands this work on a fresh `{name}`, and its plugin opens a new pull request.")
            { Refusal = AutoLandingCode.Completed };
        }

        if (!facts.Descends)
        {
            return Exists($"`{sessionBranch}` does not grow from `{name}`, so moving it on would not be a fast-forward, and Daoris "
                + "merges nothing into a branch. Land this work by hand, or change the pattern with `daoris driver landing`.");
        }

        return facts.Ahead > 0
            ? null
            : new TreeLanding(false, $"`{name}` already holds everything on `{sessionBranch}` — nothing to land.")
            {
                Refusal = AutoLandingCode.Nothing,
            };
    }

    private static string Short(string commit) => commit[..Math.Min(8, commit.Length)];
}

/// <summary>What a session branch holds, for the clean-up's list (D88). Only the empty, the landed and the carried go.</summary>
public static class SweepKind
{
    /// <summary>Nothing beyond the line.</summary>
    public const string Empty = "empty";

    /// <summary>Every commit on a branch of the person's.</summary>
    public const string Landed = "landed";

    /// <summary>Commits only Daoris's branches hold — kept, and named.</summary>
    public const string Unlanded = "unlanded";

    /// <summary>
    /// Commits no branch of the person's holds, which a landed branch's completed pull request carried, as its plugin answered and
    /// git confirms (PLUGHOOK1a, D148 point 4): what a squash leaves once the platform deleted the source branch.
    /// </summary>
    public const string Carried = "carried";

    /// <summary>Its tree holds uncommitted work — kept.</summary>
    public const string Dirty = "dirty";

    /// <summary>A session still running or waiting names its tree — kept.</summary>
    public const string InUse = "in-use";
}

/// <param name="Commits">Unlanded and carried: how many only Daoris holds. Landed: how many it carried.</param>
/// <param name="Where">Landed: the first branch of the person's that holds it. Empty: the line. Carried: the landed branch whose pull request carried it.</param>
/// <param name="Detail">Git's own lines where they say more: the unlanded commits, the uncommitted count.</param>
public sealed record SweepItem(
    string Repository, string Workspace, string Branch, string? Tree, string Kind, int Commits, string? Where, string? Detail)
{
    public bool Removable => Kind is SweepKind.Empty or SweepKind.Landed or SweepKind.Carried;

    /// <summary>Carried: the landing entry whose completed pull request carried it (PLUGHOOK1a), on which its removal is kept.</summary>
    public LandedBranch? CarriedBy { get; init; }
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
        // A merge writes into the person's checkout, and with no press nothing would stand between the work and the
        // line (D145 point 1, D51 rule 6).
        LandingForm.Merge when rule.AutoAccept =>
            "only a branch rule accepts automatically — a merge writes into your checkout, and with no press nothing would "
            + "stand between the work and the line. `branch <pattern> --auto-accept` is the form that does.",
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
    /// What a door says as <i>Accept automatically</i> is set (LAND2a, D145 point 5, design §6): the switch is the
    /// person's standing say-so for a push with no press, so it is said where it is given. With no plugin, the warning
    /// that nothing leaves the machine. The CLI's <c>driverconfig.ts</c> says the same words; the page says them in its
    /// own catalogue.
    /// </summary>
    public static string AutoAcceptSays(string? plugin) => plugin is { } named
        ? $"when a quest here is done, its work is put on its branch, and `{named}` pushes it and opens a pull request without "
          + "asking you each time. The pull request is where it is judged. Switch it off to accept each one yourself."
        : "when a quest here is done, its work is put on its branch, and nothing leaves this machine: no plugin opens a pull "
          + "request, so each done's branch waits here for you to push it. Switch it off to accept each one yourself.";

    /// <summary>
    /// What the conversation's record keeps of a landing (D100): the whole sentence the press said, the
    /// plugin's part in it. Machine-local, like every note there — a plugin's word never rides the
    /// session record, which travels (D64 §4).
    /// </summary>
    public static SessionEvent Note(TreeLanding landed) =>
        new() { Kind = SessionEventKind.Note, Text = $"{PersonAccepted} {landed.Message}" };

    /// <summary>How the conversation's note of a press opens: what the trace knows an acceptance by (TRACE1).</summary>
    public const string PersonAccepted = "the person accepted this work:";

    /// <summary>
    /// How the conversation's note of a landing at done opens (LAND2b, D145 point 4): who accepted it, which the review's note
    /// and the trace name. The rest of the note is its code's (<see cref="AutoLandingNotes"/>).
    /// </summary>
    public const string AutoAccepted = "accepted automatically when its quest was done:";

    /// <summary>Whether a conversation's note is an acceptance of the session's work: a press's, or a landing at done.</summary>
    public static bool IsAcceptance(string text) =>
        text.StartsWith(PersonAccepted, StringComparison.Ordinal) || text.StartsWith(AutoAccepted, StringComparison.Ordinal);

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

        return LandingSubject.Of(session, current);
    }

    /// <summary>The repository's rule, else its workspace's (a repository in no workspace is in `default`'s), else merge.</summary>
    public static Landing Choose(DriverConfig config, string repository, string? workspace)
    {
        if (config.Landings.TryGetValue(repository, out var own)) return new(own, LandingSource.Repository);
        if (config.WorkspaceLandings.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)) return new(shared, LandingSource.Workspace);
        return new(LandingRule.Merge, LandingSource.Default);
    }
}
