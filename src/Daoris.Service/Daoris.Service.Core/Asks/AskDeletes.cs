namespace Daoris.Knowledge;

/// <summary>Deleting an ask made by mistake (D95), with every quest asked by it.</summary>
internal sealed class AskDeletes(AskStore asks, QuestExchange exchange, QuestFiles? askFiles, AskStandings standings)
{
    /// <summary>
    /// Delete an ask made by mistake (D95), with every quest asked by it — or none of it, when any of
    /// those quests must stay. An ask that became nothing goes alone. The files it kept go with it.
    /// </summary>
    public async Task<AskOutcome> DeleteAsync(string id, DateTimeOffset now, CancellationToken ct)
    {
        var ask = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (ask is null) return new(AskRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);

        const string Instead =
            "An ask is deleted only with every quest asked by it — close the ask instead, with the reason, and it leaves the list.";
        var asked = await exchange.Store.FromAsync(AskDesk.SenderOf(ask.Id), ct: ct).ConfigureAwait(false);
        foreach (var quest in asked)
        {
            if (await exchange.KeptAsync(quest, ct).ConfigureAwait(false) is { } kept)
            {
                return new(
                    AskRefusal.Kept,
                    $"Ask `#{ask.Id}` stays, with its quests: quest `#{quest.Id}` {kept.Stands}. {Instead}",
                    await standings.FindAsync(ask.Id, ct).ConfigureAwait(false));
            }
        }

        var deleted = new List<string>();
        var notes = new List<string>();
        foreach (var quest in asked)
        {
            var outcome = await exchange.DeleteAsync(quest.Id, now, ct, askGoesToo: true).ConfigureAwait(false);
            if (outcome.Refusal == QuestDeleteRefusal.None)
            {
                deleted.Add($"`#{quest.Id}`");
                if (outcome.Unconfirmed) notes.Add(outcome.Message);
                continue;
            }

            // It moved while the ask was being deleted — taken on another machine first, most likely.
            // The ask stays, with whatever is left of it, and the answer says what did go.
            return new(
                AskRefusal.Kept,
                $"Ask `#{ask.Id}` stays: {outcome.Message}"
                + (deleted.Count > 0 ? $" Deleted before it: {string.Join(", ", deleted)}." : ""),
                await standings.FindAsync(ask.Id, ct).ConfigureAwait(false));
        }

        await asks.DeleteAsync(ask.Id, ct).ConfigureAwait(false);
        askFiles?.Forget(ask.Id);
        var message = $"Deleted ask `#{ask.Id}`"
                      + (deleted.Count == 0 ? "." : $", and the quests asked by it with it: {string.Join(", ", deleted)}.");
        return new(AskRefusal.None, string.Join("\n\n", [message, .. notes]), Ask: null);
    }
}
