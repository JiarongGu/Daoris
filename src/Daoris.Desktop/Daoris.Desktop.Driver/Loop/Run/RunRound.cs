namespace Daoris.Driver;

/// <summary>
/// What one run of a driven session hands its stages: the start as the look planned it, what the look read beside it, and the
/// <see cref="RunState"/> each stage reads from the stages before it and writes for the ones after.
/// </summary>
/// <param name="registry">The registry as the look's snapshot answered it: what the session may reach across.</param>
/// <param name="untrusted">The trees this look found their harness does not trust; the lock guards its adds.</param>
/// <param name="onOpened">Called once the session's record is open.</param>
/// <param name="movedOffModel">The agent and model the first run of this start is refused for.</param>
/// <param name="movedFrom">The agent the first run ran on before its sign-in moved it off.</param>
/// <param name="movedOff">The account the first run of this start is refused for its sign-in; a walk that gives it again holds.</param>
/// <param name="fresh">The tree this start opens, let go on each way out that starts nothing.</param>
internal sealed class RunRound(
    Consideration start, IReadOnlyList<RepoView> registry, List<TrustHold> untrusted, Action onOpened, string? movedOff,
    (string Agent, string Model)? movedOffModel, string? movedFrom, FreshTree fresh, RunState state)
{
    /// <summary>The agent and model the first run of this start is refused for, which a walk that gives them again holds.</summary>
    public (string Agent, string Model)? MovedOffModel { get; } = movedOffModel;

    /// <summary>The agent the first run of this start ran on before its sign-in moved it off.</summary>
    public string? MovedFrom { get; } = movedFrom;

    /// <summary>Called once the session's record is open, and never for a start that held or is refused.</summary>
    public Action OnOpened { get; } = onOpened;

    /// <summary>The trees this look found their harness does not trust, which a start held for it adds to.</summary>
    public List<TrustHold> Untrusted { get; } = untrusted;

    public FreshTree Fresh { get; } = fresh;

    public Consideration Start { get; } = start;

    public QuestView Quest => Start.Quest;

    public IReadOnlyList<RepoView> Registry { get; } = registry;

    public string? MovedOff { get; } = movedOff;

    public RunState State { get; } = state;

    /// <summary>The run's end where the start is held, naming the agent it asked as it stands now, which the look's report of a wait names.</summary>
    public StartRun Hold(string why) => new($"held  #{Quest.Id} → {Quest.To}: {why}", false, null, why) { Agent = State.Agent };
}
