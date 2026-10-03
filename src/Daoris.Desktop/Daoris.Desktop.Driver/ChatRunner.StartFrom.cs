namespace Daoris.Driver;

/// <summary>What *Start a conversation with these words* came to (MSG1f2): the conversation it opened, or why none.</summary>
/// <param name="SessionId">The conversation it opened; null where none opened.</param>
/// <param name="Sent">
/// The words are that conversation's first message and are off the record they were said to; false where nothing started,
/// or the conversation opened without taking them, and the words stay where they were.
/// </param>
/// <param name="Why">Why nothing was done, by a code of <see cref="WordsNever"/> or <see cref="StartFrom"/>; null otherwise.</param>
/// <param name="Message">The sentence, in the driver's words; the screen words <see cref="Why"/> itself.</param>
public sealed record StartedFrom(string? SessionId, bool Sent, string? Why, string Message);

/// <summary>
/// *Start a conversation with these words* (MSG1f2, D137 §2.2, §5.3's <c>SESSION_START_FROM</c>): words a session cannot go on
/// with, and that nothing carries on by itself, are the first message of a new conversation in the session's repository; then
/// they leave the record they were said to, naming that conversation, as one act. The person's press, since a new conversation
/// has none of the old one's context.
/// </summary>
/// <remarks>
/// <para><b>Refused before anything starts</b>, by a code: D137 §2.2's nevers by <see cref="WordsNever"/>, a session still
/// running or parked (<see cref="Running"/>: its words reach it there), one with no words waiting (<see cref="NoWords"/>), and
/// one whose quest is open or taken (<see cref="Carried"/>), which the driver carries on with the words by itself.</para>
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
        switch (WordsNever.Judge(record, last))
        {
            case WordsNever.Teammate:
                return Refused(WordsNever.Teammate, $"{id} ran on {id[..id.IndexOf('/')]}, where its words are; nothing here takes them.");
            case WordsNever.Help:
                return Refused(WordsNever.Help, $"{id} is Ask Daoris's own conversation, which keeps no words to start another with.");
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

        var started = await start(record.Repository, ct).ConfigureAwait(false);
        if (started.SessionId is not { } to) return new StartedFrom(null, false, null, started.Message);

        var words = prior.Waiting;
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
        var line = $"— the words this conversation started with could not be taken off session `{id}`: {failed}";
        output?.Append(to, line);
        events.Keep(to, new SessionEvent { Kind = SessionEventKind.Note, Text = line }, say: null);
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
                uploads.Add(new ChatUpload(kept.Name, File.ReadAllBytes(kept.Path)));
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

public sealed partial class ChatRunner
{
    /// <summary>
    /// *Start a conversation with these words* (MSG1f2): <see cref="StartFrom.RunAsync"/> through this runner's own start, on
    /// the adapter and account a new conversation takes, and its own first message. The record they were written to goes on
    /// nowhere meanwhile: a word said to it while this runs waits rather than reopening it under the words handed on.
    /// </summary>
    /// <param name="onEnded">Told when the new conversation ends, as a start's is.</param>
    public async Task<StartedFrom> StartFromAsync(
        string sessionId, string adapter, DriverConfig config, Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        if (_closing) return new StartedFrom(null, false, null, "the application is closing, so no conversation starts now.");

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_goingOn.TryAdd(sessionId, going.Task))
        {
            return new StartedFrom(null, false, StartFrom.Running, $"conversation `{sessionId}` is going on with its words already.");
        }

        try
        {
            return await StartFrom.RunAsync(
                _service, _home, _events, _output, sessionId,
                (repository, token) => StartAsync(repository, adapter, config, onEnded, ct: token),
                (id, text, files, preface) => Say(id, text, files, preface),
                ct).ConfigureAwait(false);
        }
        finally
        {
            _goingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }
}
