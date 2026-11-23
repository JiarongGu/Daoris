namespace Daoris.Driver;

/// <summary>
/// The second opinion's facts teed into the bus's log, their types the registry's (<see cref="BusTypes"/>): a pass asked, given and
/// answered (from the landing line's channel), and its findings delivered. Ids, codes, counts and times only; the findings stay in the
/// opinion's record.
/// </summary>
/// <remarks>
/// A draft names the keys a later one follows from (<see cref="BusDraft.Key"/>, <see cref="BusDraft.After"/>), so an answer is caused by
/// its pass where this process appended it; one from before a restart is not known, and its causes stay empty.
/// </remarks>
public static class OpinionBusFacts
{
    /// <summary>The landing lines that are opinion facts, by their event.</summary>
    public static readonly IReadOnlyDictionary<string, BusType> Lines = new Dictionary<string, BusType>(StringComparer.Ordinal)
    {
        ["opinion.asked"] = BusTypes.OpinionAsked,
        ["opinion.given"] = BusTypes.OpinionGiven,
        ["opinion.answered"] = BusTypes.OpinionAnswered,
    };

    /// <summary>The kinds the gate's acknowledgement always ends: an owed opinion, answers, the person's word, and a move it judged.</summary>
    private static readonly HashSet<string> Judged = new(
        [BusTypes.OpinionOwed.Kind, BusTypes.OpinionAnswered.Kind, BusTypes.OpinionSaid.Kind, BusTypes.QuestMoved.Kind, BusTypes.NodeBegun.Kind, BusTypes.ChoicesWritten.Kind, BusTypes.SessionTurnEnded.Kind],
        StringComparer.Ordinal);

    /// <summary>Whether the gate's acknowledgement of <paramref name="entry"/> ends its obligation: one of <see cref="Judged"/>, a given pass only when clean.</summary>
    public static bool EndsAtTheGate(BusEnvelope entry)
    {
        if (Judged.Contains(entry.Kind)) return true;
        if (entry.Kind != BusTypes.OpinionGiven.Kind) return false;
        long? Count(string key) => entry.Payload.FirstOrDefault(pair => pair.Key == key).Value switch
        {
            int number => number,
            long number => number,
            _ => null,
        };

        return Count("must") is 0 && Count("should") is 0 && Count("note") is 0;
    }

    public static string Owed(string session, string occasion) => $"{session}/{occasion}";

    /// <summary>The <c>by</c> of an owed fact a person's ask tees: its occasion is the ask's (<c>asked</c>, <c>failure</c>), so it names every owed opinion of its session.</summary>
    public const string Pressed = "press";

    /// <summary>A session's work owed a second opinion, or a person's ask of its gate (<paramref name="by"/>): commit-class, the gate's acknowledgement waits on it.</summary>
    public static BusDraft OpinionOwed(string session, string? quest, string occasion, string by) => new(BusTypes.OpinionOwed, Owed(session, occasion))
    {
        Commit = true,
        Payload = [("session", session), ("quest", string.IsNullOrEmpty(quest) ? null : quest), ("occasion", occasion), ("by", by)],
    };

    /// <summary>The person's word at <paramref name="session"/>'s gate, kept: commit-class, so the gate judges it at once.</summary>
    public static BusDraft OpinionSaid(string session, string said, string? door) => new(BusTypes.OpinionSaid, session)
    {
        Commit = true,
        Payload = [("session", session), ("said", said), ("door", door)],
    };

    /// <summary>A turn of <paramref name="session"/>, whose work is owed a second opinion, ended: the gate judges what waited on it.</summary>
    public static BusDraft TurnEnded(string session, IReadOnlyList<string>? owed) => new(BusTypes.SessionTurnEnded, session)
    {
        Commit = true,
        Payload = [("session", session), ("owed", owed)],
    };

    /// <summary>
    /// The turn's end of <paramref name="session"/>, naming the owed opinions it reaches: its own, and a chat run's kept in its
    /// <paramref name="tree"/> by its line's first session; none where none is owed, and every one, unnamed, where the due list does not read.
    /// </summary>
    public static BusDraft? TurnEndedOwed(string home, string session, string tree)
    {
        IReadOnlyList<OpinionDue> open;
        try
        {
            open = new OpinionDues(home).OpenStrict();
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return TurnEnded(session, null);
        }

        List<string> owed = [.. open.Where(due => Same(due.Session, session) || (due.OnRun && SameTree(due.Tree, tree))).Select(due => due.Session)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        return owed.Count == 0 ? null : TurnEnded(session, owed);
    }

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool SameTree(string left, string right) =>
        left.Length > 0 && right.Length > 0 && Same(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)));

    /// <summary>The service's quests moved, the families its change stream named, and what changed where the list could say (<see cref="QuestMoves"/>).</summary>
    public static BusDraft QuestMoved(IReadOnlyList<string> families, QuestMoveNames? names = null) => new(BusTypes.QuestMoved, "quests")
    {
        Commit = true,
        Payload = [("families", families), ("quests", names?.Quests), ("repositories", names?.Repositories)],
    };

    /// <summary>The unit keyed <paramref name="unit"/> began <paramref name="nodes"/>.</summary>
    public static BusDraft NodeBegun(string unit, IReadOnlyList<string> nodes) => new(BusTypes.NodeBegun, unit)
    {
        Commit = true,
        Payload = [("nodes", nodes)],
    };

    /// <summary>The standing choices written by this process.</summary>
    public static BusDraft ChoicesWritten() => new(BusTypes.ChoicesWritten, "choices") { Commit = true };

    /// <summary>The findings handed to <paramref name="session"/>: how (<c>claims</c>, <c>send-back</c>) and how many.</summary>
    public static BusDraft OpinionDelivered(string opinion, string session, int findings, string how) => new(BusTypes.OpinionDelivered, opinion)
    {
        After = ["opinion-given:" + opinion, "opinion:" + opinion],
        Commit = true,
        Payload = [("session", session), ("findings", findings), ("how", how)],
    };

    /// <summary>
    /// The draft an opinion's landing line tees, or null for a line that is no opinion fact; <paramref name="passes"/> keeps each asked
    /// opinion's pass, which its given line does not say.
    /// </summary>
    public static BusDraft? OfLanding(LandingLine line, IDictionary<string, string> passes)
    {
        if (!Lines.TryGetValue(line.Event, out var type)) return null;
        var data = line.Data.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (data.GetValueOrDefault("opinion") is not string { Length: > 0 } opinion) return null;
        if (ReferenceEquals(type, BusTypes.OpinionAsked))
        {
            var pass = data.GetValueOrDefault("pass") as string;
            if (pass is not null)
            {
                if (passes.Count >= 1024) passes.Clear();
                passes[opinion] = pass;
            }

            return new BusDraft(type, opinion)
            {
                Answerer = data.GetValueOrDefault("door") as string,
                Effect = opinion,
                Key = "opinion:" + opinion,
                Payload = [("session", data.GetValueOrDefault("session")), ("occasion", data.GetValueOrDefault("occasion")), ("pass", pass)],
            };
        }

        if (ReferenceEquals(type, BusTypes.OpinionGiven))
        {
            return new BusDraft(type, opinion)
            {
                After = ["opinion:" + opinion],
                Key = "opinion-given:" + opinion,
                Commit = true,
                Took = data.GetValueOrDefault("seconds") is long seconds ? TimeSpan.FromSeconds(seconds) : null,
                Payload =
                [
                    ("pass", passes.TryGetValue(opinion, out var given) ? given : null), ("must", data.GetValueOrDefault("must")),
                    ("should", data.GetValueOrDefault("should")), ("note", data.GetValueOrDefault("note")), ("seconds", data.GetValueOrDefault("seconds")),
                    ("tier", data.GetValueOrDefault("tier")),
                ],
            };
        }

        return new BusDraft(type, opinion)
        {
            After = ["opinion-given:" + opinion, "opinion:" + opinion],
            Commit = true,
            Payload =
            [
                ("fixed", data.GetValueOrDefault("fixed")), ("rejected", data.GetValueOrDefault("rejected")),
                ("unresolved", data.GetValueOrDefault("unresolved")), ("notAnswered", data.GetValueOrDefault("notAnswered")),
            ],
        };
    }
}
