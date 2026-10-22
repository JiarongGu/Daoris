namespace Daoris.Driver;

/// <summary>
/// A completed pull request that carried work here (PLUGHOOK1a, D148 point 4): the landing entry whose kept answer it is, the
/// source commit it merged, and the landed branch's tip where the answer cleared that branch too. A session branch at or under
/// either goes, every keep still applying.
/// </summary>
public sealed record PullRequestCarrier(LandedBranch Entry, string Source, string? ClearedTip)
{
    /// <summary>
    /// What a removal on its word rests on (FABRIC-END4a, third review): the form of the line its merge commit was found on, at the commit
    /// read, and the cleared tip's branch where that tip is what carried the revision (<see cref="PullRequestAsks.CarrierOfAsync"/>).
    /// </summary>
    internal IReadOnlyList<(string Ref, string Commit)> Rests { get; init; } = [];
}

/// <summary>
/// A landed branch's pull request, asked about and judged (PLUGHOOK1a, PLUGHOOK1c, D148; PART10l, out of
/// <c>SessionTrees.PullRequests.cs</c>): one occasion's asks over the plugin wire, <i>Ask again</i>, what a kept answer proves
/// here, and the completed pull requests that carried work. Over the trees' context for the wire, the record and the line.
/// </summary>
/// <remarks>
/// The occasions that ask are the clean-up's look, bringing up to date's look, LAND3's tidy, <i>Ask again</i> and the due moments
/// (FABRIC-END6, <see cref="PullRequestMoments"/>), each by its method's name (<c>PullRequestOccasionTests</c>):
/// <see cref="AskStatesAsync"/> is called from those alone.
/// </remarks>
internal sealed class PullRequestAsks(TreeContext context)
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
    /// <param name="moment">
    /// A due moment's entries (FABRIC-END6): these alone, each still due by D148's minute, asked whether or not the answer could remove
    /// something now.
    /// </param>
    internal async Task<IReadOnlyList<StateAnswer>> AskStatesAsync(
        IEnumerable<(string Root, string Repository, string Workspace)> repositories, bool landedMayGo, CancellationToken ct,
        LandedBranch? again = null, IReadOnlyCollection<LandedBranch>? moment = null)
    {
        var now = context.Plugins.Now;
        var asks = new List<StateAsk>();
        foreach (var (root, repository, workspace) in repositories)
        {
            asks.AddRange(await DueAsksAsync(root, repository, workspace, now, landedMayGo, again, ct, moment).ConfigureAwait(false));
        }

        var answers = await context.Plugins.AskStatesAsync(asks, ct).ConfigureAwait(false);
        foreach (var answer in answers)
        {
            // FABRIC-END6c: each answer stamped with the time its own request went out, the minute counted from there for every occasion.
            // A look's minute for an entry it sent nothing for (a plugin not ready, or its bound first) is given back, so the next look asks
            // again as D148 says; a due moment's stays, so a plugin not ready is not started again inside it.
            var asked = answer.AskedAt ?? now;
            if (answer.AskedAt is { } sent) PullRequestMinute.Asked(context.Home, answer.Ask.Entry, sent);
            else if (again is null && moment is null) PullRequestMinute.Release(context.Home, answer.Ask.Entry, now);
            try
            {
                if (answer.Answer is { } said)
                {
                    context.Recorded.Answered(answer.Ask.Entry, said with { AskedAt = asked });
                }
                else if (answer.Failure is { } code && code is not (PullRequestCodes.NotAsked or PullRequestCodes.Unready))
                {
                    context.Recorded.AskFailed(answer.Ask.Entry, new PullRequestAskFailed(code, answer.Ask.Plugin, asked));
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
        string root, string repository, string workspace, DateTimeOffset now, bool landedMayGo, LandedBranch? again, CancellationToken ct,
        IReadOnlyCollection<LandedBranch>? moment = null)
    {
        var rule = LandingRules.Choose(context.Config(), repository, workspace).Rule;
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        // The repository's refs read once for its entries (PROC1d-ii), and only where one is due.
        var refs = RefSnapshot.Take(root);
        IReadOnlyList<LandedProofs.LineForm>? forms = null;
        IReadOnlyList<string>? unlanded = null;
        // GITAUTH1b: origin as the one reader gives it, read once and only where an entry is asked (the git-auth design §4.4).
        var originRead = false;
        string? origin = null;

        var asks = new List<StateAsk>();
        foreach (var entry in context.Recorded.Entries().Where(entry => Ours(entry, repository) && (again is null || SameLanding(entry, again))))
        {
            if (!PullRequestAsking.Due(entry, now, again is not null)) continue;
            if (PullRequestAsking.PluginFor(entry, rule) is not { } plugin) continue;
            // A due moment's own entries alone, its answer now feeding the end as well as a removal (FABRIC-END6).
            if (moment is not null && !moment.Any(each => SameLanding(each, entry))) continue;
            if (again is null && moment is null)
            {
                forms ??= await LandedProofs.LineFormsAsync(root, line, ct, refs).ConfigureAwait(false);
                unlanded ??= await UnlandedSessionTipsAsync(root, ct, refs).ConfigureAwait(false);
                if (!await CouldRemoveAsync(root, entry, line, forms, unlanded, landedMayGo, ct, refs).ConfigureAwait(false)) continue;
            }

            // FABRIC-END6c: D148's minute shared with every other occasion in this process, taken last so only an entry asked takes it. A
            // due moment took its entries' minutes itself, just before it asked; Ask again, a person's door, asks whatever it says.
            if (again is null && moment is null && !PullRequestMinute.TryTake(context.Home, entry, now)) continue;
            if (!originRead)
            {
                origin = await GitRemotes.AddressOf(root, "origin", ct: ct).ConfigureAwait(false);
                originRead = true;
            }

            asks.Add(new StateAsk(entry, plugin,
                new StateFrame(repository, workspace, root, entry.Branch, line, entry.PullRequest, entry.PushedTip, origin)));
        }

        return asks;
    }

    /// <summary>
    /// A due moment's asks (FABRIC-END6, the design §A4.3, link 6): origin's line moved, Daoris pushed, or the window's focus, as
    /// <see cref="PullRequestMoments"/> judged it. These entries alone, each still due by D148's minute as the record keeps it, are asked
    /// of their plugin whether or not the answer could remove something now, since it now feeds the end (§B) as well as a removal. Kept
    /// as an occasion keeps it; it removes nothing. How many came back completed where the kept answer was not.
    /// </summary>
    internal async Task<int> AskAtMomentAsync(
        string root, string repository, string workspace, IReadOnlyCollection<LandedBranch> entries, CancellationToken ct)
    {
        var answers = await AskStatesAsync([(root, repository, workspace)], landedMayGo: false, ct, moment: entries).ConfigureAwait(false);
        return answers.Count(answer => answer.Answer?.State == PullRequestStates.Completed
            && answer.Ask.Entry.PullRequestState?.State != PullRequestStates.Completed);
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
    internal async Task<PullRequestAskedAgain> AskAgainAsync(string root, LandedBranch entry, CancellationToken ct = default)
    {
        var workspace = RemoteTarget.Workspace(entry.Workspace);
        var asked = new PullRequestAskedAgain(entry);
        if (entry.PullRequestState?.State == PullRequestStates.Completed)
        {
            asked = asked with { Final = true };
        }
        else if (PullRequestAsking.PluginFor(entry, LandingRules.Choose(context.Config(), entry.Repository, workspace).Rule) is null)
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

        var now = context.Recorded.Entries().LastOrDefault(each => SameLanding(each, entry)) ?? entry;
        if (now.PullRequestState is not { } kept) return asked with { Entry = now };
        var line = (await context.LineAsync(root, now.Repository, workspace, ct).ConfigureAwait(false)).Branch;
        var forms = await LandedProofs.LineFormsAsync(root, line, ct).ConfigureAwait(false);
        var tip = now.GoneAt is null ? await LandedProofs.StandingTipAsync(root, now, ct).ConfigureAwait(false) : null;
        return asked with { Entry = now, Verdict = await VerdictAsync(root, line, forms, kept, tip, ct).ConfigureAwait(false) };
    }

    /// <summary>A landing's own branch in this repository: a session branch's name is D88's to judge.</summary>
    private static bool Ours(LandedBranch entry, string repository) =>
        string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
        && !entry.Branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Whether an entry's answer could remove something now (design §2.1): it stands, this occasion may remove a landed branch,
    /// and D102's proof did not clear it; or its recorded tip, where git still holds that commit, contains the tip of a session
    /// branch that stands and that D88 does not clear.
    /// </summary>
    private static async Task<bool> CouldRemoveAsync(
        string root, LandedBranch entry, string? line, IReadOnlyList<LandedProofs.LineForm> forms, IReadOnlyList<string> unlanded,
        bool landedMayGo, CancellationToken ct, RefSnapshot? refs = null)
    {
        if (entry.GoneAt is null && landedMayGo && await LandedProofs.StandingTipAsync(root, entry, ct, refs).ConfigureAwait(false) is { } tip)
        {
            var proof = await LandedProofs.ProveAsync(root, tip, line, forms, ct).ConfigureAwait(false);
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
    private static async Task<IReadOnlyList<string>> UnlandedSessionTipsAsync(string root, CancellationToken ct, RefSnapshot? refs = null)
    {
        var tips = new List<string>();
        foreach (var branch in await LandedProofs.SessionBranchesAsync(root, ct, refs).ConfigureAwait(false))
        {
            var (code, log, _) = await TreeFollows.UnlandedLogAsync(root, branch, ct).ConfigureAwait(false);
            if (code == 0 && !string.IsNullOrWhiteSpace(log)) tips.Add($"refs/heads/{branch}");
        }

        return tips;
    }

    /// <summary>
    /// What a kept answer proves here (design §2.3): git asked only what the answer needs, in the proof's order, and the pure
    /// table (<see cref="PullRequestProof"/>) says the rest.
    /// </summary>
    /// <param name="standingTip">The landed branch's tip where it stands as the landing's, else null.</param>
    internal static async Task<PullRequestVerdict> VerdictAsync(
        string root, string? line, IReadOnlyList<LandedProofs.LineForm> forms, PullRequestState kept, string? standingTip, CancellationToken ct)
    {
        if (kept.State != PullRequestStates.Completed || kept.MergeCommit is not { } merge || kept.SourceCommit is not { } source)
        {
            return PullRequestProof.Judge(kept, new PullRequestFacts(line, null, false, standingTip is not null, false));
        }

        string? mergeOn = null;
        foreach (var form in forms)
        {
            // FABRIC-END4a, third review: at the commit the form was read at, which a removal by this verdict verifies.
            var (on, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", merge, form.Rev], ct).ConfigureAwait(false);
            if (on != 0) continue;
            mergeOn = form.Name;
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
    /// <param name="refs">The judgement's refs (PROC1d-ii); null asks git.</param>
    internal async Task<IReadOnlyList<PullRequestCarrier>> CarriersAsync(
        string root, string repository, string? line, CancellationToken ct, RefSnapshot? refs = null)
    {
        var carriers = new List<PullRequestCarrier>();
        var entries = context.Recorded.Entries()
            .Where(entry => Ours(entry, repository) && entry.PullRequestState?.State == PullRequestStates.Completed).ToList();
        if (entries.Count == 0) return carriers;
        var forms = await LandedProofs.LineFormsAsync(root, line, ct, refs).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            var tip = entry.GoneAt is null ? await LandedProofs.StandingTipAsync(root, entry, ct, refs).ConfigureAwait(false) : null;
            var verdict = await VerdictAsync(root, line, forms, entry.PullRequestState!, tip, ct).ConfigureAwait(false);
            if (verdict.Carries)
            {
                carriers.Add(new PullRequestCarrier(entry, entry.PullRequestState!.SourceCommit!, verdict.Clears ? tip : null)
                {
                    Rests = LandedProofs.RestsOn(forms.FirstOrDefault(form => form.Name == verdict.Form)),
                });
            }
        }

        return carriers;
    }

    /// <summary>
    /// The carrier whose pull request carried <paramref name="revision"/>: at or under its source commit, or under a tip it cleared; that tip's
    /// branch at it is then one more thing a removal by it rests on (FABRIC-END4a, third review).
    /// </summary>
    internal static async Task<PullRequestCarrier?> CarrierOfAsync(
        string root, string revision, IReadOnlyList<PullRequestCarrier> carriers, CancellationToken ct)
    {
        foreach (var carrier in carriers)
        {
            foreach (var over in new[] { carrier.Source, carrier.ClearedTip })
            {
                if (over is null) continue;
                var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", revision, over], ct).ConfigureAwait(false);
                if (code != 0) continue;
                return over == carrier.Source ? carrier : carrier with { Rests = [.. carrier.Rests, ($"refs/heads/{carrier.Entry.Branch}", over)] };
            }
        }

        return null;
    }
}
