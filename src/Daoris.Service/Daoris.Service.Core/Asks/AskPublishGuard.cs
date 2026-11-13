namespace Daoris.Knowledge;

/// <summary>
/// An ask asks each receiver once: another hand's publish to a receiver the ask already holds a quest for, open, taken or done, is answered
/// with that quest and publishes nothing, as the exchange answers a retry; and a session's publish beside the ask's intake is refused while the intake
/// answers it, or before any intake has served it, since the intake publishes it. The person's own press is never refused for the intake.
/// </summary>
internal sealed class AskPublishGuard(QuestExchange exchange, SessionStore? sessions)
{
    /// <summary>Said of a quest an ask became that carries no requirement, so whoever published it knows its done is checked against nothing.</summary>
    public const string Unrequired =
        "\n\nIt carries no requirement of the person's, so nothing checks its done against what was asked: a run's `quest.holds` keeps that "
        + "done waiting, unchecked, until the person reads its handback.";

    /// <summary>The states an intake answers its ask in: opened and neither parked nor ended.</summary>
    private static readonly SessionState[] Answering = [SessionState.Queued, SessionState.Starting, SessionState.Working];

    /// <summary>The answer in place of a publish of <paramref name="ask"/> to <paramref name="to"/>, or null where the publish goes on.</summary>
    /// <param name="session">The session publishing, where one is named; null for the person's press.</param>
    public async Task<AskOutcome?> JudgeAsync(Ask ask, string to, string? session, CancellationToken ct, bool part = false)
    {
        var address = QuestAddress.Parse(to.Trim());
        var by = string.IsNullOrWhiteSpace(session) ? null : session.Trim();
        var asked = await exchange.Store.FromAsync(AskDesk.SenderOf(ask.Id), ct: ct).ConfigureAwait(false);
        // Another hand's publish to a receiver already asked is the same work asked twice; one publisher's several quests to one receiver
        // are its pieces of the work, and its exact retry is the exchange's to answer; a plan's part carries its own key, so it is never the same work twice.
        if (!part && asked.FirstOrDefault(quest => quest.Status != QuestStatus.Declined && Same(quest, address)
                && !string.Equals(quest.PublishedBy, by, StringComparison.Ordinal)) is { } first)
        {
            return new(
                AskRefusal.None,
                $"Ask `#{ask.Id}` already became quest `#{first.Id}` to `{QuestAddress.Spell(first.To, first.Lanes)}`, which is still "
                + $"{first.Status}: this is that quest, and nothing new was published. An ask asks each receiver once; say more on that "
                + "quest, or ask anew.",
                ask, first);
        }

        if (session is not { Length: > 0 } || sessions is null) return null;
        var publisher = await sessions.FindAsync(session, ct).ConfigureAwait(false);
        if (string.Equals(session, ask.Intake, StringComparison.Ordinal) || publisher?.Ask == ask.Id) return null;

        if (ask.Intake is { } intake && await sessions.FindAsync(intake, ct).ConfigureAwait(false) is { } answering
            && Answering.Contains(answering.State))
        {
            return new(
                AskRefusal.IntakeAnswering,
                $"Ask `#{ask.Id}`'s intake session `{intake}` is answering it and publishes its quests, so nothing was published: a "
                + "second publish would ask its receivers twice. Its answer comes on the ask, and where the declarations do not settle it, "
                + "it asks the person.",
                ask);
        }

        if (publisher is not null && ask is { State: AskState.Proposed, Intake: null } && ask.Tier != AskDesk.ByName && asked.Count == 0)
        {
            return new(
                AskRefusal.IntakeAnswering,
                $"Ask `#{ask.Id}` waits for its intake: where Intake is set on this machine the driver starts one at its next look, and "
                + "the intake publishes its quests, so nothing was published. A session does not publish an ask the declarations left "
                + "open before an intake has served it; propose the receiver to the person, whose press publishes it.",
                ask);
        }

        return null;
    }

    /// <summary>Whether <paramref name="quest"/> is to the receiver <paramref name="address"/> names: its repository and the same lanes.</summary>
    private static bool Same(Quest quest, QuestAddress address) =>
        string.Equals(quest.To, address.Repository.Trim(), StringComparison.OrdinalIgnoreCase)
        && quest.Lanes.Order(StringComparer.Ordinal).SequenceEqual(address.Lanes.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
