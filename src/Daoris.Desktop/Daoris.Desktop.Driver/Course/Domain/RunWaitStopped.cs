namespace Daoris.Driver;

/// <summary>What the person's stop of a session awaiting its run's step came to: the steps whose stop is kept, or why none is.</summary>
/// <param name="Run">The run the session waits on, as its record's wait names it.</param>
/// <param name="Steps">The steps whose stop is kept, in the graph's order, for Daoris's next look to apply.</param>
public sealed record RunWaitStopped(string Run, IReadOnlyList<string> Steps)
{
    /// <summary>Why nothing is stopped: the run's node door's code, or <see cref="NothingStands"/>; null where a stop is kept.</summary>
    public string? Refused { get; init; }

    /// <summary>The step a refusal is about; null where the run names none.</summary>
    public string? Node { get; init; }

    /// <summary>Why nothing is stopped, in the node door's own sentence; null where a stop is kept.</summary>
    public string? Why { get; init; }

    public bool Done => Refused is null;

    /// <summary>The run stands at no step a stop reaches: none live, none failed.</summary>
    public const string NothingStands = "nothing-stands";

    /// <summary>The sentence the doors say for this stop of session <paramref name="session"/>: what is kept, or why nothing is.</summary>
    public string Said(string session) => Done
        ? $"sessions: {session} waits on run {Run}: your stop of it is kept at {string.Join(", ", Steps.Select(step => $"`{step}`"))}, "
          + "and Daoris's next look ends the run and the session with it."
        : $"sessions: {session} waits on run {Run}, and nothing of it was stopped: {Why ?? Refused}";
}
