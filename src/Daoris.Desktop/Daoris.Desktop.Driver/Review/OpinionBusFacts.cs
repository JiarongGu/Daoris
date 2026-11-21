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

    /// <summary>Whether the gate's acknowledgement of <paramref name="entry"/> ends its obligation: an owed opinion or answers judged, a given pass only when clean.</summary>
    public static bool EndsAtTheGate(BusEnvelope entry)
    {
        if (entry.Kind == BusTypes.OpinionOwed.Kind || entry.Kind == BusTypes.OpinionAnswered.Kind) return true;
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

    /// <summary>A session's work owed a second opinion, or a person's ask of its gate (<paramref name="by"/>): commit-class, the gate's acknowledgement waits on it.</summary>
    public static BusDraft OpinionOwed(string session, string? quest, string occasion, string by) => new(BusTypes.OpinionOwed, Owed(session, occasion))
    {
        Commit = true,
        Payload = [("session", session), ("quest", string.IsNullOrEmpty(quest) ? null : quest), ("occasion", occasion), ("by", by)],
    };

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
