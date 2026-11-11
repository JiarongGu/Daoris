namespace Daoris.Knowledge;

/// <summary>What an ask is now, read against the quests asked by it.</summary>
internal sealed class AskStandings(AskStore asks, QuestExchange exchange)
{
    /// <summary>
    /// A circle's asks, or every circle's, newest first, each as it STANDS — a done one only
    /// when closed ones are asked for, as a closed one is.
    /// </summary>
    public async Task<IReadOnlyList<Ask>> ListAsync(string? workspace, bool includeClosed, CancellationToken ct)
    {
        var stored = await asks.ListAsync(workspace, includeClosed, ct).ConfigureAwait(false);
        // One read for every ask's quests: a sender that is an ask always begins the same way.
        var everyAsked = await exchange.Store.FromAsync(AskDesk.SenderOf(""), startingWith: true, ct).ConfigureAwait(false);
        var deletable = await exchange.DeletableAsync(everyAsked, ct).ConfigureAwait(false);
        var asked = everyAsked.ToLookup(quest => quest.From, StringComparer.Ordinal);
        var standing = stored.Select(ask => Stood(ask, [.. asked[AskDesk.SenderOf(ask.Id)]], deletable));
        return [.. includeClosed ? standing : standing.Where(ask => ask.State != AskState.Done)];
    }

    /// <summary>One ask as it stands, or null when there is none.</summary>
    public async Task<Ask?> FindAsync(string id, CancellationToken ct)
    {
        if (await asks.FindAsync(id, ct).ConfigureAwait(false) is not { } ask) return null;
        var asked = await exchange.Store.FromAsync(AskDesk.SenderOf(ask.Id), ct: ct).ConfigureAwait(false);
        return Stood(ask, asked, await exchange.DeletableAsync(asked, ct).ConfigureAwait(false));
    }

    /// <summary>An ask as a reader is given it: as it stands, and whether it may be deleted.</summary>
    private static Ask Stood(Ask ask, IReadOnlyList<Quest> asked, IReadOnlySet<string> deletable) =>
        Standing(ask, asked) with { Deletable = asked.All(quest => deletable.Contains(quest.Id)) };

    /// <summary>
    /// What an ask is, read against the quests asked by it: a published ask whose every quest
    /// has closed is <see cref="AskState.Done"/>. Derived on every read and never stored, because the
    /// quests can move on another machine, and the sync that brings the move here knows nothing of
    /// asks. One judgement, for every reader of an ask's state.
    /// </summary>
    /// <remarks>
    /// A quest deleted, here or on another machine, leaves the record as if the ask had never
    /// become it; a published ask left with none is a proposal again, for a person to publish or close —
    /// unless the person NAMED its receiver (<see cref="AskDesk.ByName"/>). Its routing is their
    /// word, so the proposal of the declarations tier is not a route for it: it stands with no quest and
    /// says so in its note, and no intake serves it — only the person publishes it again, or closes it.
    /// A quest closed done departing from what the person required has not closed for the ask: it waits for their yes, and
    /// after it still leaves what was not done (<see cref="Quest.Left"/>), so an ask reading done would say it was finished.
    /// </remarks>
    /// <param name="asked">Every quest asked by this ask — chain steps included, closed ones included.</param>
    public static Ask Standing(Ask ask, IReadOnlyList<Quest> asked)
    {
        var held = asked.Select(quest => quest.Id).ToHashSet(StringComparer.Ordinal);
        var standing = ask.Quests.All(held.Contains) ? ask : ask with { Quests = [.. ask.Quests.Where(held.Contains)] };
        if (standing.State != AskState.Published) return standing;
        if (asked.Count == 0)
        {
            return standing with
            {
                State = AskState.Proposed,
                Note = standing.Tier == AskDesk.ByName ? standing.Note ?? NoQuestLeft(standing) : standing.Note,
            };
        }

        return asked.All(quest => quest.Status is QuestStatus.Done or QuestStatus.Declined && !quest.Held && !quest.Left)
            ? standing with { State = AskState.Done }
            : standing;
    }

    /// <summary>
    /// What stands of an ask whose last quest is deleted, in a sentence: said by the ask's note where the
    /// person named its receiver, and by the answer to the delete of its quest for every tier, so no door is silent about it.
    /// </summary>
    public static string NoQuestLeft(Ask ask) => ask switch
    {
        { Tier: AskDesk.ByName } =>
            $"Ask `#{ask.Id}` stands with no quest: you named its receiver, and the quest it became was deleted. It is not "
            + "published again, and no intake routes it by its proposal — publish it to a receiver you name, or close it.",
        { Intake: not null } =>
            $"Ask `#{ask.Id}` stands with no quest: its intake session already served it, so nothing routes it again — "
            + "publish it to a receiver you name, or close it.",
        _ =>
            $"Ask `#{ask.Id}` has no quest left and is a proposal again: an intake may route it by what the declarations "
            + "propose, or you publish it to a receiver you name, or close it.",
    };
}
