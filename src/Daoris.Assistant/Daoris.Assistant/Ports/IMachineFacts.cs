namespace Daoris.Assistant.Ports;

/// <summary>
/// The room's readings of the machine, by topic: what each topic's judge last answered, or that it has not answered yet, and a
/// signal once a reading moves. The facts carry names and states only, so the room holds no type of the code that judged them.
/// </summary>
public interface IMachineFacts
{
    /// <summary>
    /// The reading of <paramref name="topic"/>, one of <see cref="FactTopics.All"/>: <see cref="FactsState.NotReadYet"/> with no facts
    /// until its judge answers. A topic the room does not read is refused (<see cref="ArgumentException"/>).
    /// </summary>
    FactsReading Read(string topic);

    /// <summary>Raised with a topic's name once its reading moved.</summary>
    event Action<string>? Changed;
}

/// <summary>One topic's reading: whether its judge has answered, and the facts it answered, in its order.</summary>
public sealed record FactsReading(string Topic, FactsState State, IReadOnlyList<MachineFact> Facts);

/// <summary>One fact of a reading: what it is about, by name, its state in the judge's words, and its parts where it has some.</summary>
public sealed record MachineFact(string Name, string State)
{
    public IReadOnlyList<MachineFact> Parts { get; init; } = [];
}

/// <summary>Whether a topic's judge has answered.</summary>
public enum FactsState
{
    NotReadYet,
    Read,
}

/// <summary>The topics the room reads, by their names.</summary>
public static class FactTopics
{
    public const string Machine = "machine";
    public const string Sessions = "sessions";
    public const string Trees = "trees";
    public const string Work = "work";
    public const string History = "history";
    public const string Sweep = "sweep";
    public const string Exchanges = "exchanges";
    public const string Roles = "roles";
    public const string Rules = "rules";
    public const string Plugins = "plugins";
    public const string Workflows = "workflows";
    public const string Browser = "browser";
    public const string Agents = "agents";

    public static readonly IReadOnlyList<string> All =
        [Machine, Sessions, Trees, Work, History, Sweep, Exchanges, Roles, Rules, Plugins, Workflows, Browser, Agents];
}
