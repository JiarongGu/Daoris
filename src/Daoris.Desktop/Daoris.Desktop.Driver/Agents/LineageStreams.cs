namespace Daoris.Driver;

/// <summary>
/// What a part's lineage is told of one process's native children (COWORKVIEW1a): bound to that process's session and token when it
/// is made, so no caller names either. Participants writes it; never throws.
/// </summary>
public interface ILineageWriter
{
    /// <summary>One raw line of the harness's stream, the pipe door's; the writer reads what it says of a subagent.</summary>
    void Stream(string line);

    /// <summary>A child the protocol door announced.</summary>
    void Spawned(LineageAnnouncement said);

    /// <summary>A child's end the wire said, with its word, null where it said none.</summary>
    void Ended(string agent, string? state);
}

/// <summary>
/// A child the protocol door announced: its id, its name and task as <see cref="LineageLabels"/> bound them, and its place, the
/// announcing child's id where a child announced it, <see cref="ByRoot"/> where the part's own agent did, neither where unknown.
/// </summary>
public sealed record LineageAnnouncement(string Agent, string? Name, string? Task)
{
    public string? Parent { get; init; }
    public bool ByRoot { get; init; }
}

/// <summary>Participants supplies one lineage writer per process; Agents hands it to the process's capture and streams.</summary>
public interface ILineageStreams
{
    ILineageWriter Writer(string home, string sessionId, string? proc = null);
}

/// <summary>The required writer operation, installed by Participants when the driver assembly loads.</summary>
public static class LineageStreams
{
    private static ILineageStreams? _current;

    public static ILineageStreams Current
    {
        get => _current ?? throw new InvalidOperationException("no lineage streams are installed: Participants installs them when the driver loads");
        set => _current = value;
    }
}

/// <summary>A child's label as its lineage keeps it (COWORKVIEW1a, D173): a name to 120 characters, a task's first line to 200.</summary>
public static class LineageLabels
{
    public const int NameLimit = 120;
    public const int TaskLimit = 200;

    public static string? Name(string? said) => Bounded(said?.Trim(), NameLimit);

    public static string? Task(string? said) => Bounded(said?.Split('\n', 2)[0].Trim(), TaskLimit);

    private static string? Bounded(string? text, int limit)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length <= limit) return text;
        // Never half a surrogate pair.
        return text[..(char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit)];
    }
}
