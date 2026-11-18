using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A subagent's own record: its words and calls, mapped by its door as the session's are, one event per line in
/// <c>sessions/&lt;session&gt;/children/&lt;child&gt;.jsonl</c>, beside the session's record and never in it.
/// </summary>
/// <remarks>
/// <para><b>Never the session's.</b> The driver reads the session's record for what the session said to the person (a parked card's
/// question), and a build that predates the child's record would quote a child's words as the session's: so its own record is in a
/// folder no enumeration of the sessions folder reads as a session (<c>*.events.jsonl</c> at its top), and no watch of the session's
/// record is told of it. The session's record keeps the child's start and end alone, as <see cref="SessionEventKind.Child"/>.</para>
///
/// <para><b>Numbered and stamped here</b>, from one, as the session's record is: a page reads it after a number, and the second check
/// orders its calls by the moment.</para>
/// </remarks>
public sealed class ChildRecords(string directory, Func<DateTimeOffset>? clock = null)
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, long> Latest = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The folder under a session's own (<c>sessions/&lt;session&gt;/</c>) its subagents' records are kept in.</summary>
    public const string Folder = "children";

    /// <summary>The stream prefix a subagent runs beside its session on, whose rest is the child its record is kept by.</summary>
    public const string StreamPrefix = SessionStreamKind.Subagent + "/";

    /// <summary>The child a stream is a subagent's of (<c>subagent/&lt;child&gt;</c>), or null for any other stream.</summary>
    public static string? ChildOf(string? stream) =>
        stream is { Length: > 0 } && stream.StartsWith(StreamPrefix, StringComparison.Ordinal) ? stream[StreamPrefix.Length..] : null;

    /// <summary>Where a child's record is kept; null for a session or a child no file can be named by.</summary>
    public string? PathOf(string session, string child) =>
        SessionEventText.IsLocalId(session) && child.Length is > 0 and <= 120
            && child.All(each => char.IsAsciiLetterOrDigit(each) || each is '-' or '_' or '.') && child.Trim('.').Length > 0
            ? Path.Combine(directory, session, Folder, child + ".jsonl")
            : null;

    /// <summary>
    /// One of the child's events as its door mapped it, numbered and stamped. A line not kept costs the child's own view, never the session.
    /// </summary>
    public void Keep(string session, string child, SessionEvent e)
    {
        if (PathOf(session, child) is not { } path) return;
        try
        {
            lock (Gate)
            {
                var seq = (Latest.TryGetValue(path, out var last) ? last : Count(path)) + 1;
                var stamped = SessionEventText.Bound(e) with { Seq = seq, At = e.At == default && clock is not null ? clock() : e.At };
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, JsonSerializer.Serialize(stamped, SessionEventText.Json) + "\n");
                Latest[path] = seq;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A child's line not kept leaves it said as delegated, not read.
        }
    }

    /// <summary>The child's events in order; null where none is kept, which its reader then says.</summary>
    public IReadOnlyList<SessionEvent>? Read(string session, string child)
    {
        if (PathOf(session, child) is not { } path || !File.Exists(path)) return null;
        string[] lines;
        try
        {
            lock (Gate) lines = AtomicFile.ReadText(path).Split('\n', StringSplitOptions.RemoveEmptyEntries);
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
                if (JsonSerializer.Deserialize<SessionEvent>(line, SessionEventText.Json) is { Kind.Length: > 0 } e) events.Add(e);
            }
            catch (JsonException)
            {
                // A torn line is no event.
            }
        }

        return events;
    }

    private static long Count(string path) =>
        File.Exists(path) ? AtomicFile.ReadText(path).Split('\n', StringSplitOptions.RemoveEmptyEntries).LongLength : 0;
}
