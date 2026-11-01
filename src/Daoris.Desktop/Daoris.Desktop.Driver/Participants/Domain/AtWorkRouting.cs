using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>One part of an entry as the groups slice holds it: the strip's word for it and how it began (the design §R11.2 point 1).</summary>
public sealed record AtWorkEntryPart(string Session, string? Quest, string Shown, string Began)
{
    /// <summary>The session it follows: what it retried, resumed or carried on, or the session a hand-over continues.</summary>
    public string? After { get; init; }

    /// <summary>For a sibling's request: the repository that asked.</summary>
    public string? AskedBy { get; init; }
}

/// <summary>A session's row in the groups slice, the two fields the strip words a wait by.</summary>
public sealed record AtWorkRow(string? Awaits, string? AwaitsOf);

/// <summary>A quest as the quests slice holds it, the fields a part's wait and its joining read.</summary>
public sealed record AtWorkQuest(string Id, string Status)
{
    public const string Done = "Done";
    public const string Declined = "Declined";
    public const string Taken = "Taken";

    public string? To { get; init; }

    public string? From { get; init; }

    /// <summary>The question its taker asked another repository and waits on.</summary>
    public string? Awaits { get; init; }

    public string? PublishedBy { get; init; }

    public string? Parent { get; init; }

    /// <summary>The lanes it addresses, sorted and joined with <c>+</c> as an address writes them; null for the whole repository.</summary>
    public string? Lanes { get; init; }

    /// <summary>Whether its take is this machine's; null where the slice says none, which reads as here.</summary>
    public bool? Here { get; init; }

    public bool Closed => Status is Done or Declined;

    public IReadOnlyList<string>? LaneList => Lanes is { Length: > 0 } lanes ? lanes.Split('+', StringSplitOptions.RemoveEmptyEntries) : null;
}

/// <summary>
/// What the routing half reads beside the sessions (COWORKVIEW1b-i, the design §R11.2 points 1 and 3): the entry's parts as the groups
/// slice holds them, each session's row, the quests slice's quests and how many each session published. Plain values and lookups, so
/// the composition reads nothing itself.
/// </summary>
/// <param name="Parts">The entry's parts, oldest first.</param>
/// <param name="Row">A session's row; null where the slice holds none.</param>
/// <param name="Quest">A quest by id; null where the slice holds none.</param>
/// <param name="Published">How many quests the session published.</param>
/// <param name="Quests">The quests slice's health.</param>
public sealed record AtWorkRouting(
    IReadOnlyList<AtWorkEntryPart> Parts, Func<string, AtWorkRow?> Row, Func<string, AtWorkQuest?> Quest, Func<string, int> Published,
    SliceHealth Quests);

/// <summary>What a part waits on, as the strip places it: a quest (with the repository it was asked of and its lanes), or its helper.</summary>
public sealed record AtWorkWait
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Quest { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Of { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Lanes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Helper { get; init; }
}

/// <summary>How a part joined the entry (the design §3.4): the strip's word for its start, and the routing its record and quest say.</summary>
public sealed record AtWorkJoined(string Began)
{
    /// <summary>The strip's number of the part it follows.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? After { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Quest { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Lanes { get; init; }

    /// <summary>The strip's number of the part that published its quest, where that part is in the entry.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ByPart { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AskedBy { get; init; }

    /// <summary>The ask its quest came from.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ask { get; init; }

    /// <summary>The quest whose close published its quest: it is that chain's next step.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parent { get; init; }

    /// <summary>A chat that took <see cref="Quest"/> through its own connector.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Took { get; init; }

    /// <summary>The repository it ran in.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Of { get; init; }
}

/// <summary>The routing half of <see cref="AtWork.Compose"/> (COWORKVIEW1b-i): each part's wait, how it joined, where it runs and which fold.</summary>
internal static class AtWorkParts
{
    private const string AwaitingReply = "awaiting-reply";
    private const string AwaitingHelper = "awaiting-helper";
    private const string AskPrefix = "ask #";
    private static readonly string[] Active = ["queued", "starting", "working", "awaiting-person"];

    /// <summary>The wait the strip places on a part, else a chat's wait on a quest it took.</summary>
    internal static AtWorkWait? Wait(AtWorkRouting routing, string id, AtWorkEntryPart? part, AtWorkRecord? held)
    {
        switch (part?.Shown)
        {
            case AwaitingHelper:
                return new AtWorkWait { Helper = true };
            case AwaitingReply when routing.Row(id) is { Awaits: { Length: > 0 } awaits } row:
                return new AtWorkWait { Quest = awaits, Of = row.AwaitsOf, Lanes = routing.Quest(awaits)?.LaneList };
        }

        // No planner verdict reads a chat's take: its quest names a question the chat waits on while that question stands.
        if (held?.TookQuest is not { Length: > 0 } took || routing.Quest(took) is not { Status: AtWorkQuest.Taken, Here: not false } taken) return null;
        if (taken.Awaits is not { Length: > 0 } question) return null;
        var asked = routing.Quest(question);
        return asked is { Closed: true } ? null : new AtWorkWait { Quest = question, Of = asked?.To, Lanes = asked?.LaneList };
    }

    internal static AtWorkJoined? Joined(AtWorkRouting routing, AtWorkEntryPart? part, AtWorkRecord? held, Func<string?, int?> numberOf)
    {
        if (part is null) return null;
        var took = part.Quest is null ? held?.TookQuest : null;
        var quest = routing.Quest(part.Quest ?? took ?? "");
        return new AtWorkJoined(part.Began)
        {
            After = numberOf(part.After),
            Quest = part.Quest ?? took,
            Lanes = quest?.LaneList,
            ByPart = numberOf(quest?.PublishedBy),
            AskedBy = part.AskedBy,
            Ask = quest?.From is { } from && from.StartsWith(AskPrefix, StringComparison.Ordinal) ? from[AskPrefix.Length..] : null,
            Parent = quest?.Parent,
            Took = took is null ? null : true,
            Of = held?.Repository,
        };
    }

    /// <summary>Whether the part runs in a tree no earlier part of the entry worked in, and runs (or waits).</summary>
    internal static bool Alongside(AtWorkRecord? held, bool waiting, IEnumerable<AtWorkRecord?> earlier)
    {
        if (held?.Tree is not { Length: > 0 } tree || !(Runs(held.State) || waiting)) return false;
        var mine = Tree(tree);
        return earlier.All(each => each?.Tree is not { Length: > 0 } other || Tree(other) != mine) && earlier.Any();
    }

    internal static bool Runs(string? state) => state is not null && Active.Contains(state);

    /// <summary>A part that has ended: its record is no longer active. A record the slice does not hold is not known to have.</summary>
    internal static bool Ended(AtWorkRecord? held) => held?.State is { } state && !Active.Contains(state);

    private static string Tree(string path) => path.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
}
