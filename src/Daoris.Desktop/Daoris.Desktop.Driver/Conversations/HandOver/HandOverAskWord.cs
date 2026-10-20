namespace Daoris.Driver;

/// <summary>Why the person's yes or no to an agent's ask to hand over did nothing: the codes the page words (MIXAGENT1c).</summary>
public static class HandOverWordWhy
{
    /// <summary>The session holds no such ask.</summary>
    public const string Gone = "gone";

    /// <summary>The driver's look has not read it yet: a no waits until it waits on the person's word.</summary>
    public const string Unread = "unread";

    /// <summary>A no to an ask that has the person's yes already, or the rule's.</summary>
    public const string Approved = "approved";

    /// <summary>The ask is past its word: taken or refused.</summary>
    public const string Past = "past";
}

/// <summary>What the person's yes or no did: whether it stands, the code of why not, a sentence, the agent it goes to, and whether it wrote the state.</summary>
public sealed record HandOverWordAnswer(bool Stands, string? Why, string Message, bool Wrote)
{
    /// <summary>The agent the ask goes to, where the look named one.</summary>
    public string? Agent { get; init; }
}

/// <summary>
/// The person's yes and no to an agent's ask to hand its session over under the <c>handOver</c> rule's <c>ask</c> (MIXAGENT1c, D163
/// point 3): one judge for the page's <c>SESSION_HAND_OVER_APPROVE</c> and <c>SESSION_HAND_OVER_DECLINE</c> and the terminal's
/// <c>sessions hand-over --approve|--decline</c>, in the library because the terminal host cannot reference Modules (HELPERTERM1's way).
/// </summary>
/// <remarks>
/// A yes is written where the ask waits on it: no state yet, or <c>waiting</c>; it is <c>approved</c> with the agent the look named,
/// and the look takes it as the rule's <c>on</c> would. The page's route, which holds the shell's chat runner, may take a conversation's
/// at once (<paramref name="taking"/>: the state says <c>taken</c> before the runner is asked, so no look takes it twice). A no is
/// written only where the look holds the ask <c>waiting</c>, a refusal <see cref="HandOverAskWhy.Declined"/> with the note the look writes
/// for every refusal.
/// </remarks>
public static class HandOverAskWord
{
    /// <param name="session">The session whose agent asked.</param>
    /// <param name="taking">The route takes it at once itself: the state written is <c>taken</c>, else <c>approved</c>.</param>
    public static HandOverWordAnswer Approve(HandOverAskFiles files, string session, string askId, bool taking = false)
    {
        var ask = files.Ask(session) is { } found && found.Id == askId ? found : null;
        var state = ask is null ? null : files.ReadState(session) is { } kept && kept.Id == askId ? kept : null;
        return (ask, state) switch
        {
            (null, _) => new(false, HandOverWordWhy.Gone, Gone(session, askId), false),
            (_, { State: HandOverAskStates.Approved }) => new(true, null, $"#{askId} has your yes.", false) { Agent = state!.Agent },
            (_, null) or (_, { State: HandOverAskStates.Waiting }) => Approved(files, session, askId, state?.Agent, taking),
            _ => new(false, HandOverWordWhy.Past, $"#{askId} is past your word: {state!.State}.", false),
        };
    }

    /// <summary>
    /// A no: the ask refused <c>declined</c> at once, with the coded note the look writes on the session's record for every refusal
    /// (<c>handover.refused</c>). <paramref name="output"/> is the loop's transcript where one runs here, null for the terminal.
    /// </summary>
    public static HandOverWordAnswer Decline(HandOverAskFiles files, SessionEvents events, SessionOutput? output, string session, string askId)
    {
        var ask = files.Ask(session) is { } found && found.Id == askId ? found : null;
        var state = ask is null ? null : files.ReadState(session) is { } kept && kept.Id == askId ? kept : null;
        return (ask, state) switch
        {
            (null, _) => new(false, HandOverWordWhy.Gone, Gone(session, askId), false),
            (_, { State: HandOverAskStates.Refused, Code: HandOverAskWhy.Declined }) => new(true, null, $"#{askId} has your no.", false),
            (_, null) => new(false, HandOverWordWhy.Unread, $"#{askId} is not read by the driver yet: say no once it waits on your word.", false),
            (_, { State: HandOverAskStates.Approved }) => new(false, HandOverWordWhy.Approved, $"#{askId} has your yes, and is taken at the driver's next look.", false),
            (_, { State: HandOverAskStates.Waiting }) => Declined(files, events, output, session, askId),
            _ => new(false, HandOverWordWhy.Past, $"#{askId} is past your word: {state!.State}.", false),
        };
    }

    /// <summary>
    /// <c>sessions hand-over &lt;id&gt; --approve|--decline</c> alone: the person's word on the ask the session's agent made, its
    /// current one, by this judge. The driver's look reads the file at its next look; the loop is not nudged from here. Exit 0 where
    /// the word stands, 1 where the judge refuses it, 2 where it could not be written.
    /// </summary>
    public static Task<int> RunAsync(SessionsWorld world, SessionsAsk ask, TextWriter output)
    {
        var session = ask.Ids[0];
        var files = new HandOverAskFiles(world.Home);
        try
        {
            var askId = files.Ask(session)?.Id ?? "";
            var answer = ask.Verb == "hand-over-approve"
                ? Approve(files, session, askId)
                : Decline(files, new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions)), null, session, askId);
            output.WriteLine($"sessions: {answer.Message}{(answer.Stands ? "" : " Nothing was changed.")}");
            return Task.FromResult(answer.Stands ? 0 : 1);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"sessions: the word could not be written under the home ({error.Message}), so nothing was changed.");
            return Task.FromResult(2);
        }
    }

    private static string Gone(string session, string askId) =>
        askId.Length == 0 ? $"Session {session} holds no ask of its agent's to hand it over." : $"Session {session} holds no ask #{askId} to hand it over.";

    private static HandOverWordAnswer Approved(HandOverAskFiles files, string session, string id, string? agent, bool taking)
    {
        files.WriteState(session, new HandOverAskState(id, taking ? HandOverAskStates.Taken : HandOverAskStates.Approved, null, agent, DateTimeOffset.UtcNow));
        return new(true, null, agent is null
            ? $"#{id} has your yes: the driver hands the session over at its next look, to the next agent in your order."
            : $"#{id} has your yes: the session goes on with {agent}.", true) { Agent = agent };
    }

    // The look's own refusal, as `DriverHandOvers` writes it: the state, then the note on the session's record.
    private static HandOverWordAnswer Declined(HandOverAskFiles files, SessionEvents events, SessionOutput? output, string session, string id)
    {
        files.WriteState(session, new HandOverAskState(id, HandOverAskStates.Refused, HandOverAskWhy.Declined, null, DateTimeOffset.UtcNow));
        var note = HandOverAskNotes.Refused(HandOverAskWhy.Declined);
        output?.Append(session, $"— {note.Note}");
        events.Keep(session, new SessionEvent { Kind = SessionEventKind.Note, Text = note.Note, Parts = note.Parts }, say: null);
        return new(true, null, $"#{id} has your no: the session stays with its agent, and its timeline says so.", true);
    }
}
