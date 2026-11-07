namespace Daoris.Driver;

/// <summary>
/// The second opinion's gate (XAGENT1f, D155 points 9 and 10; the second-agent design §7–§8): whether the level asks one, and
/// whether the work that would land may go as far as it is concerned. A table of facts and the person's presses: the opinion's
/// coverage and the fixes are git's answers, and no model reads any of it (§10).
/// </summary>
/// <remarks>
/// <para><b>One gate, in a fixed order</b> (§7): the quest's own holds, then this, then D154's look, then the landing. Every
/// landing door reads both through <see cref="SessionTreeGates.GateAsync"/> and hands them to <see cref="SessionTrees.LandAsync"/>,
/// which refuses the first that holds (<see cref="LandingGate"/>).</para>
///
/// <para><b>Settled</b> (§8.2): an opinion covers the tip (its candidate's tip is the tip or a descendant of it, by
/// <c>merge-base --is-ancestor</c>), the working session answered, and no dispute is open or the person answered it. A dispute and
/// commits nobody read are answered by the press that comes next: <i>Accept…</i> with them shown, D154's <i>Reviewed</i> given
/// after they stood, or <i>Go on anyway…</i>. <c>required</c> governs only absence (§8.4).</para>
/// </remarks>
public static class OpinionGate
{
    /// <summary>
    /// The gate for the facts (design §8.1–§8.4): the level first, then the person's answer covering the tip, then the chain's
    /// later step here, then the newest pass asked and how far it went.
    /// </summary>
    public static async Task<OpinionGateState> JudgeAsync(OpinionGateFacts facts, OpinionReads reads)
    {
        OpinionGateState Of(string state) => new(state, facts.Repository)
        {
            Rule = facts.Rule, Session = facts.Session, Tip = facts.Tip, Later = facts.Later,
            Held = facts.Quest is { Held: true } quest && quest.Hold != EvidenceCodes.Unreviewed,
            Passes = facts.Asks.Count(ask => ask.Opinion is not null),
            Run = facts.Quest is null ? facts.Run : null, Asked = facts.Quest is null && facts.Asked,
        };

        // A conversation's work lands nothing by the driver's gate (§12.2) unless it is a chat run's (CHATFLOW1d), and a level that names
        // no occasion here asks nothing.
        if ((facts.Quest is null && !WorkplaceRunFiles.IsChatRun(facts.Run)) || !Reads(facts.Rule, facts.Cannot, facts.Occasion)) return Of(OpinionGateStates.None);

        // The person's own answer, while what would land is the commit they answered at or one before it (§8.5).
        foreach (var word in facts.Person.OrderByDescending(word => word.At))
        {
            if (facts.Tip is null || await reads.Covers(word.Tip).ConfigureAwait(false) != true) continue;
            var said = word.Said switch
            {
                OpinionPersonSaid.Anyway => OpinionGateStates.Anyway,
                OpinionPersonSaid.Myself => OpinionGateStates.Myself,
                _ => OpinionGateStates.Answered,
            };
            var standing = await StandingAsync(facts, reads, Of).ConfigureAwait(false);
            return standing with { State = said, Person = word, Answered = standing.State };
        }

        var judged = await StandingAsync(facts, reads, Of).ConfigureAwait(false);

        // D154's Reviewed answers what it showed beside it (§7, §8.2–§8.3): given after the disputes or the commits stood.
        if (judged.PressAnswers && facts.Review is { State: ReviewStates.Reviewed, Verdict.At: { } reviewed }
            && reviewed >= (judged.Answers?.At ?? DateTimeOffset.MinValue))
        {
            return judged with
            {
                State = OpinionGateStates.Answered, Answered = judged.State,
                Person = new OpinionPersonWord(ReviewVerdicts.Reviewed, facts.Tip ?? "", reviewed) { Words = facts.Review.Verdict.Words },
            };
        }

        return judged;
    }

    /// <summary>
    /// Whether a rule reads at an occasion (§2.1): one that names a reviewer and the occasion. A named workflow's opinion step that
    /// cannot start reads at its occasions too, so it holds there saying why (WORKFLOW1f), whoever it names.
    /// </summary>
    public static bool Reads(ResolvedOpinion? rule, string? cannot, string occasion) =>
        rule is not null && (cannot is not null || !rule.Rule.IsNone) && rule.Rule.On.Contains(occasion);

    /// <summary>Where the opinion stands before any answer of the person's: the chain, the newest pass, its delivery, its coverage.</summary>
    private static async Task<OpinionGateState> StandingAsync(OpinionGateFacts facts, OpinionReads reads, Func<string, OpinionGateState> of)
    {
        // A named workflow's opinion step that cannot start sits, saying why, before anything is asked (WORKFLOW1f, design §3.7).
        if (facts.Cannot is { } cannot) return of(OpinionGateStates.CannotStart) with { Problem = cannot };
        if (facts.Later is not null) return of(OpinionGateStates.WaitsChain);

        // The newest ask decides: an opinion the host opened, or why none could be read.
        OpinionView? first = null;
        foreach (var ask in facts.Asks.OrderByDescending(ask => ask.At))
        {
            if (ask.Opinion is { } id)
            {
                first = await reads.Opinion(id).ConfigureAwait(false);
                if (first is not null) break;
                continue;
            }

            return of(OpinionGateStates.Unavailable) with { Code = ask.Code ?? ReviewerUnavailable.Refused, Until = ask.Until };
        }

        if (first is null) return of(OpinionGateStates.NotAsked);
        var state = of(OpinionGateStates.Reading) with { Opinion = first };
        // LIMITRESUME1h (D206 design §3.8): a pass an account's limit ended is gated as a choice refused at its start is, cooling until its
        // account's reset, which the look's re-ask reads (`OpinionGateLook`).
        if (first is { State: OpinionViews.Failed, Why: OpinionViews.Limited } && reads.Ready is { } ready)
        {
            return state with { State = OpinionGateStates.Unavailable, Code = ReviewerUnavailable.Cooling, Until = ready(first.Session) };
        }

        if (first.State == OpinionViews.Failed) return state with { State = OpinionGateStates.Unavailable, Code = first.Why ?? "ended" };
        if (!first.Given) return state;

        // What was read, and what waits for the person once it went where it goes (§6.3–§6.7).
        var read = first.Tip;
        OpinionDisputes disputes = new([], []);
        if (first.Findings!.Count > 0)
        {
            var kept = reads.Delivered(first.Id);
            if (kept.Person is { } toPerson)
            {
                disputes = OpinionDisputes.Unanswered(first);
                state = state with { ToPerson = toPerson };
            }
            else if (kept.Answered is { } answers)
            {
                state = state with { Answers = answers };
                var recheck = kept.Recheck is { } again ? await reads.Opinion(again).ConfigureAwait(false) : null;
                state = state with { Recheck = recheck };
                if ((kept.Recheck is null && kept.RecheckWhy is null && OpinionRechecks.WhyNot(first, answers, facts.Rule!.Rule) is null)
                    || recheck is { State: not (OpinionViews.Failed or OpinionViews.GivenState) }
                    || recheck is { State: OpinionViews.GivenState, Given: false })
                {
                    return state with { State = OpinionGateStates.ReadAgain };
                }

                disputes = OpinionDisputes.Of(answers, recheck);
                if (recheck is { Given: true }) read = recheck.Tip;
            }
            else
            {
                return state with { State = OpinionGateStates.WithSession };
            }
        }

        // Bound to its candidate (§8.3): what would land must be what was read, or a commit before it.
        int? since = 0;
        if (facts.Tip is null || await reads.Covers(read).ConfigureAwait(false) != true)
        {
            since = facts.Tip is null ? null : await reads.Since(read).ConfigureAwait(false);
            if (since is null)
            {
                return state with
                {
                    State = OpinionGateStates.Unread, Disputes = disputes,
                    Problem = "git could not say whether the commits that would land are the ones another agent read",
                };
            }
        }

        state = state with { Disputes = disputes, Since = since };
        return disputes.Count > 0 ? state with { State = OpinionGateStates.Disputed }
            : since > 0 ? state with { State = OpinionGateStates.CommitsSince }
            : state with { State = OpinionGateStates.Settled };
    }

    /// <summary>What the gate says (design §8.5's table): <see cref="OpinionGateWords.Says"/>.</summary>
    public static string Says(OpinionGateState gate) => OpinionGateWords.Says(gate);

    internal static string Reviewer(OpinionGateState gate) => OpinionGateWords.Reviewer(gate);

    /// <summary>What a chat run's press says where it asked the opinion (CHATFLOW1d): <see cref="OpinionGateWords.AskedAtPress"/>.</summary>
    public static string AskedAtPress(OpinionGateState gate) => OpinionGateWords.AskedAtPress(gate);

    /// <summary>What the landing record keeps of a gate that let the work go (§8.6); null where the level asked none.</summary>
    internal static LandingOpinion? Landing(OpinionGateState gate)
    {
        if (gate.State == OpinionGateStates.None || !gate.LetsGo) return null;
        var opinion = gate.Opinion;
        var findings = opinion?.Findings ?? [];
        var read = gate.Recheck is { Given: true } recheck ? recheck.Tip : opinion?.Tip;
        return new LandingOpinion(gate.State)
        {
            Opinion = opinion?.Id,
            Reviewer = opinion?.Door,
            Label = opinion?.Label,
            Base = opinion?.Base,
            Tip = read,
            Passes = gate.Passes + (gate.Recheck is null ? 0 : 1),
            Weights = findings.GroupBy(finding => finding.Weight, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            Answers = (gate.Answers?.Findings ?? []).GroupBy(row => row.Counts, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            Disputes = gate.Disputes?.Count ?? 0,
            Unread = gate.Since ?? 0,
            Person = gate.Person?.Said,
            At = gate.Person?.At,
            Words = gate.Person?.Words,
            Code = gate.State == OpinionGateStates.Unavailable || gate.Answered == OpinionGateStates.Unavailable ? gate.Code : null,
        };
    }

    /// <summary>
    /// Whether a work step of the chain in <paramref name="repository"/> comes after <paramref name="quest"/>, run or still to run,
    /// and not declined (design §8.1): then the opinion on the chain's work there is that step's to ask, once, at its end.
    /// </summary>
    public static bool HasLaterWork(IReadOnlyList<QuestView> chain, string quest, string repository)
    {
        var at = chain.ToList().FindIndex(each => string.Equals(each.Id, quest.TrimStart('#'), StringComparison.OrdinalIgnoreCase));
        return at >= 0 && chain.Skip(at + 1).Any(later => later.SetUpIn is null && later.Status != "Declined"
            && string.Equals(later.To, repository, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The quest of the chain in <paramref name="repository"/> still to run after <paramref name="quest"/> (design §8.1): a work
    /// step, open or taken, listed after it, or one still composed in an open quest's <c>then</c>; null where none is. A set-up step
    /// is D154's, never a later step of the work.
    /// </summary>
    public static string? LaterStep(IReadOnlyList<QuestView> chain, string quest, string repository)
    {
        var at = chain.ToList().FindIndex(each => string.Equals(each.Id, quest.TrimStart('#'), StringComparison.OrdinalIgnoreCase));
        if (at < 0) return null;
        foreach (var later in chain.Skip(at + 1))
        {
            // FABRIC-CLOSE1h (§E5): a step a closure graph holds has run at its submission, and published its steps there, as a done has.
            if (later.SetUpIn is null && QuestResults.Status(later) is "Open" or "Taken" && string.Equals(later.To, repository, StringComparison.OrdinalIgnoreCase))
            {
                return later.Id;
            }
        }

        foreach (var open in chain.Where(each => QuestResults.Status(each) is "Open" or "Taken"))
        {
            if (open.Then.Any(step => step.SetUpIn is null && string.Equals(step.To, repository, StringComparison.OrdinalIgnoreCase))) return open.Id;
        }

        return null;
    }
}
