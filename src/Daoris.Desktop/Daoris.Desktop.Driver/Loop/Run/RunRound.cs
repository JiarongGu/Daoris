namespace Daoris.Driver;

/// <summary>
/// What one run of a driven session hands its stages: the start as the look planned it, what the look read beside it, and the
/// <see cref="RunState"/> each stage reads from the stages before it and writes for the ones after.
/// </summary>
/// <param name="registry">The registry as the look's snapshot answered it: what the session may reach across.</param>
/// <param name="movedOff">The account the first run of this start was refused for its sign-in; a walk that gives it again holds.</param>
/// <param name="fresh">The tree this start opens, let go on each way out that starts nothing.</param>
internal sealed class RunRound(Consideration start, IReadOnlyList<RepoView> registry, string? movedOff, FreshTree fresh, RunState state)
{
    public FreshTree Fresh { get; } = fresh;

    public Consideration Start { get; } = start;

    public QuestView Quest => Start.Quest;

    public IReadOnlyList<RepoView> Registry { get; } = registry;

    public string? MovedOff { get; } = movedOff;

    public RunState State { get; } = state;

    /// <summary>The run's end where the start is held, naming the agent it asked as it stands now, which the look's report of a wait names.</summary>
    public StartRun Hold(string why) => new($"held  #{Quest.Id} → {Quest.To}: {why}", false, null, why) { Agent = State.Agent };
}
