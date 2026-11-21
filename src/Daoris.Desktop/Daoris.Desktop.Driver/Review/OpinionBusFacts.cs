namespace Daoris.Driver;

/// <summary>
/// The second opinion's facts teed into the bus's log, each kind named and versioned once here: a pass asked, given and answered (from
/// the landing line's channel), and its findings delivered. Ids, codes, counts and times only; the findings stay in the opinion's
/// record. Here until one registry declares every type.
/// </summary>
/// <remarks>
/// A draft names the keys a later one follows from (<see cref="BusDraft.Key"/>, <see cref="BusDraft.After"/>), so an answer is caused by
/// its pass where this process appended it; one from before a restart is not known, and its causes stay empty.
/// </remarks>
public static class OpinionBusFacts
{
    /// <summary>The emitter of the opinion facts.</summary>
    public const string Emitter = "driver/opinions";

    /// <summary>A pass asked of a reviewer (a request; its answerer the reviewer's door).</summary>
    public const string OpinionAskedKind = "opinion.asked/1";

    /// <summary>A pass given: its findings counted by weight.</summary>
    public const string OpinionGivenKind = "opinion.given/1";

    /// <summary>The working session's answers to the findings, counted as its turn's end read them.</summary>
    public const string OpinionAnsweredKind = "opinion.answered/1";

    /// <summary>The findings handed to the working session, as another agent's claims or between a chat's turns.</summary>
    public const string OpinionDeliveredKind = "opinion.delivered/1";

    /// <summary>The landing lines that are opinion facts, by their event.</summary>
    public static readonly IReadOnlyDictionary<string, string> Lines = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["opinion.asked"] = OpinionAskedKind,
        ["opinion.given"] = OpinionGivenKind,
        ["opinion.answered"] = OpinionAnsweredKind,
    };

    /// <summary>The findings handed to <paramref name="session"/>: how (<c>claims</c>, <c>send-back</c>) and how many.</summary>
    public static BusDraft OpinionDelivered(string opinion, string session, int findings, string how) => new(OpinionDeliveredKind, opinion, Emitter)
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
        if (!Lines.TryGetValue(line.Event, out var kind)) return null;
        var data = line.Data.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (data.GetValueOrDefault("opinion") is not string { Length: > 0 } opinion) return null;
        switch (kind)
        {
            case OpinionAskedKind:
                var pass = data.GetValueOrDefault("pass") as string;
                if (pass is not null)
                {
                    if (passes.Count >= 1024) passes.Clear();
                    passes[opinion] = pass;
                }

                return new BusDraft(kind, opinion, Emitter)
                {
                    Answerer = data.GetValueOrDefault("door") as string,
                    Effect = opinion,
                    Key = "opinion:" + opinion,
                    Payload = [("session", data.GetValueOrDefault("session")), ("occasion", data.GetValueOrDefault("occasion")), ("pass", pass)],
                };
            case OpinionGivenKind:
                return new BusDraft(kind, opinion, Emitter)
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
            default:
                return new BusDraft(kind, opinion, Emitter)
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
}
