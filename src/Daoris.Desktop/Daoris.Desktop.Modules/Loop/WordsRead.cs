using System.Text.Json;
using Daoris.Driver;
using static Daoris.Driver.Json;

namespace Daoris.Desktop;

/// <summary>
/// A record as the say reads it, the session its quest last ran in here, and the conversation it went on in,
/// each null for none; and the records' answer it is read from, whose words waiting only the queue reads.
/// </summary>
internal sealed record WordsRead(SessionRecord? Record, string? Last, string? Next, string Json)
{
    /// <summary>
    /// The records, and this one among them with its quest's last session here and the conversation it went on in; null with no
    /// service yet.
    /// </summary>
    public static async Task<WordsRead?> OfAsync(DriverLoop loop, string id, CancellationToken ct)
    {
        if (loop.Service is not { } service) return null;
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var records = SessionRecords.Parse(json);
        var record = records.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        return new WordsRead(record, WordsNever.LastHere(json, record?.Quest), WordsNever.ContinuedIn(records, id), json);
    }

    /// <summary>Whether the service gave no answer: nothing known is nothing claimed.</summary>
    public static bool Unanswered(Exception error) =>
        error is HttpRequestException or DriverException or JsonException or OperationCanceledException or TaskCanceledException;

    /// <summary>
    /// The record as the say judges it: with the driver's own walk to the conversation it went on in, where the
    /// host has no <c>head</c> mark, as today; without it where the host has the mark, whose say door judges the line's head inside its own step.
    /// </summary>
    public async Task<WordsRead> JudgedAsync(DriverLoop loop, CancellationToken ct) =>
        Next is not null && loop.Service is { } service && await service.Opens.HeadAsync(ct).ConfigureAwait(false) >= 1
            ? this with { Next = null }
            : this;

    /// <summary>
    /// What never goes on, as the answer says it: a conversation that went on in another names that one,
    /// with the terminal's sentence beside the code. Null where the record can go on with words.
    /// </summary>
    public WordsAnswer? Never(SessionRecord record) =>
        WordsNever.Judge(record, Last, Next) switch
        {
            null => null,
            WordsNever.Continued => WordsAnswer.Refused(WordsNever.Continued, WordsNever.WentOn(record.Id, Next!)) with { To = Next },
            var never => WordsAnswer.Refused(never),
        };

    /// <summary>
    /// The person's words waiting on this record, from the records' answer, in the order said. None on a
    /// record that runs again (queued, starting, working), whose run is already taking them; and never another agent's words
    /// waiting there, a second opinion's findings (<c>by</c>) or a helper's answer (<c>helper</c>), which are not the person's to
    /// take back or reword. A word without its id or its words is skipped, as the driver's client skips it.
    /// </summary>
    public static IReadOnlyList<WaitingWord> WaitingOn(string json, string id, bool starting = false)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        foreach (var record in document.RootElement.EnumerateArray())
        {
            if (!string.Equals(Text(record, "id"), id, StringComparison.Ordinal)) continue;
            if (!starting && Text(record, "state") is "queued" or "starting" or "working") return [];
            return
            [
                // The person's alone: never another agent's (by, helper), nor Daoris's own words (a word with a from).
                .. Items(record, "said")
                    .Where(word => NonEmpty(word, "by") is null && NonEmpty(word, "helper") is null && NonEmpty(word, "from") is null)
                    .Select(word => NonEmpty(word, "id") is { } said && Text(word, "text") is { } text
                        ? new WaitingWord(said, text, Strings(word, "files"))
                        : null)
                    .OfType<WaitingWord>(),
            ];
        }

        return [];
    }
}
