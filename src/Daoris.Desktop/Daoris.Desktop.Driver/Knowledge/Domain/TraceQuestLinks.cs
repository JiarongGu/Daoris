using static Daoris.Driver.TraceLookups;

namespace Daoris.Driver;

/// <summary>The quest link of a chain, as its operations replay.</summary>
internal static class TraceQuestLinks
{
    /// <summary>The quest as its operations replay, from its door: where it came from, its requirements and answers, its records.</summary>
    internal static TraceQuestLink Quest(string id, TracedQuest? quest, string? askId, TraceNamedBy namedBy, TraceFacts facts)
    {
        var records = RecordsOn(id, facts);
        if (quest is null)
        {
            return new TraceQuestLink(id)
            {
                Missing = facts.QuestsUnread is not null ? TraceLinkGaps.Unread : TraceLinkGaps.NotFound,
                NamedBy = namedBy,
                Records = records,
            };
        }

        return new TraceQuestLink(quest.Id)
        {
            NamedBy = namedBy,
            Address = quest.Address,
            Title = quest.Title,
            Status = quest.Status,
            Filed = quest.Filed,
            Moved = quest.Updated,
            Ask = askId,
            From = quest.From,
            Parent = quest.Parent,
            PublishedBy = quest.PublishedBy,
            Awaits = quest.Awaits,
            Held = quest.Held,
            Hold = quest.Hold,
            Evidence = quest.Evidence is { } verdict
                ? new TraceEvidenceRead(verdict.Commit, verdict.How, verdict.Items.Count, verdict.Count(EvidenceCodes.Found))
                {
                    Session = verdict.Session, Machine = verdict.Machine, At = verdict.At,
                }
                : null,
            Accepted = quest.Accepted,
            Note = quest.Note,
            NoteParts = quest.NoteParts,
            Requirements =
            [
                .. quest.Requirements.Select((requirement, index) =>
                {
                    var number = index + 1;
                    var answer = quest.Answers.FirstOrDefault(each => each.Requirement == number);
                    var required = answer switch
                    {
                        { Met: { } met } => new TraceRequirement(number, requirement.Quote, requirement.Check, TraceAnswers.Met) { Met = met },
                        { Departed: { } departed } => new TraceRequirement(number, requirement.Quote, requirement.Check, TraceAnswers.Departed)
                        {
                            Departed = departed,
                            On = answer.Quote,
                        },
                        _ => new TraceRequirement(number, requirement.Quote, requirement.Check,
                            quest.Status == "Done" ? TraceAnswers.Unanswered : TraceAnswers.NotYet),
                    };
                    return required with { Evidence = [.. requirement.Evidence.Select(item => EvidenceItem(number, item, quest.Evidence))] };
                }),
            ],
            Then = [.. quest.Then.Select(step => new TraceStep(step.To, step.Title))],
            Records = records,
            Moves = facts.Moves.GetValueOrDefault(quest.Id)?.Moves,
            MovesMissing = facts.Moves.GetValueOrDefault(quest.Id) switch
            {
                { NoDoor: true } => TraceMovesGaps.NoDoor,
                { Unread: not null } => TraceMovesGaps.Unread,
                _ => null,
            },
            MovesProblem = facts.Moves.GetValueOrDefault(quest.Id)?.Unread,
        };
    }

    /// <summary>One item a requirement names, with what the quest's last verdict read of it; unread where it read none.</summary>
    private static TraceEvidenceItem EvidenceItem(int requirement, QuestEvidenceItem item, EvidenceVerdict? verdict) =>
        verdict?.Items.FirstOrDefault(read => read.Requirement == requirement && read.Path == item.Path && read.Gate == item.Gate) is { } read
            ? new TraceEvidenceItem(item.Kind, item.Named) { Result = read.Result, Object = read.Object, Changed = read.Changed, Spelled = read.Spelled }
            : new TraceEvidenceItem(item.Kind, item.Named);

    /// <summary>The records that name a quest, oldest first, by id and state; null where the session records could not be read.</summary>
    private static IReadOnlyList<TraceRecordRef>? RecordsOn(string questId, TraceFacts facts) =>
        facts.SessionsUnread is not null
            ? null
            : [.. facts.Sessions.Where(session => Same(session.Quest, questId))
                .OrderBy(session => session.Created ?? DateTimeOffset.MaxValue)
                .Select(session => new TraceRecordRef(session.Id, session.State))];
}
