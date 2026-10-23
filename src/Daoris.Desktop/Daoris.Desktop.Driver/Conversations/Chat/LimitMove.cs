namespace Daoris.Driver;

/// <summary>
/// A conversation whose turn its account's limit refused goes on, in the same conversation, on the next account (LIMITGOON1; the
/// limit-goes-on design §2; D197, D189 point 3): at that turn's end, a new part of its line on the account of the same agent the walk
/// offers next, the cooling one passed, handed Daoris's brief and then the person's words the refused turns held, its record continuing
/// the old by <c>limit</c>, asked by <c>daoris</c>. A repository's chat opens it in the same tree; Ask Daoris's opens it in its
/// workspace's room, the link kept in the room's history, since the room's record takes no <c>from</c>. Over the runner's context, as
/// <see cref="CurveMove"/> and <see cref="BriefedMove"/> are.
/// </summary>
/// <remarks>
/// <para><b>The carrier is the brief</b> (design §1): the agent's own conversation is in the cooling account's home, which Daoris never
/// reads, copies or writes (D189 point 5), so it cannot resume on the next account.</para>
/// <para><b>Never mid-turn, never during a take.</b> Judged once per turn's end with nothing running, opening or queued, as the curve's
/// move is; the turns are held while it moves, so a word said as it begins either runs its turn first or waits for the new part.</para>
/// <para><b>Where no other account has room</b> it stays where it is and its words with it: the next refused turn tries again. A teammate's
/// record, an intake and a conversation that went on already never move; a conversation the person ended has no refused turn.</para>
/// </remarks>
internal sealed class LimitMove(ChatContext context, ChatRunner runner, ChatHandOver handOver, ChatHelp help)
{
    /// <summary>What each move came to, by the session it moved from: a test hears it; no person reads it.</summary>
    public event Action<string, StartedFrom>? Moved;

    /// <summary>A turn's end, told as the turns move: judged on a task of its own, as <see cref="CurveMove.Due"/> is.</summary>
    public void Due(string sessionId, DriverConfig config, Func<string, string, Task>? onEnded)
    {
        if (context.Closing) return;
        _ = Task.Run(async () =>
        {
            try
            {
                if (await MoveAsync(sessionId, config, onEnded).ConfigureAwait(false) is { } moved) Moved?.Invoke(sessionId, moved);
            }
            catch (Exception)
            {
                // A move that failed leaves the conversation where it is, its record saying the account cools: the next refused turn tries again.
            }
        });
    }

    /// <summary>
    /// Move the conversation where its last turn was refused by its account's limit (<see cref="LimitHeld"/>); null where nothing was
    /// judged to move: its last turn ended otherwise, it is no conversation of this machine's that runs here, or a move is under way.
    /// </summary>
    public async Task<StartedFrom?> MoveAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default)
    {
        if (context.Closing || context.GoingOn.ContainsKey(sessionId) || new HandOverBox(context.Home).Read(sessionId) is not null) return null;
        if (!context.Turned.TryGetValue(sessionId, out var chat)) return null;
        if (LimitHeld.Of(context.Events.After(sessionId, 0).Events) is not { } held) return null;

        var json = await context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var listed = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, sessionId, StringComparison.Ordinal));
        if (listed is null
            || WordsNever.Judge(listed, WordsNever.LastHere(json, listed.Quest), WordsNever.ContinuedIn(json, sessionId)) is not null)
        {
            return null;
        }

        if (ServiceClient.ReadRecord(json, sessionId) is not { Kind: "chat", Teammate: false, Ask: null, Adapter: { Length: > 0 } ranOn } prior
            || prior.Tree is not { Length: > 0 } tree)
        {
            return null;
        }

        var isHelp = WordsNever.IsHelp(listed);
        ISessionAdapter agent;
        try
        {
            agent = context.Harnesses.Adapters.Resolve(ranOn);
        }
        catch (DriverException)
        {
            return null;
        }

        // The account the walk offers now, the cooling one passed (D197): where it is the same, or none, nothing has room, and it stays.
        var workspace = isHelp ? prior.Workspace : await WorkspaceOfAsync(prior.Repository, ct).ConfigureAwait(false);
        var selection = await context.Harnesses
            .SelectAsync(agent.Name, config, workspace, null, StartKind.Conversation, ct).ConfigureAwait(false);
        if (!selection.Allowed || string.Equals(selection.Profile ?? "", prior.Profile ?? "", StringComparison.OrdinalIgnoreCase))
        {
            return new StartedFrom(null, false, null, $"no other account of {agent.Name} has room, so `{sessionId}` stays on its own.");
        }

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!context.GoingOn.TryAdd(sessionId, going.Task)) return null;
        var holding = false;
        try
        {
            // Never mid-turn: a word said meanwhile started its turn first, and that turn's end judges again.
            if (!chat.Turns.Hold()) return null;
            holding = true;

            // The brief of its line before the refused turns, which go after it as the person's words, so they are told once.
            var brief = await ContextBriefs.ComposeAsync(
                    context.Service, context.Events, context.Home, sessionId, HandOverWhy.Limit, conversation: true, ct: ct, before: held.Before)
                .ConfigureAwait(false);
            var high = RotatedOpening.ContextOf(null, context.Events, sessionId);

            // The old part ends first (D51): the new one holds its tree, or its room.
            await handOver.EndForHandOverAsync(sessionId).ConfigureAwait(false);
            chat.Turns.Release();
            holding = false;

            var started = isHelp
                ? await HelpStartAsync(prior, agent, config, onEnded, ct).ConfigureAwait(false)
                // By the walk, as a carry-on's start is, so its opening and `account.rotated` name the cooling one passed and what it carries.
                : await runner.StartAsync(
                        prior.Repository!, agent.Name, config, onEnded, profile: null, ct: ct, tree: tree,
                        from: new ContinuedFrom(sessionId, HandOverWhy.Limit, HandOverBy.Daoris))
                    .ConfigureAwait(false);
            if (started.SessionId is not { } to)
            {
                context.Note(sessionId, LimitNotes.NotMoved(started.Message));
                return new StartedFrom(null, false, null, started.Message);
            }

            var account = await ProfileOfAsync(to, ct).ConfigureAwait(false);
            var opening = HandOverNotes.MovedFrom(sessionId, ranOn, prior.Profile, HandOverWhy.Limit, high, brief.Text.Length);
            var words = held.Words;
            var files = words.SelectMany(word => ChatFiles.Kept(context.Home, sessionId, word.Files ?? [])).ToList();
            var message = new ChatMessage(string.Join("\n\n", words.Select(word => word.Text)), files) { Brief = brief, TakenOver = opening };
            var taken = await handOver.SayHandedOver(to, message).ConfigureAwait(false);

            // Said once where the person wrote, and in the machine log, accounts by profile name (D125 point 7).
            context.Note(sessionId, HandOverNotes.MovedOn(to, agent.Name, account, HandOverWhy.Limit, high, brief.Text.Length));
            context.Service.AccountSaid(
                Continuations.Moved(to, sessionId, ranOn, agent.Name, prior.Profile, account, HandOverWhy.Limit, briefed: true));
            if (!taken)
            {
                context.Note(to, HandOverNotes.Unsaid());
                return new StartedFrom(to, false, null, $"conversation `{to}` opened on the next account, but what Daoris kept of `{sessionId}` did not reach it.");
            }

            return new StartedFrom(to, true, null, $"`{sessionId}` went on in conversation `{to}` on the next account, handed Daoris's brief and your words.");
        }
        finally
        {
            if (holding) chat.Turns.Release();
            context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    /// <summary>
    /// Ask Daoris's new part, in its workspace's room (ASKSCOPE1b): placed as a press's is, the walk choosing its account and the log's
    /// <c>account.rotated</c> naming the part it carries; its room as the go-on finds it; its link kept in the room's history.
    /// </summary>
    private async Task<ChatStart> HelpStartAsync(
        PriorSession prior, ISessionAdapter agent, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        if (await help.SetAsideHelpAsync(null, prior.Workspace, ct).ConfigureAwait(false) is { } answering)
        {
            return new ChatStart(null, $"{HeldWords.AnsweringWhy(answering)}.");
        }

        var placed = await runner.PlaceHelpAsync(agent.Name, config, prior.Workspace, ct, carries: prior.Session).ConfigureAwait(false);
        if (placed is not { SessionId: { } id, Adapter: { } resolved, Selection: { } selection }) return new ChatStart(null, placed.Message);
        try
        {
            new HelpConversations(context.Home).StartedFrom(id, prior.Session, HelpConversations.Brief);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // Its history names no earlier conversation; its brief still says which it continues.
        }

        var (room, reads) = await help.HelpRoomNowAsync(id, HelpRoom.GoesOnIn(context.Home, prior.Workspace, prior.Tree), ct).ConfigureAwait(false);
        return await runner.RunAsync(id, placed.Message, resolved, selection, config, runner.HelpPlace(room, reads, prior.Workspace), onEnded, ct)
            .ConfigureAwait(false);
    }

    /// <summary>The workspace a repository's chat runs in, by the registry; null where the service does not name one.</summary>
    private async Task<string?> WorkspaceOfAsync(string? repository, CancellationToken ct)
    {
        var registry = await context.Service.RegistryAsync(ct).ConfigureAwait(false);
        return registry.FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;
    }

    /// <summary>The account the new part's record opened on.</summary>
    private async Task<string?> ProfileOfAsync(string session, CancellationToken ct) =>
        (await context.Service.RecordAsync(session, ct).ConfigureAwait(false))?.Profile;
}

/// <summary>A conversation's own line where its limit's move could not open the next part (LIMITGOON1), by its code.</summary>
public static class LimitNotes
{
    /// <summary>
    /// The new part did not open: the conversation ended, the words its refused turns held stay in it, and the start's own words follow
    /// in the English alone, as a program's.
    /// </summary>
    public static Noted NotMoved(string why) =>
        Noted.Of(
                NoteCodes.ChatLimitNotMoved,
                "Its account reached its limit, and it could not go on on the next account: write to it again once one has room.")
            .Then(" ", Noted.Said(why, NoteBy.Program));
}
