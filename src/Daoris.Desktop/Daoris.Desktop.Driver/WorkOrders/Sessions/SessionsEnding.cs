namespace Daoris.Driver;

/// <summary>Stop, finish and decline: a session another process runs is reached through a request its loop honours, and one nothing runs is moved by the ledger directly.</summary>
internal static class SessionsEnding
{
    /// <summary>
    /// <c>stop &lt;id&gt; --turn</c>, the keeping stop: the session's turn stopped so what waits goes next, through the Daoris whose
    /// loop holds its turns, and what it came to said. Only that Daoris holds a turn, so alone, with no seam, the verb could not:
    /// exit 2. A door with no stop that keeps the session, or no turn to stop, is a policy answer, exit 1; a door that carries only
    /// text refuses in the driver's words.
    /// </summary>
    internal static async Task<int> StopTurnAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        if (world.StopTurn is not { } stopTurn)
        {
            output.WriteLine(
                $"sessions: the Daoris that runs {id} holds its turns and did not answer here; stop its turn in Daoris's window, beside the words that wait.");
            return 2;
        }

        string came;
        try
        {
            came = await stopTurn(id, ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            output.WriteLine($"sessions: {refused.Message}");
            return 1;
        }

        output.WriteLine(came switch
        {
            KeepingStops.Stopped => $"sessions: stopping {id}'s turn: what waits goes next, and what it has done stays.",
            KeepingStops.NoInterrupt =>
                $"sessions: {id}'s door cannot stop a turn and keep the session, so nothing was stopped: what waits goes when its turn ends, "
                + $"and `daoris session stop {id}` ends the session.",
            KeepingStops.NotHere => $"sessions: nothing here runs {id}, so it has no turn to stop.",
            _ => $"sessions: {id} has no turn running with words waiting for its end, so nothing was stopped.",
        });
        return came == KeepingStops.Stopped ? 0 : 1;
    }

    internal static async Task<int> StopAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        var record = await RecordAsync(world, id, output, ct).ConfigureAwait(false);
        if (record is null) return 1;
        if (!record.Live)
        {
            output.WriteLine($"sessions: {id} has already ended ({SessionsListing.Word(record.State)}).");
            return 1;
        }

        var parked = record.State == "awaiting-person";
        if (world.Processes.AliveOnThisMachine(id))
        {
            return await AskAsync(
                    world, record, new SessionRequest(id, SessionMove.Stop, DateTimeOffset.UtcNow) { Parked = parked }, "stopped",
                    output, ct)
                .ConfigureAwait(false);
        }

        if (parked)
        {
            try
            {
                await SessionMoves.ResolveAsync(
                        _ => false, world.Service, id, "stopped", note: null, ct)
                    .ConfigureAwait(false);
            }
            catch (DriverException refused)
            {
                output.WriteLine($"sessions: {refused.Message}");
                return 1;
            }

            output.WriteLine($"sessions: stopped {id}: it waited on you with no process left, so the ledger ended it unanswered.");
            Held(record, output);
            return 0;
        }

        var answer = await SessionMoves.StopAsync(world.Processes, world.Service, id, ct).ConfigureAwait(false);
        if (answer.Orphan)
        {
            output.WriteLine($"sessions: stopped {id}: nothing on this machine ran it any more, so its record is ended as your stop.");
            Held(record, output);
            return 0;
        }

        output.WriteLine(answer.Elsewhere
            ? $"sessions: another Daoris process on this machine began running {id} as it was asked; ask again."
            : $"sessions: {id} is {SessionsListing.Word(record.State)}, and nothing on this machine runs it yet to stop.");
        return answer.Elsewhere ? 2 : 1;
    }

    internal static async Task<int> ResolveAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        var id = ask.Ids[0];
        var record = await RecordAsync(world, id, output, ct).ConfigureAwait(false);
        if (record is null) return 1;
        if (record.Ask is { } asked)
        {
            output.WriteLine($"sessions: {id} is an intake; answer its ask #{asked} instead: publish it or close it.");
            return 1;
        }

        if (record.State != "awaiting-person")
        {
            output.WriteLine($"sessions: {id} is {SessionsListing.Word(record.State)}, not waiting on you; finish and decline answer a session that waits on you.");
            return 1;
        }

        var (move, state, past) = ask.Verb == "finish"
            ? (SessionMove.Finish, "completed", "finished")
            : (SessionMove.Decline, "declined", "declined");
        int exit;
        if (world.Processes.AliveOnThisMachine(id))
        {
            exit = await AskAsync(world, record, new SessionRequest(id, move, DateTimeOffset.UtcNow) { Note = ask.Note, Parked = true }, past, output, ct)
                .ConfigureAwait(false);
        }
        else
        {
            try
            {
                var said = await SessionMoves.ResolveAsync(_ => false, world.Service, id, state, ask.Note, ct).ConfigureAwait(false);
                output.WriteLine($"sessions: {past} {id}: {said}");
                exit = 0;
            }
            catch (DriverException refused)
            {
                output.WriteLine($"sessions: {refused.Message}");
                return 1;
            }
        }

        if (exit == 0 && move == SessionMove.Finish) await StillOpenAsync(world, record, output, ct).ConfigureAwait(false);
        return exit;
    }

    /// <summary>
    /// A finish at a checkpoint leaves its quest as it stands: where that is still open or taken, said right after it with the door that
    /// closes it as the person's, since nothing here carries a finished session's quest on. A quest that cannot be read says nothing:
    /// the finish stood, and <c>quest done</c> answers for itself.
    /// </summary>
    private static async Task StillOpenAsync(SessionsWorld world, SessionRecord record, TextWriter output, CancellationToken ct)
    {
        if (record.Quest is not { } id) return;
        QuestView? quest;
        try
        {
            quest = await world.Service.FindQuestAsync(id, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return;
        }

        if (quest?.Status is "Open" or "Taken")
        {
            output.WriteLine(
                $"  its quest #{quest.Id} is still {quest.Status.ToLowerInvariant()}: daoris quest done {quest.Id} [--note \"…\"] "
                + "marks it done as yours.");
        }
    }

    /// <summary>
    /// Ask the loop that runs a session, and wait for its record to move: what happened is said, and a request nothing took in time
    /// is withdrawn so no loop acts on it after the person is told.
    /// </summary>
    private static async Task<int> AskAsync(
        SessionsWorld world, SessionRecord record, SessionRequest request, string past, TextWriter output, CancellationToken ct)
    {
        var requests = new SessionRequests(world.Home);
        requests.Write(request);
        var waited = Waited.From(world);
        while (waited.Elapsed < world.Wait)
        {
            await Task.Delay(world.Poll, ct).ConfigureAwait(false);
            var now = (await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Id, record.Id, StringComparison.Ordinal));
            if (now is not null && now.State == record.State) continue;

            output.WriteLine($"sessions: {past} {record.Id} — the loop that runs it took the request, and its record says {SessionsListing.Word(now?.State ?? "gone")}.");
            if (request.Move == SessionMove.Stop) Held(record, output);
            return 0;
        }

        var seconds = (int)Math.Round(world.Wait.TotalSeconds);
        output.WriteLine(requests.Withdraw(record.Id)
            ? $"sessions: nothing that runs {record.Id} took the request within {seconds} seconds, so it was withdrawn: it runs in "
              + "another Daoris process on this machine that takes no requests (a terminal's chat stops with its own Ctrl+C)."
            : $"sessions: the loop that runs {record.Id} took the request, and its record had not moved after {seconds} seconds; "
              + "`daoris session list` says where it stands.");
        return 2;
    }

    /// <summary>A stopped driven session's quest is held here until the person tries again: said with its door.</summary>
    private static void Held(SessionRecord record, TextWriter output)
    {
        if (record.Quest is { } quest)
        {
            output.WriteLine($"  its quest #{quest} is held here until you try again: daoris quest retry {quest} --session {record.Id}");
        }
    }

    /// <summary>This machine's record of one session, or null with why it is not one this machine can act on.</summary>
    internal static async Task<SessionRecord?> RecordAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        var record = (await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null)
        {
            output.WriteLine($"sessions: no session here is {id}.");
            return null;
        }

        if (record.Teammate)
        {
            output.WriteLine($"sessions: {id} runs on {id[..id.IndexOf('/')]}; nothing this machine sends reaches its process.");
            return null;
        }

        return record;
    }
}
