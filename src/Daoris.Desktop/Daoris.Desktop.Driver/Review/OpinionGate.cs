using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// Where the second opinion's part of the landing gate stands (XAGENT1f, D155 points 9 and 10; the second-agent design §8.5), as
/// a code a log line and a page can carry. Five let the work go: nothing to wait for, settled, and the person's three answers.
/// An opinion that is unavailable lets it go only where the rule does not require one (§8.4).
/// </summary>
public static class OpinionGateStates
{
    /// <summary>The level says no second opinion here: today's behaviour.</summary>
    public const string None = "none";

    /// <summary>A later step of the chain in this repository is still to run: one opinion reads the chain's whole work there.</summary>
    public const string WaitsChain = "waits-chain";

    /// <summary>The work is done and nothing has been asked yet: the driver's next look asks it, after the quest's own hold.</summary>
    public const string NotAsked = "not-asked";

    /// <summary>Another agent is reading it.</summary>
    public const string Reading = "reading";

    /// <summary>Its findings are with the working session, which answers them as its next turn.</summary>
    public const string WithSession = "with-session";

    /// <summary>The one recheck reads the commits made in answer.</summary>
    public const string ReadAgain = "read-again";

    /// <summary>A <c>must</c> finding not fixed and not withdrawn: the person's to answer, in the press that comes next.</summary>
    public const string Disputed = "disputed";

    /// <summary>No opinion could be had: no reviewer, a failed pass, or one out of time. It holds only where the rule requires one.</summary>
    public const string Unavailable = "unavailable";

    /// <summary>Commits that would land that no other agent read: the cap of one pass and one recheck is spent.</summary>
    public const string CommitsSince = "commits-since";

    /// <summary>An opinion covers the tip, the working session answered it, and no dispute is open.</summary>
    public const string Settled = "settled";

    /// <summary>The person's <i>Go on anyway…</i>.</summary>
    public const string Anyway = "anyway";

    /// <summary>The person's <i>I looked myself…</i>: their own reading, an opinion of tier <c>person</c>.</summary>
    public const string Myself = "myself";

    /// <summary>The press that came next answered what was unsettled: <i>Accept…</i> with it shown, or D154's <i>Reviewed</i> after it.</summary>
    public const string Answered = "answered";

    /// <summary>Whether it waits could not be read: the service or git did not answer, where a rule says one is asked.</summary>
    public const string Unread = "unread";

    /// <summary>
    /// The run's named workflow asks for a second opinion by reviewers its repository does not declare (WORKFLOW1f, the workflow
    /// design §2.3, §3.7): it sits saying why, until they are declared or the person goes on, or looks, themselves.
    /// </summary>
    public const string CannotStart = "cannot-start";

    /// <summary>The states in which another agent, or the working session, is still at work on the opinion: what sits a set-up step (§7).</summary>
    public static bool AtWork(string state) => state is NotAsked or Reading or WithSession or ReadAgain;
}

/// <summary>What the person said at the gate (design §8.5), kept by the driver for the chain's work in a repository.</summary>
public static class OpinionPersonSaid
{
    /// <summary><i>Go on anyway…</i>: what was unsettled stays so, and the gate's next item follows.</summary>
    public const string Anyway = "anyway";

    /// <summary><i>I looked myself…</i>: the person's own reading, in place of another agent's.</summary>
    public const string Myself = "myself";

    /// <summary>A press that showed what was unsettled and was made with it shown: <i>Accept…</i> on the screen.</summary>
    public const string Press = "press";
}

/// <summary>
/// One answer of the person's at the gate (design §8.5): what they said, the commit it was said at, when, their words, and the
/// door. It covers the work while what would land is that commit or one before it: a commit after it is work they did not see.
/// </summary>
public sealed record OpinionPersonWord(string Said, string Tip, DateTimeOffset At)
{
    public string? Words { get; init; }

    /// <summary><c>terminal</c> or <c>screen</c> (<see cref="ReviewDoors"/>).</summary>
    public string? Door { get; init; }
}

/// <summary>
/// One pass the driver asked for the chain's work in a repository (design §8.6), kept beside it: the opinion the host opened, or
/// why no reviewer could read it, with the cool-off's reset where that is why.
/// </summary>
/// <param name="Occasion">One of <c>landing</c>, <c>steps</c>, <c>failure</c> or <c>asked</c> (design §2.1).</param>
/// <param name="Working">The session whose work it reads.</param>
/// <param name="Tip">The candidate's tip as asked.</param>
public sealed record OpinionAskKept(DateTimeOffset At, string Occasion, string Working, string Tip)
{
    /// <summary>The second opinion the host opened; null where none was.</summary>
    public string? Opinion { get; init; }

    /// <summary>Why no other maker's agent could read it, the choice's code (§3.3), or why the pass did not open; null where one opened.</summary>
    public string? Code { get; init; }

    /// <summary>The first reset among the reviewers passed for a cool-off, where that is why (§8.4).</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>Who asked: <c>look</c> for the occasion's own ask, <c>person</c> for a press.</summary>
    public string By { get; init; } = OpinionAskers.Look;

    /// <summary>The adapter that read it, where one did.</summary>
    public string? Reviewer { get; init; }

    /// <summary>How the reviewer stands to the work's families (<see cref="ReviewerLabels"/>), where one read it.</summary>
    public string? Label { get; init; }
}

/// <summary>Who asked a pass: the occasion's look, or the person's press.</summary>
public static class OpinionAskers
{
    public const string Look = "look";
    public const string Person = "person";
}

/// <summary>
/// A pass the person asked for at the gate (design §8.5's <i>Ask now</i>, <i>Try again</i>, <i>Ask again</i> and <i>Ask the same
/// agent, fresh</i>; §2.1's failure and asked occasions), kept until the driver's next look starts it.
/// </summary>
/// <param name="Session">The working session whose work it reads.</param>
public sealed record OpinionRequest(DateTimeOffset At, string Session, string Occasion)
{
    /// <summary>The reviewer the person named among the rule's, or null for the rule's order.</summary>
    public string? Reviewer { get; init; }

    /// <summary>The working agent's own, in a fresh conversation: never independent, and said so on its face (§3.3).</summary>
    public bool SameAgent { get; init; }

    /// <summary>The person's words with it.</summary>
    public string? Words { get; init; }
}

/// <summary>
/// What the gate reads for the chain's work in a repository (design §8.2): the rule standing for it, the quest that would land
/// and whether a later step here is still to run, the commit that would land, what the driver kept of the asks and the person's
/// answers, and D154's gate, whose <i>Reviewed</i> answers what it shows.
/// </summary>
public sealed record OpinionGateFacts(string Repository, ResolvedOpinion? Rule)
{
    /// <summary>The working session the door lands, which the terminal's doors name.</summary>
    public string? Session { get; init; }

    /// <summary>The quest the work served; null for a conversation's, which lands nothing by the driver's gate (§12.2).</summary>
    public QuestView? Quest { get; init; }

    /// <summary>A later step of the chain in this repository still to run, by its quest's id; null where none is.</summary>
    public string? Later { get; init; }

    /// <summary>The commit that would land: the tree's <c>HEAD</c>; null where it was not read.</summary>
    public string? Tip { get; init; }

    /// <summary>The occasion the gate is read for: <c>landing</c>, or <c>steps</c> for the chain's next step (§8.1).</summary>
    public string Occasion { get; init; } = OpinionRules.Landing;

    public IReadOnlyList<OpinionAskKept> Asks { get; init; } = [];

    public IReadOnlyList<OpinionPersonWord> Person { get; init; } = [];

    /// <summary>D154's gate for the same work, whose <i>Reviewed</i> after the opinion stood answers its disputes (§7).</summary>
    public ReviewGateState? Review { get; init; }

    /// <summary>Why the run's named workflow's opinion step cannot start (WORKFLOW1f, <see cref="WorkflowProcess.OpinionCannot"/>); else null.</summary>
    public string? Cannot { get; init; }
}

/// <summary>What the judgement reads beyond the facts: the host's opinions, what the driver did with each, and git's ancestry.</summary>
/// <param name="Opinion">An opinion as the host answers it, or null.</param>
/// <param name="Delivered">What the driver kept of an opinion's delivery (<see cref="OpinionDeliveries"/>).</param>
/// <param name="Covers">Whether the tip that would land is a commit or an ancestor of it: true, false, or null where git could not say.</param>
/// <param name="Since">How many commits that would land a commit does not hold; null where git could not say.</param>
public sealed record OpinionReads(
    Func<string, Task<OpinionView?>> Opinion, Func<string, OpinionDelivered> Delivered, Func<string, Task<bool?>> Covers,
    Func<string, Task<int?>> Since);

/// <summary>
/// The second opinion's part of the landing gate for the chain's work in one repository (XAGENT1f, D155 points 9 and 10; the
/// second-agent design §7–§8), and why it stands so.
/// </summary>
/// <param name="State">One of <see cref="OpinionGateStates"/>.</param>
public sealed record OpinionGateState(string State, string Repository)
{
    /// <summary>The rule standing for the repository; null where nothing is set anywhere.</summary>
    public ResolvedOpinion? Rule { get; init; }

    /// <summary>The working session the door lands, which the terminal's doors name.</summary>
    public string? Session { get; init; }

    /// <summary>The commit that would land; null where it was not read.</summary>
    public string? Tip { get; init; }

    /// <summary>A later step of the chain here still to run.</summary>
    public string? Later { get; init; }

    /// <summary>Whether the quest's own hold comes first (§7 item 1): nothing is asked until the person lifts it.</summary>
    public bool Held { get; init; }

    /// <summary>The first pass the state speaks of, the newest asked; null where none was.</summary>
    public OpinionView? Opinion { get; init; }

    /// <summary>Its recheck, where one was asked.</summary>
    public OpinionView? Recheck { get; init; }

    /// <summary>The working session's answers as the driver read them when its turn ended.</summary>
    public OpinionReading? Answers { get; init; }

    /// <summary>Why the findings went to the person instead of the working session (§6.7), a code.</summary>
    public string? ToPerson { get; init; }

    /// <summary>What waits for the person (§6.6).</summary>
    public OpinionDisputes? Disputes { get; init; }

    /// <summary>Commits that would land that no other agent read; null where git could not say.</summary>
    public int? Since { get; init; }

    /// <summary>Why no opinion could be had, a code: the choice's (§3.3), or a failed pass's (<c>ended</c>, <c>out-of-time</c>).</summary>
    public string? Code { get; init; }

    /// <summary>The cool-off's reset, where that is why none could be had.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>The person's answer that let it go, or that a press answered with.</summary>
    public OpinionPersonWord? Person { get; init; }

    /// <summary>What the person's answer settled: the state they answered, for the landing record.</summary>
    public string? Answered { get; init; }

    /// <summary>Why it could not be read, for <see cref="OpinionGateStates.Unread"/>.</summary>
    public string? Problem { get; init; }

    /// <summary>How many passes were asked for this work, the person's own included (§8.3, §8.6).</summary>
    public int Passes { get; init; }

    /// <summary>Whether the rule requires an opinion where none can be had (§8.4).</summary>
    public bool Required => Rule?.Rule.Required == true;

    /// <summary>Whether the work may land as far as the second opinion is concerned.</summary>
    public bool LetsGo => State is OpinionGateStates.None or OpinionGateStates.Settled or OpinionGateStates.Anyway
        or OpinionGateStates.Myself or OpinionGateStates.Answered
        || (State == OpinionGateStates.Unavailable && !Required);

    /// <summary>Whether a press that shows what is unsettled answers it by being made (§8.2–§8.3): a dispute, or commits unread.</summary>
    public bool PressAnswers => State is OpinionGateStates.Disputed or OpinionGateStates.CommitsSince;

    /// <summary>What the gate says, in the driver's words: the terminal's line and every door's refusal.</summary>
    public string Says => OpinionGate.Says(this);

    /// <summary>
    /// What a press must echo to answer what it showed (§8.2): the opinion, the commit and what was unsettled, so a press made
    /// over a page drawn before the gate moved answers nothing.
    /// </summary>
    public string Token => string.Join(':', Opinion?.Id ?? "none", Tip ?? "-", State, Disputes?.Count ?? 0, Since ?? 0);

    /// <summary>What the landing record keeps of it (§8.6); null where the level asks none.</summary>
    public LandingOpinion? Landing => OpinionGate.Landing(this);
}

/// <summary>
/// What the landing record keeps of the second opinion (XAGENT1f, design §8.6): what let the work go, the reviewer and its label,
/// the candidate, the passes, the findings counted by weight and by answer, the person's adjudication, and the commits unread;
/// or <c>none</c> with its code.
/// </summary>
/// <param name="Said">The gate's state that let it go: <c>settled</c>, <c>anyway</c>, <c>myself</c>, <c>answered</c> or <c>unavailable</c>.</param>
public sealed record LandingOpinion(string Said)
{
    public string? Opinion { get; init; }

    public string? Reviewer { get; init; }

    public string? Label { get; init; }

    public string? Base { get; init; }

    public string? Tip { get; init; }

    public int Passes { get; init; }

    /// <summary>The findings, counted by weight: <c>must</c>, <c>should</c>, <c>note</c>.</summary>
    public IReadOnlyDictionary<string, int> Weights { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>The answers, counted: <c>fixed</c>, <c>rejected</c>, <c>unresolved</c>.</summary>
    public IReadOnlyDictionary<string, int> Answers { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    public int Disputes { get; init; }

    /// <summary>The commits that landed which no other agent read.</summary>
    public int Unread { get; init; }

    /// <summary>The person's adjudication: what they said, and when, and their words.</summary>
    public string? Person { get; init; }

    public DateTimeOffset? At { get; init; }

    public string? Words { get; init; }

    /// <summary>Why none could be had, where it landed without one.</summary>
    public string? Code { get; init; }
}

/// <summary>
/// The second opinion's gate (XAGENT1f, D155 points 9 and 10; the second-agent design §7–§8): whether the level asks one, and
/// whether the work that would land may go as far as it is concerned. A table of facts and the person's presses: the opinion's
/// coverage and the fixes are git's answers, and no model reads any of it (§10).
/// </summary>
/// <remarks>
/// <para><b>One gate, in a fixed order</b> (§7): the quest's own holds, then this, then D154's look, then the landing. Every
/// landing door reads both through <see cref="SessionTrees.GateAsync"/> and hands them to <see cref="SessionTrees.LandAsync"/>,
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
        };

        // A conversation's work lands nothing by the driver's gate (§12.2), and a level that names no occasion here asks nothing.
        if (facts.Quest is null || !Reads(facts.Rule, facts.Cannot, facts.Occasion)) return Of(OpinionGateStates.None);

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

    /// <summary>The terminal's verb for a door, naming the session where the state knows it.</summary>
    private static string Verb(OpinionGateState gate, string verb, string? words = null) =>
        $"`daoris-driver opinion {verb} {gate.Session ?? "<session>"}{(words is null ? "" : $" {words}")}`";

    /// <summary>
    /// What the gate says (design §8.5's table): the state, why, and the terminal's doors that move it, where it holds; what let
    /// it go, where it does.
    /// </summary>
    public static string Says(OpinionGateState gate)
    {
        var who = gate.Opinion is { } opinion ? opinion.Who : null;
        var anyway = Verb(gate, "anyway", "\"…\"");
        return gate.State switch
        {
            OpinionGateStates.None => gate.Rule is null
                ? "Nothing waits for a second opinion here."
                : $"No second opinion waits for this work: {NoneWhy(gate)}.",
            OpinionGateStates.WaitsChain =>
                $"Waits for a second opinion: it is asked once this chain's last step in `{gate.Repository}` is done, and quest `#{gate.Later}` "
                + $"still works there, so one opinion reads the chain's whole work. {Verb(gate, "ask")} asks one for this work now, or {anyway} "
                + "lets it go on without one.",
            OpinionGateStates.NotAsked => gate.Held
                ? "Waits for a second opinion: it is asked once the hold on its quest is lifted, since that comes first."
                : $"Waits for a second opinion: it is asked at the driver's next look. {Verb(gate, "ask")} asks it now.",
            OpinionGateStates.Reading =>
                $"Waits for a second opinion: being read by {who}, {LabelSaid(gate.Opinion!.Label)}"
                + (gate.Opinion.Minutes is { } minutes ? string.Create(CultureInfo.InvariantCulture, $", for at most {minutes} minutes") : "")
                + $". `daoris-driver opinion stop {gate.Opinion.Id}` stops it.",
            OpinionGateStates.WithSession =>
                $"Waits for a second opinion: the working session is answering {Plural(gate.Opinion!.Findings!.Count, "finding")} by {who}.",
            OpinionGateStates.ReadAgain => $"Waits for a second opinion: read again by {who}, the commits made in answer.",
            OpinionGateStates.Disputed =>
                $"{Plural(gate.Disputes!.Count, "finding")} by {who} {(gate.Disputes.Count == 1 ? "is" : "are")} disputed: a `must` the working "
                + "session did not fix and no recheck withdrew" + SinceSaid(gate) + $". {Verb(gate, "show")} lists them with their answers; your "
                + $"Accept, or your Reviewed where a look is required, answers them; {anyway} lands it over them, or "
                + $"`daoris-driver sessions say {gate.Opinion!.Working} \"…\"` sends it back.",
            OpinionGateStates.CommitsSince =>
                $"{Plural(gate.Since ?? 0, "commit")} since {(gate.Since == 1 ? "was" : "were")} not read by another agent: {who} read it at "
                + $"`{Short(gate.Recheck is { Given: true } recheck ? recheck.Tip : gate.Opinion!.Tip)}`, and one pass and one recheck are "
                + $"spent for this work. Your Accept, or your Reviewed where a look is required, answers them; {anyway} lands them, or "
                + $"{Verb(gate, "ask")} asks again.",
            OpinionGateStates.Unavailable => $"No second opinion: {UnavailableWhy(gate)}. " + (gate.Required
                ? $"The rule requires one, so the work waits for you: {Verb(gate, "ask")} tries again, with `--same-agent` asks the same "
                  + $"agent in a fresh conversation, {Verb(gate, "myself", "\"…\"")} records your own reading, or {anyway} lets it land without one."
                : "The rule does not require one, so nothing waits for it."),
            OpinionGateStates.Settled => Settled(gate),
            OpinionGateStates.Anyway => $"You went on without a settled second opinion{Answering(gate)}{WordsSaid(gate.Person)}.",
            OpinionGateStates.Myself => $"You looked at it yourself in place of another agent's reading{WordsSaid(gate.Person)}.",
            OpinionGateStates.Answered => gate.Person?.Said == ReviewVerdicts.Reviewed
                ? $"Your Reviewed answered the second opinion{Answering(gate)}{WordsSaid(gate.Person)}."
                : $"Your Accept answered the second opinion{Answering(gate)}{WordsSaid(gate.Person)}.",
            // WORKFLOW1f (the workflow design §3.7): the named workflow's step sits, saying what declares it; the person's own answers
            // are the floor, as they are where no opinion can be had.
            OpinionGateStates.CannotStart => $"Waits for a second opinion, and it cannot start: {gate.Problem} {anyway} lets it land "
                + $"without one, or {Verb(gate, "myself", "\"…\"")} records your own reading.",
            _ => $"Whether this work waits for a second opinion could not be read: {gate.Problem}. Nothing lands until it can be.",
        };
    }

    /// <summary>What the person's answer answered: the disputes, or the commits nobody read, as the gate stood beneath it.</summary>
    private static string Answering(OpinionGateState gate) => gate.Answered switch
    {
        OpinionGateStates.Disputed => $" over {Plural(gate.Disputes?.Count ?? 0, "disputed finding")}",
        OpinionGateStates.CommitsSince => $" with {Plural(gate.Since ?? 0, "commit")} no other agent read",
        OpinionGateStates.Unavailable => " when none could be had",
        OpinionGateStates.WaitsChain or OpinionGateStates.NotAsked => " before one was asked",
        OpinionGateStates.CannotStart => " when its workflow's step could not start",
        OpinionGateStates.Reading or OpinionGateStates.WithSession or OpinionGateStates.ReadAgain => " while it was still being read",
        _ => "",
    };

    private static string Settled(OpinionGateState gate)
    {
        var opinion = gate.Opinion!;
        var read = Short(gate.Recheck is { Given: true } recheck ? recheck.Tip : opinion.Tip);
        if (opinion.Findings is { Count: 0 }) return $"Second opinion settled: {opinion.Who} raised nothing in what it read at `{read}`: {opinion.Read}";
        return $"Second opinion settled: {opinion.Who} read it at `{read}`, and its {Plural(opinion.Findings!.Count, "finding")} "
            + $"{(gate.ToPerson is null ? "were answered by the working session" : "went to you")} with no dispute open.";
    }

    private static string NoneWhy(OpinionGateState gate) =>
        gate.Rule!.Rule.IsNone
            ? $"`{gate.Repository}` is set to none"
            : $"{(gate.Rule.Source == OpinionSource.Repository ? $"`{gate.Repository}`'s rule" : "the workspace's rule")} reads before each next "
              + "step, not before landing";

    private static string UnavailableWhy(OpinionGateState gate) => gate.Code switch
    {
        ReviewerUnavailable.NoReviewer => "no listed reviewer of another maker is installed",
        ReviewerUnavailable.Cooling => "every listed reviewer of another maker is cooling"
            + (gate.Until is { } until ? $" until {until.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC, when the driver asks again" : ""),
        ReviewerUnavailable.SignedOut => "no listed reviewer of another maker has an account signed in",
        ReviewerUnavailable.NotIndependent => "no listed reviewer is another maker's agent",
        "ended" => $"{gate.Opinion?.Who ?? "its reviewer"} ended without saying one",
        "out-of-time" => $"{gate.Opinion?.Who ?? "its reviewer"} ran out of its minutes before it said one",
        _ => "no listed reviewer of another maker could read it",
    };

    private static string LabelSaid(string label) => label switch
    {
        ReviewerLabels.AnotherMaker => "another maker's agent",
        ReviewerLabels.SameAgent => "the same agent in a fresh conversation, which is not an independent reading",
        _ => "an agent whose maker is not declared, whose reading is not counted as another maker's",
    };

    private static string SinceSaid(OpinionGateState gate) =>
        gate.Since is > 0 ? $", and {Plural(gate.Since.Value, "commit")} since {(gate.Since == 1 ? "was" : "were")} not read by another agent" : "";

    private static string WordsSaid(OpinionPersonWord? word) => word?.Words is { Length: > 0 } words ? $": \"{words}\"" : "";

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;

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
            Reviewer = opinion?.Adapter,
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
            if (later.SetUpIn is null && later.Status is "Open" or "Taken" && string.Equals(later.To, repository, StringComparison.OrdinalIgnoreCase))
            {
                return later.Id;
            }
        }

        foreach (var open in chain.Where(each => each.Status is "Open" or "Taken"))
        {
            if (open.Then.Any(step => step.SetUpIn is null && string.Equals(step.To, repository, StringComparison.OrdinalIgnoreCase))) return open.Id;
        }

        return null;
    }
}

/// <summary>
/// The whole landing gate a door hands <see cref="SessionTrees.LandAsync"/> (XAGENT1f; the second-agent design §7): the second
/// opinion and D154's look, read together, refused in that order. The quest's own holds come first, as they hold the opinion's
/// ask; the landing comes last. Since WORKFLOW1f the run's own part comes before the opinion (the workflow design §4.4), and the
/// landing step's own declarations after the look, all read from one process.
/// </summary>
public sealed record LandingGate(OpinionGateState Opinion, ReviewGateState Review)
{
    /// <summary>
    /// The run's own part (WORKFLOW1f): the process every part was read with, and what of the run's own holds the work. Null for a
    /// gate no door read one for, which is today's.
    /// </summary>
    public WorkflowGateState? Workflow { get; init; }

    /// <summary>The process the gate was read with, which the landing lands by; null where none was read.</summary>
    public WorkflowProcess? Process => Workflow?.Process;

    /// <summary>Whether the work may land: every part lets it go.</summary>
    public bool LetsGo => Refusal is null;

    /// <summary>
    /// The first part that holds, in §7's order, as a landing's refusal: its sentence and its code; null where all let go. The run's
    /// own part and the landing step's are refused as <see cref="AutoLandingCode.Refused"/>: their sentence says what holds them.
    /// </summary>
    public TreeLanding? Refusal =>
        Workflow is { LetsGo: false } workflow ? new(false, workflow.Says) { Refusal = AutoLandingCode.Refused }
        : !Opinion.LetsGo ? new(false, Opinion.Says) { Refusal = AutoLandingCode.Opinion }
        : !Review.LetsGo ? new(false, Review.Says) { Refusal = AutoLandingCode.Unreviewed }
        : Process?.LandingCannot is not null ? new(false, WorkflowGate.LandingSays(Process)) { Refusal = AutoLandingCode.Refused }
        : null;

    /// <summary>Whether what holds the work is the run's own part or its landing step's (WORKFLOW1f), where it holds.</summary>
    public bool WorkflowHolds => Workflow is { LetsGo: false }
        || (Opinion.LetsGo && Review.LetsGo && Process?.LandingCannot is not null);

    /// <summary>
    /// The gate with the person's press counted (§8.2–§8.3): where the press was made with what was unsettled shown, and the gate
    /// still stands as it was drawn (<see cref="OpinionGateState.Token"/>), its disputes or its commits unread are answered.
    /// </summary>
    public bool Answers(string? token) => Opinion.PressAnswers && token is not null && string.Equals(token, Opinion.Token, StringComparison.Ordinal);

    /// <summary>A gate that holds nothing, for a door that asked none: today's behaviour.</summary>
    public static LandingGate Open(string repository) =>
        new(new OpinionGateState(OpinionGateStates.None, repository), new ReviewGateState(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = repository }));
}

/// <summary>What a door says of the whole landing gate (XAGENT1f): the plan's lines, and the log's line for a landing it held.</summary>
public static class LandingGateWords
{
    /// <summary>
    /// What <c>trees land --plan</c> says of the gate before the press (the second-agent design §8.6; the review design §3.5): each
    /// part that asks anything, in the gate's order, what holds it or what let it go. Nothing where neither asks.
    /// </summary>
    public static IReadOnlyList<string> Plan(LandingGate gate)
    {
        var lines = new List<string>();
        // The run's own part first (WORKFLOW1f): which workflow decides, where a named one does, and what of its own holds it.
        if (gate.Workflow is { State: not WorkflowGateStates.None } workflow) lines.Add($"trees: {workflow.Says}");
        if (gate.Opinion.State != OpinionGateStates.None) lines.Add($"trees: {gate.Opinion.Says}");
        if (gate.Review.State != ReviewStates.None) lines.Add($"trees: {gate.Review.Says}");
        if (gate.Process is { LandingCannot: not null } process) lines.Add($"trees: {WorkflowGate.LandingSays(process)}");
        return lines;
    }

    /// <summary>The machine log's line for a landing the gate held, by the part that held it; null where none did.</summary>
    /// <param name="owner">The tree's workspace and repository.</param>
    public static LandingLine? Held(string session, (string Workspace, string Repository) owner, LandingGate gate, TreeLanding landed, string door) =>
        landed.Refusal switch
        {
            AutoLandingCode.Refused when gate.WorkflowHolds && gate.Workflow is { } workflow =>
                WorkflowLines.Held(session, owner.Repository, owner.Workspace, workflow, door, workflow.LetsGo ? WorkflowKinds.Landing : null),
            AutoLandingCode.Opinion => OpinionLines.Held(session, owner.Repository, owner.Workspace, gate.Opinion, door),
            AutoLandingCode.Unreviewed => ReviewLines.Held(session, owner.Repository, owner.Workspace, gate.Review, door),
            _ => null,
        };
}
