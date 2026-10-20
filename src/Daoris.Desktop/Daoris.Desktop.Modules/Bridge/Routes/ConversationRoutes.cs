using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A conversation, the page's `bridge/conversation.ts` (MOD5): its record a page at a time (D76 §2), its
/// start (D49 §3), the person's messages with their files, finishing it, stopping a turn, its options and
/// its queue (CONV4, AGT6b).
/// </summary>
public sealed class ConversationRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;
    private readonly IEventBus _events = context.Events;

    // A session's conversation (D76 §2): the newest page, an earlier one (`before`), or only
    // what is newer (`after`) — how a page opens a session after a restart, loads earlier
    // turns, and closes a gap in the live events. From the record under the home, so it
    // answers whether or not this app was running when the session spoke.
    [DriverRoute("SESSION_HISTORY")]
    private object? SessionHistory(IpcRequest request)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var page = Number(request, "after") is { } after
            ? _loop.Events.After(id, after)
            : _loop.Events.Page(id, Number(request, "before"), (int)(Number(request, "limit") ?? SessionEventText.PageLimit));
        return new { Session = id, Events = page.Events.ToArray(), page.Earlier, page.Latest, page.Opening, page.FirstFailure };
    }

    // A conversation in a repository (D49 §3). The record is the service's and the lock is the
    // ledger's; what only this side can do is put a harness behind it — a process on this
    // machine, which never leaves it (D46 §7).
    // A start changes the sessions and the driver's state its loop, nudged, reads again (STATEHUB1d, D169), as each press below
    // that opens a conversation does.
    [DriverRoute("START_CHAT")]
    [Publishes("START_CHAT", StateScopes.Sessions | StateScopes.Driver)]
    private async Task<object?> StartChatAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var chat = _loop.Chat ?? throw NotReady();

        var config = DriverConfig.Load(_loop.ConfigPath);
        // A blank adapter is none named, and the machine's own is the answer. This route took a
        // blank one as a name (REV3 CLEAN1); the page happens never to send one.
        var adapter = Optional(request, "adapter") ?? config.Adapter;

        // The end of a conversation is news the page wants without asking: the drawer is
        // probably open, and a record that moved silently reads as one that hung.
        Task OnEnded(string session, string state) => DriverLoop.Ended(_events, session, state);
        // The per-session picker (D49 §4). Absent takes the workspace's default, then the
        // machine's, then the harness's own configuration home.
        var profile = Optional(request, "profile");

        // The opening words (NEW1a, D162): the conversation opens on them. Absent, blank or whitespace is the old empty start.
        var text = Optional(request, "text");
        if (!string.IsNullOrWhiteSpace(text))
        {
            var files = request.Payload is { } payload ? FilesOf(payload) : [];
            var started = await chat.StartWithAsync(
                repository, adapter, config, text, files, Optional(request, "requestId"),
                ownTree: TreeChoice(request), profile: profile, onEnded: OnEnded, ct: cancellationToken);
            _loop.Nudge();
            return new { started.SessionId, started.Sent, started.Why, started.Message };
        }

        var start = await chat.StartAsync(
            repository, adapter, config, OnEnded, profile,
            // The per-conversation tree choice (D51). Absent falls back to the repository's
            // standing opt-in, which the runner reads from the same config.
            ownTree: TreeChoice(request),
            ct: cancellationToken);

        _loop.Nudge();
        return new { start.SessionId, start.Message };
    }

    // The person's words to a session, whatever its state (MSG1d, D137 §5.3): `{ sent, reaches, why }`. A running session
    // hears them at its door as before (SESS3, D136, CONV4a); one that parked or ended has them kept on its record and the
    // loop nudged, so the same session goes on with them (MSG1b); words said as one winds up are held until its record ends,
    // never refused. What never goes on is `sent: false` with its code. 🔴 A session running here that takes no input at
    // all is REFUSED instead, in the driver's words (INT4h): false would tell a stale page it ended when it is running.
    // Words kept on a park's or an ended record move it among the sessions (STATEHUB1d); a live chat's words reach its door
    // alone, and the publisher reads no answer to tell them apart (D169 point 5), so every one asks the sessions again. It
    // declares Ask Daoris's history too (HELPHISTORYHUB1): the route cannot tell a help conversation's words from another's, a
    // conversation's first words list it there, named by its question, and words kept on an ended one resume it; a history
    // nobody has open only goes stale.
    [DriverRoute("SESSION_INPUT")]
    [Publishes("SESSION_INPUT", StateScopes.Sessions | StateScopes.Help, Session = "id")]
    private async Task<object?> SessionInputAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var text = PayloadHelper.GetRequiredValue<string>(request.Payload, "text");
        // COWORK1a (D188 point 1): `level` is when the words should reach a working session, `ordinary`, `urgent` or `now`; an older
        // page's `now: true` is now; none is the person's default, urgent. No level stops a turn.
        var level = LevelOf(request);
        // RPC1d: `sessions say` sent over the pipe asks for the verb's own words and names the files it was given by their paths on this
        // machine, which the verb reads as it does alone; the page asks for none and attaches its files whole.
        if (Flag(request, "said"))
        {
            var paths = request.Payload is { } named && named.TryGetProperty("paths", out var listed) && listed.ValueKind == JsonValueKind.Array
                ? listed.EnumerateArray().Where(path => path.ValueKind == JsonValueKind.String).Select(path => path.GetString()!).ToList()
                : [];
            return await SessionsVerbs.SaidAsync(
                context,
                new SessionsAsk("say")
                {
                    Ids = [id], Text = text, Files = paths, Now = level == MessageLevel.Now, Level = Optional(request, "level"),
                },
                cancellationToken)
                .ConfigureAwait(false);
        }

        // What the person attached, kept for its session before the words go (CONV4c).
        var files = request.Payload is { } payload ? FilesOf(payload) : [];
        // Where the person is (HELP1b), which a conversation's agent is handed ahead of the words; absent for most. `now` is the
        // person's *Send now* (STEER2a), which a running session carries out by its door.
        // COWORK1c: `key`, the press's own where the page names one, so a press that arrives twice keeps one word; the shell gives each
        // press one otherwise.
        var said = await _loop.Words
            .SayAsync(id, text, files, Optional(request, "preface"), door: "screen", cancellationToken, level, Optional(request, "key"))
            .ConfigureAwait(false) ?? throw NotReady();
        // Words a running session took are kept on its ask now (DRIFT1a2); words kept on a record are kept there once a
        // session takes them (MSG1a's taken door), so they are never posted twice.
        if (said.Running) _loop.Words.KeepOnAsk(id, text);
        // `to`: the conversation a conversation handed over or moved went on in, where `why` is `continued` (HANDWORDS1).
        // `now`: the press was carried out; false where it was not asked, or the door could do neither, `why` saying so.
        return new { said.Sent, said.Reaches, said.Why, said.To, said.Now };
    }

    // *Start a conversation with these words* (MSG1f2, D137 §2.2, §5.3): words a session cannot go on with, and that nothing
    // carries on by itself, start a conversation in its repository on the machine's adapter, handed with a preface naming the
    // session, and leave its record naming that conversation, as one act of the chat runner's. `{ sessionId, sent, why,
    // message }`: what never goes on, a session still running, no words waiting and a quest that carries them on by itself are
    // refused by their codes before anything starts. Its end reaches the page as a start's does.
    [DriverRoute("SESSION_START_FROM")]
    [Publishes("SESSION_START_FROM", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SessionStartFromAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        // RPC1d: `sessions start-from` sent over the pipe asks for the verb's own words; the conversation opens in this window.
        if (Flag(request, "said")) return await SessionsVerbs.SaidAsync(context, new SessionsAsk("start-from") { Ids = [id] }, cancellationToken).ConfigureAwait(false);

        var chat = _loop.Chat ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);
        var started = await chat.StartFromAsync(
                id, config.Adapter, config, onEnded: (session, state) => DriverLoop.Ended(_events, session, state), cancellationToken)
            .ConfigureAwait(false);
        _loop.Nudge();
        return new { started.SessionId, started.Sent, started.Why, started.Message };
    }

    // *Go on with another agent…* (MIXAGENT1b-i, D163): a conversation of this machine handed to another agent, from its page.
    // The chat runner judges and keeps the ask: taken at once for a conversation that is not talking, kept for its turn's end
    // for one that is (the page's *Stop and hand over…* asks this first, then stops the turn). `{ sessionId, kept, why, message }`;
    // a refusal is the answer by its code, never thrown. The reason is `press` unless named; `near` and `limit` are Daoris's own
    // offer's (MIXAGENT1b-ii), taken from the person's press on the offer, so only with them is `askedBy` `daoris` honoured.
    public static string HandOverAskedBy(string why, string? askedBy) =>
        askedBy == HandOverBy.Daoris && why is HandOverWhy.Near or HandOverWhy.Limit ? HandOverBy.Daoris : HandOverBy.Person;

    [DriverRoute("SESSION_HAND_OVER")]
    [Publishes("SESSION_HAND_OVER", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SessionHandOverAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var agent = PayloadHelper.GetRequiredValue<string>(request.Payload, "agent");
        // RPC1d: `sessions hand-over` sent over the pipe asks for the verb's own words (the person's press, its reason `press`).
        if (Flag(request, "said"))
        {
            return await SessionsVerbs.SaidAsync(
                context, new SessionsAsk("hand-over") { Ids = [id], Agent = agent, Account = Optional(request, "account") }, cancellationToken)
                .ConfigureAwait(false);
        }

        var why = Optional(request, "why") ?? HandOverWhy.Press;
        var chat = _loop.Chat ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);
        var handed = await chat.HandOverAsync(
                id, agent, Optional(request, "account"), why, HandOverAskedBy(why, Optional(request, "askedBy")), config,
                onEnded: (session, state) => DriverLoop.Ended(_events, session, state), cancellationToken)
            .ConfigureAwait(false);
        _loop.Nudge();
        return new { handed.SessionId, handed.Kept, handed.Why, handed.Message };
    }

    // A go-ahead a parked session asked, answered on the session's own page (KNOWUSE1a2, D135 §2), by the rule the ask's
    // door keeps (GOAHEAD2b): through that door with `goesOn`, so the service answers the park only where none of the
    // go-aheads the session asked is still open, and the resumed conversation is handed every answer. The person's words
    // are the go-ahead's, as on the ask's page, never said to the park: there they would send it on with one still open, or
    // join a *carry on.* that was never theirs (GOAHEAD2). A refused go-ahead answers nothing else, in the service's sentence.
    // `{ message, sent, reaches, why, waits }`: the go-ahead's sentence, what became of the park as `SESSION_INPUT` says it,
    // and whether it still waits on the person. It moves the ask and the park (STATEHUB1h, D169).
    [DriverRoute("SESSION_GO_AHEAD")]
    [Publishes("SESSION_GO_AHEAD", StateScopes.Sessions | StateScopes.Asks, Session = "id")]
    private async Task<object?> SessionGoAheadAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var ask = PayloadHelper.GetRequiredValue<string>(request.Payload, "ask");
        var approved = PayloadHelper.GetRequiredValue<bool>(request.Payload, "approved");
        var number = Number(request, "number") is { } named and > 0 and <= int.MaxValue
            ? (int)named
            : throw new DriverException("a go-ahead is named by its number on its ask, a whole number from 1; nothing was answered.");
        var words = Optional(request, "words")?.Trim() is { Length: > 0 } said ? said : null;
        var service = _loop.Service ?? throw NotReady();

        var (answered, message) = await service.AnswerGoAheadAsync(ask, number, approved, words, cancellationToken, goesOn: true)
            .ConfigureAwait(false);
        if (!answered) throw new DriverException(message);

        var (park, waits) = await _loop.Words.AfterGoAheadAsync(id, cancellationToken).ConfigureAwait(false);
        return new { Message = message, park.Sent, park.Reaches, park.Why, Waits = waits };
    }

    // Finishing a conversation rather than cutting it off: the harness gets end-of-input, says
    // what it was going to say, and exits on its own. `STOP_SESSION` is the other verb. Refused
    // for a session that takes no input, for the same reason as a message.
    [DriverRoute("END_CHAT")]
    [Publishes("END_CHAT", StateScopes.Sessions, Session = "id")]
    private object? EndChat(IpcRequest request)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        if (_loop.Processes.RefusesInput(id) is { } why) throw new DriverException(why);
        // Through the conversation, which knows its door: on the protocol door, the turns asked
        // for, then `session/close`, then the end of input (CONV3b).
        return new { Ended = _loop.Chat?.Finish(id) ?? _loop.Processes.CloseInput(id) };
    }

    // Stopping the turn and keeping the conversation (CONV4a) — the third verb, beside finishing
    // and stopping the session. What was waiting comes back, so the page can hand it to the
    // person rather than lose it. Refused for a session that takes no input, as a message is,
    // and for a door that carries only text, in the driver's words: it has no turn to stop. It publishes as a word said does
    // (STATEHUB1h, D169): a driven session's held words go as its next prompt. `keep: true` (STEER2a) stops a conversation's
    // turn keeping what waits for the next turn, withdrawing nothing: the keeping stop (COWORK1a, the message-urgency design
    // §3.2), the person's own press beside words held for the turn's end, which no word presses for them (D188 point 3).
    // RPC1d: `sessions stop <id> --turn` sent over the pipe asks for the verb's own words.
    [DriverRoute("CANCEL_TURN")]
    [Publishes("CANCEL_TURN", StateScopes.Sessions, Session = "id")]
    private async Task<object?> CancelTurnAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        if (Flag(request, "said"))
        {
            return await SessionsVerbs.SaidAsync(context, new SessionsAsk("stop-turn") { Ids = [id] }, cancellationToken).ConfigureAwait(false);
        }

        // A driven session's stop sends what is held NOW (SESS3): the turn stops, nothing is
        // withdrawn, and the person's words are its next prompt. With nothing held it stops nothing.
        if (_loop.Processes.InboxOf(id) is { } inbox)
        {
            var now = await inbox.SendNowAsync().ConfigureAwait(false);
            return new { now.Cancelled, Withdrawn = Array.Empty<object>() };
        }

        if (_loop.Processes.RefusesInput(id) is { } why) throw new DriverException(why);
        var stop = _loop.Chat is { } chat ? await chat.CancelTurnAsync(id, keep: Flag(request, "keep")).ConfigureAwait(false) : TurnStop.Nothing;
        return new { stop.Cancelled, Withdrawn = stop.Withdrawn.Select(Said).ToArray() };
    }

    // Taking back or rewording one word that waits behind a running turn (QUEUEEDIT1a, D76): `{ session, id, text? }`, where
    // `id` is the one SESSION_QUEUE and SESSION_QUEUED name it by. With no text it removes the word; with some it replaces its
    // words where it waits, order and files kept, under a new id. A turn that took it already is the answer `{ done: false,
    // why: "taken" }`, as a stop with nothing to stop is: the page asking a moment late deserves no error. Refused for a
    // session that takes no input, as CANCEL_TURN is. The queue tells every list as it moves (SESSION_QUEUED). A word nothing
    // here holds may wait on the session's record, parked or ended (SAIDBACK1b): the service's take-back door answers for it,
    // and refuses what never goes on in its own sentence. A word taken back from a record moves it, so the edit publishes as a
    // word said does (STATEHUB1h, D169), naming the session by its `session` field.
    [DriverRoute("SESSION_QUEUE_EDIT")]
    [Publishes("SESSION_QUEUE_EDIT", StateScopes.Sessions, Session = "session")]
    private async Task<object?> SessionQueueEditAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        string? text = null;
        if (request.Payload is { } payload && payload.TryGetProperty("text", out var said) && said.ValueKind == JsonValueKind.String)
        {
            text = said.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new DriverException("Say what it should say instead, or take it back: words reworded to nothing are not kept.");
            }
        }

        // COWORK1a (the message-urgency design §8.2): `level` changes the urgency of a word that waits for a running turn, under its
        // own id; raised where the door takes words at its next step, it goes there.
        MessageLevel? level = Optional(request, "level") is null ? null : LevelOf(request);

        // RPC1d: `sessions take-back` and `reword` sent over the pipe ask for the verb's own words, which the service's take-back door
        // judges as the alone path asks it.
        if (Flag(request, "said"))
        {
            return await SessionsVerbs.SaidAsync(
                context, new SessionsAsk(text is null ? "take-back" : "reword") { Ids = [session], Word = id, Text = text }, cancellationToken)
                .ConfigureAwait(false);
        }

        // A driven session's held words; a conversation's turns otherwise. The one that holds the word answers.
        QueueEdit edit;
        if (_loop.Processes.InboxOf(session) is { } inbox)
        {
            // COWORK1c: a word kept on the record for the run is taken back there too, and reworded as a word said is kept.
            edit = await inbox.EditAsync(id, text, level, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (_loop.Processes.RefusesInput(session) is { } why) throw new DriverException(why);
            edit = _loop.Chat is { } chat ? await chat.EditQueuedAsync(session, id, text, level, cancellationToken).ConfigureAwait(false) : QueueEdit.Taken;
        }

        // Neither holds it: the record's words, which only the service can take back (SAIDBACK1b). A record that runs again
        // answers taken there, so a word a live turn took a moment ago is still the answer it was. A level alone is never asked of
        // the record: its words wait for no running turn, and a take-back with no words would remove them.
        if (edit == QueueEdit.Taken && (text is not null || level is null))
        {
            edit = await _loop.Words.TakeBackAsync(session, id, text, cancellationToken).ConfigureAwait(false);
        }

        return edit == QueueEdit.Done ? new { Done = true } : new { Done = false, Why = "taken" };
    }

    // A conversation's model and effort, as its agent offers them on the protocol door (AGT6b, D98):
    // the page asks once, and takes every change after that as `SESSION_OPTIONS_CHANGED`. Nothing here
    // holding it, or a door that carries none, is an empty list — never a refusal, since a composer
    // asks of every conversation it shows.
    [DriverRoute("SESSION_OPTIONS")]
    private object? SessionOptions(IpcRequest request)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return OptionsAnswer(id, _loop.Chat?.Options(id) ?? []);
    }

    // The person's change to one of them (AGT6b): `session/set_config_option` on the conversation's
    // session. The driver refuses the mode, an option never offered and a conversation it does not
    // hold, in its own words; the agent refuses a value it does not take, in its. It declares nothing (STATEHUB1h, D169): every
    // window hears the options whole as `SESSION_OPTIONS_CHANGED` when they change.
    [DriverRoute("SET_SESSION_OPTION")]
    private async Task<object?> SetSessionOptionAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var option = PayloadHelper.GetRequiredValue<string>(request.Payload, "option");
        var value = PayloadHelper.GetRequiredValue<string>(request.Payload, "value");
        var chat = _loop.Chat ?? throw NotReady();
        var after = await chat.SetOptionAsync(id, option, value, cancellationToken).ConfigureAwait(false);
        return OptionsAnswer(id, after);
    }

    // Where a conversation's turns stand (CONV4a): whether one is in flight, and what is waiting.
    // A page that just opened it asks once, and takes every change after that as `SESSION_QUEUED`. What waits on a parked or
    // ended record is read with the record (SAIDBACK1b), so it is listed here and in each `SESSION_QUEUED` the record tells.
    [DriverRoute("SESSION_QUEUE")]
    private async Task<object?> SessionQueueAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var reach = await _loop.Words.ReachAsync(id, cancellationToken).ConfigureAwait(false);
        return QueueAnswer(_loop, id, reach);
    }

    /// <summary>
    /// Where a session's turns stand and what a word said now would do, whole (CONV4a, MSG1d, MSG1f2): the queue's answer, and
    /// <c>SESSION_QUEUED</c> where a record's move or a later session of its quest told it again, in one shape.
    /// </summary>
    /// <remarks>
    /// <c>Listening</c> says a running driven session's door is open (SESS3), on the protocol door or, where its run can go on
    /// in its own conversation, the native door (MSG1b); a door that closed listens no longer, though the registry holds it a
    /// moment longer. Whether the page offers a box is <c>reaches</c> and <c>why</c>'s: what a word said now would do (D137
    /// §5.3), and why nothing takes one, which is when it draws the line instead. <c>reach</c> says the same as an object
    /// (<see cref="ReachOf"/>), which tells a page this shell follows it live.
    ///
    /// <c>Queued</c> is what an inbox or a conversation's turns hold here, or, where neither holds any, the person's words
    /// waiting on the record as <paramref name="reach"/> read it (SAIDBACK1b): each by its id, which <c>SESSION_QUEUE_EDIT</c>
    /// takes back through the service. Read with the reach, never apart from it, so this answer stays whole and synchronous
    /// for <c>SESSION_QUEUED</c>, and a record that tells its reach again lists its words again, never an empty list over them.
    /// </remarks>
    internal static object QueueAnswer(DriverLoop loop, string id, WordsAnswer reach)
    {
        var inbox = loop.Processes.InboxOf(id);
        // Ask Daoris's conversation still starting holds its words itself, opening (HELPSTART1).
        var queue = loop.HelpStarting.Queue(id) ?? inbox?.State ?? loop.Chat?.Queue(id) ?? ChatQueue.Idle;
        var queued = inbox is null && queue.Queued.Count == 0 && reach.Waiting is { } waiting
            ? waiting.Select(Said).ToArray()
            : queue.Queued.Select(Said).ToArray();
        return new
        {
            Session = id, Queued = queued, queue.Taking, queue.Opening,
            Listening = inbox is { State.Taking: true },
            // When its last turn ended here (RAIL2), the page's "moved" for a live chat.
            LastTurn = queue.LastTurnEnded,
            reach.Reaches,
            reach.Why,
            // The conversation it went on in, where `why` is `continued` (HANDWORDS1): where a word goes instead.
            reach.To,
            Reach = ReachOf(reach),
        };
    }

    /// <summary>
    /// What a word said now would do, as <c>SESSION_QUEUED</c> carries it (MSG1f2): <c>{ reaches, why }</c>, an object even
    /// where both are null (a word that would start a turn at once), because the bridge leaves a null out and an absent
    /// field could not tell that from a shell older than the field. Null, and so absent, where nothing is known: nothing here
    /// runs the session and its record has not answered. <c>to</c> names the conversation a <c>continued</c> one went on in
    /// (HANDWORDS1), and is left out with the rest of the nulls.
    /// </summary>
    /// <remarks>
    /// <c>steers</c> and <c>keepingStop</c> (COWORK1a, the message-urgency design §8.1-§8.2) say what a running session's door can
    /// do while a turn runs: whether its agent steers, so the composer offers <i>Now, cutting its step</i>; and whether it can stop
    /// a turn keeping the session, so the keeping stop is offered beside words held for the turn's end. Left out where nothing
    /// here runs it, its door is not known yet, or it carries only text.
    /// </remarks>
    public static object? ReachOf(WordsAnswer? reach) =>
        reach is null or { Sent: false, Why: null }
            ? null
            : new { reach.Reaches, reach.Why, reach.To, Steers = reach.Door?.Steers, KeepingStop = reach.Door?.KeepingStop };

    /// <summary>
    /// The level a press asked for (COWORK1a): its <c>level</c>, or an older page's <c>now: true</c>, else the person's default,
    /// urgent. A level this build does not know is refused in a sentence, before anything is kept.
    /// </summary>
    internal static MessageLevel LevelOf(IpcRequest request)
    {
        var word = Optional(request, "level");
        if (word is not null && MessageLevels.Read(word) is null)
        {
            throw new DriverException($"`{word}` is no level: when it reads your words is ordinary, urgent or now; nothing was kept.");
        }

        return MessageLevels.Asked(word, Flag(request, "now"));
    }

    /// <summary>
    /// The keeping stop (COWORK1a, the message-urgency design §3.2): a running session's turn stopped so what waits goes next, the
    /// person's own press, answered as a <see cref="KeepingStops"/> code for <c>sessions stop &lt;id&gt; --turn</c>. A driven session's
    /// door stops only where it can keep the session and words wait for its turn's end; a conversation's stops keeping what waits.
    /// </summary>
    /// <exception cref="DriverException">A session that takes no input, or a conversation that carries only text, in the driver's words.</exception>
    internal static async Task<string> KeepingStopAsync(DriverLoop loop, string id)
    {
        if (loop.Processes.InboxOf(id) is { } inbox)
        {
            if (!inbox.KeepingStop) return KeepingStops.NoInterrupt;
            return (await inbox.SendNowAsync().ConfigureAwait(false)).Cancelled ? KeepingStops.Stopped : KeepingStops.Nothing;
        }

        if (loop.Processes.RefusesInput(id) is { } why) throw new DriverException(why);
        if (loop.Chat is not { } chat || !loop.Processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase)) return KeepingStops.NotHere;
        if (chat.Door(id) is { KeepingStop: false }) return KeepingStops.NoInterrupt;
        // A text-only door refuses here, in the driver's words: it has no turn to stop.
        return (await chat.CancelTurnAsync(id, keep: true).ConfigureAwait(false)).Cancelled ? KeepingStops.Stopped : KeepingStops.Nothing;
    }

    /// <summary>
    /// A conversation's options as the page reads them (AGT6b), the answer and the live event alike: each
    /// option's id, the agent's name for it, its category, its value now, and the values it takes — the
    /// agent's words, carried as they were given.
    /// </summary>
    public static object OptionsAnswer(string session, IReadOnlyList<AcpConfigOption> options) => new
    {
        Session = session,
        Options = options.Select(option => new
        {
            option.Id,
            option.Name,
            option.Category,
            option.Current,
            Choices = option.Choices.Select(choice => new { choice.Value, choice.Name, choice.Description }).ToArray(),
        }).ToArray(),
    };

    /// <summary>
    /// A message's attached files as the page sent them (CONV4c): each a name and its bytes as base64,
    /// the shape a quest's uploads take. Bytes that are not base64 are refused in a sentence — kept,
    /// they would be a file nobody sent.
    /// </summary>
    public static IReadOnlyList<ChatUpload> FilesOf(JsonElement payload)
    {
        if (!payload.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array) return [];

        var uploads = new List<ChatUpload>();
        foreach (var file in files.EnumerateArray())
        {
            var name = file.ValueKind == JsonValueKind.Object && file.TryGetProperty("name", out var n)
                && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
            var content = file.ValueKind == JsonValueKind.Object && file.TryGetProperty("content", out var c)
                && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            try
            {
                uploads.Add(new ChatUpload(name, Convert.FromBase64String(content ?? throw new FormatException())));
            }
            catch (FormatException)
            {
                throw new DriverException($"`{name}` did not arrive as a file's bytes, so it was not attached. Attach it again.");
            }
        }

        return uploads;
    }

    /// <summary>
    /// A message as the page is told it: the words, the names of its files — never where they are kept — the id a queued
    /// word is taken back or reworded by (QUEUEEDIT1a), null where it has none, and the level it waits at (COWORK1a), which the
    /// held line names and changes.
    /// </summary>
    public static object Said(ChatMessage message) =>
        new { message.Id, message.Text, Files = message.Files.Select(file => file.Name).ToArray(), Level = MessageLevels.Spelled(message.Level) };

    /// <summary>A word waiting on a record (SAIDBACK1b) in the same shape: its id on the record, its words, its files' names.</summary>
    public static object Said(WaitingWord word) => new { word.Id, word.Text, Files = word.Files.ToArray() };
}
