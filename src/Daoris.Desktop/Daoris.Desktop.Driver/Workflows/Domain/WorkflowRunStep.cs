namespace Daoris.Driver;

/// <summary>
/// One step of a run: the chosen workflow's step, where it stands, the row of that state's table,
/// and the facts its words and its door are made of, each the record's own.
/// </summary>
/// <param name="State">One of <see cref="WorkflowRunStates"/>.</param>
/// <param name="Detail">One of <see cref="WorkflowRunDetails"/>, read with the step's kind.</param>
public sealed record WorkflowRunStep(WorkflowStep Step, string State, string Detail)
{
    /// <summary>
    /// Why a step outside the chosen graph is in this run: the review's level that asked for a look for this work (<see cref="ReviewLevels"/>),
    /// or <c>opinion</c> for a reading nothing declared. Null for the graph's own steps.
    /// </summary>
    public string? Added { get; init; }

    /// <summary>The session its record is: the work's latest, the reviewer's, the set-up step's, the one landed or to land.</summary>
    public string? Session { get; init; }

    /// <summary>The quest it stands at: the work's current quest, the set-up step, the quest its taker waits on.</summary>
    public string? Quest { get; init; }

    /// <summary>Who acts, as the record names it: a session's adapter, or a reviewer as the person reads it.</summary>
    public string? Agent { get; init; }

    /// <summary>When it came to stand where it does, where the record kept a moment.</summary>
    public DateTimeOffset? At { get; init; }

    public string? Environment { get; init; }

    /// <summary>The commit a verdict or a set-up names.</summary>
    public string? Commit { get; init; }

    public string? Branch { get; init; }

    /// <summary>The pull request's address, as its plugin answered it.</summary>
    public string? PullRequest { get; init; }

    public string? Plugin { get; init; }

    /// <summary>How many: quests done, findings unanswered or given.</summary>
    public int? Count { get; init; }

    /// <summary>Of how many.</summary>
    public int? Of { get; init; }

    /// <summary>A go-ahead's number on the run's ask.</summary>
    public int? GoAhead { get; init; }

    /// <summary>Words the record kept: the person's (a skip's, a <i>not yet</i>'s) or a session's note, shown as written.</summary>
    public string? Words { get; init; }

    /// <summary>A code the record kept: a quest's hold, a landing try's, a pass's failure, an ask's, who accepted.</summary>
    public string? Code { get; init; }

    /// <summary>
    /// On a run bound to a graph version, the node's own kind (<c>claimed</c>, <c>handed</c>, a plugin's
    /// <c>&lt;plugin&gt;/&lt;node&gt;</c>), its detail one of <see cref="WorkflowGraphDetails"/>, read from the run's record; null on a steps run
    /// and on a look the work added beside a graph.
    /// </summary>
    public string? Node { get; init; }

    /// <summary>
    /// What a graph run's node observes, one of <see cref="WorkflowGraphSubjects"/>: its
    /// <see cref="Quest"/>, or its <see cref="Branch"/> at <see cref="Commit"/>, or its landing there; null for the run itself.
    /// </summary>
    public string? On { get; init; }

    /// <summary>
    /// What the person may say of a graph run's node now (<see cref="RunNodeWords.Acts"/>): its <c>skip</c> and its <c>stop</c>,
    /// as the one table takes them where it stands; null where it offers neither, on a steps run's step, and where its run ended or its record
    /// does not read.
    /// </summary>
    public IReadOnlyList<string>? Acts { get; init; }

    /// <summary>The person's word on a graph run's node, kept by its door and not applied yet; null where none waits.</summary>
    public NodeWord? Asked { get; init; }

    /// <summary>The gate a failed check named, read from the attempt's result by the reader; null on any other step.</summary>
    public string? Gate { get; init; }

    /// <summary>A check's newest attempt held unknown for the person, or settled by their stop; null on any other step.</summary>
    public WorkflowRunHeld? Held { get; init; }

    /// <summary>
    /// The requests the work's sessions made, on the work step only: the quests they published and the helpers they
    /// asked. Null where there are none.
    /// </summary>
    public IReadOnlyList<WorkflowRunRequest>? Requests { get; init; }

    /// <summary>The unit's integration state, on a graph run's <c>integrate</c> node only, as its record says it.</summary>
    public IntegrationHead? Integration { get; init; }

    /// <summary>What a graph run's node waits on while it stands active or ready, one of its kind's declared waits; null where it waits on none.</summary>
    public WorkflowRunWait? Wait { get; init; }
}
