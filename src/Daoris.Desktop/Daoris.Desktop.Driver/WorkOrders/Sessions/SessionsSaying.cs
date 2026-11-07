namespace Daoris.Driver;

/// <summary>Say: what the screen's box says to a session, asked of the loop that runs it and waited for.</summary>
internal static class SessionsSaying
{
    /// <summary>
    /// <c>say</c> through the instance: a file named is judged here first, where the person named it, as the alone path judges it; its
    /// full path is sent, which the Daoris on this machine reads as <see cref="SayAsync"/> does.
    /// </summary>
    internal static async Task<int> SayThroughAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        if (world.Instance is null) return await SayAsync(world, ask, output, ct).ConfigureAwait(false);

        var paths = new List<string>();
        foreach (var path in ask.Files)
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                output.WriteLine($"sessions: `{path}` is not a file on this machine.");
                return 2;
            }

            paths.Add(full);
        }

        return await SessionsPresses.ThroughAsync(
            world, PersonPresses.SessionSay,
            SessionsPresses.Body(("id", ask.Ids[0]), ("text", ask.Text ?? ""), ("paths", paths.Count > 0 ? paths : null), ("level", ask.Level)),
            () => SayAsync(world, ask, output, ct), output, ct).ConfigureAwait(false);
    }

    private static async Task<int> SayAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        var id = ask.Ids[0];
        var text = ask.Text ?? "";
        var waited = Waited.From(world);

        // Read here, on the machine that has them, as `ask --file` reads them.
        var uploads = new List<ChatUpload>();
        foreach (var path in ask.Files)
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                output.WriteLine($"sessions: `{path}` is not a file on this machine.");
                return 2;
            }

            uploads.Add(new ChatUpload(Path.GetFileName(full), await File.ReadAllBytesAsync(full, ct).ConfigureAwait(false))); // read-shares: the person's own file
        }

        // What never goes on is judged from the record before anything is asked, by the one table.
        var json = await world.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null)
        {
            output.WriteLine($"sessions: {SessionsKeeping.Refusal(WordsNever.NotFound, id, null, null, null)}");
            return 1;
        }

        var last = WordsNever.LastHere(json, record.Quest);
        var next = WordsNever.ContinuedIn(json, id);
        // A host with the `head` mark judges the line's head inside its say door's step, across kinds, so the driver's own walk,
        // which follows conversations alone, stands aside for it; a host without the mark keeps it.
        if (next is not null && await world.Service.Opens.HeadAsync(ct).ConfigureAwait(false) >= 1) next = null;
        if (WordsNever.Judge(record, last, next) is { } never)
        {
            output.WriteLine($"sessions: {SessionsKeeping.Refusal(never, id, record, last, null, next)}");
            return 1;
        }

        // Kept as the box keeps what is attached, so the names the request and the record carry name files that exist.
        IReadOnlyList<string> files;
        try
        {
            files = uploads.Count > 0 ? [.. ChatFiles.Keep(world.Home, id, uploads).Select(file => file.Name)] : [];
        }
        catch (DriverException refused)
        {
            output.WriteLine($"sessions: {refused.Message}");
            return 2;
        }

        var runs = world.Processes.AliveOnThisMachine(id);
        if (!runs && !world.LoopRuns()) return await SessionsKeeping.KeepAsync(world, record, text, files, loop: false, output, ct).ConfigureAwait(false);

        var (held, taken) = await AskLoopAsync(world, record, text, files, waited, ask, ct).ConfigureAwait(false);
        if (held is not null) return await SaidAsync(world, record, held, waited, ask.Level, output, ct).ConfigureAwait(false);
        if (taken)
        {
            output.WriteLine(
                $"sessions: the loop that runs {id} took your words, and gave no answer of where they stand; "
                + "`daoris session list` says where it stands.");
            return 2;
        }

        if (runs)
        {
            output.WriteLine(
                $"sessions: nothing that runs {id} took your words within {SessionsKeeping.Seconds(world)} seconds, so they were withdrawn: it runs in "
                + "another Daoris process on this machine that takes no requests (a terminal's chat takes words on its own input).");
            return 2;
        }

        // A loop holds the home and took nothing in time: one still coming up, or a build from before this door.
        return await SessionsKeeping.KeepAsync(world, record, text, files, loop: true, output, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A say asked of the loops on this home and waited for, until its answer or the wait's end; one a loop took by then is waited
    /// for until that loop answers it. A session winding up is asked again until its record has ended, since the say door keeps
    /// nothing for a record still running. Null and not taken where nothing took it, which is withdrawn so no loop acts on it after
    /// the person is told.
    /// </summary>
    private static async Task<(WordsHeld? Held, bool Taken)> AskLoopAsync(
        SessionsWorld world, SessionRecord record, string text, IReadOnlyList<string> files, Waited waited, SessionsAsk ask, CancellationToken ct)
    {
        var requests = new SessionRequests(world.Home);
        WordsHeld? winding = null;
        while (true)
        {
            var request = new SessionRequest(record.Id, SessionMove.Say, DateTimeOffset.UtcNow)
            {
                Text = text, Files = files, Key = SessionRequests.NewKey(), Level = ask.Level,
            };
            requests.Write(request);

            WordsHeld? held = null;
            while (held is null && waited.Elapsed < world.Wait)
            {
                await Task.Delay(world.Poll, ct).ConfigureAwait(false);
                held = requests.AnswerOf(request);
            }

            if (held is null)
            {
                if (requests.Withdraw(request)) return (winding, winding is not null);

                // Taken: the loop that took it answers it whatever became of the words, a failure included, so that answer is
                // waited for rather than the clock read. Reading once at the wait's end would catch a loop under load that has taken the
                // words and not said where they stand while it writes its answer.
                return (await AnsweredAsync(world, requests, request, ct).ConfigureAwait(false) ?? winding, true);
            }

            if (held.Why != WordsHeld.Running || waited.Elapsed >= world.Wait) return (held, true);
            winding = held;
        }
    }

    /// <summary>
    /// The answer of a loop that took a say, waited for while a loop holds the home and within that loop's own bound on a say
    /// (<see cref="SessionsWorld.Answer"/>); null where none came, as from a loop that went away with the words.
    /// </summary>
    internal static async Task<WordsHeld?> AnsweredAsync(
        SessionsWorld world, SessionRequests requests, SessionRequest request, CancellationToken ct)
    {
        var answering = Waited.From(world);
        while (true)
        {
            if (requests.AnswerOf(request) is { } held) return held;
            // Read once more as it gives up, for an answer written as its loop let go of the home.
            if (answering.Elapsed >= world.Answer || !world.LoopRuns()) return requests.AnswerOf(request);
            await Task.Delay(world.Poll, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Where the words stand, in one line, from the loop's answer; words kept on the record are followed. The level asked is named
    /// where the door could not honour it, with the reason.
    /// </summary>
    private static async Task<int> SaidAsync(
        SessionsWorld world, SessionRecord record, WordsHeld held, Waited waited, string? level, TextWriter output, CancellationToken ct)
    {
        switch (held)
        {
            // What the door did with words at now, in the terminal's words. No door's turn is stopped for them.
            case { Sent: true, Now: true, Reaches: "now" }:
                output.WriteLine("sessions: sent now: it took this at once, cutting its step in flight.");
                return 0;
            case { Sent: true, Why: SendNowWhy.NoInterrupt or SendNowWhy.Opening or SendNowWhy.Behind }:
                var nextStep = held.Reaches == "next-step";
                var reason = held.Why switch
                {
                    SendNowWhy.NoInterrupt when nextStep => "its agent cannot be steered mid-step",
                    SendNowWhy.NoInterrupt => "its door takes words only when its turn ends",
                    SendNowWhy.Opening => "its first prompt has not left yet",
                    _ => "words ahead of these are still on their way",
                };
                output.WriteLine($"sessions: not sent now: {reason}; it reads this {(nextStep ? "at its next step" : "when its turn ends")}.");
                return 0;
            case { Sent: true, Reaches: "turn-end" } when level == MessageLevels.Urgent:
                output.WriteLine("sessions: held, not urgent: its door takes words only when its turn ends; it reads this then.");
                return 0;
            case { Sent: true, Now: true, Reaches: not "resume" }:
                output.WriteLine("sessions: sent now: it takes this as its next turn.");
                return 0;
            // The loop said them at the head of the record's line, which it names: followed there.
            case { Sent: true, Reaches: "resume", Head: { } head } when head != record.Id:
                output.WriteLine($"sessions: {record.Id} went on in {head}, its line's head, which your words went to.");
                var json = await world.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
                var headRecord = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, head, StringComparison.Ordinal))
                    ?? record with { Id = head };
                return await SessionsKeeping.GoesOnAsync(world, headRecord, held.Word, waited, output, ct).ConfigureAwait(false);
            case { Sent: true, Reaches: "resume" }:
                return await SessionsKeeping.GoesOnAsync(world, record, held.Word, waited, output, ct).ConfigureAwait(false);
            case { Sent: true, Reaches: "next-step" }:
                output.WriteLine("sessions: held: it reads this at its next step.");
                return 0;
            case { Sent: true, Reaches: "turn-end" }:
                output.WriteLine("sessions: held: it reads this when its turn ends.");
                return 0;
            case { Sent: true }:
                output.WriteLine(record.Quest is null
                    ? "sessions: sent: it takes this as its next turn."
                    : "sessions: held: it reads this once its turn opens.");
                return 0;
            case { Why: WordsHeld.Running }:
                output.WriteLine(
                    $"sessions: {record.Id} was still winding up after {SessionsKeeping.Seconds(world)} seconds, and nothing here holds words for it "
                    + "until it ends; say it again once it has ended.");
                return 2;
            case { Why: WordsHeld.Unreached }:
                output.WriteLine(
                    $"sessions: the desktop runs {record.Id}, a conversation, and hands it only the words typed in its box; write in "
                    + "its box there.");
                return 2;
            case { Why: { } why }:
                output.WriteLine($"sessions: {SessionsKeeping.Refusal(why, record.Id, record, null, held.Message)}");
                return 1;
            default:
                output.WriteLine($"sessions: {held.Message ?? "the loop that took your words could not say where they went."}");
                return 2;
        }
    }
}
