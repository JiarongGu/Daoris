using System.ComponentModel;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What one run of the verdict, the promotion and the settle asks (INTEGRATE1e part e4a): the repository, its workspace, its registered checkout
/// (where git is asked of the line, and the line's own checkout is found), the line by name, the person's choices the accepted declaration is
/// read through, and the Daoris home whose admission the gates take their places in.
/// </summary>
internal sealed record IntegrationPromoteAsk(string Repository, string Workspace, string Root, string Line, DriverConfig Config, string Home)
{
    /// <summary>Signalled when the candidate is dropped: it is handed to the verdict port, which ends its confirm run and answers <c>dropped</c>.</summary>
    public CancellationToken Drop { get; init; }

    /// <summary>
    /// Whether a steward act's appointment still stands, read again at promotion (§5.2 step 8, §8). The default reads none, so a steward's request
    /// is held there: nothing in the driver reads an appointment's liveness until the role is active (STEWARDROLE1d).
    /// </summary>
    public IIntegrationAppointments Appointments { get; init; } = IntegrationAppointments.Unread;

    /// <summary>
    /// The landing's own clean-up, through which the tidy step removes a landed branch the line now holds (§5.2 step 10); null where none was
    /// handed in, and the tidy then removes nothing. Never a delete of its own.
    /// </summary>
    public IntegrationTidy? Tidy { get; init; }
}

/// <summary>What a promote run answered (<see cref="IntegrationPromoteRuns"/>), in words, with the candidate and its members as they now stand.</summary>
internal sealed record IntegrationPromoteRun(string Kind, string Said)
{
    public IntegrationCandidate? Candidate { get; init; }

    public IReadOnlyList<IntegrationMember> Members { get; init; } = [];

    /// <summary>The verdict's action the run acted on (<see cref="VerdictActions"/>); null where none was asked.</summary>
    public string? Verdict { get; init; }
}

/// <summary>What a promote run answers.</summary>
internal static class IntegrationPromoteRuns
{
    /// <summary>The line holds the candidate's tip, every member is settled, the landed branches are tidied and the close ended the candidate <c>settled</c>.</summary>
    public const string Settled = "settled";

    /// <summary>The line holds the tip, and git could not judge a member or keep its work on a recovery ref: the member stands, the candidate stands, and the settle is asked again at the next look.</summary>
    public const string Unsettled = "unsettled";

    /// <summary>A requester's authority no longer stood at promotion: its member is held, nothing was moved, and the candidate is formed again without it (INTEGRATE1e part e4b).</summary>
    public const string Lapsed = "lapsed";

    /// <summary>The step could not go on and ended <c>held</c>, said why (a hold verdict, the fence, a checkout that is not clean): tried again at the next look.</summary>
    public const string Held = "held";

    /// <summary>The line moved under the candidate: nothing was moved and the candidate is formed again.</summary>
    public const string Moved = "moved";

    /// <summary>The verdict was unknown, busy, not a pass or not for this tip, or the gates could not run: nothing was written, and it is asked again at the next look.</summary>
    public const string Waiting = "waiting";

    /// <summary>A hand back: each named member was handed back and the rest are formed again.</summary>
    public const string HandedBack = "handed-back";

    /// <summary>A split: the failing members were handed back and the rest are formed again, one at a time.</summary>
    public const string Split = "split";

    /// <summary>The line's integration stopped, and the candidate with it.</summary>
    public const string Stopped = "stopped";

    /// <summary>The candidate was dropped and its requests closed.</summary>
    public const string Dropped = "dropped";

    /// <summary>A resume found something other than a step to run (the candidate ended, git could not say, the line was moved back): nothing more was written.</summary>
    public const string Resumed = "resumed";

    /// <summary>git could not say where the line stands: nothing was written.</summary>
    public const string Unread = "unread";

    /// <summary>The candidate's members are not all replayed yet, or it has no tip: its replay goes first.</summary>
    public const string Replaying = "replaying";
}

/// <summary>
/// The integration run's gates verdict, promotion and settle over git (INTEGRATE1e part e4a, D215; the integration design §5.2 steps 6 to 9,
/// §5.6, §5.7): a verified candidate promoted onto its line only on a passing verdict for its exact tip, and only if the line has not moved
/// under it, and its members settled. Called from no production path: the unit's <c>integrate</c> node and the transport call it.
/// </summary>
/// <remarks>
/// <para><b>The verdict first.</b> The verdict port (<see cref="IntegrationVerdicts"/>) is asked for the integration tree put at the candidate's
/// tip, the base, the accepted declaration's text and blob, the members' replayed tips, the repository's runner and the owner's rule for an
/// unconfirmed failure; a line whose rule says <c>gates: none</c> integrates with no gates and never asks it. <c>green</c> promotes, and
/// <c>land</c> only where the owner's rule chose it and a gate is named; <c>hold</c> ends the judge step <c>held</c> with the candidate
/// standing; <c>split</c> and <c>hand back</c> hand each named member back and form the rest again (one at a time after a split);
/// <c>stop</c> and <c>dropped</c> end the candidate. <c>unknown</c>, <c>busy</c>, a word this build does not know, a verdict answered after the
/// tree left the tip, and gates that cannot run are never a pass: nothing is written, and the next look asks again.</para>
/// <para><b>The promotion</b> is one <c>promote</c> step, with the line's commit read immediately before it and, for a step run again after a
/// crash, the commit its cut attempt recorded. Its effect reads the line again, refuses a line that is not at the candidate's base (the
/// candidate is formed again and nothing moves), reads <c>integration.by</c> from the declaration committed at that commit (never the
/// working tree), and moves the line: one <c>update-ref</c> verifying the old value where no working tree has it out; a
/// <c>merge --ff-only</c> in the one that has it out, where it is clean, and the step held, said why, where it is not. An <c>update-ref</c>
/// under a checkout is never made.</para>
/// <para><b>Each end closes the candidate's job</b> (§5.6): the candidate's one job, which every gate and runner process ran in, ends with it.</para>
/// </remarks>
internal sealed class IntegrationPromotes(IntegrationStore store, string treesRoot)
{
    private readonly IntegrationReplays _replays = new(store, treesRoot);

    /// <summary>
    /// A test's seam: called inside the promote effect once its step is begun (<c>after-start</c>), just before the line is moved
    /// (<c>before-move</c>) and once it is moved and before the step's end is written (<c>after-move</c>); and inside the settle's effect
    /// (<c>in-settle</c>), so a test can stop the process there or move the line. Production sets none.
    /// </summary>
    internal Func<string, Task>? Seam { get; set; }

    /// <summary>
    /// Run the candidate's verdict, promotion and settle as far as they go: the promotion judged by ancestry first where a crash left one cut
    /// (<see cref="IntegrationSteps.ResumeAsync"/>), then the verdict and the promotion where the line does not hold the tip, then the settle.
    /// </summary>
    /// <exception cref="IntegrationClaimLostException">The claim no longer holds the candidate.</exception>
    public async Task<IntegrationPromoteRun> RunAsync(IntegrationClaim claim, IntegrationPromoteAsk ask, CancellationToken ct = default)
    {
        var resumed = await new IntegrationSteps(store).ResumeAsync(claim, ask.Root, ct).ConfigureAwait(false);
        if (resumed.Kind is IntegrationResumes.Ended or IntegrationResumes.Unread or IntegrationResumes.Unheld)
        {
            return await AnswerAsync(IntegrationPromoteRuns.Resumed, resumed.Said, claim).ConfigureAwait(false);
        }

        // A promotion cut before its end runs again from the line's commit its own cut attempt recorded, never a fresh read.
        var rerun = resumed is { Kind: IntegrationResumes.Again, Step: { Step: IntegrationStepNames.Promote } cut } ? cut.LineCommit : null;
        var candidate = store.Candidate(claim.Candidate) ?? throw new IntegrationClaimLostException(claim, "the store holds no such candidate");
        var members = store.Members(candidate.Id);
        if (candidate.Tip is null || members.Any(each => each is { Stands: true, State: IntegrationMemberStates.Formed or IntegrationMemberStates.Conflict }))
        {
            return await AnswerAsync(IntegrationPromoteRuns.Replaying, $"candidate {candidate.Id}'s members are not all replayed: its replay goes first.", claim).ConfigureAwait(false);
        }

        if (candidate.PromotedAt is null)
        {
            if (store.Steps(candidate.Id).LastOrDefault() is { Step: IntegrationStepNames.Judge, Outcome: IntegrationStepOutcomes.Held } waiting)
            {
                return await AnswerAsync(IntegrationPromoteRuns.Held, $"candidate {candidate.Id} waits for a decide: {waiting.Said}", claim, VerdictActions.Hold).ConfigureAwait(false);
            }

            if (await PromoteAsync(claim, ask, candidate, members, rerun, ct).ConfigureAwait(false) is { } answer) return answer;
        }

        return await FinishAsync(claim, ask, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Everything after the line holds the candidate's tip, each step once: the settle (§5.2 step 9), the tidy (step 10) and the close (step 11),
    /// by what the candidate's steps already show done, so a come-up that finds the candidate between them goes on from the step after the last
    /// and never promotes again. The candidate stands until its close ends it.
    /// </summary>
    /// <exception cref="IntegrationClaimLostException">The claim no longer holds the candidate.</exception>
    internal async Task<IntegrationPromoteRun> FinishAsync(IntegrationClaim claim, IntegrationPromoteAsk ask, CancellationToken ct = default)
    {
        bool Done(string step) => store.Steps(claim.Candidate).Any(each => each.Step == step && each.Outcome == IntegrationStepOutcomes.Done);

        if (!Done(IntegrationStepNames.Settle) && await SettleAsync(claim, ask, ct).ConfigureAwait(false) is { } unsettled) return unsettled;
        if (!Done(IntegrationStepNames.Tidy)) await TidyAsync(claim, ask, ct).ConfigureAwait(false);
        return await CloseAsync(claim, ask, ct).ConfigureAwait(false);
    }

    private async Task<IntegrationPromoteRun> AnswerAsync(string kind, string said, IntegrationClaim claim, string? verdict = null)
    {
        var candidate = store.Candidate(claim.Candidate);
        if (candidate is { Stands: false })
        {
            // §5.6: every end of the candidate closes its one job, its descendants ending with it, before anything else forms.
            try
            {
                await ProcessJob.EndNamedAsync(candidate.Job).ConfigureAwait(false);
            }
            catch (Win32Exception error)
            {
                said += $" Its job could not be opened to be closed ({error.Message}).";
            }
        }

        return new IntegrationPromoteRun(kind, said) { Candidate = candidate, Members = store.Members(claim.Candidate), Verdict = verdict };
    }

    // ——— The verdict, then the promotion (§5.2 steps 6 to 8).

    /// <summary>Everything up to and including the promotion: null where the line now holds the candidate's tip and the settle goes on; else the answer.</summary>
    private async Task<IntegrationPromoteRun?> PromoteAsync(
        IntegrationClaim claim, IntegrationPromoteAsk ask, IntegrationCandidate candidate, IReadOnlyList<IntegrationMember> members, string? rerun, CancellationToken ct)
    {
        var replayed = members.Where(each => each is { Stands: true, State: IntegrationMemberStates.Replayed, Replayed: not null }).ToList();
        if (replayed.Count == 0)
        {
            return await AnswerAsync(IntegrationPromoteRuns.Replaying, $"candidate {candidate.Id} has no replayed member to promote.", claim).ConfigureAwait(false);
        }

        // A line that moved is no reason to run the gates: the candidate is formed again, and nothing moves.
        var (line, why) = await LineAsync(ask.Root, ask.Line, ct).ConfigureAwait(false);
        if (line is null) return await AnswerAsync(IntegrationPromoteRuns.Unread, $"git could not read where `{ask.Line}` stands ({why}): nothing was written, and it is asked again at the next look.", claim).ConfigureAwait(false);
        if (line != candidate.Base) return await FormAgainAsync(claim, candidate, line, ct).ConfigureAwait(false);

        var first = store.Request(replayed[0].Request)!;
        var (verdict, stopped) = await VerdictAsync(claim, ask, candidate, replayed, first.Policy, ct).ConfigureAwait(false);
        if (stopped is not null) return stopped;
        var action = verdict!.Action;

        switch (action)
        {
            case VerdictActions.Green:
                return await MoveAsync(claim, ask, candidate, replayed, Words(verdict, first.Policy), rerun, ct).ConfigureAwait(false);
            case VerdictActions.Land when first.Policy.Gates == LandingIntegrate.Declared && Unconfirmed(first.Policy) == LandingIntegrate.Land && verdict.PassedOnConfirm.Count > 0:
                return await MoveAsync(claim, ask, candidate, replayed, Words(verdict, first.Policy), rerun, ct).ConfigureAwait(false);
            case VerdictActions.Hold:
                return await JudgedAsync(claim, IntegrationPromoteRuns.Held, action, new IntegrationStepEnd(IntegrationStepOutcomes.Held)
                {
                    Said = $"held for a decide ({verdict.On ?? "decide"}): {Said(verdict, "the verdict holds the candidate")}",
                }, ct).ConfigureAwait(false);
            case VerdictActions.HandBack or VerdictActions.Split when HandedBack(store, verdict, replayed) is { } back:
                return await JudgedAsync(claim, action == VerdictActions.Split ? IntegrationPromoteRuns.Split : IntegrationPromoteRuns.HandedBack, action, back, ct).ConfigureAwait(false);
            case VerdictActions.Stop:
                return await JudgedAsync(claim, IntegrationPromoteRuns.Stopped, action, new IntegrationStepEnd(IntegrationStepOutcomes.Stopped)
                {
                    Said = $"the line's integration stopped: {Said(verdict, "the verdict stops the line")}"
                           + (verdict.Failure is { } failure ? $" ({failure.Label})" : ""),
                    Ends = IntegrationCandidateEnds.Stopped,
                }, ct).ConfigureAwait(false);
            case VerdictActions.Dropped:
                return await JudgedAsync(claim, IntegrationPromoteRuns.Dropped, action, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
                {
                    Said = "the candidate was dropped: its requests are closed.",
                    Members = [.. members.Where(each => each.Stands).Select(each => new IntegrationMemberChange(each.Request, IntegrationMemberStates.Dropped) { Said = "dropped with its candidate.", Ends = true })],
                    Closes = [.. members.Where(each => each.Stands).Select(each => new IntegrationRequestClose(each.Request, IntegrationRequestCloses.Dropped))],
                    Ends = IntegrationCandidateEnds.Dropped,
                }, ct).ConfigureAwait(false);
            default:
                // unknown, busy, a word a newer port speaks, a hand back naming no member, and a land the owner's rule did not choose: never a pass.
                return await AnswerAsync(
                    IntegrationPromoteRuns.Waiting,
                    $"the verdict `{action}` is no pass for candidate {candidate.Id}'s tip ({Said(verdict, "nothing more was said")}): nothing was written, and it is asked again at the next look.",
                    claim, action).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The verdict of the candidate's gates at its tip, or the answer that ends this look: no declaration accepted for the line's commit, an
    /// integration tree that cannot be put at the tip, and a verdict given after the tree left it are each a look with nothing written.
    /// </summary>
    private async Task<(IntegrationVerdict? Verdict, IntegrationPromoteRun? Stopped)> VerdictAsync(
        IntegrationClaim claim, IntegrationPromoteAsk ask, IntegrationCandidate candidate, IReadOnlyList<IntegrationMember> replayed, LandingIntegrate policy, CancellationToken ct)
    {
        if (policy.Gates == LandingIntegrate.NoGates)
        {
            return (new IntegrationVerdict(VerdictActions.Green) { Said = "the line's rule integrates with no gates." }, null);
        }

        var accepted = IntegrationAcceptance.Of(ask.Config, ask.Repository);
        var text = await IntegrationAcceptance.AcceptedTextAsync(ask.Config, ask.Root, ask.Repository, candidate.Base, ct).ConfigureAwait(false);
        if (accepted is null || text is null)
        {
            return (null, await AnswerAsync(
                IntegrationPromoteRuns.Waiting,
                $"the owner has accepted no declaration the line holds at {TextBounds.ShortCommit(candidate.Base, 8)}, so its gates cannot run: nothing was written, and it is asked again at the next look.",
                claim).ConfigureAwait(false));
        }

        var tree = _replays.TreeOf(ask.Workspace, ask.Repository, ask.Line);
        var tip = candidate.Tip!;
        if (!Directory.Exists(tree) || await IntegrationReplays.ResetAsync(tree, tip, ct).ConfigureAwait(false) is not null)
        {
            return (null, await AnswerAsync(
                IntegrationPromoteRuns.Waiting,
                $"the line's integration tree cannot be put at the candidate's tip {TextBounds.ShortCommit(tip, 8)}: nothing was written, and it is asked again at the next look.",
                claim).ConfigureAwait(false));
        }

        // The evidence is kept beside the tree, outside what a reset of the tree clears.
        var scratch = Path.Combine(tree + ".evidence", candidate.Id);
        Directory.CreateDirectory(scratch);
        var read = IntegrationDeclaration.Read(text);
        var facts = read.Facts;
        IReadOnlyList<string>? gates = read.Problem is not null || facts is { Reached: true } ? null : facts?.Gates ?? IntegrationDeclaration.Declared(text);
        var asked = new IntegrationVerdictAsk(
            candidate, tree, candidate.Base, text, accepted, ask.Home, scratch, Unconfirmed(policy),
            [.. replayed.Select(each => new VerdictMember(store.Request(each.Request)!.Branch, each.Replayed!))], ask.Drop)
        {
            Runner = RunnerOf(text), Gates = gates, Pass = facts?.Pass ?? [],
        };

        IIntegrationVerdicts port;
        try
        {
            port = IntegrationVerdicts.Current;
        }
        catch (InvalidOperationException error)
        {
            return (null, await AnswerAsync(IntegrationPromoteRuns.Waiting, $"{error.Message}: nothing was written.", claim).ConfigureAwait(false));
        }

        var verdict = await port.JudgeAsync(asked).ConfigureAwait(false);

        // A verdict is acted on only for this tip: the tree stands there still, and the candidate is the one asked about.
        var (head, headOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "HEAD"], ct).ConfigureAwait(false);
        if (store.Candidate(candidate.Id) is not { Stands: true } now || now.Tip != tip || head != 0 || headOut.Trim() != tip)
        {
            return (null, await AnswerAsync(
                IntegrationPromoteRuns.Waiting,
                $"the verdict `{verdict.Action}` was given for a tree that is not at candidate {candidate.Id}'s tip {TextBounds.ShortCommit(tip, 8)}: it is no verdict of this candidate, "
                + "nothing was written, and it is asked again at the next look.",
                claim, verdict.Action).ConfigureAwait(false));
        }

        return (verdict, null);
    }

    /// <summary>One <c>judge</c> step for a verdict that ends a look in a record (a hold, a hand back, a split, a stop, a drop).</summary>
    private async Task<IntegrationPromoteRun> JudgedAsync(IntegrationClaim claim, string kind, string action, IntegrationStepEnd end, CancellationToken ct)
    {
        var ended = await new IntegrationSteps(store).RunAsync(claim, IntegrationStepNames.Judge, (_, _) => Task.FromResult(end), ct: ct).ConfigureAwait(false);
        return await AnswerAsync(kind, ended.Said ?? "", claim, action).ConfigureAwait(false);
    }

    /// <summary>
    /// A hand back's or a split's end: each member the verdict names handed back with its request closed, the candidate formed again where any
    /// member is left (and ended <c>empty</c> where none is). Null where the verdict names no member of this candidate, which is no verdict.
    /// </summary>
    private static IntegrationStepEnd? HandedBack(IntegrationStore store, IntegrationVerdict verdict, IReadOnlyList<IntegrationMember> replayed)
    {
        var byBranch = replayed.ToDictionary(each => store.Request(each.Request)!.Branch, StringComparer.Ordinal);
        var named = verdict.HandedBack.Where(each => byBranch.ContainsKey(each.Branch)).ToList();
        if (named.Count == 0 && (verdict.Action == VerdictActions.HandBack || verdict.Retain.All(each => !byBranch.ContainsKey(each)))) return null;

        var back = named.Select(each => (Member: byBranch[each.Branch], Words: HandBackWords(each))).ToList();
        var changes = back.Select(each => new IntegrationMemberChange(each.Member.Request, IntegrationMemberStates.HandedBack) { Said = each.Words, Ends = true }).ToList();
        var handed = back.Select(each => each.Member.Request).ToHashSet(StringComparer.Ordinal);
        var split = verdict.Action == VerdictActions.Split;
        return new IntegrationStepEnd(split ? IntegrationStepOutcomes.Split : IntegrationStepOutcomes.HandedBack)
        {
            Said = (split ? "split: the members left run one at a time. " : "")
                   + (back.Count == 0 ? "No member is handed back. " : string.Join(" ", back.Select(each => $"request {each.Member.Request}: {each.Words}")) + " ")
                   + "The members kept are formed again.",
            Members = changes,
            Closes = [.. handed.Select(each => new IntegrationRequestClose(each, IntegrationRequestCloses.HandedBack))],
            Ends = replayed.All(each => handed.Contains(each.Request)) ? IntegrationCandidateEnds.Empty : IntegrationCandidateEnds.FormedAgain,
        };
    }

    private static string HandBackWords(VerdictHandedBack each) =>
        $"handed back ({each.How}) for {each.Failure}"
        + (each.With.Count > 0 ? $", with {string.Join(", ", each.With)}" : "")
        + (each.Interacting.Count > 0 ? $", interacting with {string.Join(", ", each.Interacting)}" : "")
        + (string.IsNullOrWhiteSpace(each.Why) ? "." : $": {each.Why}");

    /// <summary>The candidate formed again where the line moved under it: nothing moves, and its members' requests are queued again (§5.2 step 8).</summary>
    private async Task<IntegrationPromoteRun> FormAgainAsync(IntegrationClaim claim, IntegrationCandidate candidate, string line, CancellationToken ct)
    {
        var said = MovedWords(candidate, line);
        await new IntegrationSteps(store).RunAsync(
            claim, IntegrationStepNames.Promote,
            (_, _) => Task.FromResult(new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Said = said, Ends = IntegrationCandidateEnds.FormedAgain }),
            lineCommit: line, ct: ct).ConfigureAwait(false);
        return await AnswerAsync(IntegrationPromoteRuns.Moved, said, claim).ConfigureAwait(false);
    }

    private static string MovedWords(IntegrationCandidate candidate, string line) =>
        $"`{candidate.Line}` moved under the candidate, from {TextBounds.ShortCommit(candidate.Base, 8)} to {TextBounds.ShortCommit(line, 8)}: nothing was moved, "
        + "and the candidate is formed again from the requests that are left.";

    /// <summary>
    /// The <c>promote</c> step (§5.2 step 8): begun with the line's commit read immediately before, or, for an attempt run again, the one its cut
    /// attempt recorded; its effect is <see cref="EffectAsync"/>. Null where the line now holds the candidate's tip.
    /// </summary>
    private async Task<IntegrationPromoteRun?> MoveAsync(
        IntegrationClaim claim, IntegrationPromoteAsk ask, IntegrationCandidate candidate, IReadOnlyList<IntegrationMember> replayed, string verdict, string? rerun, CancellationToken ct)
    {
        var lineCommit = rerun;
        if (lineCommit is null)
        {
            var (line, why) = await LineAsync(ask.Root, ask.Line, ct).ConfigureAwait(false);
            if (line is null) return await AnswerAsync(IntegrationPromoteRuns.Unread, $"git could not read where `{ask.Line}` stands ({why}): nothing was written, and it is asked again at the next look.", claim).ConfigureAwait(false);
            lineCommit = line;
        }

        var lapsed = false;
        var ended = await new IntegrationSteps(store).RunAsync(
            claim, IntegrationStepNames.Promote,
            async (step, token) =>
            {
                if (Seam is { } afterStart) await afterStart("after-start").ConfigureAwait(false);
                var end = await EffectAsync(ask, candidate, replayed, step, verdict, token).ConfigureAwait(false);
                lapsed = end.Members.Any(each => each.State == IntegrationMemberStates.Held);
                return end;
            },
            lineCommit: lineCommit, ct: ct).ConfigureAwait(false);
        if (ended.Outcome == IntegrationStepOutcomes.Promoted) return null;
        if (lapsed) return await AnswerAsync(IntegrationPromoteRuns.Lapsed, ended.Said ?? "", claim).ConfigureAwait(false);
        if (store.Candidate(claim.Candidate) is { EndedWhy: IntegrationCandidateEnds.FormedAgain }) return await AnswerAsync(IntegrationPromoteRuns.Moved, ended.Said ?? "", claim).ConfigureAwait(false);
        return await AnswerAsync(IntegrationPromoteRuns.Held, ended.Said ?? "the promotion could not go on.", claim, VerdictActions.Green).ConfigureAwait(false);
    }

    /// <summary>
    /// The promotion's effect, under the claim and immediately before the line moves: the line read again, the fence read from the committed
    /// declaration at the commit being moved, then the move, by the path its checkout allows.
    /// </summary>
    private async Task<IntegrationStepEnd> EffectAsync(
        IntegrationPromoteAsk ask, IntegrationCandidate candidate, IReadOnlyList<IntegrationMember> replayed, IntegrationStep step, string verdict, CancellationToken ct)
    {
        var tip = candidate.Tip!;
        var (now, why) = await LineAsync(ask.Root, ask.Line, ct).ConfigureAwait(false);
        if (now is null) return Held($"git could not read where `{ask.Line}` stands ({why}): nothing was moved, and it is tried again at the next look.");
        if (now != step.LineCommit || step.LineCommit != candidate.Base)
        {
            return new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Said = MovedWords(candidate, now), Ends = IntegrationCandidateEnds.FormedAgain };
        }

        if (await FenceAsync(ask.Root, step.LineCommit!, ct).ConfigureAwait(false) is { } fenced) return Held($"{fenced}: nothing was moved, and it is tried again at the next look.");

        // Each member's requester's authority, read again here for the race since the form (§5.2 step 8, §8): never trusted from the door.
        if (await LapsedAsync(ask, replayed, ct).ConfigureAwait(false) is { Count: > 0 } lapsed) return LapsedEnd(replayed, lapsed);

        if (await TreeWorktrees.EntriesAsync(ask.Root, ct).ConfigureAwait(false) is not { } entries)
        {
            return Held("git could not list the working trees, so it is not known whether a checkout has the line out: nothing was moved, and it is tried again at the next look.");
        }

        var checkout = entries.FirstOrDefault(each => string.Equals(each.Branch, ask.Line, StringComparison.Ordinal));
        string how;
        if (Seam is { } beforeMove) await beforeMove("before-move").ConfigureAwait(false);
        if (checkout is not null)
        {
            // A checkout that has the line out is moved by a fast-forward in it, where it is clean; never by a ref moved under it.
            var (clean, detail) = await WorkingTree.CleanAsync(checkout.Path, ct).ConfigureAwait(false);
            if (!clean) return Held($"the checkout that has `{ask.Line}` out is not clean ({detail}): nothing was moved, and it is tried again at the next look.");
            var (merged, _, mergeErr) = await WorkingTree.GitAsync(checkout.Path, ["merge", "--ff-only", "--quiet", tip], ct).ConfigureAwait(false);
            if (merged != 0) return Held($"the checkout that has `{ask.Line}` out would not fast-forward to the candidate's tip ({ContentHolds.Said(mergeErr)}): it is tried again at the next look.");
            how = "a fast-forward in the checkout that has it out";
        }
        else
        {
            var (moved, _, moveErr) = await WorkingTree.GitAsync(ask.Root, ["update-ref", $"refs/heads/{ask.Line}", tip, step.LineCommit!], ct).ConfigureAwait(false);
            if (moved != 0) return Held($"git would not move `{ask.Line}` from {TextBounds.ShortCommit(step.LineCommit!, 8)}, which it no longer holds ({ContentHolds.Said(moveErr)}): it is tried again at the next look.");
            how = "one update-ref that verified its old value";
        }

        if ((await LineAsync(ask.Root, ask.Line, ct).ConfigureAwait(false)).Commit != tip)
        {
            return Held($"`{ask.Line}` does not stand at the candidate's tip after the move: it is tried again at the next look.");
        }

        if (Seam is { } afterMove) await afterMove("after-move").ConfigureAwait(false);
        return new IntegrationStepEnd(IntegrationStepOutcomes.Promoted)
        {
            Promoted = true,
            Said = $"`{ask.Line}` moved from {TextBounds.ShortCommit(step.LineCommit!, 8)} to {TextBounds.ShortCommit(tip, 8)} by {how}; {verdict}",
            Members = [.. replayed.Select(each => new IntegrationMemberChange(each.Request, IntegrationMemberStates.Promoted))],
        };
    }

    private static IntegrationStepEnd Held(string said) => new(IntegrationStepOutcomes.Held) { Said = said };

    /// <summary>
    /// §5.6's fence: who integrates the line, read from the declaration committed at the line's commit being moved, never the working tree or the
    /// accepted blob. Null where Daoris does (<c>integration.by</c> is <c>daoris</c> or absent, or no declaration is committed); else the words.
    /// </summary>
    private static async Task<string?> FenceAsync(string root, string commit, CancellationToken ct)
    {
        var at = TextBounds.ShortCommit(commit, 8);
        if (await IntegrationAcceptance.BlobAtAsync(root, commit, ct).ConfigureAwait(false) is not { } blob)
        {
            return $"git could not say which declaration `{at}` holds, so who integrates the line is not known";
        }

        if (blob == IntegrationAcceptance.None) return null;
        if (await IntegrationAcceptance.TextAsync(root, blob, ct).ConfigureAwait(false) is not { } text)
        {
            return $"the declaration `{at}` holds could not be read, so who integrates the line is not known";
        }

        var read = IntegrationDeclaration.Read(text);
        if (read.Problem is { } problem) return $"the declaration `{at}` holds does not read, so who integrates the line is not known: {problem}";
        return read.Facts?.By == "tool" ? $"the declaration `{at}` holds says `integration.by: tool`: another integrator owns the line" : null;
    }

    /// <summary>The line's commit as git names it, or the words of git's refusal.</summary>
    private static async Task<(string? Commit, string Why)> LineAsync(string root, string line, CancellationToken ct)
    {
        var (code, output, err) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{line}^{{commit}}"], ct).ConfigureAwait(false);
        return code == 0 && output.Trim().Length > 0 ? (output.Trim(), "") : (null, ContentHolds.Said(err));
    }

    /// <summary>
    /// The one word the port is handed for an unconfirmed failure (§4.5): the owner's rule for a unit no member reaches and for one a member
    /// reaches, which the port cannot tell apart before it runs. Where the two differ, the less permissive of them (<c>split</c>, <c>hold</c>,
    /// <c>land</c>, in that order) stands, so <c>land</c> is only ever the rule saying it for both.
    /// </summary>
    internal static string Unconfirmed(LandingIntegrate policy)
    {
        var words = new[] { policy.Unconfirmed(reached: false, wholeSet: true), policy.Unconfirmed(reached: true, wholeSet: true) };
        return words.MinBy(word => Math.Max(0, LandingIntegrate.Unconfirmeds.ToList().IndexOf(word)))!;
    }

    /// <summary>The declaration's <c>verification</c> runner (D192's key: its command and its folder); none where it declares no command.</summary>
    internal static VerdictRunner? RunnerOf(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("verification", out var verification) || verification.ValueKind != JsonValueKind.Object
                || !verification.TryGetProperty("run", out var run) || run.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(run.GetString()))
            {
                return null;
            }

            return new VerdictRunner(
                run.GetString()!.Trim(),
                verification.TryGetProperty("cwd", out var cwd) && cwd.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(cwd.GetString()) ? cwd.GetString()!.Trim() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Said(IntegrationVerdict verdict, string otherwise) => verdict.Why ?? verdict.Said ?? otherwise;

    /// <summary>What a passing verdict says in the promotion's words: the gates passed (flakes read as passes and said), or the confirm run's pass the owner's rule chose.</summary>
    private static string Words(IntegrationVerdict verdict, LandingIntegrate policy)
    {
        if (policy.Gates == LandingIntegrate.NoGates) return "the line's rule integrates with no gates.";
        var said = verdict.Action == VerdictActions.Land
            ? $"landed on the confirm run's pass of {string.Join(", ", verdict.PassedOnConfirm)}, as the owner's rule `unconfirmed: land` chose, for an unconfirmed failure that is a sighting."
            : "the gates passed.";
        if (verdict.Flaked.Count > 0) said += $" A flake read as a pass: {string.Join(", ", verdict.Flaked)}.";
        if (verdict.Inherited.Count > 0) said += $" {verdict.Inherited.Count} inherited known {(verdict.Inherited.Count == 1 ? "failure" : "failures")}.";
        return said;
    }

    // ——— The requester's authority at promotion (§5.2 step 8, §8).

    /// <summary>The replayed members whose requester's authority no longer stands, each with the words that say why; none where every one does.</summary>
    private async Task<List<(IntegrationMember Member, string Why)>> LapsedAsync(
        IntegrationPromoteAsk ask, IReadOnlyList<IntegrationMember> replayed, CancellationToken ct)
    {
        var lapsed = new List<(IntegrationMember, string)>();
        foreach (var member in replayed)
        {
            var request = store.Request(member.Request)!;
            string? why;
            try
            {
                why = await IntegrationAuthority.LapsedAsync(request, ask.Config, ask.Appointments, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // An authority that cannot be read is no authority: held, and read again at the next look.
                why = $"its authority could not be read ({error.Message})";
            }

            if (why is not null) lapsed.Add((member, why));
        }

        return lapsed;
    }

    /// <summary>
    /// The promote step's end where authority lapsed: nothing was moved; each lapsed member is held (its request stays open and out of the
    /// queue, for the steward's or the owner's act) and the candidate is formed again without it, or ended <c>empty</c> where none is left.
    /// </summary>
    private static IntegrationStepEnd LapsedEnd(IReadOnlyList<IntegrationMember> replayed, IReadOnlyList<(IntegrationMember Member, string Why)> lapsed) =>
        new(IntegrationStepOutcomes.Done)
        {
            Said = string.Join(" ", lapsed.Select(each => $"request {each.Member.Request}: held at promotion, its requester's authority no longer stands ({each.Why}); nothing was moved.")
                .Append(lapsed.Count == replayed.Count
                    ? "No member is left to promote."
                    : "The candidate is formed again without it.")),
            Members =
            [
                .. lapsed.Select(each => new IntegrationMemberChange(each.Member.Request, IntegrationMemberStates.Held)
                {
                    Said = $"held at promotion: its requester's authority no longer stands ({each.Why}); it waits for the steward's or the owner's act.",
                }),
            ],
            Ends = lapsed.Count == replayed.Count ? IntegrationCandidateEnds.Empty : IntegrationCandidateEnds.FormedAgain,
        };

    // ——— The settle (§5.2 step 9, §5.7).

    /// <summary>
    /// The settle: each member judged against the promoted line, a superseded member's work kept on its recovery ref, and everything one end
    /// writes (<see cref="IntegrationSettles.EndAsync"/>). Null where it ended <c>done</c>; else the answer, the candidate standing.
    /// </summary>
    private async Task<IntegrationPromoteRun?> SettleAsync(IntegrationClaim claim, IntegrationPromoteAsk ask, CancellationToken ct)
    {
        var candidate = store.Candidate(claim.Candidate) ?? throw new IntegrationClaimLostException(claim, "the store holds no such candidate");
        var settled = await IntegrationSettles.JudgeAsync(store, ask.Root, ask.Line, candidate, [.. store.Members(candidate.Id).Where(each => each.Stands)], ct).ConfigureAwait(false);
        if (settled.Unread is { } unread) return await AnswerAsync(IntegrationPromoteRuns.Unsettled, unread, claim).ConfigureAwait(false);

        // The ref is made in the effect, after the step's start and before its end, so a crash between them leaves the ref and an open step: the
        // rerun finds the ref it made, and writes the disposition in the end that settles the member.
        var ended = await new IntegrationSteps(store).RunAsync(
            claim, IntegrationStepNames.Settle,
            async (_, token) =>
            {
                if (Seam is { } inSettle) await inSettle("in-settle").ConfigureAwait(false);
                return await IntegrationSettles.EndAsync(ask.Root, settled, Seam is { } afterKeep ? () => afterKeep("after-keep") : null, token).ConfigureAwait(false);
            },
            ct: ct).ConfigureAwait(false);
        return ended.Outcome == IntegrationStepOutcomes.Done ? null : await AnswerAsync(IntegrationPromoteRuns.Unsettled, ended.Said ?? "", claim).ConfigureAwait(false);
    }

    // ——— The tidy (§5.2 step 10).

    /// <summary>
    /// The tidy: each settled member's landed branch handed to the landing's own clean-up, named alone, which removes it where the line holds its
    /// work and keeps its recovery ref; never a delete made here, never a branch the candidate did not carry, nothing forced. The line's integration
    /// tree is left warm for the next candidate. A look the clean-up could not make is said and is not a hold: the landing's own sweeps take it.
    /// </summary>
    private async Task TidyAsync(IntegrationClaim claim, IntegrationPromoteAsk ask, CancellationToken ct)
    {
        // A merge rule's press carried no landed branch: its session's branch and tree stay the person's to discard, as a merge left them (INTEGRATE1g).
        var settled = store.Members(claim.Candidate).Where(each => each.State == IntegrationMemberStates.Settled)
            .Select(each => store.Request(each.Request)!).Where(each => each.Route != IntegrationRoutes.Merge)
            .GroupBy(each => each.Branch, StringComparer.Ordinal).Select(group => group.First()).ToList();
        await new IntegrationSteps(store).RunAsync(
            claim, IntegrationStepNames.Tidy,
            async (_, token) =>
            {
                if (Seam is { } inTidy) await inTidy("in-tidy").ConfigureAwait(false);
                return await TidyEffectAsync(ask, settled, token).ConfigureAwait(false);
            },
            ct: ct).ConfigureAwait(false);
    }

    private static async Task<IntegrationStepEnd> TidyEffectAsync(IntegrationPromoteAsk ask, IReadOnlyList<IntegrationRequest> settled, CancellationToken ct)
    {
        const string Warm = "The line's integration tree is left warm for the next candidate.";
        if (settled.Count == 0) return new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Said = $"no member settled, so no landed branch is tidied. {Warm}" };
        if (ask.Tidy is null)
        {
            return new IntegrationStepEnd(IntegrationStepOutcomes.Done)
            {
                Said = $"no landing clean-up was handed in, so no landed branch is tidied here: the landing's own sweeps take them. {Warm}",
            };
        }

        var said = new List<string>();
        foreach (var request in settled)
        {
            try
            {
                var only = new HashSet<string>(StringComparer.Ordinal) { $"{request.Repository}:{request.Branch}" };
                var done = await ask.Tidy(request.Repository, request.Workspace, ask.Root, only, ct).ConfigureAwait(false);
                var result = done.Landed.FirstOrDefault(each => each.Item.Branch == request.Branch);
                said.Add(result is null
                    ? $"`{request.Branch}` is no landing this machine recorded, so there is nothing to tidy for it."
                    : result.Removed
                        ? $"`{request.Branch}` removed by the landing clean-up (the line holds its work): {result.Message}."
                        : $"`{request.Branch}` kept by the landing clean-up: {result.Message}.");
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                said.Add($"`{request.Branch}` could not be tidied now ({error.Message}): it is kept, and the landing's own sweeps take it.");
            }
        }

        return new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Said = $"{string.Join(" ", said)} {Warm}" };
    }

    // ——— The close (§5.2 step 11).

    /// <summary>
    /// The close: the unit's integration note written from the candidate's record, which the review, the session's head, the quest's page, the CLI
    /// and Ask Daoris read, and the candidate ended <c>settled</c>, which frees its line for the next and closes its one job. Nothing here says
    /// <i>Accept</i> or <i>waits for you</i>: the line has moved, and what is still pending is each superseded commit's own disposition.
    /// </summary>
    private async Task<IntegrationPromoteRun> CloseAsync(IntegrationClaim claim, IntegrationPromoteAsk ask, CancellationToken ct)
    {
        var ended = await new IntegrationSteps(store).RunAsync(
            claim, IntegrationStepNames.Close,
            async (_, _) =>
            {
                if (Seam is { } inClose) await inClose("in-close").ConfigureAwait(false);
                return new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Said = NoteOf(claim.Candidate), Ends = IntegrationCandidateEnds.Settled };
            },
            ct: ct).ConfigureAwait(false);
        return await AnswerAsync(IntegrationPromoteRuns.Settled, ended.Said ?? "", claim).ConfigureAwait(false);
    }

    /// <summary>The integration note, from the record alone: a code and its facts (the line's move, each member's end, the pending dispositions).</summary>
    private string NoteOf(string candidate)
    {
        var found = store.Candidate(candidate)!;
        var members = store.Members(candidate);
        var parts = members.Select(each =>
        {
            var request = store.Request(each.Request)!;
            var pending = store.Dispositions(each.Request).Count(disposition => disposition.Pending);
            return $"request {each.Request} (`{request.Branch}`) {each.State}" + (pending > 0 ? $", {pending} superseded {(pending == 1 ? "commit" : "commits")} pending a disposition" : "");
        });
        return $"settled: `{found.Line}` moved from {TextBounds.ShortCommit(found.Base, 8)} to {TextBounds.ShortCommit(found.Tip ?? found.Base, 8)}; {string.Join("; ", parts)}.";
    }
}
