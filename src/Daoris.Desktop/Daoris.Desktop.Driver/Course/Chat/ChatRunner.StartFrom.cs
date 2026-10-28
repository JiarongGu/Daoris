// This file keeps the name ChatRunner.StartFrom.cs although it holds the StartFrom class alone (PART10h, D166): the web's
// say.test.ts and tools/merge-branch.mjs read it by that name.
namespace Daoris.Driver;

/// <summary>
/// *Start a conversation with these words* (MSG1f2, D137 §2.2, §5.3's <c>SESSION_START_FROM</c>): words a session cannot go on
/// with, and that nothing carries on by itself, are the first message of a new conversation in the session's repository; then
/// they leave the record they were said to, naming that conversation, as one act. The person's press, since a new conversation
/// has none of the old one's context.
/// </summary>
/// <remarks>
/// <para><b>Refused before anything starts</b>, by a code: D137 §2.2's nevers by <see cref="WordsNever"/>, a session still
/// running or parked (<see cref="Running"/>: its words reach it there), one with no words waiting (<see cref="NoWords"/>), and
/// one whose quest is open or taken (<see cref="Carried"/>), which the driver carries on with the words by itself. Another agent's
/// claims waiting beside the person's words (a helper's answer, MIXAGENT2a-iii) are not carried: only the person's words go, and a
/// record holding none of them is refused as having no words.</para>
///
/// <para><b>The words, their files and a preface.</b> The words go in the order said, joined by a blank line, with the files
/// said beside them read back from where their session kept them (MSG1d3); more than one message carries, and they go alone.
/// The preface names the session they were written to; it is the agent's to read, so it stays in the agent's language.</para>
///
/// <para><b>Taken off only once they went.</b> A conversation that does not start, or opens without taking them, leaves the
/// words where they were. Once its first message holds them they leave the old record by their ids, naming the conversation
/// (MSG1a's taken door), so a word said meanwhile stays; that record's marks go; and its conversation says where they went, by
/// the reason <see cref="ContinueWhy.Started"/>, which the page words as its went line.</para>
/// </remarks>
public static class StartFrom
{
    /// <summary>The session runs or waits on the person: words reach it where it is, and it needs no new conversation.</summary>
    public const string Running = "running";

    /// <summary>No words wait on the record to start a conversation with.</summary>
    public const string NoWords = "no-words";

    /// <summary>Its quest is open or taken, so the driver carries the words on with it by itself (D137 §2.2).</summary>
    public const string Carried = "carried";

    /// <summary>What the new conversation's agent is told ahead of the words: where they were first written. Its own language.</summary>
    public static string Preface(string session) =>
        $"The person first wrote these words to session `{session}`, which could not go on with them; this conversation has "
        + "none of that session's context.";

    /// <summary>Judge, start, hand the words over, and take them off the record they were said to.</summary>
    /// <param name="start">Opens a conversation in a repository: the chat runner's own start.</param>
    /// <param name="say">
    /// Hands a conversation its first message, with files and a preface: false where nothing took it. Throws
    /// <see cref="DriverException"/> where the files are more than a message carries.
    /// </param>
    public static async Task<StartedFrom> RunAsync(
        ServiceClient service, string home, SessionEvents events, SessionOutput? output, string id,
        Func<string, CancellationToken, Task<ChatStart>> start, Func<string, string, IReadOnlyList<ChatUpload>, string, bool> say,
        CancellationToken ct = default)
    {
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) return Refused(WordsNever.NotFound, $"no session here is {id}.");

        var last = WordsNever.LastHere(json, record.Quest);
        var next = WordsNever.ContinuedIn(json, id);
        // Ask Daoris's own conversation has no repository to start one in (ASKHIST1): its words go on in itself, or its history
        // starts a new one from it.
        switch (WordsNever.Judge(record, last, next) ?? (WordsNever.IsHelp(record) ? WordsNever.Help : null))
        {
            // HANDWORDS1: a conversation handed over or moved went on in another, which takes its words; no new one starts from them.
            case WordsNever.Continued:
                return Refused(WordsNever.Continued, WordsNever.WentOn(id, next!));
            case WordsNever.Teammate:
                return Refused(WordsNever.Teammate, $"{id} ran on {id[..id.IndexOf('/')]}, where its words are; nothing here takes them.");
            case WordsNever.Help:
                return Refused(WordsNever.Help,
                    $"{id} is Ask Daoris's own conversation, which no repository holds: its words go on in it, or Ask Daoris's history "
                    + "starts a new conversation from it.");
            case WordsNever.Intake:
                return Refused(WordsNever.Intake, $"{id} is an intake; answer its ask #{record.Ask} instead: publish it or close it.");
            case WordsNever.StoodDown:
                return Refused(WordsNever.StoodDown, $"it stood down: #{record.Quest} is someone else's, so it keeps no words to start with.");
            case WordsNever.Superseded:
                return Refused(WordsNever.Superseded, $"#{record.Quest} went on in a later session here, {last}, and its words go there.");
        }

        if (record.Live)
        {
            return Refused(Running, $"{id} is {record.State}: words reach it there, so it needs no new conversation.");
        }

        if (ServiceClient.ReadRecord(json, id) is not { WordsWaiting: true } prior)
        {
            return Refused(NoWords, $"no words wait on {id} to start a conversation with.");
        }

        if (record.Kind != "chat" && record.Quest is { } quest
            && await service.FindQuestAsync(quest, ct).ConfigureAwait(false) is { Status: "Open" or "Taken" } serving)
        {
            return Refused(Carried,
                $"#{quest} is {serving.Status.ToLowerInvariant()}, so the driver carries these words on with it by itself.");
        }

        // Another agent's claims (a helper's answer, MIXAGENT2a-iii; a second opinion's findings) are never the person's words: a new
        // conversation is handed only the person's, and where none wait there is nothing to start one with. The claims stay on the
        // record, for the conversation they were meant for.
        var words = prior.Waiting.Where(word => word.Persons).ToList();
        if (words.Count == 0)
        {
            return Refused(NoWords, $"only another agent's answer waits on {id}, which goes on in its own conversation and is never a new one's words.");
        }

        var started = await start(record.Repository, ct).ConfigureAwait(false);
        if (started.SessionId is not { } to) return new StartedFrom(null, false, null, started.Message);

        var text = string.Join("\n\n", words.Select(word => word.Text));
        var preface = Preface(id);
        var files = Files(home, id, words);
        bool took;
        try
        {
            took = say(to, text, files, preface);
        }
        catch (DriverException) when (files.Count > 0)
        {
            // More files than one message carries: the words go alone rather than not at all.
            took = say(to, text, [], preface);
        }

        if (!took)
        {
            return new StartedFrom(
                to, false, null, $"conversation `{to}` opened, but your words did not reach it, so they stay on session `{id}`.");
        }

        var ids = words.Select(word => word.Id).OfType<string>().ToList();
        if (await TakeAsync(service, output, events, id, ids, to, ct).ConfigureAwait(false))
        {
            new GoOnMarks(home).Clear(id);
        }

        // Where they went, where the person wrote them: the words' ids, the conversation and the reason's code (MSG1d).
        var went = Continuations.Went(ids, to, ContinueWhy.Of(ContinueWhy.Started));
        output?.Append(id, went.Text!);
        events.Keep(id, went, say: null);
        return new StartedFrom(to, true, null, $"conversation `{to}` starts with your words to session `{id}`, which keeps them no longer.");
    }

    /// <summary>
    /// The words off the record they were said to by their ids, naming the conversation that took them. A refusal costs a line
    /// in that conversation, never the conversation: false then, and the record still keeps them.
    /// </summary>
    private static async Task<bool> TakeAsync(
        ServiceClient service, SessionOutput? output, SessionEvents events, string id, IReadOnlyList<string> ids, string to,
        CancellationToken ct)
    {
        if (ids.Count == 0) return true;
        string? failed;
        try
        {
            var (taken, message) = await service.TakenAsync(id, ids, by: to, ct).ConfigureAwait(false);
            failed = taken ? null : message;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            failed = error.Message;
        }

        if (failed is null) return true;
        var line = WordsNotes.StartNotTaken(id, failed);
        output?.Append(to, line.Note);
        events.Keep(to, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
        return false;
    }

    /// <summary>The files said with the words, read back from where their session kept them by name; one that will not read is left out.</summary>
    private static IReadOnlyList<ChatUpload> Files(string home, string id, IReadOnlyList<SaidWordView> words)
    {
        var uploads = new List<ChatUpload>();
        foreach (var kept in ChatFiles.Kept(home, id, [.. words.SelectMany(word => word.Files)]))
        {
            try
            {
                uploads.Add(new ChatUpload(kept.Name, AtomicFile.ReadBytes(kept.Path)));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Gone or held open since it was kept: the words still go, as a resumed run's do without a file (MSG1d3).
            }
        }

        return uploads;
    }

    private static StartedFrom Refused(string why, string message) => new(null, false, why, message);
}
