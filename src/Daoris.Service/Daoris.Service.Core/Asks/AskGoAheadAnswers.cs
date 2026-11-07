namespace Daoris.Knowledge;

/// <summary>The person's answer to a go-ahead their sessions asked for on an ask.</summary>
internal sealed class AskGoAheadAnswers(AskStore asks, AskStandings standings)
{
    /// <summary>
    /// Keep the person's answer to go-ahead <paramref name="number"/> on ask <paramref name="id"/>: yes or no, with their
    /// words where they gave any. A later answer replaces an earlier one, since the latest is what they now say; every
    /// session on the ask is handed it at its next start.
    /// </summary>
    /// <remarks>
    /// <b>The person's alone</b>: the production acts stay theirs, so no connector tool answers a go-ahead, and a
    /// local host's door is the only one, as the ask routes are.
    /// </remarks>
    public async Task<GoAheadAnswerOutcome> AnswerAsync(
        string id, int number, bool approved, string? words, DateTimeOffset now, CancellationToken ct)
    {
        var said = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        if (said is { Length: > GoAheads.WordsLimit })
        {
            return new(GoAheadAnswerRefusal.TooLong,
                $"An answer's words are at most {GoAheads.WordsLimit} characters; these are {said.Length}. Nothing was kept.",
                await standings.FindAsync(id, ct).ConfigureAwait(false));
        }

        var answer = new GoAheadAnswer(approved, said, now);
        var (found, decided) = await asks.DecideGoAheadsAsync<(GoAhead? Answered, bool WasWaiting, IReadOnlyList<int> Numbers)>(id, entries =>
        {
            var held = entries.Select(entry => entry.Read).OfType<GoAhead>().FirstOrDefault(goAhead => goAhead.Number == number);
            if (held is null) return (null, (null, false, [.. entries.Select(entry => entry.Read?.Number).OfType<int>()]));
            GoAhead? answered = held with { Answer = answer };
            IReadOnlyList<(System.Text.Json.JsonElement Raw, GoAhead? Read)> next =
                [.. entries.Select(entry => entry.Read?.Number == number ? (entry.Raw, answered) : entry)];
            // Read as stored, in the same transaction: whether this is the person's first answer to it.
            return (next, (answered, held.Answer is null, []));
        }, now, ct).ConfigureAwait(false);

        var ask = found ? await standings.FindAsync(id, ct).ConfigureAwait(false) : null;
        if (!found || ask is null) return new(GoAheadAnswerRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);

        if (decided.Answered is not { } kept)
        {
            return new(GoAheadAnswerRefusal.NoGoAhead,
                decided.Numbers.Count == 0
                    ? $"Ask `#{ask.Id}` holds no go-ahead {number}: no session has asked for one on it."
                    : $"Ask `#{ask.Id}` holds no go-ahead {number}: it holds {string.Join(", ", decided.Numbers)}.",
                ask);
        }

        return new(GoAheadAnswerRefusal.None,
            $"Go-ahead {kept.Number} on ask `#{ask.Id}` {(approved ? "approved" : "refused")}: {kept.Named}. Every session on the "
            + "ask is handed your answer at its next start" + (said is null ? "." : ", with your words."),
            ask)
        {
            GoAhead = kept,
            WasWaiting = decided.WasWaiting,
        };
    }
}
