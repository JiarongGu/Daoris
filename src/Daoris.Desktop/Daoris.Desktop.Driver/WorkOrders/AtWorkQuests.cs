namespace Daoris.Driver;

/// <summary>
/// The quests a part of an entry asked, and what asked the attended part: built over the quest units
/// <see cref="QuestUnits.Read"/> answers, never over a branch's inventory or a service call per member. A quest a part published that
/// opened no part of the entry is a row, its chain's steps after it by <c>parent</c>; one the entry holds the executing part of is that
/// part's <i>on what</i>, never a row too. It sits above the participants' composition, which takes it as a port (<see cref="AtWorkAsking"/>).
/// </summary>
public static class AtWorkQuests
{
    /// <summary>The port over the whole stores: every quest as the service answers it, the session records, and a session's row in the groups slice.</summary>
    public static AtWorkAsking Reading(IReadOnlyList<QuestView> quests, IReadOnlyList<SessionRecord> sessions, Func<string, AtWorkRow?> row) =>
        (attended, ids, parts, record) => Of(attended, ids, parts, record, new Stores(quests, sessions, row));

    private sealed record Stores(IReadOnlyList<QuestView> Quests, IReadOnlyList<SessionRecord> Sessions, Func<string, AtWorkRow?> Row);

    /// <summary>
    /// The quests an ask asked: those whose sender is the ask, each with its chain's steps after it and its
    /// taker, as the quests of a session's entry are drawn. None is left out for being executed by a part: the ask's section says who took each.
    /// </summary>
    public static AtWorkAsked ForAsk(string ask, IReadOnlyList<QuestView> quests, IReadOnlyList<SessionRecord> sessions, Func<string, AtWorkRow?> row)
    {
        var stores = new Stores(quests, sessions, row);
        var byId = ById(stores);
        var roots = byId.Values.Where(quest => string.Equals(AskWords.AskOf(quest.From), ask, StringComparison.Ordinal)).ToList();
        return Built(Chains(roots, quests), stores, byId, repository: null, by: null);
    }

    private static Dictionary<string, QuestView> ById(Stores stores)
    {
        var byId = new Dictionary<string, QuestView>(StringComparer.Ordinal);
        foreach (var quest in stores.Quests) byId.TryAdd(quest.Id, quest);
        return byId;
    }

    private static AtWorkAsked Of(string attended, IReadOnlyList<string> ids, IReadOnlyList<AtWorkEntryPart> parts, AtWorkRecord attendedRecord, Stores stores)
    {
        var members = ids.ToHashSet(StringComparer.Ordinal);
        var byId = ById(stores);

        // The quests the entry's parts execute: the strip names one on a part, and a record names the quest it serves.
        var executing = parts.Select(part => part.Quest).Concat(stores.Sessions.Where(session => members.Contains(session.Id)).Select(session => session.Quest))
            .OfType<string>().ToHashSet(StringComparer.Ordinal);

        var chains = Chains(byId.Values.Where(quest => quest.PublishedBy is { } by && members.Contains(by)).ToList(), stores.Quests)
            .Select(chain => chain.Where(quest => !executing.Contains(quest.Id)).ToList()).Where(chain => chain.Count > 0).ToList();
        return Built(chains, stores, byId, attendedRecord.Repository, AskedBy(attended, parts, attendedRecord, stores, byId));
    }

    /// <summary>A chain stays whole, and the chains with work still open come before those that are over.</summary>
    private static AtWorkAsked Built(List<List<QuestView>> chains, Stores stores, Dictionary<string, QuestView> byId, string? repository, AtWorkAsker? by)
    {
        var drawn = chains.Where(chain => !chain.All(Over)).Concat(chains.Where(chain => chain.All(Over))).SelectMany(chain => chain).ToList();
        var rows = drawn.Take(AtWork.Bound).ToList();

        var units = QuestUnits.Read(rows, stores.Sessions).ToDictionary(unit => unit.Records.Quest!.Id, StringComparer.Ordinal);
        var sessions = stores.Sessions.GroupBy(session => session.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return new AtWorkAsked([.. rows.Select(quest => Row(quest, units[quest.Id], sessions, stores, byId, repository))], drawn.Count - rows.Count, by);
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
        QuestView quest, WorkUnit unit, IReadOnlyDictionary<string, SessionRecord> sessions, Stores stores, IReadOnlyDictionary<string, QuestView> byId, string? repository)
    {
        var taker = Taker(quest, unit, sessions, stores);
        return new AtWorkAskedQuest(quest.Id, quest.Name, quest.Status)
        {
            Of = quest.To.Length > 0 && !string.Equals(quest.To, repository, StringComparison.Ordinal) ? quest.To : null,
            Lanes = quest.Lanes.Count > 0 ? quest.Lanes : null,
            Step = unit.Links.FirstOrDefault(link => link.Relation == WorkRelation.Follows)?.Unit[QuestUnits.IdOf("").Length..],
            Waits = Waits(quest, taker, stores, byId),
            Taker = taker,
        };
    }

    /// <summary>Open: nobody yet. Else the newest session that serves it, a conversation that took it, its holder's machine, or nobody recorded.</summary>
    private static AtWorkTaker? Taker(QuestView quest, WorkUnit unit, IReadOnlyDictionary<string, SessionRecord> sessions, Stores stores)
    {
        if (quest.Status == "Open") return new AtWorkTaker { Untaken = true };
        if (unit.Executors.Count > 0 && sessions.GetValueOrDefault(unit.Executors[^1]) is { } serving)
        {
            return new AtWorkTaker { Session = serving.Id, Agent = serving.Adapter, State = serving.State };
        }

        var chat = stores.Sessions.Where(session => string.Equals(session.TookQuest, quest.Id, StringComparison.Ordinal)).OrderBy(session => session.Created).LastOrDefault();
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
    private static AtWorkWait? Waits(QuestView quest, AtWorkTaker? taker, Stores stores, IReadOnlyDictionary<string, QuestView> byId)
    {
        string? question = null;
        string? of = null;
        if (taker?.Session is { } session)
        {
            if (stores.Row(session) is not { Awaits: { Length: > 0 } awaits } row) return null;
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
    private static AtWorkAsker? AskedBy(string attended, IReadOnlyList<AtWorkEntryPart> parts, AtWorkRecord record, Stores stores, IReadOnlyDictionary<string, QuestView> byId)
    {
        var serving = stores.Sessions.FirstOrDefault(session => session.Id == attended)?.Quest;
        var id = parts.FirstOrDefault(part => part.Session == attended)?.Quest ?? serving ?? record.TookQuest;
        if (id is null || byId.GetValueOrDefault(id) is not { } quest) return null;
        var by = quest.PublishedBy is { Length: > 0 } publisher ? new AtWorkBy { Session = publisher }
            : quest.Parent is { Length: > 0 } parent ? new AtWorkBy { Step = parent }
            : AskWords.AskOf(quest.From) is { Length: > 0 } ask ? new AtWorkBy { Ask = ask }
            : new AtWorkBy { Person = true };
        return new AtWorkAsker(quest.Id, by);
    }
}
