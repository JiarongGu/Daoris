namespace Daoris.Driver;

/// <summary>
/// What handing a landed branch to a plugin would do (WSR5b), said before the press: which branch, the
/// plugin it would go to, and the sentence the press would be refused with, where something stands in the way.
/// </summary>
/// <param name="Plugin">The plugin named for the hand-off, else the repository's landing rule's; null where neither names one.</param>
/// <param name="PullRequest">The pull request a plugin answered with the last time it pushed this branch, where the record knows one.</param>
/// <param name="Commits">The branch's commits the line does not hold — what the plugin would be told of.</param>
public sealed record HandPlan(string Repository, string Branch, string? Plugin, string? Problem, string? PullRequest, int Commits);

/// <summary>What came of a hand-off (WSR5b). A refusal is an answer, as a landing's is, and names what the person would do.</summary>
/// <param name="Plugin">What the plugin answered — null where it was never spoken to.</param>
public sealed record TreeHand(bool Handed, string Message, string? Branch = null, PluginLanding? Plugin = null);

public sealed partial class SessionTrees
{
    /// <summary>The sentence for a name the record holds no landed branch under — the hand-off's first refusal.</summary>
    public static string NotLanded(string named) =>
        $"`{named}` names no branch a landing made on this machine. Daoris hands on only a branch its own landing made "
        + "and recorded; a branch of your own is yours to push.";

    /// <summary>What a press on the hand-off would do (WSR5b) — every refusal it would give, and nothing spoken.</summary>
    public async Task<HandPlan> HandPlanAsync(string root, LandedBranch entry, string? plugin = null, CancellationToken ct = default) =>
        (await PrepareHandAsync(root, entry, plugin, ct).ConfigureAwait(false)).Plan;

    /// <summary>
    /// Hand a branch a landing made to a landing plugin (WSR5b): the one named, else the repository's rule's,
    /// told D100's own frame for the branch as it stands — the line as its base, the commits the line lacks,
    /// the quest, session and title the landing recorded.
    /// </summary>
    /// <remarks>
    /// <para><b>The same wire and the same words as the landing's plugin step</b> (D100): the plugin pushes and
    /// opens the pull request, Daoris runs neither, and its answer is said as a landing says it.</para>
    ///
    /// <para>🔴 <b>A hand-off that fails changes nothing.</b> A refusal speaks to no plugin; a plugin that
    /// fails, or answers that it did not push, leaves the branch, the record and the remote as they were, and
    /// the sentence says how to push by hand. Only a push is kept on the record.</para>
    /// </remarks>
    public async Task<TreeHand> HandAsync(string root, LandedBranch entry, string? plugin = null, CancellationToken ct = default)
    {
        var (plan, frame, tip) = await PrepareHandAsync(root, entry, plugin, ct).ConfigureAwait(false);
        if (plan.Problem is not null || frame is null || plan.Plugin is null)
        {
            return new(false, plan.Problem ?? $"`{entry.Branch}` cannot be handed on.", entry.Branch);
        }

        // The landing's own frame, said in the machine log as a hand-off (PLUGUI1d).
        var said = await _plugins.HandAsync(plan.Plugin, frame, ct).ConfigureAwait(false);
        var handed = said is { Pushed: true, Failed: false };
        var message = handed
            ? $"handed `{entry.Branch}` to plugin `{said.Plugin}`. {Said(said, entry.Branch)}"
            : $"`{entry.Branch}` was not handed on. {Said(said, entry.Branch)} Nothing changed.";
        var result = new TreeHand(handed, message, entry.Branch, said);
        if (!handed) return result;

        try
        {
            Recorded.Pushed(entry.Repository, entry.Branch, said, tip!);
            return result;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return result with { Message = message + $" Daoris could not keep the push on the record ({error.Message})." };
        }
    }

    /// <summary>Every question a hand-off asks before it speaks, in order, and the frame it would speak — or the first refusal.</summary>
    private async Task<(HandPlan Plan, LandingFrame? Frame, string? Tip)> PrepareHandAsync(
        string root, LandedBranch entry, string? named, CancellationToken ct)
    {
        var repository = entry.Repository;
        var branch = entry.Branch;
        var workspace = RemoteTarget.Workspace(entry.Workspace);
        var rule = LandingRules.Choose(Config(), repository, workspace).Rule;
        var plugin = named is { Length: > 0 } ? named : rule.Form == LandingForm.Branch ? rule.Plugin : null;
        (HandPlan, LandingFrame?, string?) Refused(string why, int commits = 0) =>
            (new HandPlan(repository, branch, plugin, why, entry.PullRequest, commits), null, null);

        var (tipCode, tipOut, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}^{{commit}}"], ct).ConfigureAwait(false);
        if (tipCode != 0) return Refused($"`{branch}` is gone from `{repository}`, so there is nothing to hand on.");
        var tip = tipOut.Trim();

        // The landing's branch only: one that took its name since is the person's.
        var (ours, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", entry.Tip, tip], ct).ConfigureAwait(false);
        if (ours != 0)
        {
            return Refused($"`{branch}` in `{repository}` no longer holds the commit the landing made it at — it is not the branch "
                + "the landing made, and Daoris does not hand on a branch it did not make.");
        }

        var line = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? "HEAD" : await ComparableAsync(root, line, ct).ConfigureAwait(false) ?? line;
        // Oldest first, each commit's id and subject: what the landing told its plugin (D100).
        var (logCode, ahead, logErr) = await WorkingTree.GitAsync(
            root, ["log", "--reverse", "--format=%H%x09%s", $"{against}..{tip}"], ct).ConfigureAwait(false);
        if (logCode != 0)
        {
            return Refused($"git cannot compare `{branch}` with `{line ?? "HEAD"}` here: {FirstLine(logErr)} "
                + "Fetch it, or set another line with `daoris driver line`.");
        }

        var commits = ahead.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(each => each.Split('\t', 2))
            .Select(parts => new LandingCommit(parts[0], parts.Length > 1 ? parts[1] : ""))
            .ToList();

        if (plugin is null)
        {
            return Refused($"no plugin is named to hand `{branch}` to: `{repository}`'s landing rule names none. Name one on the rule "
                + $"(`daoris driver landing {repository} branch <pattern> --plugin <id>`), or once with `--plugin <id>`.", commits.Count);
        }

        // D100's refusals, in its words: not installed, off, unsound, or speaking on no `work/land` point.
        if (_plugins.Problem(plugin) is { } unready) return Refused($"{unready} Nothing was handed on.", commits.Count);

        if (commits.Count == 0) return Refused($"`{branch}` holds nothing `{line ?? "HEAD"}` does not — nothing to hand on.");

        // A pull request that was merged by a squash leaves the branch's commits off the line and its files on it.
        var forms = await LineFormsAsync(root, line, ct).ConfigureAwait(false);
        var proof = await ProveAsync(root, tip, line, forms, ct).ConfigureAwait(false);
        if (proof.Kind is LandedKind.OnLine or LandedKind.Merged)
        {
            return Refused($"`{branch}`'s work already reads on the line — its pull request was merged. Nothing to hand on: the "
                + "clean-up removes it (Settings → Workspace → Session branches, or `daoris-driver trees clean`).", commits.Count);
        }

        // PLUGHOOK1a (D148, amending D102): the same refusal where the platform's kept word clears it and git confirms it.
        if (entry.PullRequestState is { State: PullRequestStates.Completed } kept
            && await VerdictAsync(root, line, forms, kept, tip, ct).ConfigureAwait(false) is { Clears: true } verdict)
        {
            return Refused($"`{branch}`'s work already reads on the line — its pull request completed"
                + (kept.Plugin is { } answered ? $", as `{answered}` answered," : "") + $" and its merge commit is on `{verdict.Form}`. "
                + "Nothing to hand on: the clean-up removes it (Settings → Workspace → Session branches, or `daoris-driver trees clean`).",
                commits.Count);
        }

        // Nothing new to push: the remote holds this very commit, and a pull request was answered for it.
        if (entry is { Pushed: true, PullRequest: { } pullRequest })
        {
            var (remoteCode, remote, _) = await WorkingTree.GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/remotes/origin/{branch}^{{commit}}"], ct).ConfigureAwait(false);
            if (remoteCode == 0 && remote.Trim() == tip)
            {
                return Refused($"`{branch}` is already on its remote at this commit, and its pull request was answered: {pullRequest} "
                    + "Nothing to hand on.", commits.Count);
            }
        }

        // The pull request a plugin already opened from it, so the hand-off grows it rather than opening a second, and who
        // accepted the work it carries (LAND2c, D149 point 4): its newest acceptance, a landing from before kept as the person's.
        var newest = entry.Advances.Count > 0 ? entry.Advances[^1].Session : entry.Session;
        return (new HandPlan(repository, branch, plugin, null, entry.PullRequest, commits.Count),
            new LandingFrame(repository, workspace, root, branch, line, entry.Title, entry.Quest, entry.Session, commits,
                entry.Pushed ? entry.PullRequest : null, entry.AcceptedByOf(newest) ?? AcceptedBy.Person),
            tip);
    }
}
