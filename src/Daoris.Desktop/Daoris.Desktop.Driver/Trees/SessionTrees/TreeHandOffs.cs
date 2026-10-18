using static Daoris.Driver.TreeGit;

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
public sealed record TreeHand(bool Handed, string Message, string? Branch = null, PluginLanding? Plugin = null)
{
    /// <summary>
    /// Why its push was held back just before its plugin was told (FABRIC-CLOSE1h8): the door's judgement, as a landing's
    /// <see cref="TreeLanding.Withheld"/> says it. No plugin was spoken to and nothing was recorded; null where it was not held there.
    /// </summary>
    public string? Withheld { get; init; }

    /// <summary>
    /// The sentence for a name the record holds no landed branch under — the hand-off's first refusal. On the public answer
    /// (PART10l), since the terminal and the Modules bridge say it too and <see cref="TreeHandOffs"/> is internal over the context.
    /// </summary>
    public static string NotLanded(string named) =>
        $"`{named}` names no branch a landing made on this machine. Daoris hands on only a branch its own landing made "
        + "and recorded; a branch of your own is yours to push.";
}

/// <summary>
/// A branch a landing made, handed to a landing plugin (WSR5b, D100; PART10l, out of <c>SessionTrees.Hand.cs</c>): every question
/// the hand-off asks before it speaks, and the one push it keeps. Over the trees' context for the plugin wire, the record and the line.
/// </summary>
internal sealed class TreeHandOffs(TreeContext context)
{
    /// <summary>What a press on the hand-off would do (WSR5b) — every refusal it would give, and nothing spoken.</summary>
    internal async Task<HandPlan> HandPlanAsync(string root, LandedBranch entry, string? plugin = null, CancellationToken ct = default) =>
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
    /// <param name="beforePush">
    /// The door's judgement under the branch's hold, just before the plugin is told (FABRIC-CLOSE1h8), told the session that last moved
    /// the branch as the record reads under that hold and whether the hold waited (FABRIC-CLOSE1h10): the sentence that holds the push
    /// back (<see cref="TreeHand.Withheld"/>), or null to go on. A person's door passes the result of the session that last moved the
    /// branch as it read it (<see cref="PressQuests.HandedAsync(LandedBranch, ServiceClient, CancellationToken)"/>), the look its own rule;
    /// none judges nothing, as before.
    /// </param>
    internal async Task<TreeHand> HandAsync(
        string root, LandedBranch entry, string? plugin = null, CancellationToken ct = default,
        Func<string, bool, CancellationToken, Task<string?>>? beforePush = null)
    {
        // LANDRACE1's review (D173's LANDRACE1 note): one push of a landed branch at a time, a hand-off's and a landing's plugin step
        // alike, so a hand-off beside the landing that is still pushing the branch never pushes it again or opens a second pull request.
        using var pushing = await LandingHolds.BranchAsync(context.Home, root, entry.Branch, ct).ConfigureAwait(false);
        if (pushing is null)
        {
            return new(false, $"`{entry.Branch}` was not handed on: another push of it was still under way after "
                + $"{(int)LandingHolds.Wait.TotalMinutes} minutes. Nothing changed; hand it on again once that one ends.", entry.Branch);
        }

        // Read again under the hold: a push that ended while this one waited is on the record now, with the pull request it opened.
        entry = context.Recorded.Of(entry.Repository, entry.Branch) ?? entry;
        var (plan, frame, tip) = await PrepareHandAsync(root, entry, plugin, ct).ConfigureAwait(false);
        if (plan.Problem is not null || frame is null || plan.Plugin is null)
        {
            return new(false, plan.Problem ?? $"`{entry.Branch}` cannot be handed on.", entry.Branch);
        }

        // The push it waited behind pushed this very commit: said as that push's answer, and the plugin is not told again.
        if (pushing.Before is { Said: { Pushed: true, Failed: false } pushed } before && string.Equals(before.Tip, tip, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, $"`{entry.Branch}` was pushed at this commit while this hand-off waited for that push: {Said(pushed, entry.Branch)} "
                + "Nothing more was handed on.", entry.Branch, pushed);
        }

        // FABRIC-CLOSE1h8 (D173's note): the door's judgement once more, just before its plugin is told, past which the push has begun.
        // Held here, no plugin is spoken to and nothing is kept, on the record or for a door behind this one: the branch stands as it was.
        // FABRIC-CLOSE1h10: told the session that last moved the branch as the record reads under this hold, so a later session's advance
        // made while this waited is judged on that session's quest, never pushed on the one the door read.
        if (beforePush is not null && await beforePush(entry.LastSession(), pushing.Waited, ct).ConfigureAwait(false) is { } withheld)
        {
            return new(false, $"`{entry.Branch}` was not handed on. {Sentence(char.ToUpperInvariant(withheld[0]) + withheld[1..])}", entry.Branch)
            {
                Withheld = withheld,
            };
        }

        // The landing's own frame, said in the machine log as a hand-off (PLUGUI1d).
        var said = await context.Plugins.HandAsync(plan.Plugin, frame, ct).ConfigureAwait(false);
        pushing.Keep(new LandingAttempt(entry.Branch, tip!, frame.AcceptedBy) { Plugin = plan.Plugin, Said = said });
        var handed = said is { Pushed: true, Failed: false };
        var message = handed
            ? $"handed `{entry.Branch}` to plugin `{said.Plugin}`. {Said(said, entry.Branch)}"
            : $"`{entry.Branch}` was not handed on. {Said(said, entry.Branch)} Nothing changed.";
        var result = new TreeHand(handed, message, entry.Branch, said);
        if (!handed) return result;

        try
        {
            context.Recorded.Pushed(entry.Repository, entry.Branch, said, tip!);
            // FABRIC-END6: Daoris pushed, a moment to ask about the pull request the plugin opened (§A4.3).
            PullRequestMoments.Pushed(context.Home, entry.Repository);
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
        var rule = LandingRules.Choose(context.Config(), repository, workspace).Rule;
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

        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
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
        if (context.Plugins.Problem(plugin) is { } unready) return Refused($"{unready} Nothing was handed on.", commits.Count);

        if (commits.Count == 0) return Refused($"`{branch}` holds nothing `{line ?? "HEAD"}` does not — nothing to hand on.");

        // A pull request that was merged by a squash leaves the branch's commits off the line and its files on it.
        var forms = await LandedProofs.LineFormsAsync(root, line, ct).ConfigureAwait(false);
        var proof = await LandedProofs.ProveAsync(root, tip, line, forms, ct).ConfigureAwait(false);
        if (proof.Kind is LandedKind.OnLine or LandedKind.Merged)
        {
            return Refused($"`{branch}`'s work already reads on the line — its pull request was merged. Nothing to hand on: the "
                + "clean-up removes it (Repositories → the workspace's page → Branches → Session branches, or `daoris-driver trees clean`).", commits.Count);
        }

        // PLUGHOOK1a (D148, amending D102): the same refusal where the platform's kept word clears it and git confirms it.
        if (entry.PullRequestState is { State: PullRequestStates.Completed } kept
            && await PullRequestAsks.VerdictAsync(root, line, forms, kept, tip, ct).ConfigureAwait(false) is { Clears: true } verdict)
        {
            return Refused($"`{branch}`'s work already reads on the line — its pull request completed"
                + (kept.Plugin is { } answered ? $", as `{answered}` answered," : "") + $" and its merge commit is on `{verdict.Form}`. "
                + "Nothing to hand on: the clean-up removes it (Repositories → the workspace's page → Branches → Session branches, or `daoris-driver trees clean`).",
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
        var newest = entry.LastSession();
        return (new HandPlan(repository, branch, plugin, null, entry.PullRequest, commits.Count),
            new LandingFrame(repository, workspace, root, branch, line, entry.Title, entry.Quest, entry.Session, commits,
                entry.Pushed ? entry.PullRequest : null, entry.AcceptedByOf(newest) ?? AcceptedBy.Person, Tip: tip),
            tip);
    }
}
