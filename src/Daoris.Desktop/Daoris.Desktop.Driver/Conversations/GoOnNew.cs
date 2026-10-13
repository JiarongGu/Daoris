using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The person's choice to go on in a new session (MSG1g): the ids of the words waiting when they chose, and when.</summary>
public sealed record NewSessionChoice(IReadOnlyList<string> Said, DateTimeOffset At);

/// <summary>
/// The choices the person made to go on in a new session rather than wait for a cooling account (MSG1g, D137 §2.2):
/// <c>&lt;home&gt;/sessions/&lt;session&gt;.new-session.json</c>, beside the session's marks and conversation id. The person's
/// choice, kept with when it was given (D143 point 4), which the driver's next look reads.
/// </summary>
/// <remarks>
/// <para><b>By the words' ids</b>, as <see cref="GoOnMarks"/> keeps its marks: a choice covers its record while any word it
/// named still waits, and a word said since goes with them, since a carry-on is handed every word waiting. Once they are
/// taken it covers nothing, and a word said later waits for the account again.</para>
///
/// <para><b>This machine's, never the record's.</b> A file that does not read is no choice: the words wait for the account,
/// which loses nothing.</para>
/// </remarks>
public sealed class NewSessionChoices(string home)
{
    public const string Suffix = ".new-session.json";

    /// <summary>Where a session's choice is kept.</summary>
    public string PathOf(string session) => Path.Combine(home, "sessions", session + Suffix);

    /// <summary>
    /// Keep the choice for these words, replacing any before. An id that is not a session's keeps nothing.
    /// </summary>
    /// <exception cref="IOException">The choice could not be written: the person is told, rather than told it was kept.</exception>
    public void Choose(string session, IEnumerable<string> said, DateTimeOffset at)
    {
        if (!SessionEvents.IsId(session)) return;

        var path = PathOf(session);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("said");
            foreach (var id in said.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)) writer.WriteStringValue(id);
            writer.WriteEndArray();
            writer.WriteString("at", at.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        // LF on every platform, as every file under the home is written.
        AtomicFile.WriteText(path, Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>The choice kept for this session, or null for none, a file that does not read, or an id that is not one.</summary>
    public NewSessionChoice? Read(string session)
    {
        if (!SessionEvents.IsId(session)) return null;

        try
        {
            var path = PathOf(session);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("said", out var said) || said.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("at", out var when) || when.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(when.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
            {
                return null;
            }

            return new NewSessionChoice([.. said.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!)], at);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Take a session's choice away: its words went on, or were taken. Nothing to take is no failure.</summary>
    public void Clear(string session)
    {
        if (!SessionEvents.IsId(session)) return;
        try
        {
            File.Delete(PathOf(session));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A choice left behind covers only words it names, and those are taken.
        }
    }

    /// <summary>
    /// Whether the person chose a new session for the words waiting on this record: any word the choice named still waits. A
    /// choice that named none, words kept by a host from before ids, covers whatever waits.
    /// </summary>
    public bool Covers(PriorSession record) =>
        Read(record.Session) is { } choice
        && record.Waiting is { Count: > 0 } waiting
        && (choice.Said.Count == 0 || waiting.Any(word => word.Id is { } id && choice.Said.Contains(id, StringComparer.Ordinal)));
}

/// <summary>What *Go on in a new session* came to (MSG1g): kept for the next look, or refused by a code, and its sentence.</summary>
/// <param name="Sent">The choice is kept: the driver's next look carries the words on in a new session.</param>
/// <param name="Why">Why not, by a code of <see cref="WordsNever"/> or <see cref="GoOnNew"/>; null where it was kept.</param>
/// <param name="Message">The sentence, in the terminal's words; the screen words <see cref="Why"/> itself.</param>
public sealed record GoOnNewAnswer(bool Sent, string? Why, string Message);

/// <summary>
/// *Go on in a new session* (MSG1g, D137 §2.2's account paragraph): words a resume holds while the account their session ran on
/// cools go on now in a new session, handed them, without that conversation. One door for the screen's route and the terminal's
/// <c>daoris-driver sessions go-on-new</c> (D50): it keeps the person's choice (<see cref="NewSessionChoices"/>), and the
/// driver's next look carries the words on as it carries on words whose account cannot run there.
/// </summary>
/// <remarks>
/// <para><b>Refused before anything is kept</b>: D137 §2.2's nevers by <see cref="WordsNever"/>, a session still running, one
/// with nothing waiting, and what nothing carries on by itself, a conversation and a closed quest's session, which are pointed
/// at *Start a conversation with these words*; and words whose account is not cooling, which the same session goes on with at
/// the next look, better than any new one.</para>
/// </remarks>
public static class GoOnNew
{
    /// <summary>Nothing waits on the record to go on with.</summary>
    public const string NoWords = "no-words";

    /// <summary>The session runs: words reach it at its door (D136), and it needs no new session.</summary>
    public const string Running = "running";

    /// <summary>A conversation, which nothing carries on by itself (D137 §2.2): *Start a conversation with these words* is the door.</summary>
    public const string Conversation = "conversation";

    /// <summary>A closed quest's session, which nothing carries on by itself either.</summary>
    public const string Closed = "closed";

    /// <summary>Its account is not cooling, so the same session goes on with the words at the next look.</summary>
    public const string NotCooling = "not-cooling";

    /// <summary>Judge and keep the choice for a session of this machine's.</summary>
    /// <param name="coolingOf">The cool-off a start on an adapter would read for an account, or null where it is ready.</param>
    public static async Task<GoOnNewAnswer> AskAsync(
        ServiceClient service, string home, Func<string?, string?, CoolingEntry?> coolingOf, string id, DateTimeOffset now,
        CancellationToken ct = default)
    {
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) return Refused(WordsNever.NotFound, $"no session here is {id}.");

        var last = WordsNever.LastHere(json, record.Quest);
        // Ask Daoris's own conversation has no quest a new session would serve (ASKHIST1): its words go on in itself, or its
        // history starts a new conversation from it.
        switch (WordsNever.Judge(record, last) ?? (WordsNever.IsHelp(record) ? WordsNever.Help : null))
        {
            case WordsNever.Teammate:
                return Refused(WordsNever.Teammate, $"{id} ran on {id[..id.IndexOf('/')]}, where its conversation is; nothing said here reaches it.");
            case WordsNever.Help:
                return Refused(WordsNever.Help,
                    $"{id} is Ask Daoris's own conversation, which has no quest a new session would serve: its words go on in it, or "
                    + "Ask Daoris's history starts a new conversation from it.");
            case WordsNever.Intake:
                return Refused(WordsNever.Intake, $"{id} is an intake; answer its ask #{record.Ask} instead: publish it or close it.");
            case WordsNever.StoodDown:
                return Refused(WordsNever.StoodDown, $"it stood down: #{record.Quest} is someone else's, so it has nothing to go on with.");
            case WordsNever.Superseded:
                return Refused(WordsNever.Superseded, $"#{record.Quest} went on in a later session here, {last}, and its words go there.");
        }

        if (record.Live && record.State != "awaiting-person")
        {
            return Refused(Running, $"{id} is {record.State}: words reach it there, so it needs no new session.");
        }

        if (ServiceClient.ReadRecord(json, id) is not { WordsWaiting: true } prior)
        {
            return Refused(NoWords, $"no words wait on {id} to go on with: `daoris-driver sessions say {id} \"…\"` says some.");
        }

        if (record.Kind == "chat" || record.Quest is null)
        {
            // MSG1f3: the terminal's *Start a conversation with these words*, which takes them off this session.
            return Refused(Conversation,
                $"{id} is a conversation, which nothing carries on by itself: start a conversation with these words instead: "
                + $"`daoris-driver sessions start-from {id}`.");
        }

        if (await service.FindQuestAsync(record.Quest, ct).ConfigureAwait(false) is not { Status: "Open" or "Taken" })
        {
            return Refused(Closed,
                $"#{record.Quest} has closed, so nothing carries its session's words on by itself: start a conversation with them "
                + $"instead: `daoris-driver sessions start-from {id}`.");
        }

        CoolingEntry? cooling;
        try
        {
            cooling = coolingOf(prior.Adapter, prior.Profile);
        }
        catch (DriverException)
        {
            // An adapter this build no longer has: nothing cools on it, and its own judgement says why it cannot go on.
            cooling = null;
        }

        if (cooling is null)
        {
            return Refused(NotCooling, $"the account {id} ran on is not cooling, so the same session goes on with your words at the driver's next look.");
        }

        try
        {
            new NewSessionChoices(home).Choose(id, prior.Waiting.Select(word => word.Id).OfType<string>(), now);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new GoOnNewAnswer(false, null, $"your choice could not be kept: {error.Message}");
        }

        return new GoOnNewAnswer(
            true, null,
            "it goes on in a new session at the driver's next look, handed your words; that session starts without this one's conversation.");
    }

    private static GoOnNewAnswer Refused(string why, string message) => new(false, why, message);
}
