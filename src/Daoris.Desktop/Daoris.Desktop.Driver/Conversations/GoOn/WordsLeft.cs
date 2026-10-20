namespace Daoris.Driver;

/// <summary>
/// The person's words a driven session held when its door failed (TURNFAIL1, USERSHOWN1; D137 §2.2): sent behind a turn that
/// failed, or held for its end, they never reached the agent, and its run ends with them. They are kept on its ended record
/// through the say door (MSG1a), to go again with the next run that goes on, as words said to it once it ended are.
/// </summary>
/// <remarks>
/// <para><b>After the conclusion.</b> The say door refuses a running record, so the words are kept once the run moved it;
/// until then only this process holds them.</para>
///
/// <para><b>The conversation says why, then shows them waiting.</b> A note naming the ids they were shown under settles them
/// there, saying they wait to go again; each is then shown at the record's foot under the id the record gave it, which the
/// run that goes on says again where it takes it (D137 §3.1). Words the record would not keep are said never to have
/// reached it, as before.</para>
/// </remarks>
public static class WordsLeft
{
    /// <returns>How many the record kept.</returns>
    public static async Task<int> KeepAsync(
        ServiceClient service, SessionEvents events, string session, IReadOnlyList<ChatMessage> left, CancellationToken ct)
    {
        if (left.Count == 0) return 0;

        // The door they were said at, as the record showed them; none where it showed none.
        var door = events.DoorOf(session, [.. left.Select(word => word.Id).OfType<string>()]);
        var kept = new List<(ChatMessage Word, SaidWordView? View)>();
        var lost = 0;
        foreach (var word in left)
        {
            try
            {
                // COWORK1b: Daoris's own word is kept as Daoris's, under its fact's key, never as the person's.
                var said = await service.SayAsync(
                        session, word.Text, word.Files.Count > 0 ? [.. word.Files.Select(file => file.Name)] : null, ct,
                        from: word.Daoris is null ? null : DaorisWords.From, key: word.Daoris?.Key)
                    .ConfigureAwait(false);
                if (said.Kept) kept.Add((word, said.Word));
                else lost++;
            }
            catch (Exception error) when (error is HttpRequestException or DriverException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                lost++;
            }
        }

        if (kept.Count > 0)
        {
            var line = FailedTurnWords.Line(WordsAfter.Wait)!;
            var named = kept.Select(each => each.Word.Id).OfType<string>().ToList();
            events.Keep(session, new SessionEvent
            {
                Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts, Words = named.Count > 0 ? named : null,
            }, say: null);
            foreach (var (_, view) in kept)
            {
                if (view is not null) events.Keep(session, LoopWords.Shown(view, door ?? "") with { Door = door }, say: null);
            }
        }

        if (lost > 0)
        {
            var never = WordsNotes.Unreached(lost, "never reached the session", UnreachedWhy.EndedFirst);
            events.Keep(session, new SessionEvent { Kind = SessionEventKind.Note, Text = never.Note, Parts = never.Parts }, say: null);
        }

        return kept.Count;
    }
}
