using static Daoris.Driver.TraceLookups;

namespace Daoris.Driver;

/// <summary>
/// The chain, read from the stores' facts (TRACE1b): each link from the store that keeps it, a link nothing keeps with why, and
/// what stood at each start by the moments kept. Both doors word what this returns.
/// </summary>
internal static class TraceChains
{
    /// <summary>Each store that did not answer, once: its links are then said not read.</summary>
    internal static IReadOnlyList<TraceUnread> Unread(TraceFacts facts)
    {
        var unread = new List<TraceUnread>();
        if (facts.SessionsUnread is { } sessions) unread.Add(new TraceUnread(TraceStores.Sessions) { Problem = sessions });
        if (facts.QuestsUnread is { } quests) unread.Add(new TraceUnread(TraceStores.Quests) { Problem = quests });
        if (facts.LandingsUnread is { } landings) unread.Add(new TraceUnread(TraceStores.Landings) { Problem = landings });
        return unread;
    }

    /// <summary>The asks the chain names, in the order it reaches them: each quest's sender's, then an intake's own.</summary>
    internal static IReadOnlyList<string> AsksNamed(TraceFound found, TraceFacts facts) =>
    [
        .. found.Quests.Select(id => QuestOf(facts, id)).OfType<TracedQuest>().Select(quest => AskSender.AskOf(quest.From))
            .Concat(found.Sessions.Where(session => session.Quest is null).Select(session => session.Ask))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>The chain, whole: each ask, its quests and their sessions, in the order the terminal prints them.</summary>
    internal static TraceChain Of(
        TraceFound found, TraceFacts facts, IReadOnlyDictionary<string, TraceAskRead> asks, TraceHere here, IReadOnlyList<TraceUnread> unread)
    {
        var links = new List<TraceLink>();
        var reached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var questId in found.Quests)
        {
            var quest = QuestOf(facts, questId);
            var askId = quest is null ? null : AskSender.AskOf(quest.From);
            if (askId is not null && reached.Add(askId))
            {
                links.Add(new TraceLink(TraceLink.AskKind) { Ask = Ask(asks[askId], new TraceNamedBy(TraceNamers.Quest, quest!.Id), facts) });
            }

            var namedBy = found.Sessions.FirstOrDefault(session => Same(session.Quest, questId)) is { } by ? new TraceNamedBy(TraceNamers.Session, by.Id)
                : facts.Landings.FirstOrDefault(landing => Same(landing.Quest, questId)) is { } landing ? new TraceNamedBy(TraceNamers.Landing, landing.Session)
                : new TraceNamedBy(TraceNamers.Trace);
            links.Add(new TraceLink(TraceLink.QuestKind) { Quest = TraceQuestLinks.Quest(questId, quest, askId, namedBy, facts) });

            foreach (var session in found.Sessions.Where(session => Same(session.Quest, questId)))
            {
                links.Add(new TraceLink(TraceLink.SessionKind) { Session = TraceSessionLinks.Session(session, facts, askId is null ? null : asks[askId], here) });
            }

            foreach (var landed in found.Unrecorded.Where(id => facts.Landings.Any(each => Same(each.Session, id) && Same(each.Quest, questId))))
            {
                links.Add(new TraceLink(TraceLink.UnrecordedKind) { Unrecorded = TraceLandings.Unrecorded(landed, facts) });
            }
        }

        foreach (var session in found.Sessions.Where(session => session.Quest is null))
        {
            if (session.Ask is { } askId && reached.Add(askId))
            {
                links.Add(new TraceLink(TraceLink.AskKind) { Ask = Ask(asks[askId], new TraceNamedBy(TraceNamers.Intake, session.Id), facts) });
            }

            links.Add(new TraceLink(TraceLink.SessionKind) { Session = TraceSessionLinks.Session(session, facts, null, here) });
        }

        foreach (var landed in found.Unrecorded.Where(id => !facts.Landings.Any(each => Same(each.Session, id) && each.Quest is not null)))
        {
            links.Add(new TraceLink(TraceLink.UnrecordedKind) { Unrecorded = TraceLandings.Unrecorded(landed, facts) });
        }

        return new TraceChain(found.Kind, found.Id) { Found = found.Found, Unread = unread, Links = links };
    }

    /// <summary>A tree's last segment: the name Daoris gave it (D51 §2), never the path, which is machine-local.</summary>
    internal static string? FolderOf(string? tree) => TraceLookups.FolderOf(tree);

    /// <summary>The ask, from its door: the person's words verbatim with how and when each was given, and its go-aheads.</summary>
    private static TraceAskLink Ask(TraceAskRead read, TraceNamedBy namedBy, TraceFacts facts)
    {
        if (read.Unread is { } why) return new TraceAskLink(read.Id) { Missing = TraceLinkGaps.Unread, Problem = why, NamedBy = namedBy };
        if (read.Ask is not { } ask) return new TraceAskLink(read.Id) { Missing = TraceLinkGaps.NotFound, NamedBy = namedBy };

        return new TraceAskLink(ask.Id)
        {
            NamedBy = namedBy,
            Workspace = ask.Workspace,
            State = ask.State,
            Tier = ask.Tier,
            Intake = ask.Intake is { } intake
                ? SessionOf(facts, intake) is { } record
                    ? new TraceIntake(intake) { State = StateOf(record), Agent = AgentOf(record) }
                    : new TraceIntake(intake)
                : null,
            Quests = ask.Quests,
            Note = ask.Note is { Length: > 0 } note ? note : null,
            Sentence = ask.Words is null ? ask.Sentence : null,
            Words = ask.Words,
            WordsKeptFrom = ask.Words is null ? null : ask.WordsKeptFrom,
            GoAheads = ask.GoAheads?.Select(goAhead =>
            {
                var first = goAhead.Asked.MinBy(request => request.At);
                return new TraceGoAhead(goAhead.Number, goAhead.Kind, goAhead.On, goAhead.Act)
                {
                    Answer = goAhead.Answer,
                    FirstAskedBy = first?.Session,
                    FirstAskedAt = first?.At,
                    Near = goAhead.Near,
                };
            }).ToList(),
        };
    }
}
