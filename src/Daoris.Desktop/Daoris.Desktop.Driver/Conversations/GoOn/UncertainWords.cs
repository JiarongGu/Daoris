namespace Daoris.Driver;

/// <summary>
/// The words a shell gone before attempted and never took off (COWORK1c, D188 point 6; the message-urgency design §6, §14's trap 8):
/// written to a harness, or about to be, when the shell went, so the agent may hold them, and no harness drops a repeated prompt by its
/// id. Read as a shell comes up, from every record of this machine's that no run holds: each such word leaves its record, and nothing
/// sends it again. Where its conversation never showed it taken, it says so there under the word's id, by the <c>lost</c> code the page
/// words as <i>It may have read this; its answer was not kept</i>, and offers the person's own words again; one it showed taken leaves
/// without the line.
/// </summary>
/// <remarks>
/// <para><b>Taken back, never taken.</b> Each leaves by the take-back door, which keeps nothing on the ask, since no session is known to
/// have taken it; the taken door would keep the person's words there as the session's.</para>
///
/// <para><b>Never a running record's.</b> A record still running when the shell comes up is another driver's on this machine, whose
/// attempts are its own; the orphan sweep ends the rest first.</para>
/// </remarks>
public static class UncertainWords
{
    /// <returns>A line for the loop's report per record settled; none where nothing was, or the service did not answer.</returns>
    public static async Task<IReadOnlyList<string>> SettleAsync(ServiceClient service, SessionEvents? events, CancellationToken ct)
    {
        string json;
        IReadOnlyList<SessionRecord> records;
        try
        {
            json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
            records = SessionRecords.Parse(json);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // Nothing read is nothing settled: the words stay on their records, and the next come-up reads them.
            return [];
        }

        var lines = new List<string>();
        foreach (var each in records)
        {
            if (each.Teammate || ServiceClient.ReadRecord(json, each.Id) is not { } record) continue;
            if (await SettleOneAsync(service, events, record, ct).ConfigureAwait(false) is > 0 and var gone)
            {
                lines.Add(
                    $"uncertain  session {each.Id}: {gone} word(s) a shell before this one wrote to its agent left its record, never "
                    + "sent again");
            }
        }

        return lines;
    }

    /// <summary>
    /// One record's: what a conversation the shell takes up as it comes up settles before it goes on, since that take-up may run before
    /// the loop's first look settles every record.
    /// </summary>
    /// <returns>How many words left the record.</returns>
    public static async Task<int> SettleOneAsync(ServiceClient service, SessionEvents? events, PriorSession record, CancellationToken ct)
    {
        if (record.Running || record.Teammate) return 0;
        List<string> attempted = [.. (record.Said ?? []).Where(word => word.Attempt is not null).Select(word => word.Id).OfType<string>()];
        if (attempted.Count == 0) return 0;

        var gone = new List<string>();
        foreach (var word in attempted)
        {
            try
            {
                if ((await service.TakeBackAsync(record.Session, word, text: null, ct).ConfigureAwait(false)) is { Done: true } or { Why: "taken" })
                {
                    gone.Add(word);
                }
            }
            catch (Exception error) when (error is HttpRequestException or DriverException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // It waits, attempted, for the next come-up; nothing goes on with it meanwhile but a go-on this shell judged.
            }
        }

        var unseen = gone.Where(word => !ShownTaken(events, record.Session, word)).ToList();
        if (unseen.Count > 0)
        {
            events?.Keep(record.Session, new SessionEvent
            {
                Kind = SessionEventKind.Note, Text = ChatTurns.LostLine, Words = unseen, Code = SessionEventCodes.Lost,
            }, say: null);
        }

        return gone.Count;
    }

    /// <summary>Whether the session's conversation already showed this word taken: the word's own event as a turn's ask.</summary>
    private static bool ShownTaken(SessionEvents? events, string session, string word) =>
        events?.After(session, 0).Events.Any(e => e.Kind == SessionEventKind.User && e.Reaches is null && e.Id == word) == true;
}
