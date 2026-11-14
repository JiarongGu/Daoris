using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What one run of forming and replaying asks: the repository, its workspace, its registered checkout (where git is
/// asked of the line, never worked in) and the line by name.
/// </summary>
internal sealed record IntegrationReplayAsk(string Repository, string Workspace, string Root, string Line)
{
    /// <summary>
    /// The merge commit's message for a request, or null for a request it names none of. None takes the merge form's own,
    /// <c>Merge branch '&lt;branch&gt;'</c>, as the landing's merge makes it (<c>TreeLands</c>): a request keeps no composed message (a later part
    /// writes it); a delivery's merge says its intent's (<see cref="IntegrationPresses"/>).
    /// </summary>
    public Func<IntegrationRequest, string?>? Messages { get; init; }

    /// <summary>
    /// The say door a member's session is told through where its conflict goes to it (its <c>session</c>); null where
    /// nothing here reaches a conversation, and the member is held for an act instead.
    /// </summary>
    public RebaseTelling.Say? Say { get; init; }

    /// <summary>
    /// The person's choices the acceptances of the generated folders' writers and the line rules are read from (as <c>BranchReplays</c> reads them);
    /// null where they could not be read, and a resolve then writes nothing again and settles by no line rule, as with none accepted.
    /// </summary>
    public DriverConfig? Config { get; init; }

    /// <summary>Which queued requests are ready, and in what order, where the line's requests are an ask's parts; null takes the queue as it stands.</summary>
    public TrainSelection? Train { get; init; }

    public string? TrainBase { get; init; }
}

/// <summary>What a form-and-replay answered (<see cref="IntegrationReplayRuns"/>), in words, with the candidate and its members as they now stand.</summary>
internal sealed record IntegrationReplayRun(string Kind, string Said)
{
    public IntegrationCandidate? Candidate { get; init; }

    /// <summary>The claim the candidate is held under; null where none is made (nothing queued, a candidate already standing).</summary>
    public IntegrationClaim? Claim { get; init; }

    public IReadOnlyList<IntegrationMember> Members { get; init; } = [];

    /// <summary>The line's integration tree, where it is made or found; null where none is reached.</summary>
    public string? Tree { get; init; }
}

/// <summary>What a form-and-replay answers.</summary>
internal static class IntegrationReplayRuns
{
    /// <summary>No open request of the line stands outside a candidate: nothing is written.</summary>
    public const string Idle = "idle";

    /// <summary>A candidate stands for the line already, held by another claim: nothing is written and no claim is made.</summary>
    public const string Standing = "standing";

    /// <summary>git could not say where the line stands, or the queue moved while the candidate is formed: nothing is written.</summary>
    public const string Unread = "unread";

    /// <summary>
    /// A step could not go on and ended <c>held</c>, said why (the tree could not be made, git could not answer): the candidate stands and the
    /// step is tried again at the next look.
    /// </summary>
    public const string Held = "held";

    /// <summary>Every member is judged: replayed, contained, or handed back. The candidate goes on to its gates, or ends with nothing to promote.</summary>
    public const string Replayed = "replayed";

    /// <summary>A resume of a candidate found something other than a step to run again (it ended, git could not say, the line holds its tip).</summary>
    public const string Resumed = "resumed";
}

/// <summary>
/// The integration run's form and replay steps over git (3): a candidate
/// made from a line's queued requests, and each member judged and replayed onto it in the line's integration tree, a warm tree of Daoris's own
/// under the trees home. The line's run calls it (<see cref="IntegrationLines"/>).
/// </summary>
/// <remarks>
/// <para><b>The tree.</b> One per line, under <c>&lt;trees home&gt;/&lt;workspace&gt;/&lt;repository&gt;/i-&lt;line&gt;</c>: made as a replay's tree is
/// (<c>worktree add --detach --lock --reason</c>, then the binding check), kept warm between candidates and never made in a person's checkout.
/// It stays detached, so no ref but its own HEAD moves: the line is promotion's.</para>
/// <para><b>A member's replay</b> judges containment first, by the frozen tip (<see cref="IntegrationRequest.Tip"/>, never the branch's live
/// tip): the line holds it, whole, by ancestry or by content, then patch (<see cref="LineHolds"/>), and it is settled at once; an earlier
/// member's replay holds it, and it stays in the candidate, <c>contained</c> by that member and provisional. Otherwise the tip is merged
/// <c>--no-ff</c> onto the candidate so far, as the merge tool's candidate merges it, so the merge commit's second parent is the frozen tip.
/// Its conflicts are then resolved (<see cref="IntegrationResolves"/>, part e3b) where the repository settles every conflicted
/// path by a rule it declares, inside the same effect, so the merge commit is made once with the settled files in it. Any path left is a
/// conflict of meaning: the merge is aborted, the tree is put back at the candidate so far, and the member is <c>conflict</c>, then goes by the
/// request's <c>conflicts</c> at its own step: <c>session</c> tells its session to rebase, keyed by the session and the frozen tip, and
/// hands the member back with its request closed; <c>hold</c>, and a session that cannot be told, sets it aside as <c>held</c> with its request
/// standing open. The next member goes on either way.</para>
/// <para><b>Crash safety.</b> Every effect begins by putting the tree back at its recorded start (the candidate's tip so far, or its base) and
/// ends in one store write under <see cref="IntegrationSteps.RunAsync"/>, so a step a crash cut, run again as the next attempt, leaves what
/// the first made of the tree behind it. Nothing is written outside a step's end.</para>
/// </remarks>
internal sealed class IntegrationReplays(IntegrationStore store, string treesRoot)
{
    /// <summary>What the integration tree's folder name starts with, beside a session tree's <c>s-</c>, a replay's <c>r-</c> and an opinion's copy's <c>o-</c>.</summary>
    internal const string Prefix = "i-";

    private const string Reason = "Daoris's integration tree: kept warm between candidates and never removed while a run holds it";

    /// <summary>
    /// A test's seam: called inside a member's replay effect once a clean merge is made (<c>after-merge</c>), once its conflicts are settled and
    /// before its commit (<c>after-resolve</c>, passed by a clean merge too), and once the commit is made and before the step's end is written
    /// (<c>after-commit</c>); and inside a member's conflict step once its session has been asked (<c>after-tell</c>), so a test can stop the
    /// process there. Production sets none.
    /// </summary>
    internal Func<string, Task>? Seam { get; set; }

    /// <summary>The line's integration tree: stable for a line, so the next candidate finds it warm.</summary>
    internal string TreeOf(string workspace, string repository, string line)
    {
        var slug = new string([.. line.Select(each => char.IsAsciiLetterOrDigit(each) ? each : '-')]).Trim('-');
        if (slug.Length > 24) slug = slug[..24];
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line)))[..8].ToLowerInvariant();
        return Path.Combine(treesRoot, workspace, repository, $"{Prefix}{(slug.Length == 0 ? "line" : slug)}-{digest}");
    }

    /// <summary>
    /// Form a candidate on the line from its queued requests and replay each member onto it (step 3): <c>batch</c> requests in
    /// their arrival order, the line's commit read once. Answers the candidate that already stands for the line, with no claim, where one does.
    /// </summary>
    public async Task<IntegrationReplayRun> RunAsync(IntegrationReplayAsk ask, CancellationToken ct = default)
    {
        if (store.StandingFor(ask.Repository, ask.Line) is { } standing)
        {
            return new(IntegrationReplayRuns.Standing, $"candidate {standing.Id} stands for `{ask.Line}` already: nothing was formed.") { Candidate = standing };
        }

        var queued = store.Queued(ask.Repository, ask.Line);
        if (ask.Train is { } train && queued.Count > 0) queued = await train.ReadyAsync(queued, ct).ConfigureAwait(false);
        if (queued.Count == 0) return new(IntegrationReplayRuns.Idle, $"no request of `{ask.Line}` is queued and ready.");

        if (ask.TrainBase is { } trainBase && TrainBranch.AskOf(ask.Line) is { } trainAsk)
        {
            var based = await TrainBases.PrepareAsync(ask.Root, trainAsk, trainBase, ct).ConfigureAwait(false);
            if (based.Kind == TrainBaseKinds.Unread) return new(IntegrationReplayRuns.Unread, based.Said);
        }

        if (await MachinePorts.Repository.CommitOfAsync(ask.Root, $"refs/heads/{ask.Line}", ct).ConfigureAwait(false) is not { Length: > 0 } commit)
        {
            return new(IntegrationReplayRuns.Unread, $"git could not read where `{ask.Line}` stands (it names no commit): nothing was formed, and it is asked again at the next look.");
        }

        // A stop is about the line as it stands, and its requests stay open: formed again at the same commit, the candidate would only be stopped again.
        if (store.LastEnded(ask.Repository, ask.Line) is { EndedWhy: IntegrationCandidateEnds.Stopped } stopped && string.Equals(stopped.Base, commit.Trim(), StringComparison.Ordinal))
        {
            return new(IntegrationReplayRuns.Idle, $"the integration of `{ask.Line}` stopped at {TextBounds.ShortCommit(stopped.Base, 8)}: nothing is formed again until the line moves.");
        }

        // The first request's policy is the candidate's, and only requests of its epoch join it (below).
        var batch = Math.Clamp(queued[0].Policy.Batch ?? 1, 1, LandingIntegrate.MaxBatch);
        // A candidate the judge split (its members run one at a time) is followed by one of a single member.
        if (store.LastEnded(ask.Repository, ask.Line) is { } last && store.Steps(last.Id).LastOrDefault() is { Step: IntegrationStepNames.Judge, Outcome: IntegrationStepOutcomes.Split })
        {
            batch = 1;
        }

        IntegrationFormed formed;
        try
        {
            // Only the first's epoch batches: the arrivals after it that share it, up to the first of another; an older record has none and goes alone.
            var epoch = store.RequestEpoch(queued[0].Id);
            var members = new List<string> { queued[0].Id };
            foreach (var next in queued.Skip(1).Take(batch - 1))
            {
                if (!IntegrationEpoch.Shared(epoch, store.RequestEpoch(next.Id))) break;
                members.Add(next.Id);
            }

            formed = store.Form(new IntegrationForming(ask.Repository, ask.Workspace, ask.Line, commit.Trim(), members));
        }
        catch (IntegrationRefusedException refused)
        {
            return new(IntegrationReplayRuns.Unread, $"the queue moved while the candidate was formed ({refused.Message}): nothing was written, and it is read again at the next look.");
        }

        if (!formed.Claimed)
        {
            return new(IntegrationReplayRuns.Standing, $"candidate {formed.Candidate.Id} stands for `{ask.Line}` already: nothing was formed.") { Candidate = formed.Candidate };
        }

        return await GoAsync(formed.Claim!, ask, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Go on with a candidate the claim holds, at the come-up or after a step's effect threw: its promotion judged by ancestry and a step a
    /// crash cut ended for its next attempt (<see cref="IntegrationSteps.ResumeAsync"/>), then every step the members still need.
    /// </summary>
    public async Task<IntegrationReplayRun> ContinueAsync(IntegrationClaim claim, IntegrationReplayAsk ask, CancellationToken ct = default)
    {
        var resumed = await new IntegrationSteps(store).ResumeAsync(claim, ask.Root, ct).ConfigureAwait(false);
        if (resumed.Kind is not (IntegrationResumes.Again or IntegrationResumes.Next))
        {
            return new(IntegrationReplayRuns.Resumed, resumed.Said) { Candidate = resumed.Candidate, Claim = claim, Members = Members(claim) };
        }

        return await GoAsync(claim, ask, ct).ConfigureAwait(false);
    }

    private async Task<IntegrationReplayRun> GoAsync(IntegrationClaim claim, IntegrationReplayAsk ask, CancellationToken ct)
    {
        var steps = new IntegrationSteps(store);
        var candidate = store.Candidate(claim.Candidate) ?? throw new IntegrationClaimLostException(claim, "the store holds no such candidate");
        var tree = TreeOf(ask.Workspace, ask.Repository, ask.Line);
        var warm = new WarmTree();

        if (!store.Steps(candidate.Id).Any(each => each is { Step: IntegrationStepNames.Form, Outcome: IntegrationStepOutcomes.Done }))
        {
            Task<IntegrationStepEnd> Form(IntegrationStep _, CancellationToken token) => FormEffectAsync(candidate, ask, tree, warm, token);
            IntegrationStep formed;
            if (store.OpenStep(candidate.Id) is { Step: IntegrationStepNames.Form } begun)
            {
                // The form step the candidate's own write began: its effect is run, then its end written.
                formed = store.End(claim, begun.Id, await Form(begun, ct).ConfigureAwait(false));
            }
            else if (store.OpenStep(candidate.Id) is { } other)
            {
                return Answer(IntegrationReplayRuns.Held, $"its {other.Step} has no end: a step a crash cut is the resume's to judge.", claim, tree);
            }
            else
            {
                formed = await steps.RunAsync(claim, IntegrationStepNames.Form, Form, lineCommit: candidate.Base, ct: ct).ConfigureAwait(false);
            }

            if (formed.Outcome != IntegrationStepOutcomes.Done) return Answer(IntegrationReplayRuns.Held, formed.Said ?? "the form step could not go on.", claim, tree);
        }

        while (true)
        {
            var next = store.Members(claim.Candidate).FirstOrDefault(each => each is { Stands: true, State: IntegrationMemberStates.Formed or IntegrationMemberStates.Conflict });
            if (next is null) break;

            var replaying = next.State == IntegrationMemberStates.Formed;
            var member = next;
            var ended = await steps.RunAsync(
                claim, replaying ? IntegrationStepNames.Replay : IntegrationStepNames.Conflict,
                (_, token) => replaying ? ReplayEffectAsync(claim, member, ask, tree, warm, token) : HandBackEffectAsync(claim, member, ask, tree, token),
                member.Request, candidate.Base, ct).ConfigureAwait(false);
            if (ended.Outcome == IntegrationStepOutcomes.Held) return Answer(IntegrationReplayRuns.Held, ended.Said ?? "a step could not go on.", claim, tree);

            // An end that leaves the member where it stands would be run again for ever.
            var after = store.Members(claim.Candidate).FirstOrDefault(each => each.Request == member.Request);
            if (after is { Stands: true } && after.State == next.State)
            {
                return Answer(IntegrationReplayRuns.Held, $"request {member.Request} stands where it did after its {ended.Step}: it is tried again at the next look.", claim, tree);
            }
        }

        var now = store.Candidate(claim.Candidate);
        var said = now is { Stands: false }
            ? $"candidate {claim.Candidate} ended ({now.EndedWhy}): nothing is left to promote."
            : $"candidate {claim.Candidate} is replayed on `{ask.Line}`'s commit {TextBounds.ShortCommit(candidate.Base, 8)}, ready for its gates.";
        return Answer(IntegrationReplayRuns.Replayed, said, claim, tree);
    }

    private IntegrationReplayRun Answer(string kind, string said, IntegrationClaim claim, string? tree) =>
        new(kind, said) { Candidate = store.Candidate(claim.Candidate), Claim = claim, Members = Members(claim), Tree = tree };

    private IReadOnlyList<IntegrationMember> Members(IntegrationClaim claim) => store.Members(claim.Candidate);

    // ——— The form step's effect: the line's integration tree, made or found, at the candidate's base.

    /// <summary>
    /// The commit the line's integration tree is found, bound-checked and put back at, clean, by the step this very run just ended, or null: the
    /// next step of the same run begins from it without finding the tree and putting it back again. Any step that begins on it clears it first,
    /// and a run that resumes a candidate never has it, so a step a crash cut still begins from a tree put back.
    /// </summary>
    private sealed class WarmTree
    {
        public string? At { get; set; }
    }

    private async Task<IntegrationStepEnd> FormEffectAsync(IntegrationCandidate candidate, IntegrationReplayAsk ask, string tree, WarmTree warm, CancellationToken ct)
    {
        warm.At = null;
        if (await OpenAsync(ask, tree, candidate.Base, ct).ConfigureAwait(false) is { } notOpened) return Held(notOpened);
        if (await ResetAsync(tree, candidate.Base, ct).ConfigureAwait(false) is { } notReset) return Held(notReset);
        warm.At = candidate.Base;
        var count = store.Members(candidate.Id).Count;
        return new(IntegrationStepOutcomes.Done)
        {
            Said = $"formed on `{ask.Line}`'s commit {TextBounds.ShortCommit(candidate.Base, 8)} with {count} {(count == 1 ? "request" : "requests")}, in the line's integration tree.",
        };
    }

    /// <summary>
    /// The line's integration tree, found where git lists it, or added there (<c>--detach --lock --reason</c>, as a replay's tree is) at
    /// <paramref name="at"/>; null where it is ready, else the words that say why it is not. A folder git does not list is never taken over.
    /// </summary>
    private async Task<string?> OpenAsync(IntegrationReplayAsk ask, string tree, string at, CancellationToken ct)
    {
        if (await TreeWorktrees.EntriesAsync(ask.Root, ct).ConfigureAwait(false) is not { } entries)
        {
            return "git could not list the working trees, so the integration tree was neither found nor made.";
        }

        var found = entries.Any(each => TreeGit.SamePath(each.Path, tree));
        if (!found)
        {
            if (Path.Exists(tree)) return $"a folder stands where the integration tree goes and git does not list it as a working tree: {Path.GetFileName(tree)} is left as it is.";
            Directory.CreateDirectory(Path.GetDirectoryName(tree)!);
            var added = await MachinePorts.Repository.AddWorktreeAsync(ask.Root, new GitWorktreeAdd(tree, at, LockReason: Reason), ct).ConfigureAwait(false);
            if (!added.Ok) return $"Daoris could not make the integration tree: {ContentHolds.Said(added.Error)}";
        }

        // A configuration binding linked trees to another checkout would have a merge here rewrite that checkout's files.
        if (await TreeBinding.RefusalAsync(tree, ct).ConfigureAwait(false) is { } bound)
        {
            if (!found) await MachinePorts.Repository.RemoveWorktreeAsync(ask.Root, tree, CancellationToken.None).ConfigureAwait(false);
            return $"nothing is replayed in the integration tree: {bound}.";
        }

        return null;
    }

    /// <summary>
    /// The tree put back at <paramref name="commit"/>, detached: a merge a crash cut aborted, what it changed reset, untracked files it made
    /// removed (ignored ones stay, as private files a gate reads do). Null where it is; else git's words.
    /// </summary>
    internal static async Task<string?> ResetAsync(string tree, string commit, CancellationToken ct)
    {
        var done = await MachinePorts.Repository.PutBackAsync(tree, commit, ct).ConfigureAwait(false);
        return done.Ok ? null : $"git could not put the integration tree back at {TextBounds.ShortCommit(commit, 8)} ({ContentHolds.Said(done.Error)}).";
    }

    private static IntegrationStepEnd Held(string said) => new(IntegrationStepOutcomes.Held) { Said = said };

    // ——— The replay step's effect: containment first, then the merge.

    private async Task<IntegrationStepEnd> ReplayEffectAsync(
        IntegrationClaim claim, IntegrationMember member, IntegrationReplayAsk ask, string tree, WarmTree warm, CancellationToken ct)
    {
        var candidate = store.Candidate(claim.Candidate)!;
        var request = store.Request(member.Request)!;
        var members = store.Members(candidate.Id);
        var onto = candidate.Tip ?? candidate.Base;
        var alreadyThere = warm.At == onto;
        warm.At = null;
        if (!alreadyThere)
        {
            if (await OpenAsync(ask, tree, onto, ct).ConfigureAwait(false) is { } notOpened) return Held(notOpened);
            if (await ResetAsync(tree, onto, ct).ConfigureAwait(false) is { } notReset) return Held(notReset);
        }

        // A merge rule's press is judged by the landing against the line's commits just before it is asked, so ancestry alone is read again here.
        var ancestryOnly = request.Route == IntegrationRoutes.Merge;

        // 1. What the line holds, whole, is settled at once: nothing to merge and nothing to gate.
        if (await HoldsAsync(tree, request.Tip, candidate.Base, ask.Line, ancestryOnly, ct).ConfigureAwait(false))
        {
            return new(IntegrationStepOutcomes.Done)
            {
                Said = $"the line holds {TextBounds.ShortCommit(request.Tip, 8)}: it is contained and settled at once.",
                Members = [new IntegrationMemberChange(member.Request, IntegrationMemberStates.Contained) { Said = $"`{ask.Line}` holds its work already.", Ends = true }],
                Closes = [new IntegrationRequestClose(member.Request, IntegrationRequestCloses.Settled)],
                Ends = EndIfNothingLeft(members, member.Request),
            };
        }

        // 2. What only an earlier member holds stays in the candidate, provisional: it settles with that member, and goes back to its own
        // request, judged again, where that member does not promote.
        foreach (var earlier in members.Where(each => each is { Stands: true, State: IntegrationMemberStates.Replayed, Replayed: not null } && each.Position < member.Position))
        {
            if (!await HoldsAsync(tree, request.Tip, earlier.Replayed!, ask.Line, ancestryOnly, ct).ConfigureAwait(false)) continue;
            return new(IntegrationStepOutcomes.Done)
            {
                Said = $"the replay of request {earlier.Request} holds {TextBounds.ShortCommit(request.Tip, 8)}: contained by it, provisionally.",
                Members = [new IntegrationMemberChange(member.Request, IntegrationMemberStates.Contained)
                {
                    ContainedBy = earlier.Request, Said = $"request {earlier.Request}'s replay holds its work: it settles when that member promotes.",
                }],
            };
        }

        // 3. The rest merged `--no-ff` onto the candidate so far, the second parent the frozen tip. A merge rule's press is merged the same way.
        if (request.Route == IntegrationRoutes.Merge) return await PressedAsync(tree, onto, member, request, ask, ct).ConfigureAwait(false);
        var merged = await MachinePorts.Repository.MergeAsync(tree, new GitMergeAsk(request.Tip, GitMergeKind.Stage), ct).ConfigureAwait(false);
        IReadOnlyList<string> paths = [];
        if (!merged.Ok)
        {
            paths = merged.Unmerged;
            if (paths.Count == 0)
            {
                return await ConflictAsync(
                    tree, onto, member.Request, $"git would not merge it ({ContentHolds.Said(merged.Error)})", ct).ConfigureAwait(false);
            }
        }
        else if (Seam is { } afterMerge)
        {
            await afterMerge("after-merge").ConfigureAwait(false);
        }

        // 4. Each conflict the repository settles by a rule it declares is settled, and the joins union left are set apart, in the same merge.
        var resolved = await IntegrationResolves.ResolveAsync(
            tree, ask.Root, ask.Repository, ask.Workspace, candidate.Base, onto, request.Tip, paths, ask.Config, ct).ConfigureAwait(false);
        if (!resolved.Stands)
        {
            return await ConflictAsync(tree, onto, member.Request, resolved.Refused ?? $"it conflicts in {Names(resolved.Unsettled)}", ct).ConfigureAwait(false);
        }

        if (Seam is { } afterResolve) await afterResolve("after-resolve").ConfigureAwait(false);
        var message = ask.Messages?.Invoke(request) ?? $"Merge branch '{request.Branch}'";
        var committed = await MachinePorts.Repository.CommitStagedAsync(tree, message, ct).ConfigureAwait(false);
        if (!committed.Ok)
        {
            return await ConflictAsync(tree, onto, member.Request, $"its merge commit could not be made ({ContentHolds.Said(committed.Error)})", ct).ConfigureAwait(false);
        }

        if (await MachinePorts.Repository.HeadAsync(tree, ct).ConfigureAwait(false) is not { } replayed) return Held("git could not read the integration tree's commit.");
        if (Seam is { } afterCommit) await afterCommit("after-commit").ConfigureAwait(false);
        return new(IntegrationStepOutcomes.Done)
        {
            Said = $"replayed {TextBounds.ShortCommit(request.Tip, 8)} onto {TextBounds.ShortCommit(onto, 8)}, as {TextBounds.ShortCommit(replayed, 8)}.{resolved.Said}",
            Tip = replayed,
            Members = [new IntegrationMemberChange(member.Request, IntegrationMemberStates.Replayed) { Replayed = replayed }],
        };
    }

    /// <summary>
    /// A merge rule's press (the legacy exception): one <c>merge --no-ff</c> with today's message, its repository's hooks run
    /// as the checkout's merge ran them, nothing resolved; a conflict, or a merge its own hook refused, is the press's refusal.
    /// </summary>
    private async Task<IntegrationStepEnd> PressedAsync(
        string tree, string onto, IntegrationMember member, IntegrationRequest request, IntegrationReplayAsk ask, CancellationToken ct)
    {
        var message = ask.Messages?.Invoke(request) ?? $"Merge branch '{request.Branch}'";
        var merged = await MachinePorts.Repository.MergeAsync(tree, new GitMergeAsk(request.Tip, GitMergeKind.Commit, message), ct).ConfigureAwait(false);
        if (!merged.Ok)
        {
            var hooked = merged.Pending && merged.Unmerged.Count == 0;
            var first = merged.Error.Trim().Split('\n')[0].Trim();
            var why = hooked
                ? LandingSayings.HookRefused(request.Branch, ask.Line, merged.Error, "")
                : $"git would not merge `{request.Branch}` into `{ask.Line}`: {first} The merge was aborted and the checkout is as it was.";
            return await ConflictAsync(tree, onto, member.Request, why, ct).ConfigureAwait(false);
        }

        if (await MachinePorts.Repository.HeadAsync(tree, ct).ConfigureAwait(false) is not { } replayed) return Held("git could not read the integration tree's commit.");
        if (Seam is { } afterCommit) await afterCommit("after-commit").ConfigureAwait(false);
        return new(IntegrationStepOutcomes.Done)
        {
            Said = $"merged {TextBounds.ShortCommit(request.Tip, 8)} onto {TextBounds.ShortCommit(onto, 8)} as the press's merge, as {TextBounds.ShortCommit(replayed, 8)}.",
            Tip = replayed,
            Members = [new IntegrationMemberChange(member.Request, IntegrationMemberStates.Replayed) { Replayed = replayed }],
        };
    }

    /// <summary>A merge that did not go: aborted, the tree back at the candidate so far, and the member <c>conflict</c>, to be handed back at its own step.</summary>
    private async Task<IntegrationStepEnd> ConflictAsync(string tree, string onto, string request, string why, CancellationToken ct)
    {
        if (await ResetAsync(tree, onto, ct).ConfigureAwait(false) is { } notReset) return Held(notReset);
        return new(IntegrationStepOutcomes.Done)
        {
            Said = $"{why}: the merge was aborted and the candidate is as it was.",
            Members = [new IntegrationMemberChange(request, IntegrationMemberStates.Conflict) { Said = why }],
        };
    }

    /// <summary>
    /// The conflict step: a conflict of meaning goes by the member's request's <c>conflicts</c>, the word it carries as the
    /// rule stands at the ask, never read again. <c>session</c> tells the member's session to rebase onto the line in the landing's own
    /// words, through the say door under the key <see cref="RebaseKeys.Of"/> gives the session and the frozen tip, so a tip is told once, and
    /// hands the member back with its request closed, for the session's next landing to request again. <c>hold</c>, and a session that cannot be
    /// told (no session, no door, a door that kept nothing), sets the member aside as <see cref="IntegrationMemberStates.Held"/>: its request stays
    /// open, shown with the conflict, and nothing else waits. The landing agent is a later design's. The tree is put back again at the
    /// candidate so far, since a conflict is only ever handed from it.
    /// </summary>
    /// <remarks>
    /// The telling is <see cref="RebaseTelling.TellAsync"/>, not <see cref="LandCarry"/>: the carry-on belongs to a person's kept Accept on the
    /// session and counts against its bound, which a candidate's member has none of; the rebase words and their per-tip key are the telling's
    /// own. A crash after the words went and before the end is written runs this step again and says them again at the same key, which the door
    /// keeps once.
    /// </remarks>
    private async Task<IntegrationStepEnd> HandBackEffectAsync(IntegrationClaim claim, IntegrationMember member, IntegrationReplayAsk ask, string tree, CancellationToken ct)
    {
        var candidate = store.Candidate(claim.Candidate)!;
        var request = store.Request(member.Request)!;
        var onto = candidate.Tip ?? candidate.Base;
        if (await ResetAsync(tree, onto, ct).ConfigureAwait(false) is { } notReset) return Held(notReset);
        var members = store.Members(candidate.Id);
        var conflict = member.Said ?? "its merge did not go";

        // A merge rule's press is answered on the press, in its own words: handed back at once, nobody told and nothing held.
        if (request.Route == IntegrationRoutes.Merge)
        {
            return new(IntegrationStepOutcomes.HandedBack)
            {
                Said = conflict,
                Members = [new IntegrationMemberChange(member.Request, IntegrationMemberStates.HandedBack) { Said = conflict, Ends = true }],
                Closes = [new IntegrationRequestClose(member.Request, IntegrationRequestCloses.HandedBack)],
                Ends = EndIfNothingLeft(members, member.Request),
            };
        }

        var held = $"conflicts: {LandingIntegrate.Hold}";
        if (request.Policy.Conflicts != LandingIntegrate.Hold)
        {
            held = request.Session is not { Length: > 0 } session ? "it has no session to tell"
                : ask.Say is not { } say ? "nothing here reaches its conversation"
                : "";
            if (held.Length == 0)
            {
                var said = await TellAsync(ask, request, request.Session!, tree, candidate, member, members, ct).ConfigureAwait(false);
                if (Seam is { } afterTell) await afterTell("after-tell").ConfigureAwait(false);
                if (said is { Kept: true })
                {
                    var why = $"handed back to its session, which was asked to rebase onto `{ask.Line}`: {conflict}.";
                    return new(IntegrationStepOutcomes.HandedBack)
                    {
                        Said = why,
                        Members = [new IntegrationMemberChange(member.Request, IntegrationMemberStates.HandedBack) { Said = why, Ends = true }],
                        Closes = [new IntegrationRequestClose(member.Request, IntegrationRequestCloses.HandedBack)],
                        Ends = EndIfNothingLeft(members, member.Request),
                    };
                }

                held = $"its session `{request.Session}` could not be asked to rebase ({(said is null ? "nothing here reaches its conversation" : said.Message).TrimEnd('.')})";
            }
        }

        var setAside = $"held for the steward's or the owner's act ({held}): {conflict}.";
        return new(IntegrationStepOutcomes.Done)
        {
            Said = setAside,
            Members = [new IntegrationMemberChange(member.Request, IntegrationMemberStates.Held) { Said = setAside }],
            Ends = EndIfNothingLeft(members, member.Request),
        };
    }

    /// <summary>
    /// The rebase words for a member's conflict, said to its session: the files and what met them found again by merging the frozen tip onto the
    /// candidate so far and resolving as the replay did (nothing is written, and the tree is put back after), else the conflict's own sentence.
    /// </summary>
    private async Task<SayAnswer?> TellAsync(
        IntegrationReplayAsk ask, IntegrationRequest request, string session, string tree, IntegrationCandidate candidate, IntegrationMember member,
        IReadOnlyList<IntegrationMember> members, CancellationToken ct)
    {
        var onto = candidate.Tip ?? candidate.Base;
        var conflicts = member.Said ?? "its merge did not go";
        var merged = await MachinePorts.Repository.MergeAsync(tree, new GitMergeAsk(request.Tip, GitMergeKind.Stage), ct).ConfigureAwait(false);
        if (!merged.Ok && merged.Unmerged is { Count: > 0 } paths)
        {
            var resolved = await IntegrationResolves.ResolveAsync(
                tree, ask.Root, ask.Repository, ask.Workspace, candidate.Base, onto, request.Tip, paths, ask.Config, ct).ConfigureAwait(false);
            if (resolved.Unsettled.Count > 0)
            {
                var earlier = members
                    .Where(each => each is { Stands: true, State: IntegrationMemberStates.Replayed, Replayed: not null } && each.Position < member.Position)
                    .Select(each => (Branch: store.Request(each.Request)?.Branch ?? each.Request, Replayed: each.Replayed!))
                    .ToList();
                conflicts = await IntegrationConflicts.DescribeAsync(tree, candidate.Base, request.Tip, resolved.Unsettled, earlier, ct).ConfigureAwait(false);
            }
        }

        // The tree goes back before the words go: a crash after them finds it at the candidate so far, as every step's effect begins.
        if (await ResetAsync(tree, onto, ct).ConfigureAwait(false) is { } notReset) return new SayAnswer(false, notReset);
        var words = new LandingConflict(conflicts, LandingSyncWords.Rebase(ask.Line, [], conflicts));
        return await RebaseTelling.TellAsync(ask.Say!, session, request.Tip, words, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The candidate ends with nothing to promote where the member leaving it is the last that could: no other standing member is yet to be
    /// replayed, replayed, or awaiting its hand-back. A member contained provisionally needs a replayed member to hold it, so none is left.
    /// </summary>
    private static string? EndIfNothingLeft(IReadOnlyList<IntegrationMember> members, string leaving) =>
        members.Any(each => each.Stands && each.Request != leaving
                            && each.State is IntegrationMemberStates.Formed or IntegrationMemberStates.Replayed or IntegrationMemberStates.Conflict)
            ? null
            : IntegrationCandidateEnds.Empty;

    /// <summary>
    /// Whether <paramref name="commit"/> holds the work of <paramref name="tip"/>: by ancestry, as the merge tool's <c>containedBy</c> judges
    /// it, else wholly by content, then patch. git unable to say reads as not held: the merge would be tried.
    /// </summary>
    private static async Task<bool> HoldsAsync(string tree, string tip, string commit, string line, bool ancestryOnly, CancellationToken ct)
    {
        var held = await MachinePorts.Repository.IsAncestorAsync(tree, tip, commit, ct).ConfigureAwait(false);
        if (held == true) return true;
        if (held != false || ancestryOnly) return false;
        var judged = await LineHolds.JudgeAsync(tree, tip, commit, line, ct).ConfigureAwait(false);
        return judged is { Commits.Count: > 0, Kept.Count: 0 };
    }

    private static string Names(IReadOnlyList<string> paths) => string.Join(", ", paths.Take(3)) + (paths.Count > 3 ? $" and {paths.Count - 3} more" : "");
}
