namespace Daoris.Driver;

/// <summary>
/// The codes *Go on with Daoris's brief* is refused by that are its own (SESSCTX1c part B), beside <see cref="StartFrom"/>'s, the
/// hand-over's tree and <see cref="WordsNever"/>'s: the page's <c>GO_ON_BRIEF</c> twin, held to this by <c>locales/say.test.ts</c>.
/// </summary>
public static class GoOnBrief
{
    /// <summary>The agent the session ran on is no longer one this machine has: the hand-over chooses another.</summary>
    public const string AgentGone = "agent-gone";
}

/// <summary>
/// A conversation, or a closed quest's session, goes on in a new part of its session handed Daoris's brief (SESSCTX1c part B, D189
/// points 2-3; the session-context design §2): by itself where its own conversation is proven lost (<see cref="ContinueReason.Lost"/>),
/// or by the person's *Go on with Daoris's brief* where it waits for them. A class over the runner's context, as
/// <see cref="ChatHandOver"/> is: the new part opens through the runner's own start, in the tree the old part ran in, on its agent and
/// its account, its record naming the one it continues, why <c>resume</c>, by Daoris or by the person.
/// </summary>
/// <remarks>
/// <para><b>Refused before anything starts</b>, by a code: what never goes on (<see cref="WordsNever"/>), a session that runs or parks
/// (<see cref="StartFrom.Running"/>), no words of the person's waiting (<see cref="StartFrom.NoWords"/>), a quest still open or taken,
/// whose words the driver carries on by itself (<see cref="StartFrom.Carried"/>), a tree that is gone (<see cref="HandOver.TreeGone"/>)
/// and an agent this machine no longer has (<see cref="GoOnBrief.AgentGone"/>), which only the hand-over's choice of agent answers.</para>
///
/// <para><b>A closed quest's session goes on as a conversation</b> in its own tree (the steward's choice): a closed quest opens no
/// driven record. <b>Ask Daoris's goes on in its own room</b>, a conversation of its workspace's, its link kept in the room's history
/// (<see cref="HelpConversations.StartedFrom"/>), since the room's record takes no <c>from</c>, on its own agent: the one path that places
/// Ask Daoris's new part on another agent is <see cref="LimitMove"/>'s, a limit with no account of its agent left (LIMITGOON2, D205
/// §3.5(e)).</para>
///
/// <para><b>Only the person's words go</b>, after the brief and in the same prompt, as the person's: another agent's answer waiting
/// beside them stays on the old record (XAGENT1e, MIXAGENT2a-iii). They leave the old record by their ids, naming the new part, once
/// it took them; until then they stay where they were.</para>
///
/// <para><b>The old part ended before the new starts</b> (D51): an ended record holds no tree, and a resume refused on the wire has put
/// its record back first (<see cref="ChatGoOn.WentBackAsync"/>). No closing note is asked: a lost conversation cannot be asked, and a
/// refused one has no turn left (D189's amendment).</para>
/// </remarks>
internal sealed class BriefedMove(ChatContext context, ChatRunner runner, ChatHandOver handOver, ChatHelp help)
{
    /// <summary>
    /// Go on in a new part handed the brief, one at a time per session: the person's press, a closed quest's session the driver hands
    /// over, and a conversation whose resume was refused on the wire once its run let go.
    /// </summary>
    /// <param name="lost">Why its own conversation was proven lost, where it goes on by itself; null for the person's press.</param>
    /// <param name="by">Who asks: <see cref="HandOverBy.Daoris"/> or <see cref="HandOverBy.Person"/>.</param>
    public async Task<StartedFrom> MoveAsync(
        string sessionId, ContinueReason? lost, string by, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        if (context.Closing) return new StartedFrom(null, false, null, "the application is closing, so no conversation goes on now.");

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!context.GoingOn.TryAdd(sessionId, going.Task))
        {
            return new StartedFrom(null, false, StartFrom.Running, $"`{sessionId}` is going on already.");
        }

        try
        {
            return await MovingAsync(sessionId, lost, by, config, onEnded, ct).ConfigureAwait(false);
        }
        finally
        {
            context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    /// <summary><see cref="MoveAsync"/> within a go-on that already holds the session (<see cref="ChatGoOn"/>'s judgement before a spawn).</summary>
    internal async Task<StartedFrom> MovingAsync(
        string sessionId, ContinueReason? lost, string by, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        var json = await context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var listed = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, sessionId, StringComparison.Ordinal));
        if (listed is null) return Refused(WordsNever.NotFound, $"no session here is {sessionId}.");

        var next = WordsNever.ContinuedIn(json, sessionId);
        if (WordsNever.Judge(listed, WordsNever.LastHere(json, listed.Quest), next) is { } never)
        {
            return Refused(never, never == WordsNever.Continued
                ? WordsNever.WentOn(sessionId, next!)
                : $"{sessionId} never goes on in a new part here ({never}).");
        }

        if (listed.Live) return Refused(StartFrom.Running, $"{sessionId} is {listed.State}: words reach it there, so it needs no new part.");
        if (ServiceClient.ReadRecord(json, sessionId) is not { WordsWaiting: true } record)
        {
            return Refused(StartFrom.NoWords, $"no words wait on {sessionId} to go on with.");
        }

        if (record.Kind != "chat" && record.Quest is { } quest
            && await context.Service.FindQuestAsync(quest, ct).ConfigureAwait(false) is { Status: "Open" or "Taken" } serving)
        {
            return Refused(StartFrom.Carried, $"#{quest} is {serving.Status.ToLowerInvariant()}, so the driver carries these words on with it by itself.");
        }

        // Only the person's words go to a new part (XAGENT1e, MIXAGENT2a-iii): another agent's claims stay for the person.
        var words = record.Waiting.Where(word => word.Persons).ToList();
        if (words.Count == 0)
        {
            return Refused(StartFrom.NoWords, $"only another agent's answer waits on {sessionId}, which is never a new part's words.");
        }

        if (record.Tree is not { Length: > 0 } tree || !ConversationTrees.Stands(tree))
        {
            return Refused(HandOver.TreeGone, $"the tree {sessionId} ran in is gone, so there is nothing to go on in.");
        }

        ISessionAdapter? agent = null;
        try
        {
            agent = context.Harnesses.Adapters.Resolve(record.Adapter ?? config.Adapter);
        }
        catch (DriverException)
        {
            // Said below: only the person can choose the agent it goes on with.
        }

        if (agent is not { Interactive: true })
        {
            return Refused(GoOnBrief.AgentGone,$"`{record.Adapter}` is no longer an agent on this machine that holds conversations: hand {sessionId} over to another.");
        }

        // The brief of its line, composed before the new part opens, while the old part is the last of it.
        var brief = await ContextBriefs.ComposeAsync(
                context.Service, context.Events, context.Home, sessionId, HandOverWhy.Resume, conversation: record.Kind == "chat", ct: ct)
            .ConfigureAwait(false);
        var help = WordsNever.IsHelp(listed);
        var started = help
            ? await HelpStartAsync(record, agent, config, onEnded, ct).ConfigureAwait(false)
            : await runner.StartAsync(
                    record.Repository!, agent.Name, config, onEnded, record.Profile, ct: ct, tree: tree,
                    from: new ContinuedFrom(sessionId, HandOverWhy.Resume, by))
                .ConfigureAwait(false);
        if (started.SessionId is not { } to) return new StartedFrom(null, false, null, started.Message);

        var high = RotatedOpening.ContextOf(null, context.Events, sessionId);
        var ranOn = record.Adapter ?? agent.Name;
        var opening = HandOverNotes.MovedFrom(sessionId, ranOn, record.Profile, HandOverWhy.Resume, high, brief.Text.Length);
        var files = words.SelectMany(word => ChatFiles.Kept(context.Home, sessionId, word.Files)).ToList();
        var message = new ChatMessage(string.Join("\n\n", words.Select(word => word.Text)), files) { Brief = brief, TakenOver = opening };
        var taken = await handOver.SayHandedOver(to, message).ConfigureAwait(false);

        // The move, said on the part it left, where the person wrote it, and in the machine log, by profile name (D125 point 7).
        var on = HandOverNotes.MovedOn(to, agent.Name, record.Profile, HandOverWhy.Resume, high, brief.Text.Length);
        context.Note(sessionId, on);
        context.Service.AccountSaid(Continuations.Moved(to, sessionId, ranOn, agent.Name, record.Profile, record.Profile, HandOverWhy.Resume, briefed: true));

        if (!taken)
        {
            // A part nobody briefed is no success: said on it, and the words stay where they were.
            context.Note(to, HandOverNotes.Unsaid());
            return new StartedFrom(to, false, null, $"conversation `{to}` opened, but what Daoris kept of `{sessionId}` and your words did not reach it, so they stay on `{sessionId}`.");
        }

        var ids = words.Select(word => word.Id).OfType<string>().ToList();
        if (await TakeAsync(sessionId, ids, to, ct).ConfigureAwait(false)) context.Marks.Clear(sessionId);

        // Where they went, where the person wrote them: their ids, the part and the reason's code (MSG1d), which the page words.
        var went = Continuations.Went(ids, to, lost ?? ContinueWhy.Of(ContinueWhy.Started));
        context.Output?.Append(sessionId, went.Text!);
        context.Events.Keep(sessionId, went, say: null);
        return new StartedFrom(to, true, null, $"`{sessionId}` goes on in conversation `{to}`, handed Daoris's brief and your words.");
    }

    /// <summary>
    /// 🔴 MODELROLE1d2 (D205 §3.5(b)): the go-on of a conversation its agent's refusal at the open ended <c>choice-not-offered</c>, before any
    /// prompt, interrupted before its replacement opened (the app closed between the two records). No conversation began there, so it is a
    /// fresh start of its actor, never a resume: the actor's order walked afresh, the refusing account passed by the reading kept, the new
    /// record continuing the refused one by <c>not-offered</c>, and the person's waiting words its first message, leaving the refused record by
    /// their ids once taken.
    /// </summary>
    /// <remarks>
    /// <para><b>A pick is never walked afresh</b>: it tries only its agent's entries on the account it was refused on, then waits in the
    /// wait's sentence; no other account or agent runs in its place.</para>
    /// <para><b>A refused continuation rebuilds its brief</b>: where its line, by <c>from</c> or the room's history, reaches an earlier part that
    /// ran a conversation, Daoris's brief of that part's line is composed again and handed before the words, as this class's own move hands
    /// it; the durable words keep only their text and their files.</para>
    /// </remarks>
    internal async Task<StartedFrom> NotOfferedAsync(PriorSession record, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        var sessionId = record.Session;
        var words = record.Waiting.Where(word => word.Persons).ToList();
        if (words.Count == 0) return Refused(StartFrom.NoWords, $"only another agent's answer waits on {sessionId}, which is never a new part's words.");
        if (record.Tree is not { Length: > 0 } tree || !ConversationTrees.Stands(tree))
        {
            return Refused(HandOver.TreeGone, $"the tree {sessionId} ran in is gone, so there is nothing to go on in.");
        }

        var help = string.Equals(record.Repository, HelpRoom.Repository, StringComparison.Ordinal);
        var actor = record.Actor ?? (help ? "ask-daoris" : "chat");
        var agent = record.Adapter ?? config.Adapter;
        var picked = record.Choice == "pick";
        IReadOnlyList<ActorEffective> order;
        try
        {
            order = picked ? ActorChoices.PickAlternatives(actor, config, agent) : ActorChoices.Effective(actor, config);
        }
        catch (ActorUnreadableException unreadable)
        {
            return Refused(StartFrom.NoWords, unreadable.Message);
        }

        if (order.Count == 0) return new StartedFrom(null, false, null, $"{ActorWords.Name(actor)} is off, so `{sessionId}` waits for you.");

        // The last part of its line that held a conversation, past every part a refusal at the open ended: a refused continuation's.
        var json = await context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var earlier = EarlierPart(json, record);
        var brief = earlier is null
            ? null
            : await ContextBriefs.ComposeAsync(context.Service, context.Events, context.Home, earlier, HandOverWhy.NotOffered, conversation: true, ct: ct)
                .ConfigureAwait(false);

        var started = help
            ? await HelpReplaceAsync(record, earlier, order, config, onEnded, ct).ConfigureAwait(false)
            : await runner.StartAsync(
                    record.Repository!, agent, config, onEnded, picked ? record.Profile : null, ct: ct, tree: tree,
                    from: new ContinuedFrom(sessionId, HandOverWhy.NotOffered, HandOverBy.Daoris), order: order)
                .ConfigureAwait(false);
        if (started.SessionId is not { } to) return new StartedFrom(null, false, null, started.Message);

        var refusedModel = record.NoteParts?.FirstOrDefault(part => part.Code == NoteCodes.EndedChoiceNotOffered.Code)?.Value("model") ?? "";
        context.Note(to, Noted.Of(
            NoteCodes.ChatReplaced,
            $"Opened in place of session {sessionId}: `{agent}` did not offer `{refusedModel}` on `{record.Profile ?? "its own sign-in"}`, so nothing ran there, and the words said to it came here.",
            ("session", sessionId), ("agent", agent), ("model", refusedModel), ("account", record.Profile ?? AgentOffers.OwnHome)));
        var files = words.SelectMany(word => ChatFiles.Kept(context.Home, sessionId, word.Files)).ToList();
        var text = string.Join("\n\n", words.Select(word => word.Text));
        var ranBefore = earlier is null ? null : ServiceClient.ReadRecord(json, earlier);
        var message = brief is null
            ? new ChatMessage(text, files)
            : new ChatMessage(text, files)
            {
                Brief = brief,
                TakenOver = HandOverNotes.MovedFrom(
                    earlier!, ranBefore?.Adapter ?? agent, ranBefore?.Profile, HandOverWhy.NotOffered, RotatedOpening.ContextOf(null, context.Events, earlier!),
                    brief.Text.Length),
            };
        if (!await handOver.SayHandedOver(to, message).ConfigureAwait(false))
        {
            context.Note(to, HandOverNotes.Unsaid());
            return new StartedFrom(to, false, null, $"conversation `{to}` opened, but your words did not reach it, so they stay on `{sessionId}`.");
        }

        var ids = words.Select(word => word.Id).OfType<string>().ToList();
        if (await TakeAsync(sessionId, ids, to, ct).ConfigureAwait(false)) context.Marks.Clear(sessionId);
        var went = Continuations.Went(ids, to, ContinueWhy.Of(ContinueWhy.Started));
        context.Output?.Append(sessionId, went.Text!);
        context.Events.Keep(sessionId, went, say: null);
        return new StartedFrom(to, true, null, $"`{sessionId}` goes on in conversation `{to}`, its actor's walk answered afresh.");
    }

    /// <summary>
    /// The last part before <paramref name="record"/> on its line that held a conversation (MODELROLE1d2): its <c>from</c>, or for Ask Daoris
    /// its room history's link, followed past every part a refusal at the open ended. Null for a new conversation's first refused record.
    /// </summary>
    private string? EarlierPart(string json, PriorSession record)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { record.Session };
        var at = Before(record);
        while (at is not null && seen.Add(at))
        {
            if (ServiceClient.ReadRecord(json, at) is not { } part) return null;
            if (part.NoteParts?.Any(each => each.Code == NoteCodes.EndedChoiceNotOffered.Code) != true) return at;
            at = Before(part);
        }

        return null;

        string? Before(PriorSession part)
        {
            if (part.From is { Length: > 0 } from) return from;
            if (!string.Equals(part.Repository, HelpRoom.Repository, StringComparison.Ordinal)) return null;
            try
            {
                return new HelpConversations(context.Home).Read(part.Session).From;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// A refused Ask Daoris conversation's fresh start (MODELROLE1d2): in its workspace's room, that workspace's running one set aside, placed
    /// on its actor's walk, linked in the room's history to the refused record.
    /// </summary>
    private async Task<ChatStart> HelpReplaceAsync(
        PriorSession record, string? earlier, IReadOnlyList<ActorEffective> order, DriverConfig config, Func<string, string, Task>? onEnded,
        CancellationToken ct)
    {
        if (await help.SetAsideHelpAsync(null, record.Workspace, ct).ConfigureAwait(false) is { } answering)
        {
            return new ChatStart(null, $"{HeldWords.AnsweringWhy(answering)}.");
        }

        var placed = await runner.PlaceHelpAsync(order[0].Agent, config, record.Workspace, ct, order: order).ConfigureAwait(false);
        if (placed.SessionId is not { } id) return new ChatStart(null, placed.Message);
        try
        {
            new HelpConversations(context.Home).StartedFrom(id, record.Session, earlier is null ? HelpConversations.Replaced : HelpConversations.Brief);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // Its history names no earlier conversation; its opening line still says which it replaced.
        }

        return await runner.RunHelpAsync(placed, config, help.DescribeHelp ?? (_ => Task.FromResult<HelpMachine?>(null)), onEnded, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Ask Daoris's new part, in the room of its workspace (ASKSCOPE1b): that workspace's running conversation set aside, as going on in an
    /// earlier one sets it aside, the new one placed and run as a press's is, and its link kept in the room's history.
    /// </summary>
    private async Task<ChatStart> HelpStartAsync(
        PriorSession record, ISessionAdapter agent, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        if (await help.SetAsideHelpAsync(null, record.Workspace, ct).ConfigureAwait(false) is { } answering)
        {
            return new ChatStart(null, $"{HeldWords.AnsweringWhy(answering)}.");
        }

        var placed = await runner.PlaceHelpAsync(agent.Name, config, record.Workspace, ct).ConfigureAwait(false);
        if (placed.SessionId is not { } id) return new ChatStart(null, placed.Message);
        try
        {
            new HelpConversations(context.Home).StartedFrom(id, record.Session, HelpConversations.Brief);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // Its history names no earlier conversation; its brief still says which it continues.
        }

        return await runner.RunHelpAsync(placed, config, help.DescribeHelp ?? (_ => Task.FromResult<HelpMachine?>(null)), onEnded, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The words off the record they were said to by their ids, naming the part that took them (MSG1a's taken door). A refusal costs a
    /// line in that part, never the part: false then, and the record still keeps them.
    /// </summary>
    private async Task<bool> TakeAsync(string sessionId, IReadOnlyList<string> ids, string to, CancellationToken ct)
    {
        if (ids.Count == 0) return true;
        string? failed;
        try
        {
            var (taken, message) = await context.Service.TakenAsync(sessionId, ids, by: to, ct).ConfigureAwait(false);
            failed = taken ? null : message;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            failed = error.Message;
        }

        if (failed is null) return true;
        context.Note(to, WordsNotes.StartNotTaken(sessionId, failed));
        return false;
    }

    private static StartedFrom Refused(string why, string message) => new(null, false, why, message);
}
