namespace Daoris.Driver;

/// <summary>Take back and reword: words waiting on a record, before a session took them.</summary>
internal static class SessionsQueueEdit
{
    /// <summary>
    /// One word waiting on a parked or ended record, taken back or reworded through the service's take-back door, which judges it;
    /// with no word named, the words waiting listed under their ids. Where no loop drives the home, the record's conversation
    /// settles the word it showed waiting with the withdrawn line and shows a reword's new words as the terminal's, as
    /// <see cref="SessionsKeeping.KeepAsync"/> shows words it keeps; where a loop does, its record of the conversation may be open,
    /// so nothing is written there. Exit 0 done or listed, 1 refused (<c>taken</c> among them), 2 could not.
    /// </summary>
    internal static async Task<int> TakeBackAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        var id = ask.Ids[0];
        if (ask.Word is not { } word)
        {
            if (await world.Service.RecordAsync(id, ct).ConfigureAwait(false) is not { } record)
            {
                output.WriteLine($"sessions: no session of this machine's is {id}.");
                return 1;
            }

            var waiting = record.Waiting.Where(each => each.Id is not null).ToList();
            if (waiting.Count == 0)
            {
                output.WriteLine($"sessions: no words wait on {id}.");
                return 0;
            }

            output.WriteLine(
                $"sessions: {waiting.Count} word{(waiting.Count == 1 ? " waits" : "s wait")} on {id}; take one back with "
                + $"`daoris session take-back {id} <word>`:");
            foreach (var each in waiting) output.WriteLine($"  {each.Id}  {OneLine(each.Text)}");
            return 0;
        }

        var text = ask.Verb == "reword" ? ask.Text : null;
        if (world.LoopRuns())
        {
            // The loop that holds the home writes the note into its own record of the conversation, which a page may have open; so
            // it is asked through the request folder, as a say is. One that takes none in time is withdrawn, and the service judges
            // it here, with nothing written.
            var held = await AskLoopTakeBackAsync(world, id, word, text, ct).ConfigureAwait(false);
            if (held is not null)
            {
                output.WriteLine($"sessions: {held.Message}");
                return held.Sent ? 0 : held.Why is null ? 2 : 1;
            }
        }

        var answer = await world.Service.TakeBackAsync(id, word, text, ct).ConfigureAwait(false);
        if (answer.Done && !world.LoopRuns())
        {
            var events = new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions));
            events.Keep(id, SessionNotes.Withdrawn(word), null);
            if (answer.Word is { } kept) events.Keep(id, LoopWords.Shown(kept, RequestDoor.Terminal), null);
        }

        output.WriteLine($"sessions: {answer.Message}");
        return answer.Done ? 0 : answer.Why is null ? 2 : 1;

        // A word's text on the listing's one line: its lines joined by a space, cut after a hundred characters.
        static string OneLine(string text)
        {
            var line = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return line.Length <= 100 ? line : line[..99] + "…";
        }
    }

    /// <summary>
    /// A take-back asked of the loops on this home and waited for, as a say is: the loop's answer, or null where none took it in time,
    /// when it is withdrawn so no loop acts on it after the verb went to the service itself. A loop that took it is waited for until
    /// it answers, within <see cref="SessionsWorld.Answer"/>, since the word is gone by then and asking the service again would only
    /// say it is no longer there.
    /// </summary>
    private static async Task<WordsHeld?> AskLoopTakeBackAsync(
        SessionsWorld world, string id, string word, string? text, CancellationToken ct)
    {
        var requests = new SessionRequests(world.Home);
        var request = new SessionRequest(id, SessionMove.TakeBack, DateTimeOffset.UtcNow)
        {
            Text = text, Word = word, Key = SessionRequests.NewKey(),
        };
        requests.Write(request);

        var waited = Waited.From(world);
        WordsHeld? held = null;
        while (held is null && waited.Elapsed < world.Wait)
        {
            await Task.Delay(world.Poll, ct).ConfigureAwait(false);
            held = requests.AnswerOf(request);
        }

        if (held is null && !requests.Withdraw(request)) held = await SessionsSaying.AnsweredAsync(world, requests, request, ct).ConfigureAwait(false);
        return held is null ? null : held with { Message = held.Message ?? (held.Sent ? $"took word `{word}` back." : $"word `{word}` was not taken back.") };
    }
}
