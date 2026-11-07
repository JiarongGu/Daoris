using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The one grammar of <c>work/event</c>'s frame: each kind's closed list of fields, and each
/// field's shape. A value is a generated id in its minted shape, a closed code, a count or a UTC time, so no frame carries a name, a word,
/// a secret or a machine path. <see cref="HookFrames.Event"/> builds by it, and the census and the relay's check read it.
/// </summary>
public static class WorkEventGrammar
{
    /// <summary>Raised whenever a kind or a field's shape changes; the handshake says it.</summary>
    public const int Version = 3;

    /// <summary>A trial's <c>run</c>: a sample is never taken for the live point's.</summary>
    public const string Sample = "sample";

    /// <summary>A kind's fields beyond <see cref="Common"/>: those it must carry, and those it carries where its source holds them.</summary>
    public sealed record KindFields(IReadOnlyList<string> Required, IReadOnlyList<string> Optional);

    /// <summary>What every frame carries, in its order.</summary>
    public static readonly IReadOnlyList<string> Common = ["run", "seq", "missed", "kind", "at"];

    /// <summary>Each kind this version tells, and its closed list.</summary>
    public static readonly IReadOnlyDictionary<string, KindFields> Fields = new Dictionary<string, KindFields>(StringComparer.Ordinal)
    {
        [WorkEventKinds.SessionParked] = new(["session"], ["quest", "ask"]),
        [WorkEventKinds.QuestParked] = new(["quest"], ["session", "strikes"]),
        [WorkEventKinds.SessionEnded] = new(["session", "state"], ["quest", "ask", "adapter"]),
        [WorkEventKinds.WorkLanded] = new(["session", "code"], ["quest"]),
        [WorkEventKinds.Missed] = new([], []),
        // Version 2.
        [WorkEventKinds.QuestPublished] = new(["quest"], []),
        [WorkEventKinds.QuestClosed] = new(["quest", "state"], []),
        [WorkEventKinds.QuestCloseLost] = new(["quest", "state"], []),
        [WorkEventKinds.ConfirmationEnded] = new(["confirmation", "end"], []),
        [WorkEventKinds.WorkEnded] = new(["session"], ["quest"]),
        // Version 3.
        [WorkEventKinds.ChannelPosted] = new(["by"], ["message", "ask", "poster", "urgent"]),
    };

    /// <summary>A landing's codes: landed, or a chain's branch moved on.</summary>
    public static readonly IReadOnlyList<string> LandedCodes = ["landed", "advanced"];

    /// <summary>How a press ends, its five closed codes.</summary>
    public static readonly IReadOnlyList<string> EndCodes = ["confirmed", "refused", "changed", "withdrawn", "lost"];

    private static readonly Regex ConfirmationShape = new(@"\A[0-9a-f]{32}\z", RegexOptions.CultureInvariant);

    private const string Time = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static readonly Regex RunShape = new(@"\A[0-9a-f]{12}\z", RegexOptions.CultureInvariant);
    private static readonly Regex SessionShape = new(@"\A[0-9a-f]{8}\z", RegexOptions.CultureInvariant);
    private static readonly Regex QuestShape = new(@"\A(?:[0-9a-f]{12}|[0-9a-f]{6})\z", RegexOptions.CultureInvariant);
    private static readonly Regex AskShape = new(@"\A[0-9a-f]{6}\z", RegexOptions.CultureInvariant);

    /// <summary>A message kept here, its id's minted shape.</summary>
    private static readonly Regex MessageShape = new(@"\A[0-9a-f]{8}\z", RegexOptions.CultureInvariant);

    /// <summary>The shape of a <c>--not-by</c> id or a <c>poster</c>: a session's, or a member's.</summary>
    public static bool IsPoster(string? id) => id is not null && (SessionShape.IsMatch(id) || ChannelPosts.IsMember(id));

    /// <summary>A new <c>run</c>, from the system's random source, never twelve zeros.</summary>
    public static string MintRun()
    {
        while (true)
        {
            var run = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
            if (run != new string('0', 12)) return run;
        }
    }

    /// <summary>A time as every frame spells it: ISO 8601, UTC, to the second.</summary>
    public static string TimeOf(DateTimeOffset at) => at.UtcDateTime.ToString(Time, CultureInfo.InvariantCulture);

    /// <summary>Whether <paramref name="value"/> is in <paramref name="field"/>'s shape; a field the grammar does not name is in none.</summary>
    /// <param name="adapters">This build's adapter names, the only ones an ending may carry.</param>
    /// <param name="of">The frame's kind, which a <c>state</c>'s codes depend on: a quest's close or its loss, else a session's ending.</param>
    public static bool Fits(string field, object? value, IReadOnlySet<string> adapters, string? of = null) => field switch
    {
        "state" when of == WorkEventKinds.QuestClosed => value is "done" or "declined",
        "state" when of == WorkEventKinds.QuestCloseLost => value is "open" or "taken",
        "confirmation" => value is string confirmation && ConfirmationShape.IsMatch(confirmation),
        "end" => value is string end && EndCodes.Contains(end),
        "run" => value is string run && (run == Sample || (RunShape.IsMatch(run) && run != new string('0', 12))),
        "seq" => value is long or int && Convert.ToInt64(value, CultureInfo.InvariantCulture) >= 1,
        "missed" or "strikes" => value is long or int && Convert.ToInt64(value, CultureInfo.InvariantCulture) >= 0,
        "kind" => value is string kind && Fields.ContainsKey(kind),
        "at" => value is string at && DateTimeOffset.TryParseExact(at, Time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
        "session" => value is string session && SessionShape.IsMatch(session),
        "quest" => value is string quest && QuestShape.IsMatch(quest),
        "ask" => value is string ask && AskShape.IsMatch(ask),
        "message" => value is string message && MessageShape.IsMatch(message),
        "poster" => value is string poster && IsPoster(poster),
        "by" => value is string by && ChannelPosts.Classes.Contains(by),
        "urgent" => value is true or "true",
        "state" => value is string state && SessionLog.Closed.Contains(state) && state == state.ToLowerInvariant(),
        "adapter" => value is string adapter && adapters.Contains(adapter),
        "code" => value is string code && LandedCodes.Contains(code),
        _ => false,
    };

    /// <summary>What a frame breaks of the grammar, a sentence each naming the field, never its value; none where it reads.</summary>
    public static IReadOnlyList<string> Check(IReadOnlyDictionary<string, object?> frame, IReadOnlySet<string> adapters)
    {
        var problems = new List<string>();
        foreach (var field in Common)
        {
            if (!frame.TryGetValue(field, out var value) || !Fits(field, value, adapters)) problems.Add($"`{field}` is missing or outside its shape");
        }

        if (frame.TryGetValue("kind", out var named) && named is string kind && Fields.TryGetValue(kind, out var fields))
        {
            foreach (var field in fields.Required.Where(field => !frame.ContainsKey(field))) problems.Add($"`{field}` is required of `{kind}`");
            foreach (var (field, value) in frame.Where(pair => !Common.Contains(pair.Key)))
            {
                if (!fields.Required.Contains(field) && !fields.Optional.Contains(field)) problems.Add($"`{field}` is not in `{kind}`'s list");
                else if (!Fits(field, value, adapters, kind)) problems.Add($"`{field}` is outside its shape");
            }
        }

        return problems;
    }
}
