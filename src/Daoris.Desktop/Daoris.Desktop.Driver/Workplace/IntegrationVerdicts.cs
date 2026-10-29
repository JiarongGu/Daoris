namespace Daoris.Driver;

/// <summary>
/// The repository's declared runner (D192's <c>verification</c> runner, the integration design §5.9): the command the driver starts for each
/// call, in <paramref name="Cwd"/> where it names one (relative to the integration tree). A repository declaring none is judged by running
/// each gate's own command whole.
/// </summary>
internal sealed record VerdictRunner(string Run, string? Cwd = null);

/// <summary>A member of the candidate as the judgement runs its prefixes: its branch, and the tip its replay made on the candidate (the prefix that ends with it).</summary>
internal sealed record VerdictMember(string Branch, string Tip)
{
    /// <summary>The commit that holds this member alone on the base, where the caller made one: the judge then asks whether the failure needs the members before it.</summary>
    public string? Alone { get; init; }
}

/// <summary>
/// What the integration step asks of the verdict port (INTEGRATE1f part f2; the integration design §5.2 steps 6 and 7, §5.6): judge a
/// candidate's gates at its tip. The tree stands at the tip; nothing here reads the tree for a gate or a command, only the accepted blob
/// does (<see cref="Declaration"/>, with its id).
/// </summary>
/// <param name="Candidate">The candidate, whose tip is set and whose <see cref="IntegrationCandidate.Job"/> every process runs in.</param>
/// <param name="Tree">The integration tree, standing at the candidate's tip.</param>
/// <param name="Base">The line's commit the candidate was formed on.</param>
/// <param name="Declaration">The accepted declaration's text, from which alone the gates and the commands are read.</param>
/// <param name="DeclarationId">The accepted blob's id, which every runner answer must name.</param>
/// <param name="Home">The Daoris home whose admission the runs take their places in.</param>
/// <param name="Scratch">A folder for this judgement's files (the declaration beside the run, the answers, the failures a probe repeats, the logs): outside what a reset of the tree clears.</param>
/// <param name="Unconfirmed">The landing rule's <c>integrate.unconfirmed</c> word, already resolved by the caller (<c>split</c>, <c>hold</c> or <c>land</c>; any other reads <c>split</c>).</param>
/// <param name="Members">The candidate's members in order, the prefixes' tips.</param>
/// <param name="Drop">Signalled when the candidate is dropped: it ends the confirm run with its job and the judgement answers <c>dropped</c>.</param>
internal sealed record IntegrationVerdictAsk(
    IntegrationCandidate Candidate, string Tree, string Base, string Declaration, string DeclarationId, string Home, string Scratch, string Unconfirmed,
    IReadOnlyList<VerdictMember> Members, CancellationToken Drop)
{
    /// <summary>The repository's runner; null where it declares none.</summary>
    public VerdictRunner? Runner { get; init; }

    /// <summary>The gates to run, by the names the declaration writes; null for the runner's own plan (<c>reached</c>), or, with no runner, every declared gate.</summary>
    public IReadOnlyList<string>? Gates { get; init; }

    /// <summary>The variables' names the accepted declaration's <c>integration.env.pass</c> lists.</summary>
    public IReadOnlyList<string> Pass { get; init; } = [];
}

/// <summary>A failure as the verdict names it: its gate, the test it names (none for a gate that names none), its label, and its signature as the one string that compares across runs.</summary>
internal sealed record VerdictFailure(string Gate, string? Test, string Label, string Signature);

/// <summary>What a gate's run showed of its composition (the units it ran, null when whole, and the digest a probe repeats it by) and how it ended.</summary>
internal sealed record VerdictRun(string Mode, string Gate, string Verdict, string? Digest)
{
    public IReadOnlyList<string>? Units { get; init; }

    public int? Exit { get; init; }

    public string? Log { get; init; }
}

/// <summary>A gate the runner's plan kept rather than ran: the commit and tree its pass stands on, and how it stands (<c>exact</c>, <c>records</c>, <c>unreached</c> or <c>composed</c>).</summary>
internal sealed record VerdictKept(string Gate, string Why, string? Commit, string? Tree, string? Standing);

/// <summary>What the machine's admission showed of a confirm run: whether it held the whole machine and nothing else did, what doubts say otherwise, how long it waited, and the places.</summary>
internal sealed record VerdictAdmission(bool Conclusive, IReadOnlyList<string> Doubts, long? WaitedMs, IReadOnlyList<string> Ids);

/// <summary>A failure's reading by its gate's confirm run: <c>confirmed</c>, <c>unconfirmed</c>, <c>inconclusive</c> or <c>dropped</c>, with why, its admission evidence and the composition it was repeated as.</summary>
internal sealed record VerdictConfirm(string Result, string? Why)
{
    public VerdictAdmission? Admission { get; init; }

    public string? Digest { get; init; }

    public IReadOnlyList<string>? Units { get; init; }
}

/// <summary>One failure judged: how (<c>inherited</c>, <c>stop</c>, <c>pinned</c>, <c>unresolved</c>, <c>unconfirmed</c>, <c>inconclusive</c>, <c>dropped</c>), why, and the members it concerns.</summary>
internal sealed record VerdictJudgement(VerdictFailure Failure, string Outcome)
{
    public string? Why { get; init; }

    public string? Said { get; init; }

    /// <summary>The member a failure is pinned to, from 1.</summary>
    public int? Member { get; init; }

    public IReadOnlyList<int> With { get; init; } = [];

    public IReadOnlyList<int> Interacting { get; init; } = [];

    public VerdictConfirm? Confirm { get; init; }
}

/// <summary>A member handed back, with how it is (<c>pinned</c>, <c>pinned with</c> or <c>unresolved</c>), the failure it is handed back for and, where unresolved, why.</summary>
internal sealed record VerdictHandedBack(string Branch, string How, string Failure, IReadOnlyList<string> With, IReadOnlyList<string> Interacting, string? Why);

/// <summary>The words an <see cref="IntegrationVerdict"/>'s action is one of: the merge queue's candidate actions, and the two answers that are no judgement.</summary>
internal static class VerdictActions
{
    internal const string Green = "green";
    internal const string Land = "land";
    internal const string Hold = "hold";
    internal const string Split = "split";
    internal const string HandBack = "hand back";
    internal const string Stop = "stop";
    internal const string Dropped = "dropped";

    /// <summary>The runner's answer, or the gates' run, cannot be read as a verdict of the tip (the runner refused, wrote nothing, or named another commit, base or blob). Never a pass.</summary>
    internal const string Unknown = "unknown";

    /// <summary>The runner's own run holds its lock: the step waits it out.</summary>
    internal const string Busy = "busy";
}

/// <summary>
/// What the verdict port answers: the candidate's action and the facts the action rests on. Only <see cref="VerdictActions.Green"/> and
/// <see cref="VerdictActions.Land"/> promote, and <c>land</c> only where the owner chose it for an unconfirmed failure; a sighting is never a pass.
/// </summary>
internal sealed record IntegrationVerdict(string Action)
{
    public string? Why { get; init; }

    public string? Said { get; init; }

    /// <summary>What a held candidate waits for: <c>decide</c>.</summary>
    public string? On { get; init; }

    /// <summary>The failure that stops the line (<see cref="VerdictActions.Stop"/>).</summary>
    public VerdictFailure? Failure { get; init; }

    /// <summary>The gates whose only pass was the confirm run (<see cref="VerdictActions.Land"/>).</summary>
    public IReadOnlyList<string> PassedOnConfirm { get; init; } = [];

    /// <summary>The gates whose pass was a flake, read as a pass and said.</summary>
    public IReadOnlyList<string> Flaked { get; init; } = [];

    public IReadOnlyList<VerdictJudgement> Judgements { get; init; } = [];

    public IReadOnlyList<VerdictFailure> Inherited { get; init; } = [];

    public IReadOnlyList<VerdictFailure> Flakes { get; init; } = [];

    /// <summary>The failures that did not fail again on their gate's confirm run, to be filed on their unit with the tip and the confirm run's evidence.</summary>
    public IReadOnlyList<VerdictJudgement> Sightings { get; init; } = [];

    public IReadOnlyList<VerdictFailure> Inconclusive { get; init; } = [];

    public IReadOnlyList<VerdictHandedBack> HandedBack { get; init; } = [];

    /// <summary>The members kept to be judged again, or run one at a time under <see cref="VerdictActions.Split"/>, by branch.</summary>
    public IReadOnlyList<string> Retain { get; init; } = [];

    /// <summary>Every run the gates made, each with the digest of its composition.</summary>
    public IReadOnlyList<VerdictRun> Runs { get; init; } = [];

    /// <summary>The gates the runner's plan kept, each on the standing it states: what a promotion's record shows beside the gates that ran.</summary>
    public IReadOnlyList<VerdictKept> Kept { get; init; } = [];
}

/// <summary>
/// What the workplace asks of the review when it integrates a candidate (INTEGRATE1f part f2; the integration design §5.6, §6.1): the gates'
/// run on the candidate's tip and the judgement of what failed. The review keeps the runs, the admission, the job and the judge, and
/// implements this, installed when the driver loads (<c>Review/Integration/IntegrationVerdictWiring.cs</c>). Workplace declares it in lower
/// vocabulary only, so it names no type of the review.
/// </summary>
internal interface IIntegrationVerdicts
{
    /// <summary>
    /// Run the candidate's gates at its tip and judge them: one job for every process, closed before any place in the machine's admission
    /// is let go; the runner called with its contract; the whole admission taken for the confirm run. Never throws for a gate that fails,
    /// a runner that refuses or an answer that does not read: those are answers (<see cref="VerdictActions.Unknown"/>, never a pass).
    /// </summary>
    Task<IntegrationVerdict> JudgeAsync(IntegrationVerdictAsk ask);
}

internal static class IntegrationVerdicts
{
    private static IIntegrationVerdicts? _current;

    internal static IIntegrationVerdicts Current
    {
        get => _current ?? throw new InvalidOperationException("no integration verdicts are installed: Review installs them when the driver loads");
        set => _current = value;
    }
}
