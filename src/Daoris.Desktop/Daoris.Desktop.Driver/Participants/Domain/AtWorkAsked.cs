using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>
/// The quests half of the routing, a port the layer above the composition fills: what the entry's parts asked, over the
/// quest units that layer builds from the stores it holds (<c>AtWorkQuests.Reading</c>). The composition reads nothing itself.
/// </summary>
/// <param name="attended">The attended session.</param>
/// <param name="ids">The entry's sessions, the attended one among them.</param>
/// <param name="parts">The entry's parts as the groups slice holds them.</param>
/// <param name="attendedRecord">The attended record's read fields.</param>
public delegate AtWorkAsked AtWorkAsking(string attended, IReadOnlyList<string> ids, IReadOnlyList<AtWorkEntryPart> parts, AtWorkRecord attendedRecord);

/// <summary>Who took a quest: a session here (with its record's agent and state), a conversation, another machine, nobody recorded, or nobody yet.</summary>
public sealed record AtWorkTaker
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Session { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Agent { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; init; }

    /// <summary>A chat that took it through its own connector: its record's <c>tookQuest</c>, its <c>quest</c> staying empty.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Conversation { get; init; }

    /// <summary>The quest is taken, its holder another machine.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Elsewhere { get; init; }

    /// <summary>The quest is taken, and no session recorded here serves it or took it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Unrecorded { get; init; }

    /// <summary>The quest is open: the tick's reason for it sitting is the page's to read beside.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Untaken { get; init; }
}

/// <summary>One quest a part of the entry published, or a step of one's chain, as the quest unit reads it.</summary>
public sealed record AtWorkAskedQuest(string Id, string Title, string Status)
{
    /// <summary>The repository it is sent to, where that is not the attended part's own.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Of { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Lanes { get; init; }

    /// <summary>The quest whose close published this one: the chain's step before it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Step { get; init; }

    /// <summary>What its taker waits on while that wait stands.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AtWorkWait? Waits { get; init; }

    /// <summary>Null for a quest nobody took and that is not open: a decline of work nobody started.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AtWorkTaker? Taker { get; init; }
}

/// <summary>What asked the attended part's quest: the publishing session, the step before, the ask, or the person.</summary>
public sealed record AtWorkBy
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Session { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Step { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ask { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Person { get; init; }
}

public sealed record AtWorkAsker(string Quest, AtWorkBy By);

/// <summary>The quests half of an answer: the rows within the bound, how many were left out, and the attended part's asker.</summary>
public sealed record AtWorkAsked(IReadOnlyList<AtWorkAskedQuest> Quests, int More, AtWorkAsker? By);
