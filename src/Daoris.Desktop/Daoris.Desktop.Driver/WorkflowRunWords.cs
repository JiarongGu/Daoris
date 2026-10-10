using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a run says in a terminal (WORKFLOW1c2; the workflow design §5.2's <i>Said</i>, §7, D50): each step's title, its state's
/// word and what that state says, composed from the step's own facts as <see cref="WorkflowRuns.Derive"/> set them, so it cannot
/// drift from what the reader read. The page's <c>workflow/runSaid.ts</c> says the same in its catalogue's words; these are its
/// English, kept beside it by <c>WorkflowRunCommandTests</c>.
/// </summary>
/// <remarks>
/// A name from a record (an environment, a branch, a plugin, a commit) is set in backticks; a person's words and a session's are
/// shown as written. A state or a detail these words do not hold is said as recorded, never dropped.
/// </remarks>
public static class WorkflowRunWords
{
    /// <summary>The details each kind words, by kind; the second opinion's are its gate's own states (XAGENT1f).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Details { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [WorkflowKinds.Work] =
        [
            WorkflowRunDetails.Queued, WorkflowRunDetails.Agent, WorkflowRunDetails.Answered, WorkflowRunDetails.Park,
            WorkflowRunDetails.GoAhead, WorkflowRunDetails.Held, WorkflowRunDetails.Awaits, WorkflowRunDetails.Unclosed,
            WorkflowRunDetails.Failed, WorkflowRunDetails.Stopped, WorkflowRunDetails.Declined, WorkflowRunDetails.Finished,
        ],
        [WorkflowKinds.Opinion] =
        [
            OpinionGateStates.None, OpinionGateStates.WaitsChain, OpinionGateStates.NotAsked, OpinionGateStates.Reading,
            OpinionGateStates.ReadAgain, OpinionGateStates.WithSession, OpinionGateStates.Disputed, OpinionGateStates.CommitsSince,
            OpinionGateStates.Unavailable, OpinionGateStates.Settled, OpinionGateStates.Anyway, OpinionGateStates.Myself,
            OpinionGateStates.Answered, WorkflowRunDetails.Landed, OpinionGateStates.Unread, OpinionGateStates.CannotStart,
        ],
        [WorkflowKinds.Look] =
        [
            WorkflowRunDetails.NotShown, WorkflowRunDetails.BeingSetUp, WorkflowRunDetails.Shown, WorkflowRunDetails.NotYet,
            WorkflowRunDetails.NotHeld, WorkflowRunDetails.Reviewed, WorkflowRunDetails.Skip, WorkflowRunDetails.Off,
            WorkflowRunDetails.Unread,
        ],
        [WorkflowKinds.Landing] =
        [
            WorkflowRunDetails.Accept, WorkflowRunDetails.Automatic, WorkflowRunDetails.Branch, WorkflowRunDetails.Merge,
            WorkflowRunDetails.Already, WorkflowRunDetails.Nothing, WorkflowRunDetails.Refused, WorkflowRunDetails.Gone,
            WorkflowRunDetails.Elsewhere,
        ],
        [WorkflowKinds.PullRequest] =
        [
            WorkflowRunDetails.Merging, WorkflowRunDetails.Merged, WorkflowRunDetails.Abandoned, WorkflowRunDetails.NotPushed,
            WorkflowRunDetails.AskFailed, WorkflowRunDetails.NoBranch, WorkflowRunDetails.Nothing,
        ],
    };

    /// <summary>A step's title, as <c>daoris driver workflow show</c> names it.</summary>
    public static string Title(WorkflowStep step) => step.Kind switch
    {
        WorkflowKinds.Work => "The work",
        WorkflowKinds.Opinion => "Second opinion",
        WorkflowKinds.Look => "Your look",
        WorkflowKinds.Landing => "The landing",
        WorkflowKinds.PullRequest => "The pull request",
        _ => step.Id,
    };

    /// <summary>A state's word; one these words do not hold, as the driver spells it.</summary>
    public static string State(string state) => state switch
    {
        WorkflowRunStates.NotReached => "not reached",
        WorkflowRunStates.Working => "working",
        WorkflowRunStates.WaitingOnYou => "waiting on you",
        WorkflowRunStates.WaitingOnAgent => "waiting on an agent",
        WorkflowRunStates.NotKnown => "not known",
        WorkflowRunStates.CannotStart => "cannot start",
        WorkflowRunStates.Done => "done",
        WorkflowRunStates.Skipped => "skipped",
        WorkflowRunStates.Failed => "failed",
        WorkflowRunStates.Stopped => "stopped",
        _ => state,
    };

    private static readonly HashSet<string> States =
    [
        WorkflowRunStates.NotReached, WorkflowRunStates.Working, WorkflowRunStates.WaitingOnYou, WorkflowRunStates.WaitingOnAgent,
        WorkflowRunStates.NotKnown, WorkflowRunStates.CannotStart, WorkflowRunStates.Done, WorkflowRunStates.Skipped,
        WorkflowRunStates.Failed, WorkflowRunStates.Stopped,
    ];

    /// <summary>
    /// What a step's state says (design §5.2's <i>Said</i>), or null where the state's word says it all (not reached).
    /// </summary>
    public static string? Said(WorkflowRunStep step)
    {
        var kind = step.Step.Kind;
        if (step.State == WorkflowRunStates.NotReached) return null;
        if (step.State == WorkflowRunStates.CannotStart && step.Words is { Length: > 0 } cannotStartWords) return $"Cannot start: {cannotStartWords}";
        if (!States.Contains(step.State) || !Details.TryGetValue(kind, out var details) || !details.Contains(step.Detail))
        {
            return $"Shown as recorded: {step.State}, {(step.Detail.Length == 0 ? "—" : step.Detail)}.";
        }

        var quest = step.Quest ?? "";
        var environment = step.Environment ?? "";
        var plugin = step.Plugin ?? "";
        var count = step.Count ?? 0;
        return (kind, step.Detail) switch
        {
            (WorkflowKinds.Work, WorkflowRunDetails.Queued) => $"Waits for the driver to start quest #{quest}.",
            (WorkflowKinds.Work, WorkflowRunDetails.Agent) =>
                step.Agent is { } agent ? $"Being worked on by `{agent}`." : "Being worked on by an agent.",
            (WorkflowKinds.Work, WorkflowRunDetails.Answered) =>
                "You answered its question; the same session goes on at the driver's next look.",
            (WorkflowKinds.Work, WorkflowRunDetails.Park) => "Its session asks you something, and waits for your answer.",
            (WorkflowKinds.Work, WorkflowRunDetails.GoAhead) => $"Waits for your go-ahead #{step.GoAhead}: {step.Words}",
            (WorkflowKinds.Work, WorkflowRunDetails.Held) => step.Code switch
            {
                "evidence-unread" => "Its done's evidence could not be read, and it waits for your yes.",
                "evidence-missing" => "Its done lacks the evidence you required, and waits for your yes.",
                _ => "Its done departs from your words, and waits for your yes.",
            },
            (WorkflowKinds.Work, WorkflowRunDetails.Awaits) => $"Waits on the answer to quest #{quest}, asked of another repository.",
            (WorkflowKinds.Work, WorkflowRunDetails.Unclosed) =>
                $"Its session finished, and quest #{quest} stays taken: your done closes it.",
            (WorkflowKinds.Work, WorkflowRunDetails.Failed) => "Its last session failed.",
            (WorkflowKinds.Work, WorkflowRunDetails.Stopped) => "You stopped its last session.",
            (WorkflowKinds.Work, WorkflowRunDetails.Declined) => $"Quest #{quest} was declined.",
            (WorkflowKinds.Work, WorkflowRunDetails.Finished) =>
                count == 1 ? "The agent finished: 1 quest here is done." : $"The agent finished: {count} quests here are done.",

            (WorkflowKinds.Opinion, OpinionGateStates.None) => "No second opinion is asked before this work lands.",
            (WorkflowKinds.Opinion, OpinionGateStates.WaitsChain) =>
                $"Waits for the chain's later step here, quest #{quest}: one opinion reads the whole work.",
            (WorkflowKinds.Opinion, OpinionGateStates.NotAsked) => "Asked at the driver's next look.",
            (WorkflowKinds.Opinion, OpinionGateStates.Reading) =>
                step.Agent is { } reader ? $"Being read by {reader}." : "Being read by another agent.",
            (WorkflowKinds.Opinion, OpinionGateStates.ReadAgain) =>
                step.Agent is { } again ? $"Read again by {again}: the commits made in answer." : "Being read by another agent.",
            (WorkflowKinds.Opinion, OpinionGateStates.WithSession) => count == 1
                ? "The working session is answering its 1 finding."
                : $"The working session is answering its {count} findings.",
            (WorkflowKinds.Opinion, OpinionGateStates.Disputed) => count == 1
                ? "1 finding is disputed: a must the working session did not fix and no recheck withdrew."
                : $"{count} findings are disputed: musts the working session did not fix and no recheck withdrew.",
            (WorkflowKinds.Opinion, OpinionGateStates.CommitsSince) => count == 1
                ? "1 commit since was not read by another agent, and one pass and one recheck are spent."
                : $"{count} commits since were not read by another agent, and one pass and one recheck are spent.",
            // Required, it waits on the person; not, it is passed over and holds nothing (the second-agent design §8.4).
            (WorkflowKinds.Opinion, OpinionGateStates.Unavailable) => step.State == WorkflowRunStates.WaitingOnYou
                ? $"No second opinion could be had: {Unavailable(step.Code)}. The rule requires one, so the work waits for you."
                : $"No second opinion could be had: {Unavailable(step.Code)}. The rule does not require one, so nothing waits for it.",
            (WorkflowKinds.Opinion, OpinionGateStates.Settled) => step.Agent is { } settled
                ? $"Settled: read by {settled}, with nothing left disputed."
                : "Settled: another agent read it, with nothing left disputed.",
            (WorkflowKinds.Opinion, OpinionGateStates.Anyway) => "You went on without a settled second opinion.",
            (WorkflowKinds.Opinion, OpinionGateStates.Myself) => "You looked at it yourself in place of another agent's reading.",
            (WorkflowKinds.Opinion, OpinionGateStates.Answered) => step.Code == ReviewVerdicts.Reviewed
                ? "Your Reviewed answered the second opinion."
                : "Your Accept answered the second opinion.",
            (WorkflowKinds.Opinion, WorkflowRunDetails.Landed) => "It let the work go when the work landed.",
            (WorkflowKinds.Opinion, OpinionGateStates.Unread) => step.Words is { } why
                ? $"Whether a second opinion holds it could not be read: {why}"
                : "Whether a second opinion holds it could not be read.",
            // WORKFLOW1f (the workflow design §3.7): a named workflow's opinion step naming reviewers its repository does not declare.
            (WorkflowKinds.Opinion, OpinionGateStates.CannotStart) => step.Words is { } cannot
                ? $"It cannot start: {cannot}"
                : "It cannot start: its workflow names reviewers its repository does not declare.",

            (WorkflowKinds.Look, WorkflowRunDetails.NotShown) => $"Waits for your look in `{environment}`: nothing shows it there yet.",
            (WorkflowKinds.Look, WorkflowRunDetails.BeingSetUp) => $"Being set up in `{environment}` by its set-up step, quest #{quest}.",
            (WorkflowKinds.Look, WorkflowRunDetails.Shown) => step.Commit is { } shown
                ? $"Waits for your look in `{environment}`: shown at `{Short(shown)}`."
                : $"Waits for your look in `{environment}`.",
            (WorkflowKinds.Look, WorkflowRunDetails.NotYet) => "You said not yet: the set-up step's session is answering your words.",
            (WorkflowKinds.Look, WorkflowRunDetails.NotHeld) =>
                "What you reviewed does not hold the newest commits: it waits to be shown again.",
            (WorkflowKinds.Look, WorkflowRunDetails.Reviewed) => step.Commit is { } reviewed
                ? $"You reviewed it in `{environment}` at `{Short(reviewed)}`."
                : $"You reviewed it in `{environment}`.",
            (WorkflowKinds.Look, WorkflowRunDetails.Skip) => "Skipped for this work, by you.",
            (WorkflowKinds.Look, WorkflowRunDetails.Off) => step.Code == ReviewLevels.Chain
                ? "Not asked for this work: its chain's choice turns the review off."
                : "Not asked for this work: its ask's choice turns the review off.",
            (WorkflowKinds.Look, WorkflowRunDetails.Unread) => step.Words is { } unread
                ? $"Whether it waits for your look could not be read: {unread}"
                : "Whether it waits for your look could not be read.",

            (WorkflowKinds.Landing, WorkflowRunDetails.Accept) => "Waits for you to accept it.",
            (WorkflowKinds.Landing, WorkflowRunDetails.Automatic) => "Lands automatically at the driver's next look.",
            (WorkflowKinds.Landing, WorkflowRunDetails.Branch) => step.Code == AcceptedBy.Auto
                ? $"Landed on `{step.Branch}`, accepted automatically."
                : $"Landed on `{step.Branch}`, accepted by you.",
            (WorkflowKinds.Landing, WorkflowRunDetails.Merge) => "Landed into its line.",
            (WorkflowKinds.Landing, WorkflowRunDetails.Already) => "Its work reached the line another way.",
            (WorkflowKinds.Landing, WorkflowRunDetails.Nothing) => "Nothing to land: its work made no commits.",
            (WorkflowKinds.Landing, WorkflowRunDetails.Refused) => $"Cannot land automatically: {Refused(step.Code)}.",
            (WorkflowKinds.Landing, WorkflowRunDetails.Gone) => "Its tree was discarded before it landed.",
            (WorkflowKinds.Landing, WorkflowRunDetails.Elsewhere) => "It ran on another machine, which keeps its landing.",

            (WorkflowKinds.PullRequest, WorkflowRunDetails.Merging) => step.Code switch
            {
                PullRequestStates.Open => "Open: waits for you to merge it on the platform.",
                null => $"Opened by `{plugin}`: waits for you to merge it on the platform.",
                _ => $"Waits for you to merge it on the platform; `{plugin}` last answered no state for it.",
            },
            (WorkflowKinds.PullRequest, WorkflowRunDetails.Merged) => "Merged on the platform.",
            (WorkflowKinds.PullRequest, WorkflowRunDetails.Abandoned) => "Abandoned on the platform.",
            (WorkflowKinds.PullRequest, WorkflowRunDetails.NotPushed) => $"`{plugin}` did not push the branch, so no pull request opened.",
            (WorkflowKinds.PullRequest, WorkflowRunDetails.AskFailed) => $"Could not read `{plugin}`: {step.Code}.",
            (WorkflowKinds.PullRequest, WorkflowRunDetails.NoBranch) => "Not opened: the work landed into its line.",
            (WorkflowKinds.PullRequest, WorkflowRunDetails.Nothing) => "Not opened: there was nothing to land.",
            _ => $"Shown as recorded: {step.State}, {step.Detail}.",
        };
    }

    /// <summary>Why a step Current does not draw is in this run (design §4.6): a look the work's own choice added.</summary>
    public static string? Added(WorkflowRunStep step) => step.Added switch
    {
        null => null,
        ReviewLevels.Chain => "A look added for this work by its chain's choice.",
        ReviewLevels.Ask => "A look added for this work by its ask's choice.",
        ReviewLevels.SetUpStep => "A look added for this work: a set-up step shows it.",
        _ => "A look added for this work.",
    };

    /// <summary>
    /// The runs as the terminal prints them (design §7): each run's repository, its workflow and version, its quests and its ask;
    /// then one line per step, marked <c>✓</c> where settled, <c>●</c> where the run stands and <c>○</c> otherwise, its title, its
    /// state's word and what it says, with <i>this session</i> where <paramref name="session"/> is the step's own; and under a step,
    /// the person's words it keeps, why it is in this run, the terminal's presses where the second opinion holds the work on the
    /// person, and a pull request's address.
    /// </summary>
    public static string Say(IReadOnlyList<WorkflowRun> runs, string? session)
    {
        var said = new StringBuilder();
        for (var index = 0; index < runs.Count; index++)
        {
            var run = runs[index];
            if (index > 0) said.Append('\n');
            var quests = run.Quests.Count == 1 ? $"quest #{run.Quests[0]}" : $"quests {string.Join(", ", run.Quests.Select(id => $"#{id}"))}";
            var workflow = run.Process?.Named == true ? $"{run.Process.Name} workflow"
                : run.Problem is not null ? "Workflow unread" : $"Current workflow, version {run.Current.Version}";
            said.Append($"{run.Repository} · {workflow} · {quests}");
            if (run.Ask is { } ask) said.Append($" · ask #{ask}");
            said.Append('\n');
            if (run.Problem is { } problem) said.Append($"  Cannot read this run: {problem}\n");
            if (run.WorkflowGate is { LetsGo: false } held) said.Append($"  {held.Says}\n");

            foreach (var step in run.Steps)
            {
                var mark = step.Step.Id == run.At ? "●" : WorkflowRunStates.Settled(step.State) ? "✓" : "○";
                var line = $"  {mark} {Title(step.Step),-16}  {State(step.State),-19}  {Said(step)}".TrimEnd();
                if (session is not null && string.Equals(step.Session, session.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    line += " · this session";
                }

                said.Append(line).Append('\n');
                foreach (var under in Under(step)) said.Append(Indent).Append(under).Append('\n');
            }
        }

        return said.ToString();
    }

    /// <summary>Where a step's words start, so a line under it starts there too: the mark, the title's 16 and the state's 19.</summary>
    private static readonly string Indent = new(' ', 2 + 2 + 16 + 2 + 19 + 2);

    /// <summary>The lines under a step: the person's words, why it is here, the opinion's presses in this terminal, the pull request.</summary>
    private static IEnumerable<string> Under(WorkflowRunStep step)
    {
        var kind = step.Step.Kind;
        // The person's words a step keeps, shown as written: a skip's and a not yet's (D154).
        if (kind == WorkflowKinds.Look && step.Detail is WorkflowRunDetails.Skip or WorkflowRunDetails.NotYet && step.Words is { Length: > 0 } words)
        {
            yield return $"“{words}”";
        }

        if (Added(step) is { } added) yield return added;

        // The second opinion's gate holding the work on the person (XAGENT1f): its presses are this terminal's own.
        if (kind == WorkflowKinds.Opinion && step.State == WorkflowRunStates.WaitingOnYou && step.Session is { } held)
        {
            yield return $"`daoris-driver opinion show {held}` lists what is unsettled; `daoris-driver opinion anyway {held} \"…\"` "
                + "goes on without it.";
        }

        if (kind == WorkflowKinds.PullRequest && step.PullRequest is { Length: > 0 } address) yield return address;
    }

    /// <summary>A commit as a person reads it: its first eight characters, as the driver says one.</summary>
    private static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;

    /// <summary>Why no second opinion could be had, by the code the gate kept (<c>ReviewerUnavailable</c>, a failed pass's).</summary>
    private static string Unavailable(string? code) => code switch
    {
        "no-reviewer" => "no listed reviewer of another maker is installed",
        "cooling" => "every listed reviewer of another maker is cooling",
        "signed-out" => "no listed reviewer of another maker has an account signed in",
        "not-independent" => "no listed reviewer is another maker's agent",
        "ended" => "its reviewer ended without saying one",
        "out-of-time" => "its reviewer ran out of its minutes before it said one",
        _ => "no listed reviewer of another maker could read it",
    };

    /// <summary>What stood in an automatic landing's way, by its last try's code (LAND2b).</summary>
    private static string Refused(string? code) => code switch
    {
        AutoLandingCode.Uncommitted => "its tree holds uncommitted work",
        AutoLandingCode.Exists => "the branch it would make already stands",
        AutoLandingCode.Completed => "its branch's pull request was merged, so bring the repository up to date and accept it",
        _ => "the landing was refused",
    };
}
