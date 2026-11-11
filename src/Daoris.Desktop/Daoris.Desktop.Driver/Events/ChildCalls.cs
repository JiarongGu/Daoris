using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The tool calls of a child a session ran beside itself, kept by its stream in the session's own folder, never in its record: so the
/// second check reads a delegated child's acts by the table it reads the session's with, and they go with the session's files.
/// </summary>
public static class ChildCalls
{
    private static readonly object Gate = new();

    /// <summary>Where a child's calls are kept: <c>sessions/&lt;id&gt;/streams/&lt;stream&gt;.jsonl</c>; null for a stream no file can be named by.</summary>
    public static string? PathOf(string home, string session, string stream) =>
        SessionEventText.IsId(session) && stream.Length is > 0 and <= 120 && stream.All(each => char.IsAsciiLetterOrDigit(each) || each is '-' or '_' or '.')
            && stream.Trim('.').Length > 0
            ? DaorisHome.Entry(home, HomeEntry.Sessions, session, "streams", stream + ".jsonl")
            : null;

    /// <summary>One of the child's tool events as its door mapped it.</summary>
    public static void Keep(string home, string session, string stream, SessionEvent e)
    {
        if (e.Kind != SessionEventKind.Tool || PathOf(home, session, stream) is not { } path) return;
        var line = JsonSerializer.Serialize(SessionEventText.Bound(e), SessionEventText.Json) + "\n";
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, line);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A call not kept leaves its child said as delegated, not read.
        }
    }

    /// <summary>The child's tool events in order; null where none is kept, which its reading then says.</summary>
    public static IReadOnlyList<SessionEvent>? Read(string home, string session, string stream)
    {
        if (PathOf(home, session, stream) is not { } path || !File.Exists(path)) return null;
        string[] lines;
        try
        {
            lock (Gate) lines = File.ReadAllText(path).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var events = new List<SessionEvent>();
        foreach (var line in lines)
        {
            try
            {
                if (JsonSerializer.Deserialize<SessionEvent>(line, SessionEventText.Json) is { } e) events.Add(e);
            }
            catch (JsonException)
            {
                // A torn line is no call.
            }
        }

        return events;
    }
}
