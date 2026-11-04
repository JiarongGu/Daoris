using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Words a say leaves on the record: followed until the wait's end, kept for the next loop, and refused with the reason the terminal words.</summary>
internal static class SessionsKeeping
{
    /// <summary>
    /// Words kept on the record, followed until the wait's end: taken by the same session, gone to a new one, judged unable to go on,
    /// or still held, with what holds them.
    /// </summary>
    internal static async Task<int> GoesOnAsync(
        SessionsWorld world, SessionRecord record, string? word, Waited waited, TextWriter output, CancellationToken ct)
    {
        var events = new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions));
        var gone = false;
        while (word is not null)
        {
            // The driver's note on these words, where it wrote one: where they went, or why they cannot go on.
            var note = events.Page(record.Id, limit: SessionEventText.MaxPage).Events
                .LastOrDefault(each => each.Kind == SessionEventKind.Note && each.Words?.Contains(word, StringComparer.Ordinal) == true);
            if (note is { To: { } to })
            {
                output.WriteLine($"sessions: went to session `{to}`, because {Because(note)}.");
                return 0;
            }

            if (note is not null)
            {
                output.WriteLine($"sessions: kept, but it cannot go on in this session, because {Because(note)}.");
                // The terminal's *Start a conversation with these words*, which takes them off this session.
                output.WriteLine($"  start a conversation with them instead: daoris session start-from {record.Id}");
                return 1;
            }

            var json = await world.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
            gone = !Waiting(json, record.Id).Contains(word, StringComparer.Ordinal);
            var now = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, record.Id, StringComparison.Ordinal));
            if (gone && now is { Live: true })
            {
                output.WriteLine("sessions: going on: the same session took it.");
                return 0;
            }

            if (waited.Elapsed >= world.Wait) break;
            await Task.Delay(world.Poll, ct).ConfigureAwait(false);
        }

        output.WriteLine(gone
            ? "sessions: taken: the session took it, and has moved since; `daoris session list` says where it stands."
            : $"sessions: {await HeldAsync(world, record, ct).ConfigureAwait(false)}");
        return 0;
    }

    /// <summary>
    /// Kept words not taken within the wait: held for the driver's next look, or what holds them, by the planner's own verdict on the
    /// record's quest, since whatever holds a start holds a reopen. A conversation goes on as it opens again.
    /// </summary>
    private static async Task<string> HeldAsync(SessionsWorld world, SessionRecord record, CancellationToken ct)
    {
        const string Next = "held: the same session goes on with this at the driver's next look.";
        if (record.Quest is not { } quest) return "held: the same conversation goes on with this as it opens again.";

        Consideration? verdict;
        try
        {
            verdict = (await SessionGroups.VerdictsAsync(world.Service, world.Config, world.Door, lastLook: null, ct, world.Home).ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Quest.Id, quest, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or JsonException)
        {
            world.Fell?.Invoke($"the look failed: {error.GetType().Name}: {error.Message}");
            return Next;
        }

        if (verdict is null)
        {
            world.Fell?.Invoke($"the planner gave no verdict for quest {quest}");
            return Next;
        }

        // The listing's line for words that wait: one sentence for both doors.
        if (WordsHold.Of(verdict, wait: null) is not { } holds)
        {
            world.Fell?.Invoke($"the planner's verdict for quest {quest} holds nothing: {verdict.Verdict}");
            return Next;
        }

        return $"held: {holds.Sentence}";
    }

    /// <summary>
    /// Words the verb keeps on the record itself. Where no loop drives the home, a driven session's, for the next loop to take up,
    /// shown in its conversation as the terminal's, since nothing else writes that record now; where a loop holds the home but took
    /// nothing in time, the same, unshown, since that loop's record of it may be open. A conversation goes on only through a loop's
    /// chat runner, so where none runs nothing here could open it.
    /// </summary>
    internal static async Task<int> KeepAsync(
        SessionsWorld world, SessionRecord record, string text, IReadOnlyList<string> files, bool loop, TextWriter output, CancellationToken ct)
    {
        if (record.Quest is null)
        {
            output.WriteLine(loop
                ? $"sessions: the driver on this machine took nothing within {Seconds(world)} seconds, so your words were withdrawn; say them again in a moment."
                : WordsNever.IsHelp(record)
                    // Ask Daoris's conversation goes on in the window, whose panel holds it.
                    ? $"sessions: {record.Id} is Ask Daoris's conversation, which goes on in the window: start Daoris, then say it again."
                    : $"sessions: {record.Id} is a conversation, and no driver runs on this machine to open it again: start the desktop, "
                      + "or `daoris drive`, then say it again.");
            return loop ? 2 : 1;
        }

        // Kept at the head of the record's line where it went on, the same say, its files carried to the head's folder first, and
        // the head named.
        var (said, at) = await WordsHead.FollowAsync(
                record.Id, named => world.Service.SayAsync(named, text, files, ct),
                files.Count > 0 ? (from, to) => ChatFiles.Carry(world.Home, from, to, files) : null)
            .ConfigureAwait(false);
        if (said.Kept)
        {
            if (!loop && said.Word is { } kept)
            {
                new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions)).Keep(at, LoopWords.Shown(kept, RequestDoor.Terminal), null);
            }

            var when = loop ? "at the driver's next look" : "when a driver next runs on this machine";
            output.WriteLine(at != record.Id
                ? $"sessions: held: {record.Id} went on in {at}, which goes on with this {when}."
                : $"sessions: held: the same session goes on with this {when}.");
            return 0;
        }

        switch (said.Refusal)
        {
            case "running":
                output.WriteLine(
                    $"sessions: {record.Id} is still {SessionsListing.Word(record.State)}, and nothing on this machine runs it: a driver's next start "
                    + "ends it, and then it can go on; say it again then.");
                return 2;
            case { } refusal:
                output.WriteLine($"sessions: {Refusal(WordsNever.Code(refusal), record.Id, record, null, said.Message)}");
                return 1;
            default:
                output.WriteLine($"sessions: {said.Message}");
                return 2;
        }
    }

    /// <summary>What never goes on, by its code, in the terminal's words; any other code, the service's sentence.</summary>
    /// <param name="next">The conversation a conversation went on in, where it was read; else its code says the sentence it came with.</param>
    internal static string Refusal(string code, string id, SessionRecord? record, string? last, string? message, string? next = null) => code switch
    {
        WordsNever.Continued when next is not null => $"{WordsNever.WentOn(id, next).TrimEnd('.')}: daoris session say {next} \"…\"",
        WordsNever.NotFound => $"no session here is {id}.",
        WordsNever.Teammate =>
            $"{id} ran on {(id.Contains('/') ? id[..id.IndexOf('/')] : "another machine")}, where its conversation is; nothing said here reaches it.",
        // Only a window from before Ask Daoris's conversations went on in themselves answers it.
        WordsNever.Help => $"{id} is Ask Daoris's own conversation, which the Daoris running here does not go on with: its panel opens a new one.",
        WordsNever.Intake => $"{id} is an intake; answer its ask #{record?.Ask} instead: publish it or close it.",
        WordsNever.StoodDown => $"it stood down: #{record?.Quest} is someone else's, so it has nothing to go on with.",
        WordsNever.Superseded =>
            $"#{record?.Quest} went on in a later session here, {last}; say it to that one: daoris session say {last} \"…\"",
        _ => message ?? $"{id} did not take your words ({code}).",
    };

    /// <summary>A driver's note's reason, as its line says it after <c>because</c>; its code where the line says none.</summary>
    private static string Because(SessionEvent note)
    {
        var text = note.Text ?? "";
        var at = text.IndexOf("because ", StringComparison.Ordinal);
        return at >= 0 ? text[(at + "because ".Length)..].TrimEnd().TrimEnd('.') : note.Why ?? "it could not";
    }

    /// <summary>The ids of the words waiting on one record, as the service answers its <c>said</c>; none where it answers none.</summary>
    private static IReadOnlyList<string> Waiting(string json, string id)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object
                || !session.TryGetProperty("id", out var named) || named.ValueKind != JsonValueKind.String || named.GetString() != id)
            {
                continue;
            }

            return session.TryGetProperty("said", out var said) && said.ValueKind == JsonValueKind.Array
                ? [.. said.EnumerateArray()
                    .Where(each => each.ValueKind == JsonValueKind.Object && each.TryGetProperty("id", out var word) && word.ValueKind == JsonValueKind.String)
                    .Select(each => each.GetProperty("id").GetString()!)]
                : [];
        }

        return [];
    }

    internal static int Seconds(SessionsWorld world) => (int)Math.Round(world.Wait.TotalSeconds);
}
