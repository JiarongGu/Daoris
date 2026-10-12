using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a session in a tree of its own is told of its process at its start (PROCESS1d-i, D164's amendment point 4): how its work
/// lands, by the branch the pull request comes from where it has one, then the commit rule standing for its repository (its mode,
/// where it was set, the person's convention quoted), so the agent and the repository's own rules no longer stop each other. The
/// instruction's landing section, in every form the tail carries; Daoris's one-copy English (D142 point 6).
/// </summary>
/// <remarks>
/// Pure, from what the driver resolved (<see cref="CommitRules.Resolve"/>, <see cref="LandingPlan"/>): null where nothing is said,
/// as <see cref="InAppBrowserServers.Told"/> is. A session in the person's checkout is handed neither, and says nothing.
/// </remarks>
public static class ProcessWords
{
    /// <summary>
    /// The landing section, its leading and trailing line breaks included, or null with no rule and no branch to go on. A branch plan
    /// handed without a rule is told the default, as a tree with none set is; a merge with none says nothing, as it always has.
    /// </summary>
    public static string? Said(ResolvedCommits? commits, LandingPlan? plan)
    {
        var branch = plan is { Form: LandingForm.Branch };
        if (commits is null && !branch) return null;
        var (rule, source) = commits ?? new ResolvedCommits(CommitRule.Default, CommitRules.DefaultSource);
        var auto = branch && plan!.AutoAccept;

        var text = new StringBuilder("\n");
        if (Lead(plan) is { } lead) text.Append(lead).Append("\n\n");
        text.Append("This repository's process on this machine")
            .Append(source == CommitRules.DefaultSource ? "" : $", set for its {source}")
            .Append(":\n\n")
            .Append("- ").Append(Mode(rule.Mode, auto)).Append('\n')
            .Append("- Do not create, switch or push branches")
            .Append(branch && !plan!.Own && plan.Target.Length > 0
                ? $": Daoris makes the pull request's branch when {(auto ? "the work is accepted" : "the person accepts the work")}.\n"
                : ".\n");
        if (auto) text.Append("- ").Append(BeforeDone(rule.Mode)).Append('\n');
        if (rule.Convention is { } convention)
        {
            text.Append("\nThe person's words on how a commit is written, to follow:\n\n> ").Append(convention).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// How the work lands, said where it has a form: the branch it goes on and who takes it from there, the one the pull request comes
    /// from named as the repository's rule names it (PROCESS1b-i), or the tree's own branch (TREEFOLLOW1); or the line a merge goes
    /// into. Null with no plan. A named workflow's landing that cannot start has no branch (WORKFLOW1f), and none is named.
    /// </summary>
    private static string? Lead(LandingPlan? plan) => plan switch
    {
        { Form: LandingForm.Branch } => (plan.AutoAccept ? "This work is accepted automatically." : "This work goes through review.")
            + (plan.Target.Length == 0 ? "" : $" {Lands(plan)} {Named(plan)}"),
        { Form: LandingForm.Merge } => $"When the person accepts, this tree's branch is merged into `{plan.Target}`.",
        _ => null,
    };

    private static string Named(LandingPlan plan) => plan.Own
        ? $"This tree is on `{plan.Target}`, which lands as it stands; you do not merge it, push it, or open a pull request yourself."
        : $"The branch the pull request comes from is named `{plan.Target}` by this repository's rule; you do not name it, and you do not "
          + "merge it, push it, or open a pull request yourself.";

    /// <summary>
    /// Who takes the branch on from there: the person, or the plugin the rule names (D100) once the person accepts it; or, under
    /// a rule that accepts automatically (LAND2b, D145), the quest's done, with no press.
    /// </summary>
    private static string Lands(LandingPlan plan) => (plan.AutoAccept, plan.Plugin) switch
    {
        (true, null) => $"When you close the quest done, this tree's branch is put on `{plan.Target}`, for the person to push and open a pull request from.",
        (true, _) => $"When you close the quest done, this tree's branch is put on `{plan.Target}`, pushed, and a pull request opened from it, where the work is judged.",
        (false, null) => $"When it is done, the person puts this tree's branch on `{plan.Target}` and opens a pull request from it.",
        _ => $"When it is done and the person accepts it, this tree's branch is put on `{plan.Target}`, pushed, and a pull request opened from it.",
    };

    /// <summary>How commits are made, by the rule's mode: under a rule that accepts automatically there is no Accept to wait for.</summary>
    private static string Mode(string mode, bool auto) => mode switch
    {
        CommitRules.Ask => auto
            ? "Ask the person before each commit, and commit only on their yes."
            : "Ask the person before each commit, and commit only on their yes. Uncommitted work stays in this tree and the person sees "
              + "it as it stands, but Accept will not land it until it is committed.",
        CommitRules.AtEnd => "Make one commit on this tree's branch when the work is done, and none before.",
        _ => "Commit on this tree's branch as you go, each piece once it passes its checks.",
    };

    /// <summary>Under a rule that accepts automatically, the quest's done lands what is committed, so the commit comes first.</summary>
    private static string BeforeDone(string mode) => mode switch
    {
        CommitRules.Ask => "Ask for the yes and commit before you close the quest done: uncommitted work does not land.",
        CommitRules.AtEnd => "Make that commit before you close the quest done: uncommitted work does not land.",
        _ => "Commit all of your work before you close the quest done: uncommitted work does not land.",
    };
}
