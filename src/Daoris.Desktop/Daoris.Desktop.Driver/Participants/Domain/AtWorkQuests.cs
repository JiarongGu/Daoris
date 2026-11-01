using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>
/// The whole stores the quests half reads (COWORKVIEW1b-ii): the quests as the service answers them and the session records, as plain
/// values, so the composition builds the quest units by <see cref="QuestUnits.Read"/> and reads nothing itself.
/// </summary>
/// <param name="Row">A session's row in the groups slice: what its planner verdict has it wait on.</param>
public sealed record AtWorkAsking(IReadOnlyList<QuestView> Quests, IReadOnlyList<SessionRecord> Sessions, Func<string, AtWorkRow?> Row);

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

    /// <summary>The quest is open: the tick's reason for it sitting is the page's to read beside (§3.9).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Untaken { get; init; }
}

/// <summary>One quest a part of the entry published, or a step of one's chain, as the quest unit reads it.</summary>
public sealed record AtWorkAskedQuest(string Id, string Title, string Status)
{
    /// <summary>The repository it was sent to, where that is not the attended part's own.</summary>
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

/// <summary>
/// The quests a part of an entry asked, and what asked the attended part (COWORKVIEW1b-ii, the design §3.1): built over the quest units
/// <see cref="QuestUnits.Read"/> answers, never over a branch's inventory or a service call per member. A quest a part published that
/// opened no part of the entry is a row, its chain's steps after it by <c>parent</c>; one the entry holds the executing part of is that
/// part's <i>on what</i>, never a row too.
/// </summary>
public static class AtWorkQuests
{
    /// <param name="attended">The attended session.</param>
    /// <param name="ids">The entry's sessions, the attended one among them.</param>
    /// <param name="parts">The entry's parts as the groups slice holds them.</param>
    /// <param name="attendedRecord">The attended record's read fields.</param>
    public static AtWorkAsked Of(string attended, IReadOnlyList<string> ids, IReadOnlyList<AtWorkEntryPart> parts, AtWorkRecord attendedRecord, AtWorkAsking asking)
    {
        var members = ids.ToHashSet(StringComparer.Ordinal);
        var byId = new Dictionary<string, QuestView>(StringComparer.Ordinal);
        foreach (var quest in asking.Quests) byId.TryAdd(quest.Id, quest);

        // The quests the entry's parts execute: the strip names one on a part, and a record names the quest it serves.
        var executing = parts.Select(part => part.Quest).Concat(asking.Sessions.Where(session => members.Contains(session.Id)).Select(session => session.Quest))
            .OfType<string>().ToHashSet(StringComparer.Ordinal);

        // A chain stays whole, and the chains with work still open come before those that are over.
        var chains = Chains(byId.Values.Where(quest => quest.PublishedBy is { } by && members.Contains(by)).ToList(), asking.Quests)
            .Select(chain => chain.Where(quest => !executing.Contains(quest.Id)).ToList()).Where(chain => chain.Count > 0).ToList();
        var drawn = chains.Where(chain => !chain.All(Over)).Concat(chains.Where(chain => chain.All(Over))).SelectMany(chain => chain).ToList();
        var rows = drawn.Take(AtWork.Bound).ToList();

        var units = QuestUnits.Read(rows, asking.Sessions).ToDictionary(unit => unit.Records.Quest!.Id, StringComparer.Ordinal);
        var sessions = asking.Sessions.GroupBy(session => session.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return new AtWorkAsked(
            [.. rows.Select(quest => Row(quest, units[quest.Id], sessions, asking, byId, attendedRecord.Repository))],
            drawn.Count - rows.Count,
            AskedBy(attended, parts, attendedRecord, asking, byId));
    }

    /// <summary>Each root with, after it, the quests whose <c>parent</c> is one of them, deepest last; a cycle's members once each.</summary>
    private static List<List<QuestView>> Chains(List<QuestView> roots, IReadOnlyList<QuestView> all)
    {
        var below = all.Where(quest => quest.Parent is { Length: > 0 }).GroupBy(quest => quest.Parent!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var chains = new List<List<QuestView>>();

        void Walk(QuestView quest, List<QuestView> chain)
        {
            if (!seen.Add(quest.Id)) return;
            chain.Add(quest);
            foreach (var step in below.GetValueOrDefault(quest.Id) ?? []) Walk(step, chain);
        }

        foreach (var root in roots)
        {
            var chain = new List<QuestView>();
            Walk(root, chain);
            chains.Add(chain);
        }

        return chains;
    }

    private static bool Over(QuestView quest) => quest.Status is "Done" or "Declined";

    private static AtWorkAskedQuest Row(
        QuestView quest, WorkUnit unit, IReadOnlyDictionary<string, SessionRecord> sessions, AtWorkAsking asking, IReadOnlyDictionary<string, QuestView> byId, string? repository)
    {
        var taker = Taker(quest, unit, sessions, asking);
        return new AtWorkAskedQuest(quest.Id, quest.Name, quest.Status)
        {
            Of = quest.To.Length > 0 && !string.Equals(quest.To, repository, StringComparison.Ordinal) ? quest.To : null,
            Lanes = quest.Lanes.Count > 0 ? quest.Lanes : null,
            Step = unit.Links.FirstOrDefault(link => link.Relation == WorkRelation.Follows)?.Unit[QuestUnits.IdOf("").Length..],
            Waits = Waits(quest, taker, asking, byId),
            Taker = taker,
        };
    }

    /// <summary>Open: nobody yet. Else the newest session that serves it, a conversation that took it, its holder's machine, or nobody recorded.</summary>
    private static AtWorkTaker? Taker(QuestView quest, WorkUnit unit, IReadOnlyDictionary<string, SessionRecord> sessions, AtWorkAsking asking)
    {
        if (quest.Status == "Open") return new AtWorkTaker { Untaken = true };
        if (unit.Executors.Count > 0 && sessions.GetValueOrDefault(unit.Executors[^1]) is { } serving)
        {
            return new AtWorkTaker { Session = serving.Id, Agent = serving.Adapter, State = serving.State };
        }

        var chat = asking.Sessions.Where(session => string.Equals(session.TookQuest, quest.Id, StringComparison.Ordinal)).OrderBy(session => session.Created).LastOrDefault();
        if (chat is not null) return new AtWorkTaker { Conversation = chat.Id };
        if (quest.Status == "Declined") return null;
        return Mine(quest) ? new AtWorkTaker { Unrecorded = true } : new AtWorkTaker { Elsewhere = true };
    }

    /// <summary>Whether its take is this machine's; a list that names no holder or no machine reads as here.</summary>
    private static bool Mine(QuestView quest) =>
        quest.Holder is not { Length: > 0 } holder || quest.Here is not { Length: > 0 } here || string.Equals(holder, here, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A session taker waits on what its row names, the planner's verdict; a conversation, on the question of its take while that question
    /// stands. A question closed (declined, or done and held by nothing) is no wait.
    /// </summary>
    private static AtWorkWait? Waits(QuestView quest, AtWorkTaker? taker, AtWorkAsking asking, IReadOnlyDictionary<string, QuestView> byId)
    {
        string? question = null;
        string? of = null;
        if (taker?.Session is { } session)
        {
            if (asking.Row(session) is not { Awaits: { Length: > 0 } awaits } row) return null;
            (question, of) = (awaits, row.AwaitsOf);
        }
        else if (taker?.Conversation is not null && quest is { Status: "Taken", Awaits: { Length: > 0 } awaited } && Mine(quest))
        {
            question = awaited;
            of = byId.GetValueOrDefault(awaited)?.To;
            if (byId.GetValueOrDefault(awaited) is { } asked && (asked.Status == "Declined" || (asked.Status == "Done" && !asked.Held))) return null;
        }

        if (question is null) return null;
        var lanes = byId.GetValueOrDefault(question)?.Lanes;
        return new AtWorkWait { Quest = question, Of = of, Lanes = lanes is { Count: > 0 } ? lanes : null };
    }

    /// <summary>The attended part's quest (the strip's, else its record's, else the one it took) and what asked it; none where the quest is unknown here.</summary>
    private static AtWorkAsker? AskedBy(string attended, IReadOnlyList<AtWorkEntryPart> parts, AtWorkRecord record, AtWorkAsking asking, IReadOnlyDictionary<string, QuestView> byId)
    {
        var serving = asking.Sessions.FirstOrDefault(session => session.Id == attended)?.Quest;
        var id = parts.FirstOrDefault(part => part.Session == attended)?.Quest ?? serving ?? record.TookQuest;
        if (id is null || byId.GetValueOrDefault(id) is not { } quest) return null;
        var by = quest.PublishedBy is { Length: > 0 } publisher ? new AtWorkBy { Session = publisher }
            : quest.Parent is { Length: > 0 } parent ? new AtWorkBy { Step = parent }
            : AskWords.AskOf(quest.From) is { Length: > 0 } ask ? new AtWorkBy { Ask = ask }
            : new AtWorkBy { Person = true };
        return new AtWorkAsker(quest.Id, by);
    }
}
