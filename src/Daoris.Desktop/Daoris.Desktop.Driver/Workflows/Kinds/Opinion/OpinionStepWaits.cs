using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What the <c>opinion</c> step says while its gate holds it: every hold is a wait of its own, never a bare <i>working</i>. A pass still
/// reading is its reviewer's (<see cref="Reading"/>, <see cref="Rechecking"/>); one nothing has asked yet says why and whose turn it is
/// (<see cref="NotAsked"/> and its variants, <see cref="WaitsChain"/>); one nobody is free to read is the line's vacancy
/// (<see cref="Vacant"/>); findings handed to the work's session wait on its answer (<see cref="Answer"/>); read and answered but holding
/// the work for the person (<see cref="Disputed"/>, <see cref="CommitsSince"/>) it waits on their answer, said with its count; a gate git
/// could not read says so (<see cref="Unread"/>); and a pass whose reviewer ended without giving an opinion, or whose minutes ran out
/// first, fails the step, saying so, until another pass reaches it.
/// </summary>
public static class OpinionStepWaits
{
    /// <summary>The opinion's declared wait: its findings are with the work's session, which answers them.</summary>
    public const string Answer = "answer";

    /// <summary>
    /// The opinion's wait where its gate is disputed: a <c>must</c> the working session left unfixed that no recheck withdrew (the first
    /// pass's), or one a recheck raised itself, waits on the person's answer (<c>count</c>: the disputed findings, of which <c>first</c> the
    /// first pass's and <c>raised</c> the recheck's own).
    /// </summary>
    public const string Disputed = "disputed";

    /// <summary>
    /// The opinion's wait where commits after the read tip wait on the person, the one pass and the one recheck spent (<c>count</c>:
    /// the commits no other agent read).
    /// </summary>
    public const string CommitsSince = "commits-since";

    /// <summary>The opinion's wait while its first pass is still reading: the reviewer's (<c>agent</c>), until its minutes run out.</summary>
    public const string Reading = "reading";

    /// <summary>The opinion's wait while its one recheck of the commits made in answer is still reading: the recheck's reviewer's.</summary>
    public const string Rechecking = "rechecking";

    /// <summary>The opinion's wait before any pass is asked: the driver's next look asks it.</summary>
    public const string NotAsked = "not-asked";

    /// <summary>The opinion's wait before the recheck is asked: the driver's next look asks it.</summary>
    public const string NotAskedRecheck = "not-asked-recheck";

    /// <summary>The opinion's wait where the quest's own hold comes first: nothing is asked until the person lifts it.</summary>
    public const string NotAskedHeld = "not-asked-held";

    /// <summary>The opinion's wait where a chat run's opinion is asked by the person's Accept press.</summary>
    public const string NotAskedPress = "not-asked-press";

    /// <summary>The opinion's wait where git could not say whether the commits that would land are the ones another agent read.</summary>
    public const string Unread = "unread";

    /// <summary>The opinion's wait where the chain's later step in this repository reads the whole work with one opinion.</summary>
    public const string WaitsChain = "waits-chain";

    /// <summary>
    /// The opinion's wait where its rule requires one and nobody is free to read it (<c>why</c>: no reviewer of another maker installed, every
    /// one cooling until <c>until</c>, none signed in, none another maker's, or the choice refused); the line's first level holds it.
    /// </summary>
    public const string Vacant = "vacant";

    /// <summary>The vacant wait where a cool-off names the reset (the wait's <c>until</c>), when the driver asks again.</summary>
    public const string VacantUntil = "vacant-until";

    public const string ReadingUntil = "reading-until";

    public const string RecheckingUntil = "rechecking-until";

    /// <summary>
    /// The opinion's wait where its pass ended with no opinion given and its asks are not spent: the driver's next look asks again, the next ready
    /// reviewer first, and nobody's answer is waited on (<c>agent</c>: the reviewer that ended, <c>count</c> of <c>bound</c> asks, <c>why</c> the code).
    /// </summary>
    public const string Again = "asking-again";

    /// <summary>
    /// The opinion's wait where its rule requires one and no pass could settle it for a reason no vacancy names (<c>why</c>: a provider that
    /// kept failing, or another cause): the person's doors answer it.
    /// </summary>
    public const string Unavailable = "unavailable";

    /// <summary>Not a wait: a hold its record keeps whose gate the risk policy now passes, said with the pass's line (<c>why</c>) on nobody's answer.</summary>
    public const string Passed = "passed";

    /// <summary>Every wait the opinion kind declares.</summary>
    public static IReadOnlyList<string> Codes { get; } =
        [Answer, Disputed, CommitsSince, Reading, Rechecking, NotAsked, NotAskedRecheck, NotAskedHeld, NotAskedPress, Unread, WaitsChain, Vacant, VacantUntil, ReadingUntil, RecheckingUntil, Again, Unavailable];

    /// <summary>A failed pass's code (the gate's <see cref="OpinionGateState.Code"/>): its reviewer's session ended without giving one.</summary>
    public const string Ended = OpinionAgain.Ended;

    /// <summary>A failed pass's code: its reviewer's minutes ran out before it gave one.</summary>
    public const string OutOfTime = OpinionAgain.OutOfTime;

    /// <summary>The words a step a pass left with no opinion fails with; null for every other code, a wait for a reviewer among them.</summary>
    public static string? EndedSaid(string? code) => code switch
    {
        Ended => "Its reviewer's session ended without giving an opinion, so no agent's reading stands for this work: ask again, or pass it over.",
        OutOfTime => "Its reviewer's minutes ran out before it gave an opinion, so no agent's reading stands for this work: ask again, or pass it over.",
        _ => null,
    };

    /// <summary>
    /// The wait of an active <c>opinion</c>, read from its gate as it stands (a record that lags says nothing the gate does not): with the
    /// work's session (<see cref="OpinionGateStates.WithSession"/>), the session the findings went to; a pass reading, its reviewer and
    /// when its minutes run out; a gate nothing has asked, why and whose turn it is; disputed or with commits since nobody read, the
    /// person's answer, with its count and the session whose work it is. Null for a state the step ends or is passed over on, and where no
    /// gate is read and the record holds none of the session's.
    /// </summary>
    /// <remarks>A dispute the policy gates on a role names the step's own role (<see cref="StepRoles.Of"/>), the one its wait is held on, never the row's <c>by</c>.</remarks>
    public static WorkflowRunWait? WaitOf(NodeDeclaration node, NodeStatus status, OpinionGateState? gate)
    {
        if (status.State != NodeStates.Active) return null;
        var state = gate?.State ?? status.Holds;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        void Put(string name, string? value)
        {
            if (value is { Length: > 0 }) values[name] = value;
        }

        WorkflowRunWait Wait(string code, DateTimeOffset? until = null) => new(code, values, status.Since, until);

        // A dispute the policy gates on a role is that role's answer, said with the policy's reason, never the person's.
        void OnRoleOf()
        {
            if (status.Holds is not (OpinionGateReading.RoleDisputed or OpinionGateReading.RoleCommitsSince) || gate?.Risk is not { } verdict) return;
            values[RoleKey] = StepRoles.Of(node);
            values["why"] = verdict.Why;
        }

        if (OpinionGateReading.PassedLine(gate) is { } passed)
        {
            values["held"] = gate!.Answered ?? "";
            values["count"] = Number(gate.Answered == OpinionGateStates.Disputed ? gate.Disputes?.Count ?? 0 : gate.Since ?? 0);
            values["why"] = passed;
            return Wait(Passed);
        }

        switch (state)
        {
            case OpinionGateStates.WithSession:
                Put("session", gate?.Opinion?.HandedTo ?? gate?.Opinion?.Working ?? gate?.Session);
                return Wait(Answer);
            case OpinionGateStates.Disputed when gate?.Disputes is { } disputes:
                Put("count", Number(disputes.Count));
                Put("first", Number(disputes.First.Count));
                Put("raised", Number(disputes.Raised.Count));
                Put("session", gate.Session ?? gate.Opinion?.Working);
                OnRoleOf();
                return Wait(Disputed);
            case OpinionGateStates.CommitsSince when gate?.Since is { } since:
                Put("count", Number(since));
                Put("session", gate.Session ?? gate.Opinion?.Working);
                OnRoleOf();
                return Wait(CommitsSince);
            case OpinionGateStates.Reading or OpinionGateStates.ReadAgain when gate is not null:
            {
                // The recheck's pass where the gate reads again, else the first's; a recheck not asked yet is the driver's to ask.
                var again = state == OpinionGateStates.ReadAgain;
                var pass = again ? gate.Recheck : gate.Opinion;
                if (pass is null) return again ? Wait(NotAskedRecheck) : null;
                Put("agent", pass.Who);
                Put("session", gate.Session ?? pass.Working);
                return Wait(again ? (pass.Due is null ? Rechecking : RecheckingUntil) : (pass.Due is null ? Reading : ReadingUntil), pass.Due);
            }

            case OpinionGateStates.NotAsked when gate is not null:
                // The quest's own hold comes first, and a chat run's opinion is asked by the person's press; else the driver's next look asks.
                Put("session", gate.Session);
                return Wait(gate.Held ? NotAskedHeld : gate.Run is not null && !gate.Asked ? NotAskedPress : NotAsked);
            case OpinionGateStates.Unread when gate is not null:
                values["why"] = gate.Problem ?? "";
                return Wait(Unread);
            case OpinionGateStates.WaitsChain when gate is not null:
                values["quest"] = gate.Later?.TrimStart('#') ?? "";
                Put("session", gate.Session);
                return Wait(WaitsChain);
            case OpinionGateStates.Unavailable when gate is not null && OpinionAgain.Pending(gate):
                Put("agent", gate.Opinion?.Who);
                Put("count", Number(gate.Ended));
                Put("bound", Number(OpinionAgain.Bound));
                Put("why", gate.Code);
                Put("session", gate.Session);
                return Wait(Again);
            case OpinionGateStates.Unavailable when gate is { Required: true } && OpinionVacancy.Of(gate) is { } vacancy:
                // The reason's words are the page's, by the code it has them for; a choice refused is the general one.
                values["why"] = vacancy.Code is ReviewerUnavailable.NoReviewer or ReviewerUnavailable.Cooling or ReviewerUnavailable.SignedOut
                    or ReviewerUnavailable.NotIndependent ? vacancy.Code : "other";
                Put("back", vacancy.Back);
                return Wait(vacancy.Until is null ? Vacant : VacantUntil, vacancy.Until);
            case OpinionGateStates.Unavailable when gate is { Required: true }:
                values["why"] = gate.Code == OpinionViews.Transient ? OpinionViews.Transient : "other";
                Put("session", gate.Session);
                return Wait(Unavailable);
            default:
                return null;
        }
    }

    /// <summary>The wait's value naming the role the risk policy gates a dispute on.</summary>
    public const string RoleKey = "role";

    /// <summary>Whether <paramref name="wait"/> is the answer of the role the risk policy gates the dispute on.</summary>
    public static bool OnRole(WorkflowRunWait wait) => wait.Code is Disputed or CommitsSince && wait.Values.ContainsKey(RoleKey);

    /// <summary>Whether <paramref name="wait"/> is the person's answer or act on the gate, never an agent's.</summary>
    public static bool OnPerson(WorkflowRunWait wait) =>
        wait.Code is Disputed or CommitsSince && !OnRole(wait) || wait.Code is NotAskedHeld or NotAskedPress or Unavailable;

    /// <summary>
    /// Who holds the step on <paramref name="wait"/>: the person where it is theirs; the reviewer reading a pass, named; the work's session
    /// answering its findings, named; else an agent the role appoints, unnamed (the driver's next look asks, the later step works, git is read
    /// again). A vacancy is the line's, which the step's tenure and cast name, so none is named here.
    /// </summary>
    public static StepHolder? HolderOf(WorkflowRunWait wait) => wait.Code switch
    {
        _ when OnPerson(wait) => new StepHolder(StepHolders.Person),
        Vacant or VacantUntil => null,
        Reading or Rechecking or ReadingUntil or RecheckingUntil => new StepHolder(StepHolders.Agent, wait.Values.GetValueOrDefault("agent")),
        Answer => new StepHolder(StepHolders.Agent, wait.Values.GetValueOrDefault("session")),
        _ => new StepHolder(StepHolders.Agent),
    };

    /// <summary>The terminal's words for the wait: on whose answer, and for a person's wait the doors that answer it.</summary>
    public static string Said(WorkflowRunWait wait)
    {
        var session = wait.Values.GetValueOrDefault("session") is { Length: > 0 } named ? named : "<session>";
        var count = Read(wait, "count");
        return wait.Code switch
        {
            Passed => $"The risk policy passed {(wait.Values.GetValueOrDefault("held") == Disputed ? Plural(count, "disputed finding") : Plural(count, "commit") + " no other agent read")} "
                      + $"of the second opinion: {wait.Values.GetValueOrDefault("why")?.TrimEnd('.')}. Daoris's next look reaches the step.",
            Disputed or CommitsSince when OnRole(wait) =>
                $"Waits on the role's answer to {(wait.Code == Disputed ? Plural(count, "disputed finding") : Plural(count, "commit") + " no other agent read")} of the second "
                + $"opinion: `daoris session opinion show {session}` lists them. The risk policy asks the `{wait.Values[RoleKey]}` role, not you, for the answer: {wait.Values.GetValueOrDefault("why")}.",
            Disputed => $"Waits on your answer to {Plural(count, "disputed finding")} of the second opinion ({Whose(Read(wait, "first"), Read(wait, "raised"))}): "
                        + $"`daoris session opinion show {session}` lists them; your Accept answers them, `daoris session opinion anyway {session} \"…\"` "
                        + $"lands it over them, or `daoris session say {session} \"…\"` sends it back.",
            CommitsSince => $"Waits on your answer to {Plural(count, "commit")} no other agent read, its one pass and one recheck spent: your Accept "
                            + $"answers them, `daoris session opinion anyway {session} \"…\"` lands them, or `daoris session opinion ask {session}` asks again.",
            Reading or ReadingUntil => $"Waits on {Reviewer(wait)}'s reading of the work{Until(wait)}.",
            Rechecking or RecheckingUntil => $"Waits on {Reviewer(wait)}'s recheck of the commits made in answer{Until(wait)}; its findings go to you alone.",
            NotAsked => "Waits for the driver's next look to ask the second opinion: nothing holds it but that look.",
            NotAskedRecheck => "Waits for the driver's next look to ask the recheck of the commits made in answer.",
            NotAskedHeld => "Waits on the lift of the hold on its quest, which comes first: nothing is asked until you lift it.",
            NotAskedPress => "Waits for you to press Accept, which asks the second opinion of this conversation's work.",
            Unread => $"Whether a second opinion holds the work could not be read{WhyUnread(wait)}; the driver reads it again at its next look.",
            WaitsChain => wait.Values.GetValueOrDefault("quest") is { Length: > 0 } quest
                ? $"Waits for the chain's later step here, quest #{quest}: one opinion reads the whole work."
                : "Waits for the chain's later step here: one opinion reads the whole work.",
            Again =>
                $"Waits for the driver's next look to ask the second opinion again: {OpinionGateWords.UnavailableWhy(wait.Values.GetValueOrDefault("why"), null, null)}, "
                + $"ask {Read(wait, "count")} of {Read(wait, "bound")}. Nothing waits for you; past the last ask Daoris gives it up as the rule says.",
            Unavailable =>
                $"Waits on your answer: no second opinion could be had, {OpinionGateWords.UnavailableWhy(wait.Values.GetValueOrDefault("why"), null, null)}. The rule requires one: "
                + $"`daoris session opinion ask {session}` tries again, `daoris session opinion myself {session} \"…\"` records your own reading, or "
                + $"`daoris session opinion anyway {session} \"…\"` lets it land without one.",
            Vacant or VacantUntil =>
                $"Waits on the repository's supervisor: no second opinion can be had, {OpinionGateWords.UnavailableWhy(wait.Values.GetValueOrDefault("why"), wait.Until, null)}.",
            _ => session != "<session>"
                ? $"Waits on `{session}`'s answer to the second opinion."
                : "Waits on the work's session's answer to the second opinion.",
        };
    }

    /// <summary>What a dispute's count is made of: the first pass's musts the working session left unfixed, and the recheck's own.</summary>
    private static string Whose(int first, int raised) => $"{first} the working session left unfixed, {raised} the recheck's own";

    private static string Reviewer(WorkflowRunWait wait) => wait.Values.GetValueOrDefault("agent") is { Length: > 0 } agent ? agent : "another agent";

    private static string Until(WorkflowRunWait wait) =>
        wait.Until is { } until ? $", whose minutes run out at {until.ToUniversalTime().ToString("HH:mm", CultureInfo.InvariantCulture)} UTC" : "";

    private static string WhyUnread(WorkflowRunWait wait) => wait.Values.GetValueOrDefault("why") is { Length: > 0 } why ? $": {why}" : "";

    private static int Read(WorkflowRunWait wait, string name) =>
        int.TryParse(wait.Values.GetValueOrDefault(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var read) ? read : 0;

    private static string Number(int count) => count.ToString(CultureInfo.InvariantCulture);

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
