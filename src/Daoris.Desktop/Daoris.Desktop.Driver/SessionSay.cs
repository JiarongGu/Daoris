using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What never goes on with the person's words (D137 §2.2; MSG1d's codes), by code: the one table the terminal's
/// <c>sessions say</c> judges by before it asks anything (MSG1e), and the screen's judge, the modules' <c>SessionWords</c>,
/// judges by too (MSG1e2), so the nevers have one source.
/// </summary>
/// <remarks>
/// The first three are <see cref="ContinueWhy"/>'s nevers, the codes the driver's judgement says them by; the rest are the
/// screen's: a session whose quest went on in a later session here (MSG1b plans a quest's last session only, so no look would
/// take its words up), and no record. <see cref="Help"/> is no never since ASKHIST1: Ask Daoris's own conversation goes on in
/// itself as a chat does, and the code stays the word for what only a repository's session has (a new session, a conversation
/// started in its repository), which <see cref="IsHelp"/> says.
/// </remarks>
public static class WordsNever
{
    public const string Teammate = ContinueWhy.Teammate;

    public const string Intake = ContinueWhy.Intake;

    public const string StoodDown = ContinueWhy.StoodDown;

    public const string Help = "help";

    public const string Superseded = "superseded";

    public const string NotFound = "not-found";

    /// <summary>The code of what never goes on, in D137 §2.2's order, or null where the record can go on with words.</summary>
    /// <param name="last">The session its quest last ran in here (D79's reading, <see cref="LastHere"/>), or null.</param>
    public static string? Judge(SessionRecord record, string? last) =>
        record.Teammate ? Teammate
        : record.Ask is not null ? Intake
        : record.State == "stood-down" ? StoodDown
        : record.Quest is not null && last is not null && !string.Equals(last, record.Id, StringComparison.Ordinal) ? Superseded
        : null;

    /// <summary>
    /// Whether a record is Ask Daoris's own conversation (HELP1a), which belongs to no repository: its words go on in itself
    /// (ASKHIST1), and what only a repository's session has, a new session or a conversation started in its repository, is
    /// refused it as <see cref="Help"/>.
    /// </summary>
    public static bool IsHelp(SessionRecord record) => record.Repository == HelpRoom.Repository;

    /// <summary>The session a quest last ran in here, read from the records as the planner reads them (D79); null for none.</summary>
    public static string? LastHere(string recordsJson, string? quest) =>
        quest is not null && ServiceClient.ReadLastRun(recordsJson).TryGetValue(quest, out var run) ? run.Session : null;

    /// <summary>The say door's refusal word as a code: its word for a teammate's record is <c>not-ours</c>.</summary>
    public static string Code(string refusal) => refusal == "not-ours" ? Teammate : refusal;
}

/// <summary>
/// What the person said to a running session, kept on the ask its work is for (DRIFT1a2, D133 §1): once the session's door
/// holds the words, they go to the service's door for them by the session's own id, and the service judges which ask, if
/// any. The one rule a loop keeps them by: the headless host's <see cref="LoopWords"/> (MSG1e4) and the shell's judge, the
/// modules' <c>SessionWords</c> (MSG1e5), both call it.
/// </summary>
/// <remarks>
/// Never awaited by the answer: the words have reached their session whatever the service says, so <c>kept: false</c> (a
/// session on no ask), a refusal and a service that does not answer change nothing the person is told. Only the words travel,
/// never the files or where the person is (DRIFT1a keeps words alone). Words a record kept after it ended are not these: the
/// service keeps those on the ask itself once they are taken, as <c>reopened</c> (MSG1a).
/// </remarks>
public static class WordsOnAsk
{
    /// <summary>Keep the words on the session's ask, in the background. The task never faults, so a caller need not await it.</summary>
    /// <param name="service">The loop's service; null while it is not answering yet, which keeps nothing.</param>
    public static Task Keep(ServiceClient? service, string session, string text)
    {
        if (service is null) return Task.CompletedTask;
        return Task.Run(async () =>
        {
            try
            {
                await service.AddedToSessionAsync(session, text).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException or DriverException
                                              or JsonException or ObjectDisposedException or InvalidOperationException)
            {
                // The words are still in the session's own record; the ask misses one of them, and nothing else does.
            }
        });
    }
}

/// <summary>
/// What a loop did with words a terminal said to a session (MSG1e, D137 §5.2), written beside the request for the door that
/// asked: the shape the screen's routes answer (<c>{sent, reaches, why}</c>), with the id a record gave words it kept.
/// </summary>
/// <param name="Sent">Held or taken: they reach the session, now or when it can.</param>
/// <param name="Reaches">
/// <c>next-step</c>, <c>turn-end</c> or <c>resume</c>; null where they are taken at once (a conversation between turns) or the
/// door is not known yet (a driven session still opening).
/// </param>
/// <param name="Why">
/// Where nothing took them: a code of <see cref="WordsNever"/>, <see cref="Running"/> or <see cref="Unreached"/>. Null with a
/// <see cref="Message"/> where the loop could not say.
/// </param>
public sealed record WordsHeld(bool Sent, string? Reaches, string? Why)
{
    /// <summary>
    /// The record still runs where this loop's registry does not reach its door: a session winding up, or one still opening. Its
    /// record ends or opens in a moment, so the asker says the words again; nothing here would hold them meanwhile.
    /// </summary>
    public const string Running = "running";

    /// <summary>A conversation this loop runs, whose turns take only the words its runner is handed: the window's box.</summary>
    public const string Unreached = "unreached";

    /// <summary>The id the record gave the words it kept, which its events say again where the session took them.</summary>
    public string? Word { get; init; }

    /// <summary>The sentence where nothing took them: the service's, or why the loop could not.</summary>
    public string? Message { get; init; }

    public static WordsHeld Refused(string why, string? message = null) => new(false, null, why) { Message = message };

    public static WordsHeld Failed(string message) => new(false, null, null) { Message = message };
}

/// <summary>
/// A loop's half of a terminal's <c>sessions say</c> (MSG1e, D137 §5.2), as the driver library can do it: a session this loop
/// runs hears the words at its door, as the screen's box hands them (D90, D136; MSG1b's native door), and they are kept on its
/// ask as the box's are (<see cref="WordsOnAsk"/>, MSG1e4); one nothing on this machine runs has them kept on its record by the
/// service's say door (MSG1a), shown in its conversation where the loop handed its record, and the loop nudged, so its look
/// goes on with them (MSG1b).
/// </summary>
/// <remarks>
/// <para><b>The screen's judge is the modules' <c>SessionWords</c></b>, which this library cannot reach. The shell hands the
/// watch its own (<see cref="SessionRequestWatch.Say"/>, MSG1e2), so a conversation the window runs hears a terminal too, and
/// words said as a session winds up wait in the shell. This is what a loop without one does: the headless host's.</para>
///
/// <para><b>Two things it does not.</b> A conversation this loop runs is <see cref="WordsHeld.Unreached"/>: its turns are its
/// runner's, which takes the window's words. Words said as a session winds up, or before it opens (the say door answers
/// <c>running</c>), are <see cref="WordsHeld.Running"/>, so the asker says them again once the record moved: nothing here holds
/// them meanwhile, and a restart would lose what only a process held.</para>
/// </remarks>
public sealed class LoopWords(SessionProcesses processes, Func<ServiceClient?> service)
{
    /// <summary>
    /// The loop's own record of each session's conversation, where kept words are shown the moment they are kept (D137 §3.1);
    /// null shows none, so a loop never numbers a record another writer of it numbers too.
    /// </summary>
    public SessionEvents? Events { get; init; }

    /// <summary>The loop's nudge, so its look takes kept words up now rather than at its next interval; null waits for that.</summary>
    public Action? Nudge { get; init; }

    public async Task<WordsHeld> HoldAsync(SessionRequest request, CancellationToken ct)
    {
        var id = request.Session;
        var text = request.Text ?? "";

        // A driven session this loop runs, on the protocol door or the native door's run (MSG1b): its inbox holds them, and
        // they are the person's words on its ask (D133 §1), which the headless loop missed until MSG1e4.
        if (processes.InboxOf(id) is { } inbox && inbox.Hold(new ChatMessage(text, [])))
        {
            _ = WordsOnAsk.Keep(service(), id, text);
            return new WordsHeld(true, Spelled(inbox.Reach), null);
        }

        if (processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase) && processes.RefusesInput(id) is null)
        {
            return WordsHeld.Refused(WordsHeld.Unreached);
        }

        if (service() is not { } client)
        {
            return WordsHeld.Failed("the driver is still coming up — its service is not answering yet. A moment.");
        }

        var said = await client.SayAsync(id, text, request.Files, ct).ConfigureAwait(false);
        if (said.Kept)
        {
            if (said.Word is { } kept) Events?.Keep(id, Shown(kept, request.By), null);
            Nudge?.Invoke();
            return new WordsHeld(true, "resume", null) { Word = said.Word?.Id };
        }

        return said.Refusal switch
        {
            "running" => WordsHeld.Refused(WordsHeld.Running, said.Message),
            { } refusal => WordsHeld.Refused(WordsNever.Code(refusal), said.Message),
            null => WordsHeld.Failed(said.Message),
        };
    }

    /// <summary>
    /// Words a record kept, as its conversation shows them the moment they are kept (D137 §3.1): the person's, under the id the
    /// record gave them, reaching it on <c>resume</c>, with the door they were said at, which <c>session.reopened</c> names.
    /// </summary>
    public static SessionEvent Shown(SaidWordView kept, string door) => new()
    {
        Kind = SessionEventKind.User,
        Origin = "person",
        Id = kept.Id,
        Text = kept.Text,
        Files = kept.Files.Count > 0 ? kept.Files : null,
        Reaches = "resume",
        Door = door,
    };

    /// <summary>A driven inbox's reach in the wire's spelling, as the record's events spell it (D136).</summary>
    private static string? Spelled(DrivenReach? reach) => reach switch
    {
        DrivenReach.NextStep => "next-step",
        DrivenReach.TurnEnd => "turn-end",
        _ => null,
    };
}
