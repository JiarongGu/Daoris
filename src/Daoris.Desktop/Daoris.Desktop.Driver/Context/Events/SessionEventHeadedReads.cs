namespace Daoris.Driver;

/// <summary>
/// The reads of a long events file that its <see cref="SessionEventHead"/> answers from a part of it: the newest page or the page before a
/// number, the events after one, the opening and the plan. Each returns what <see cref="SessionEvents"/>'s whole read of the file returns.
/// </summary>
internal static class SessionEventHeadedReads
{
    /// <summary>The page <see cref="SessionEvents.Page"/> answers: read from the kept offset before the page's first number, not from the file's start.</summary>
    public static EventPage Page(string path, SessionEventHead head, long? before, int take)
    {
        var upper = before is { } b ? Math.Min(b - 1, head.LastSeq) : head.LastSeq;
        var from = head.OffsetBefore(upper - take);
        List<SessionEvent> page;
        while (true)
        {
            page = Collect(path, from, take, before);
            if (page.Count >= take || from == 0) break;
            from = head.OffsetBeforeThat(from);
        }

        return new EventPage(page, page.Count > 0 && page[0].Seq > head.FirstSeq, head.LastSeq)
        {
            Opening = head.OpeningAt >= 0 && page.Count > 0 && head.OpeningSeq < page[0].Seq ? EventAt(path, head.OpeningAt) : null,
            FirstFailure = head.FirstFailure > 0 ? head.FirstFailure : null,
        };
    }

    /// <summary>Everything newer than <paramref name="after"/>, as <see cref="SessionEvents.After"/> answers it.</summary>
    public static EventPage After(string path, SessionEventHead head, long after)
    {
        if (after >= head.LastSeq) return new EventPage([], Earlier: false, head.LastSeq);
        var events = new List<SessionEvent>();
        foreach (var e in Lines(path, head.OffsetBefore(after))) if (e.Seq > after) events.Add(e);
        return new EventPage(events, Earlier: false, head.LastSeq);
    }

    /// <summary>The plan the newest plan event kept, whole; empty where there is none.</summary>
    public static IReadOnlyList<PlanEntry> Plan(string path, SessionEventHead head) =>
        head.PlanAt >= 0 && EventAt(path, head.PlanAt)?.Entries is { } entries ? entries : [];

    private static List<SessionEvent> Collect(string path, long from, int take, long? before)
    {
        var page = new Queue<SessionEvent>(take + 1);
        foreach (var e in Lines(path, from))
        {
            if (before is { } b && e.Seq >= b) break;
            page.Enqueue(e);
            if (page.Count > take) page.Dequeue();
        }

        return [.. page];
    }

    private static SessionEvent? EventAt(string path, long at) => Lines(path, at).FirstOrDefault();

    /// <summary>The readable events from byte offset <paramref name="from"/>, which begins a line, to the file's end.</summary>
    private static IEnumerable<SessionEvent> Lines(string path, long from)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        stream.Seek(from, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        while (reader.ReadLine() is { } line)
        {
            if (SessionEvents.Event(line) is { } e) yield return e;
        }
    }
}
