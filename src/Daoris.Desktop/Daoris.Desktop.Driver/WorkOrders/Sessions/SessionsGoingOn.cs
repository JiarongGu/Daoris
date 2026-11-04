namespace Daoris.Driver;

/// <summary>Going on in a new session, a conversation started from a session's words, and a conversation handed to another agent: the screen's presses, at a terminal.</summary>
internal static class SessionsGoingOn
{
    /// <summary>
    /// Words a resume holds while their account cools go on in a new session at the driver's next look (<see cref="GoOnNew"/>):
    /// one line, exit 0 where the choice is kept, 1 where it is refused, 2 where it could not be kept.
    /// </summary>
    internal static async Task<int> GoOnNewAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        var answer = await GoOnNew.AskAsync(
                world.Service, world.Home, world.CoolingOf ?? SessionGroups.CoolingOn(world.Home), id, DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        output.WriteLine($"sessions: {answer.Message}");
        return answer.Sent ? 0 : answer.Why is null ? 2 : 1;
    }

    /// <summary>
    /// The terminal's own conversation, opened by the host's chat door with the words a session could not go on with; its exit is the
    /// conversation's. A world with no door to open one has nothing to start it in.
    /// </summary>
    internal static async Task<int> StartFromAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        if (world.StartFrom is { } open) return await open(id, ct).ConfigureAwait(false);

        output.WriteLine($"sessions: no conversation can be opened from here; run `daoris session start-from {id}` at a terminal.");
        return 2;
    }

    /// <summary>
    /// What the chat runner's start-from came to, in one line (<see cref="StartFrom.RunAsync"/>'s sentence): null where a
    /// conversation opened, which then runs on in this terminal, its words taken or left where they were; else the exit, 1, a
    /// refusal by its code or a start the runner refused, as <c>chat</c>'s refusals are.
    /// </summary>
    internal static int? Started(StartedFrom started, TextWriter output)
    {
        output.WriteLine($"sessions: {started.Message}");
        return started.SessionId is null ? 1 : null;
    }

    /// <summary>
    /// A conversation goes on with another agent: opened by the host's chat door, as start-from's is, since a conversation lives in
    /// the shell's chat runner; its exit is the door's. A world with no door has nothing to hand it to.
    /// </summary>
    internal static async Task<int> HandOverAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        // A quest's own session is no conversation: it opens none here. Judged and decided as a driven move in the store, which the
        // driver's next look takes: it stops the session and carries its quest on in the same tree with the agent named. This run
        // decides it and goes no further, so the loop's look receives it from this run, alive or not.
        var record = (await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, ask.Ids[0], StringComparison.Ordinal));
        if (record is not null && QuestHandOver.IsQuestSession(record))
        {
            var roster = world.Drives ?? DrivesBy(world.Home);
            var moves = new ConversationMoves(
                world.Home, world.Service, new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions)), scope: MoveTakeUpScope.LeaseHolder);
            var answer = await QuestHandOver.AskAsync(
                world.Service, moves, roster, ask.Ids[0], ask.Agent!, ask.Account, HandOverWhy.Press, HandOverBy.Person, ct).ConfigureAwait(false);
            output.WriteLine($"sessions: {answer.Message}");
            if (answer.Kept)
            {
                // The session's record names who asked, the person at a terminal, as the agent's own ask names its agent.
                var note = HandOverAskNotes.AskedBy(HandOverAsker.Terminal, ask.Agent!);
                if (world.Notes is { } said) said(ask.Ids[0], note);
                else HandOverAskNotes.Say(new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions)), null, ask.Ids[0], note);
            }

            if (answer.Kept && !world.LoopRuns()) output.WriteLine("sessions: no driver is running on this home now: its look takes it when one does.");
            return answer.Kept ? 0 : 1;
        }

        if (world.HandOver is { } open) return await open(ask.Ids[0], ask.Agent!, ask.Account, ct).ConfigureAwait(false);

        output.WriteLine($"sessions: nothing here can hand a conversation over; run `daoris session hand-over {ask.Ids[0]} --agent {ask.Agent}` at a terminal.");
        return 2;
    }

    /// <summary>This build's adapters with the home's plugins, as the loop lays them for a look: what a terminal with no roster of its own judges by.</summary>
    internal static Func<string, HandOverAgent?> DrivesBy(string home)
    {
        var built = AdapterSet.Built();
        return QuestHandOver.Roster(built.WithPlugins(AgentPluginReads.Current.Load(home, built.Names)), home);
    }

    /// <summary>
    /// What the chat runner's hand-over came to, in one line: null where the new conversation opened, which then runs on in this
    /// terminal; else the exit, 0 where the ask is kept for the conversation's turn to end, 1 where it is refused.
    /// </summary>
    internal static int? HandedOver(HandedOver handed, TextWriter output)
    {
        output.WriteLine($"sessions: {handed.Message}");
        return handed.SessionId is not null ? null : handed.Kept ? 0 : 1;
    }
}
