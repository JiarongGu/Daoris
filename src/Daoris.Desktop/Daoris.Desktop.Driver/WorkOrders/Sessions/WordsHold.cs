namespace Daoris.Driver;

/// <summary>
/// What holds the person's words on an ended record that goes on with them by itself once it lifts: the planner's own verdict on its
/// quest, since whatever holds a start holds a reopen, by a code the page words, with the planner's sentence beside it.
/// </summary>
/// <param name="Why">One of the codes below.</param>
/// <param name="Reason">The planner's sentence, or the spawn's hold's: the line where the page has none of its own.</param>
public sealed record WordsHold(string Why, string Reason)
{
    /// <summary>The work's pause: <i>Resume</i> moves it, and the row's line already names whose.</summary>
    public const string Paused = "paused";

    /// <summary>The person holds its repository.</summary>
    public const string Hold = "hold";

    /// <summary>The concurrency cap is spent: a reopen goes first once a running session here ends.</summary>
    public const string Cap = "cap";

    /// <summary>Its account is cooling: the words go at its reset.</summary>
    public const string Cooling = "cooling";

    /// <summary>A session is active in the tree or the repository it goes back into.</summary>
    public const string Busy = "busy";

    /// <summary>Anything else the planner or the spawn holds it by, said in its own sentence.</summary>
    public const string Waits = "waits";

    /// <summary>The repository the person holds, for <see cref="Hold"/>.</summary>
    public string? Repository { get; init; }

    /// <summary>When the cool-off ends, for <see cref="Cooling"/>.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>
    /// The account that resets at <see cref="Until"/>, for <see cref="Cooling"/>: the earliest of the line's accounts, which the
    /// page says the wait on in words. Null for the tool's own sign-in.
    /// </summary>
    public string? Account { get; init; }

    /// <summary>The name the person gave <see cref="Account"/>, where it has one.</summary>
    public string? AccountName { get; init; }

    /// <summary>Whose pause, for <see cref="Paused"/>.</summary>
    public PausedBy? PausedBy { get; init; }

    /// <summary>
    /// What holds the words, by the planner's verdict on the record's quest and the cool-off the look held its start on; null where
    /// the verdict starts it, which is going on.
    /// </summary>
    /// <param name="wait">
    /// The look's wait naming this quest, or null. A wait on signed-out accounts has no reset, so the words wait in its sentence, as
    /// any other hold's.
    /// </param>
    public static WordsHold? Of(Consideration verdict, AccountWait? wait) => verdict.Verdict switch
    {
        StartVerdict.Start => null,
        StartVerdict.Paused => new(Paused, verdict.Reason) { PausedBy = verdict.PausedBy },
        StartVerdict.Held => new(Hold, verdict.Reason) { Repository = verdict.Quest.To },
        StartVerdict.AtCapacity => new(Cap, verdict.Reason),
        StartVerdict.Blocked when wait?.Until is { } until => new(Cooling, verdict.Reason) { Until = until, Account = wait.Account, AccountName = wait.Name },
        StartVerdict.RepositoryBusy => new(Busy, verdict.Reason),
        _ => new(Waits, verdict.Reason),
    };

    /// <summary>
    /// What it says in the terminal's words, after <c>held: </c>: the same lines <c>sessions say</c> prints for words that wait, so
    /// the listing and the verb agree.
    /// </summary>
    public string Sentence => this switch
    {
        { Why: Paused, PausedBy: { } pause } => $"it goes on with this once you resume its work: {pause.Door}",
        { Why: Hold, Repository: { } repository } =>
            $"it goes on with this once {repository} is no longer held: daoris place resume {repository}",
        { Why: Cap } => "it goes on with this when a running session here ends.",
        _ => $"it waits: {Reason}",
    };
}
