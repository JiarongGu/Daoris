namespace Daoris.Driver;

/// <summary>
/// A completed pull request that carried work here (PLUGHOOK1a, D148 point 4): the landing entry whose kept answer it is, the
/// source commit it merged, and the landed branch's tip where the answer cleared that branch too. A session branch at or under
/// either goes, every keep still applying.
/// </summary>
public sealed record PullRequestCarrier(LandedBranch Entry, string Source, string? ClearedTip);

public sealed partial class SessionTrees
{
    /// <summary>
    /// One occasion's asks (PLUGHOOK1a, D148 point 2, design §2.1), over every repository it looks at: each entry, standing or a
    /// trace, whose kept answer is not completed, that was not asked in the last minute, and whose answer could remove something
    /// now, is asked of the plugin that pushed it, else the one the rule names. One process per plugin and one bound for the
    /// whole occasion. Answers and failures are kept on the record; a failure never overwrites an answer. Never at a press that
    /// removes, nor at a read: the callers are the looks that may remove, which <c>PullRequestOccasionTests</c> holds.
    /// </summary>
    /// <param name="landedMayGo">
    /// Whether this occasion may remove a landed branch: the clean-up's look and bringing up to date's, yes; LAND3's tidy, which
    /// removes session branches only, no. A standing entry D102's proof does not clear is asked about only where it may.
    /// </param>
    /// <param name="again">
    /// <i>Ask again</i> (PLUGHOOK1c, design §2.1 occasion 4): this one entry alone, asked whatever its kept answer's age and
    /// whether or not its answer could remove something now, since a person asked. Completed is final and still asked no more.
    /// </param>
    internal async Task<IReadOnlyList<StateAnswer>> AskStatesAsync(
        IEnumerable<(string Root, string Repository, string Workspace)> repositories, bool landedMayGo, CancellationToken ct,
        LandedBranch? again = null)
    {
        var now = _plugins.Now;
        var asks = new List<StateAsk>();
        foreach (var (root, repository, workspace) in repositories)
        {
            asks.AddRange(await DueAsksAsync(root, repository, workspace, now, landedMayGo, again, ct).ConfigureAwait(false));
        }

        var answers = await _plugins.AskStatesAsync(asks, ct).ConfigureAwait(false);
        foreach (var answer in answers)
        {
            try
            {
                if (answer.Answer is { } said)
                {
                    Recorded.Answered(answer.Ask.Entry, said with { AskedAt = now });
                }
                else if (answer.Failure is { } code && code is not (PullRequestCodes.NotAsked or PullRequestCodes.Unready))
                {
                    Recorded.AskFailed(answer.Ask.Entry, new PullRequestAskFailed(code, answer.Ask.Plugin, now));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Only the kept answer is lost, and with it the removal it would allow: the safe side.
            }
        }

        return answers;
    }

    /// <summary>
    /// One repository's entries due an ask at this occasion, each with its plugin and its frame (design §2.1); with
    /// <paramref name="again"/>, that entry alone, due whatever its kept answer's age.
    /// </summary>
    private async Task<IReadOnlyList<StateAsk>> DueAsksAsync(
        string root, string repository, string workspace, DateTimeOffset now, bool landedMayGo, LandedBranch? again, CancellationToken ct)
    {
        var rule = LandingRules.Choose(Config(), repository, workspace).Rule;
        var line = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var forms = await LineFormsAsync(root, line, ct).ConfigureAwait(false);
        IReadOnlyList<string>? unlanded = null;

        var asks = new List<StateAsk>();
        foreach (var entry in Recorded.Entries().Where(entry => Ours(entry, repository) && (again is null || SameLanding(entry, again))))
        {
            if (!PullRequestAsking.Due(entry, now, again is not null)) continue;
            if (PullRequestAsking.PluginFor(entry, rule) is not { } plugin) continue;
            if (again is null)
            {
                unlanded ??= await UnlandedSessionTipsAsync(root, ct).ConfigureAwait(false);
                if (!await CouldRemoveAsync(root, entry, line, forms, unlanded, landedMayGo, ct).ConfigureAwait(false)) continue;
            }

            asks.Add(new StateAsk(entry, plugin,
                new StateFrame(repository, workspace, root, entry.Branch, line, entry.PullRequest, entry.PushedTip)));
        }

        return asks;
    }

    /// <summary>The same landing, standing or a trace: its repository, its branch, the session that made it, and when.</summary>
    private static bool SameLanding(LandedBranch each, LandedBranch entry) =>
        string.Equals(each.Repository, entry.Repository, StringComparison.OrdinalIgnoreCase)
        && string.Equals(each.Branch, entry.Branch, StringComparison.Ordinal)
        && string.Equals(each.Session, entry.Session, StringComparison.OrdinalIgnoreCase)
        && each.LandedAt == entry.LandedAt;

    /// <summary>
    /// <i>Ask again</i> (PLUGHOOK1c, D148 point 2, design §2.1 occasion 4): one landed branch's plugin — the one that pushed it, else
    /// the one its repository's rule names — asked about its pull request whatever the kept answer's age, as an occasion asks;
    /// the answer or the failure kept as an occasion keeps it; and what the kept answer proves here, by the kept answer alone,
    /// as a look judges it. Completed is final and asked no more. A person's door alone opens it: `trees state`, and the review's
    /// press (PLUGHOOK1d), which <c>PullRequestOccasionTests</c> holds.
    /// </summary>
    /// <param name="root">The repository's checkout here, where the plugin runs its platform's tool and git judges the answer.</param>
    /// <param name="entry">The landing, standing or a trace.</param>
    public async Task<PullRequestAskedAgain> AskAgainAsync(string root, LandedBranch entry, CancellationToken ct = default)
    {
        var workspace = RemoteTarget.Workspace(entry.Workspace);
        var asked = new PullRequestAskedAgain(entry);
        if (entry.PullRequestState?.State == PullRequestStates.Completed)
        {
            asked = asked with { Final = true };
        }
        else if (PullRequestAsking.PluginFor(entry, LandingRules.Choose(Config(), entry.Repository, workspace).Rule) is null)
        {
            asked = asked with { Code = PullRequestCodes.NoPlugin, Why = PullRequestWords.NoPlugin };
        }
        else
        {
            var answers = await AskStatesAsync([(root, entry.Repository, workspace)], landedMayGo: true, ct, again: entry).ConfigureAwait(false);
            asked = answers.FirstOrDefault() is { } said
                ? asked with { Answered = said.Answer is not null, Code = said.Failure, Why = said.Sentence }
                // Nothing was due: a branch under Daoris's own namespace, which D88 judges, or a landing the record no longer holds.
                : asked with
                {
                    Code = PullRequestCodes.NotAsked,
                    Why = $"`{entry.Branch}` is no landing the record holds to ask about, so its pull request was not asked about.",
                };
        }

        var now = Recorded.Entries().LastOrDefault(each => SameLanding(each, entry)) ?? entry;
        if (now.PullRequestState is not { } kept) return asked with { Entry = now };
        var line = (await LineAsync(root, now.Repository, workspace, ct).ConfigureAwait(false)).Branch;
        var forms = await LineFormsAsync(root, line, ct).ConfigureAwait(false);
        var tip = now.GoneAt is null ? await StandingTipAsync(root, now, ct).ConfigureAwait(false) : null;
        return asked with { Entry = now, Verdict = await VerdictAsync(root, line, forms, kept, tip, ct).ConfigureAwait(false) };
    }

    /// <summary>A landing's own branch in this repository: a session branch's name is D88's to judge.</summary>
    private static bool Ours(LandedBranch entry, string repository) =>
        string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase) && !entry.Branch.StartsWith(SessionPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Whether an entry's answer could remove something now (design §2.1): it stands, this occasion may remove a landed branch,
    /// and D102's proof did not clear it; or its recorded tip, where git still holds that commit, contains the tip of a session
    /// branch that stands and that D88 does not clear.
    /// </summary>
    private async Task<bool> CouldRemoveAsync(
        string root, LandedBranch entry, string? line, IReadOnlyList<string> forms, IReadOnlyList<string> unlanded, bool landedMayGo,
        CancellationToken ct)
    {
        if (entry.GoneAt is null && landedMayGo && await StandingTipAsync(root, entry, ct).ConfigureAwait(false) is { } tip)
        {
            var proof = await ProveAsync(root, tip, line, forms, ct).ConfigureAwait(false);
            if (proof.Kind is not (LandedKind.OnLine or LandedKind.Merged)) return true;
        }

        if (unlanded.Count == 0) return false;
        var (held, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{entry.Tip}^{{commit}}"], ct).ConfigureAwait(false);
        if (held != 0) return false;
        foreach (var session in unlanded)
        {
            var (under, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", session, entry.Tip], ct).ConfigureAwait(false);
            if (under == 0) return true;
        }

        return false;
    }

    /// <summary>The tips of the session branches that stand here and hold commits no branch of the person's holds (D88).</summary>
    private static async Task<IReadOnlyList<string>> UnlandedSessionTipsAsync(string root, CancellationToken ct)
    {
        var tips = new List<string>();
        foreach (var branch in await SessionBranchesAsync(root, ct).ConfigureAwait(false))
        {
            var (code, log, _) = await UnlandedLogAsync(root, branch, ct).ConfigureAwait(false);
            if (code == 0 && !string.IsNullOrWhiteSpace(log)) tips.Add($"refs/heads/{branch}");
        }

        return tips;
    }

    /// <summary>The landed branch's tip where it stands holding the commit the landing recorded (D102's rule), else null.</summary>
    private static async Task<string?> StandingTipAsync(string root, LandedBranch entry, CancellationToken ct)
    {
        var (code, tip, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{entry.Branch}^{{commit}}"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var (ours, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", entry.Tip, tip.Trim()], ct).ConfigureAwait(false);
        return ours == 0 ? tip.Trim() : null;
    }

    /// <summary>
    /// What a kept answer proves here (design §2.3): git asked only what the answer needs, in the proof's order, and the pure
    /// table (<see cref="PullRequestProof"/>) says the rest.
    /// </summary>
    /// <param name="standingTip">The landed branch's tip where it stands as the landing's, else null.</param>
    private static async Task<PullRequestVerdict> VerdictAsync(
        string root, string? line, IReadOnlyList<string> forms, PullRequestState kept, string? standingTip, CancellationToken ct)
    {
        if (kept.State != PullRequestStates.Completed || kept.MergeCommit is not { } merge || kept.SourceCommit is not { } source)
        {
            return PullRequestProof.Judge(kept, new PullRequestFacts(line, null, false, standingTip is not null, false));
        }

        string? mergeOn = null;
        foreach (var form in forms)
        {
            var (on, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", merge, form], ct).ConfigureAwait(false);
            if (on != 0) continue;
            mergeOn = form;
            break;
        }

        var (held, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{source}^{{commit}}"], ct).ConfigureAwait(false);
        var under = false;
        if (held == 0 && standingTip is not null)
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", standingTip, source], ct).ConfigureAwait(false);
            under = code == 0;
        }

        return PullRequestProof.Judge(kept, new PullRequestFacts(line, mergeOn, held == 0, standingTip is not null, under));
    }

    /// <summary>
    /// Every completed pull request of one repository that carried work here, by the kept answers alone (design §2.3): what a
    /// look lists and a press re-judges by, so what was listed is what goes. Nothing is asked.
    /// </summary>
    internal async Task<IReadOnlyList<PullRequestCarrier>> CarriersAsync(string root, string repository, string? line, CancellationToken ct)
    {
        var carriers = new List<PullRequestCarrier>();
        var entries = Recorded.Entries().Where(entry => Ours(entry, repository) && entry.PullRequestState?.State == PullRequestStates.Completed).ToList();
        if (entries.Count == 0) return carriers;
        var forms = await LineFormsAsync(root, line, ct).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            var tip = entry.GoneAt is null ? await StandingTipAsync(root, entry, ct).ConfigureAwait(false) : null;
            var verdict = await VerdictAsync(root, line, forms, entry.PullRequestState!, tip, ct).ConfigureAwait(false);
            if (verdict.Carries) carriers.Add(new PullRequestCarrier(entry, entry.PullRequestState!.SourceCommit!, verdict.Clears ? tip : null));
        }

        return carriers;
    }

    /// <summary>The carrier whose pull request carried <paramref name="revision"/>: at or under its source commit, or under a tip it cleared.</summary>
    private static async Task<PullRequestCarrier?> CarrierOfAsync(
        string root, string revision, IReadOnlyList<PullRequestCarrier> carriers, CancellationToken ct)
    {
        foreach (var carrier in carriers)
        {
            foreach (var over in new[] { carrier.Source, carrier.ClearedTip })
            {
                if (over is null) continue;
                var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", revision, over], ct).ConfigureAwait(false);
                if (code == 0) return carrier;
            }
        }

        return null;
    }

    /// <summary>
    /// LAND3's tidy on a pull request's word (PLUGHOOK1a, design §2.1 occasion 1): after the containment pass, the repository's
    /// entries are asked about, and each recorded session branch a completed pull request carried goes too, with the tidy's
    /// keeps. Each removal is kept on the entry whose answer allowed it.
    /// </summary>
    private async Task<IReadOnlyList<TidiedBranch>> TidyCarriedAsync(
        string root, string repository, string workspace, IReadOnlyCollection<string> handled, string? pressed,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse, CancellationToken ct)
    {
        await AskStatesAsync([(root, repository, workspace)], landedMayGo: false, ct).ConfigureAwait(false);
        var line = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var carriers = await CarriersAsync(root, repository, line, ct).ConfigureAwait(false);
        if (carriers.Count == 0) return [];

        var carried = new List<(string Branch, string Tip, PullRequestCarrier Carrier)>();
        foreach (var branch in Grown.All()
                     .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
                     .Select(entry => entry.Branch)
                     .Where(branch => branch.StartsWith(SessionPrefix, StringComparison.Ordinal)
                                      && !string.Equals(branch, pressed, StringComparison.Ordinal) && !handled.Contains(branch))
                     .Distinct(StringComparer.Ordinal))
        {
            var (code, tip, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
                .ConfigureAwait(false);
            if (code != 0) continue;
            if (await CarrierOfAsync(root, $"refs/heads/{branch}", carriers, ct).ConfigureAwait(false) is { } carrier)
            {
                carried.Add((branch, tip.Trim(), carrier));
            }
        }

        if (carried.Count == 0) return [];
        var results = new List<TidiedBranch>();
        var worktrees = await WorktreesAsync(root, ct).ConfigureAwait(false);
        using var hold = TreeLock.TryReplaying(home, workspace, repository);
        var busy = hold is not null && inUse is not null
            ? new HashSet<string>((await inUse(ct).ConfigureAwait(false)).Select(Normal), StringComparer.OrdinalIgnoreCase)
            : null;
        var unasked = inUse is null
            ? "whether a session still holds its tree was not asked here"
            : "a session was starting in one of the repository's trees";
        foreach (var (branch, tip, carrier) in carried)
        {
            var tidied = await TidyOneAsync(root, branch, tip, worktrees.GetValueOrDefault(branch), busy, unasked, ct).ConfigureAwait(false)
                with { CarriedBy = carrier.Entry.Branch };
            results.Add(tidied);
            if (!tidied.Removed) continue;
            try
            {
                Recorded.Carried(carrier.Entry, new CarriedBranch(branch, tip, _plugins.Now, CarriedBy.Tidy));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The branch went on a confirmed answer; only the note of it on the record is lost.
            }
        }

        return results;
    }
}
