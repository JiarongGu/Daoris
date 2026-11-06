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
    [DriverRoute("SESSION_HISTORY", PipeClass.Open)]
    private object? SessionHistory(SessionHistoryRead press)
    {
        var id = press.Id;
        var page = press.After is { } after
            ? _loop.Events.After(id, after)
            : _loop.Events.Page(id, press.Before, (int)(press.Limit ?? SessionEventText.PageLimit));
        return new { Session = id, Events = page.Events.ToArray(), page.Earlier, page.Latest, page.Opening, page.FirstFailure };
    }

    // A conversation in a repository (D49 §3). The record is the service's and the lock is the
    // ledger's; what only this side can do is put a harness behind it — a process on this
    // machine, which never leaves it (D46 §7).
    // A start changes the sessions and the driver's state its loop, nudged, reads again (STATEHUB1d, D169), as each press below
    // that opens a conversation does.
    [DriverRoute("START_CHAT", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("START_CHAT", None = OwnerNone.OwnConversation)]
    [GroundRepository("START_CHAT", "repository")]
    [Says("START_CHAT", "repository:repository", "adapter:agent", "text:words")]
    [Publishes("START_CHAT", StateScopes.Sessions | StateScopes.Driver)]
    private async Task<object?> StartChatAsync(StartChatPress press, CancellationToken cancellationToken)
    {
        var repository = press.Repository;
        var chat = _loop.Chat ?? throw NotReady();

        var config = DriverConfig.Load(_loop.ConfigPath);
        // A blank adapter is none named, and Chat's order is walked (D205 §4.1, MODELROLE1d); one named is the person's pick, its model
        // Chat's first entry for that agent (§3.5(d)). This route took a blank one as a name (REV3 CLEAN1); the page happens never to
        // send one.
        var (adapter, order) = ActorChoices.NewChat(config, press.Adapter);

        // The end of a conversation is news the page wants without asking: the drawer is
        // probably open, and a record that moved silently reads as one that hung.
        Task OnEnded(string session, string state) => DriverLoop.Ended(_events, session, state);
        // The per-session picker (D49 §4). Absent takes the workspace's default, then the
        // machine's, then the harness's own configuration home.
        var profile = press.Profile;

        // The opening words (NEW1a, D162): the conversation opens on them. Absent, blank or whitespace is the old empty start.
        var text = press.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            var started = await chat.StartWithAsync(
                repository, adapter, config, text, UploadsOf(press.Files), press.RequestId,
                ownTree: press.OwnTree, profile: profile, onEnded: OnEnded, ct: cancellationToken, order: order);
            _loop.Nudge();
            return new { started.SessionId, started.Sent, started.Why, started.Message };
        }

        var start = await chat.StartAsync(
            repository, adapter, config, OnEnded, profile,
            // The per-conversation tree choice (D51). Absent falls back to the repository's
            // standing opt-in, which the runner reads from the same config.
            ownTree: press.OwnTree,
            ct: cancellationToken, order: order, picked: !string.IsNullOrWhiteSpace(profile));

        _loop.Nudge();
        return new { start.SessionId, start.Message };
    }

    // The person's words to a session, whatever its state: `{ sent, reaches, why }`. A running session hears them at its door;
    // a parked or ended one keeps them on its record and goes on with them. A running session that takes no input is refused
    // in the driver's words, since `sent: false` would tell a stale page it ended. Every answer publishes the sessions and Ask
    // Daoris's history, since the route cannot tell a help conversation's words from another's.
    [DriverRoute("SESSION_INPUT", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_INPUT", Session = "id")]
    [GroundSession("SESSION_INPUT", "id")]
    [GroundRead("SESSION_INPUT", nameof(PipeReads.QuestionOpen))]
    [GroundRead("SESSION_INPUT", nameof(PipeReads.AttachedFiles))]
    [Says("SESSION_INPUT", "id:session", "text:words", "level:level", "paths:files", "said:form")]
    [Publishes("SESSION_INPUT", StateScopes.Sessions | StateScopes.Help, Session = "id")]
    private async Task<object?> SessionInputAsync(SessionInputPress press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var text = press.Text;
        // COWORK1a (D188 point 1): `level` is when the words should reach a working session, `ordinary`, `urgent` or `now`; none is
        // the person's default, urgent. No level stops a turn.
        var level = LevelOf(press.Level);
        // RPC1d: `sessions say` sent over the pipe asks for the verb's own words and names the files it was given by their paths on this
        // machine, which the verb reads as it does alone; the page asks for none and attaches its files whole.
        if (press.Said)
        {
            return await SessionsVerbs.SaidAsync(
                context,
                new SessionsAsk("say")
                {
                    Ids = [id], Text = text, Files = [.. press.Paths ?? []], Level = press.Level,
                },
                cancellationToken)
                .ConfigureAwait(false);
        }

        // PARKVIEW1 (the park's ask design §4): a press answering a park's question names it, and words for a question the park no
        // longer asks (answered and asked anew in another window, or gone as its record went on) are kept nowhere, said by their code,
        // so a label never answers a question it was not offered for. Fable's read, Q4: words that begin with one of its answers, as
        // the page composes a press, are marked with it, so the conversation draws a choice and the session is handed one.
        string? choice = null;
        var asking = press.Question;
        if (asking is { } question)
        {
            if (new QuestionFiles(_loop.Home).Read(id) is not { } asked || asked.Id != question)
            {
                return new { Sent = false, Reaches = (string?)null, Why = "question-changed", To = (string?)null, Now = false };
            }

            choice = QuestionWords.Pressed(asked, text);
        }

        // FLOWCHART2d (D223): the workflow draft open in the person's editor, which the page sends where its fingerprint moved since this
        // conversation was told: refused whole past its bound before anything is kept, else handed with these words beside the palette it is
        // composed from, which the room has no reader of.
        if (press.Draft is { } drafted && drafted.ValueKind != JsonValueKind.Null)
        {
            var draft = WorkflowDraftTold.Read(drafted);
            var palette = WorkflowEditRoutes.PaletteOf(_loop.Home).Compact();
            _loop.Chat?.HandDraft(id, draft with { Palette = new WorkflowDraftPalette(WorkflowPalette.DigestOf(palette), palette) });
        }

        // What the person attached, kept for its session before the words go (CONV4c).
        var files = UploadsOf(press.Files);
        // Where the person is (HELP1b), which a conversation's agent is handed ahead of the words; absent for most. `now` is the
        // person's *Send now* (STEER2a), which a running session carries out by its door.
        // COWORK1c: `key`, the press's own where the page names one, so a press that arrives twice keeps one word; the shell gives each
        // press one otherwise.
        var said = await _loop.Words
            .SayAsync(id, text, files, press.Preface, door: "screen", cancellationToken, level, press.Key, choice, asking)
            .ConfigureAwait(false) ?? throw NotReady();
        // Words a running session took are kept on its ask now (DRIFT1a2); words kept on a record are kept there once a
        // session takes them (MSG1a's taken door), so they are never posted twice.
        if (said.Running) _loop.Words.KeepOnAsk(id, text);
        // SUPERVISOR1a-i (the supervisor design §6.3): the owner's answer to a question a supervisor's decision is on supersedes it at
        // once; the service's one owner-word operation took its released word back in the same step.
        if (said.Sent && asking is not null) await _loop.Supervisor.SupersedeAsync(id, asking, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        // `to`: the conversation a conversation handed over or moved went on in, where `why` is `continued` (HANDWORDS1).
        // `now`: the press was carried out; false where it was not asked, or the door could do neither, `why` saying so.
        return new { said.Sent, said.Reaches, said.Why, said.To, said.Now };
    }

    // *Start a conversation with these words*: words a session cannot go on with start a conversation in its repository, handed
    // with a preface naming the session, as one act of the chat runner's; what cannot go on is refused by its code before it starts.
    [DriverRoute("SESSION_START_FROM", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_START_FROM", Session = "id")]
    [GroundSession("SESSION_START_FROM", "id")]
    [Says("SESSION_START_FROM", "id:session")]
    [Publishes("SESSION_START_FROM", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SessionStartFromAsync(SessionIdPress press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        // The terminal's `session start-from` sends this route as the window does, with `hold: true` beside it (RPC1g3, D238), and holds
        // the conversation this answer names.
        var chat = _loop.Chat ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);
        // A new conversation: Chat's order walked (MODELROLE1d, D205 §4.1).
        var (adapter, order) = ActorChoices.NewChat(config);
        var started = await chat.StartFromAsync(
                id, adapter, config, onEnded: (session, state) => DriverLoop.Ended(_events, session, state), cancellationToken, order)
            .ConfigureAwait(false);
        _loop.Nudge();
        return new { started.SessionId, started.Sent, started.Why, started.Message };
    }

    // *Stop waiting*: an ended conversation held by its account's limit waits no more, so nothing goes on by itself; a refusal is
    // data in the runner's sentence.
    [DriverRoute("SESSION_STOP_WAITING", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_STOP_WAITING", Session = "id")]
    [GroundSession("SESSION_STOP_WAITING", "id")]
    [Says("SESSION_STOP_WAITING", "id:session")]
    [Publishes("SESSION_STOP_WAITING", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SessionStopWaitingAsync(SessionIdPress press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var chat = _loop.Chat ?? throw NotReady();
        var refused = await chat.StopWaitingAsync(id, cancellationToken).ConfigureAwait(false);
        if (refused is null) _loop.Nudge();
        return new { Stopped = refused is null, Message = refused ?? "" };
    }

    // *Go on with another agent…*: the chat runner keeps the ask, taken at once for a conversation not talking, at its turn's end for
    // one that is. `near` and `limit` are Daoris's own offer's reasons, so only with them is `askedBy` `daoris` honoured.
    public static string HandOverAskedBy(string why, string? askedBy) =>
        askedBy == HandOverBy.Daoris && why is HandOverWhy.Near or HandOverWhy.Limit ? HandOverBy.Daoris : HandOverBy.Person;

    [DriverRoute("SESSION_HAND_OVER", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_HAND_OVER", Session = "id")]
    [GroundSession("SESSION_HAND_OVER", "id")]
    [Says("SESSION_HAND_OVER", "id:session", "agent:agent")]
    [Publishes("SESSION_HAND_OVER", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SessionHandOverAsync(SessionHandOverPress press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var named = string.IsNullOrWhiteSpace(press.Agent) ? null : press.Agent.Trim();
        // RPC1d: `sessions hand-over` sent over the pipe asks for the verb's own words (the person's press, its reason `press`).
        if (press.Said)
        {
            return await SessionsVerbs.SaidAsync(
                context, new SessionsAsk("hand-over") { Ids = [id], Agent = named, Account = press.Account }, cancellationToken)
                .ConfigureAwait(false);
        }

        // No agent named: the walk's pick takes the words, *Go on in a new session* or, for a conversation, *Go on with Daoris's brief*.
        if (named is null)
        {
            return await GoOnPress.AskAsync(
                    _loop, id, DriverConfig.Load(_loop.ConfigPath), (session, state) => DriverLoop.Ended(_events, session, state), cancellationToken)
                .ConfigureAwait(false);
        }

        var agent = named;

        var why = press.Why ?? HandOverWhy.Press;
        // A quest's own session is handed over through `sessions hand-over`'s judge, as a driven move the next look takes: no
        // conversation opens here.
        if (_loop.Service is { } service
            && (await service.SessionRecordsAsync(cancellationToken).ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal)) is { } record
            && QuestHandOver.IsQuestSession(record))
        {
            var moves = new ConversationMoves(_loop.Home, service, _loop.Events, _loop.Output, scope: MoveTakeUpScope.LeaseHolder);
            var answer = await QuestHandOver.AskAsync(
                    service, moves, SessionsCommand.DrivesBy(_loop.Home), id, agent, press.Account, why, HandOverBy.Person,
                    cancellationToken, model: press.Model, effort: press.Effort)
                .ConfigureAwait(false);
            if (answer.Kept)
            {
                // HANDOVERSAID1: the session's record names who asked, the person by the window's press.
                HandOverAskNotes.Say(_loop.Events, _loop.Output, id, HandOverAskNotes.AskedBy(HandOverAsker.Window, agent));
                SessionWatch.Of(_loop.Events).Pressed(id);
                _loop.Nudge();
            }

            return new { SessionId = (string?)null, answer.Kept, answer.Why, answer.Message };
        }

        var chat = _loop.Chat ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);
        var handed = await chat.HandOverAsync(
                id, agent, press.Account, why, HandOverAskedBy(why, press.AskedBy), config,
                onEnded: (session, state) => DriverLoop.Ended(_events, session, state), cancellationToken,
                // STAFFPOLICY1d: the choice the offer named, started as approved.
                model: press.Model, effort: press.Effort)
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
    // PARKVIEW1 (the park's ask design §4): `goesOn: false` where the page answers the park itself, with the answer to the question the
    // park also asked (GOAHEAD2's provision for such a caller): the go-ahead is kept and the park waits for that answer. Absent is true.
    [DriverRoute("SESSION_GO_AHEAD", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_GO_AHEAD", Session = "id")]
    [GroundWait("SESSION_GO_AHEAD", "go-ahead", "ask", "number")]
    [Says("SESSION_GO_AHEAD", "id:session", "ask:ask", "words:words")]
    [Publishes("SESSION_GO_AHEAD", StateScopes.Sessions | StateScopes.Asks, Session = "id")]
    private async Task<object?> SessionGoAheadAsync(SessionGoAheadPress press, CancellationToken cancellationToken)
    {
        var (id, ask, approved) = (press.Id, press.Ask, press.Approved);
        var number = press.Number is { } named and > 0 and <= int.MaxValue
            ? (int)named
            : throw new DriverException("a go-ahead is named by its number on its ask, a whole number from 1; nothing was answered.");
        var words = press.Words?.Trim() is { Length: > 0 } said ? said : null;
        var goesOn = press.GoesOn != false;
        var service = _loop.Service ?? throw NotReady();

        var (answered, message) = await service.AnswerGoAheadAsync(ask, number, approved, words, cancellationToken, goesOn: goesOn)
            .ConfigureAwait(false);
        if (!answered) throw new DriverException(message);

        var (park, waits) = await _loop.Words.AfterGoAheadAsync(id, cancellationToken).ConfigureAwait(false);
        return new { Message = message, park.Sent, park.Reaches, park.Why, Waits = waits };
    }

    // Finishing a conversation rather than cutting it off: the harness gets end-of-input, says
    // what it was going to say, and exits on its own. `STOP_SESSION` is the other verb. Refused
    // for a session that takes no input, for the same reason as a message.
    [DriverRoute("END_CHAT", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("END_CHAT", Session = "id")]
    [GroundSession("END_CHAT", "id")]
    [Says("END_CHAT", "id:session")]
    [Publishes("END_CHAT", StateScopes.Sessions, Session = "id")]
    private object? EndChat(SessionIdPress press)
    {
        var id = press.Id;
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
    [DriverRoute("CANCEL_TURN", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("CANCEL_TURN", Session = "id")]
    [GroundSession("CANCEL_TURN", "id")]
    [Says("CANCEL_TURN", "id:session")]
    [Publishes("CANCEL_TURN", StateScopes.Sessions, Session = "id")]
    private async Task<object?> CancelTurnAsync(CancelTurnPress press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        if (press.Said)
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
        // CLITURN1: a turn another Daoris process runs is stopped there; "no turn was running" would be untrue of it.
        if (RunsElsewhere(_loop, id))
        {
            throw new DriverException($"session {id} is run by another Daoris process on this machine, a terminal's: stop its turn there with Ctrl+C.");
        }

        var stop = _loop.Chat is { } chat ? await chat.CancelTurnAsync(id, keep: press.Keep).ConfigureAwait(false) : TurnStop.Nothing;
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
    [DriverRoute("SESSION_QUEUE_EDIT", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_QUEUE_EDIT", Session = "session")]
    [GroundRead("SESSION_QUEUE_EDIT", nameof(PipeReads.QueuedWord))]
    [Says("SESSION_QUEUE_EDIT", "session:session", "text:words", "level:level")]
    [Publishes("SESSION_QUEUE_EDIT", StateScopes.Sessions, Session = "session")]
    private async Task<object?> SessionQueueEditAsync(SessionQueueEditPress press, CancellationToken cancellationToken)
    {
        var (session, id) = (press.Session, press.Id);
        string? text = null;
        if (press.Text is { ValueKind: JsonValueKind.String } said)
        {
            text = said.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new DriverException("Say what it should say instead, or take it back: words reworded to nothing are not kept.");
            }
        }

        // COWORK1a (the message-urgency design §8.2): `level` changes the urgency of a word that waits for a running turn, under its
        // own id; raised where the door takes words at its next step, it goes there.
        MessageLevel? level = press.Level is null ? null : LevelOf(press.Level);

        // RPC1d: `sessions take-back` and `reword` sent over the pipe ask for the verb's own words, which the service's take-back door
        // judges as the alone path asks it.
        if (press.Said)
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

    // Moving one word held on a part's record to the head the page shows as `to` (LINESTOGETHER1b, the review-flow design §2), through the
    // service's move door; its answers are the desktop README's. A refusal is data, never thrown, so the page words its code and asks again
    // after `not-known`.
    [DriverRoute("SESSION_QUEUE_MOVE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_QUEUE_MOVE", Session = "session")]
    [Owner("SESSION_QUEUE_MOVE", Session = "to")]
    [GroundRead("SESSION_QUEUE_MOVE", nameof(PipeReads.QueuedWord))]
    [GroundSession("SESSION_QUEUE_MOVE", "to")]
    [Says("SESSION_QUEUE_MOVE", "session:session", "to:session")]
    [Publishes("SESSION_QUEUE_MOVE", StateScopes.Sessions, Session = "session")]
    private async Task<object?> SessionQueueMoveAsync(SessionQueueMovePress press, CancellationToken cancellationToken)
    {
        var (session, id, to) = (press.Session, press.Id, press.To);
        var moved = await _loop.Words.MoveAsync(session, id, to, cancellationToken).ConfigureAwait(false);
        if (!moved.Moved) return new { Moved = false, moved.Why, moved.Message, moved.To };
        return moved.Again ? new { Moved = true, Again = true, moved.To } : (object)new { Moved = true, moved.To, Word = moved.Word?.Id };
    }

    // A conversation's model and effort, as its agent offers them on the protocol door (AGT6b, D98):
    // the page asks once, and takes every change after that as `SESSION_OPTIONS_CHANGED`. Nothing here
    // holding it, or a door that carries none, is an empty list — never a refusal, since a composer
    // asks of every conversation it shows.
    [DriverRoute("SESSION_OPTIONS", PipeClass.Open)]
    private object? SessionOptions(SessionIdPress press)
    {
        var id = press.Id;
        return OptionsAnswer(id, _loop.Chat?.Options(id) ?? []);
    }

    // The person's change to one of them (AGT6b): `session/set_config_option` on the conversation's
    // session. The driver refuses the mode, an option never offered and a conversation it does not
    // hold, in its own words; the agent refuses a value it does not take, in its. It declares nothing (STATEHUB1h, D169): every
    // window hears the options whole as `SESSION_OPTIONS_CHANGED` when they change.
    [DriverRoute("SET_SESSION_OPTION", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_SESSION_OPTION", Session = "id")]
    [GroundSession("SET_SESSION_OPTION", "id")]
    [GroundRead("SET_SESSION_OPTION", nameof(PipeReads.SessionOption))]
    [Says("SET_SESSION_OPTION", "id:session")]
    private async Task<object?> SetSessionOptionAsync(SetSessionOptionPress press, CancellationToken cancellationToken)
    {
        var (id, option, value) = (press.Id, press.Option, press.Value);
        var chat = _loop.Chat ?? throw NotReady();
        var after = await chat.SetOptionAsync(id, option, value, cancellationToken).ConfigureAwait(false);
        return OptionsAnswer(id, after);
    }

    // Where a conversation's turns stand (CONV4a): whether one is in flight, and what is waiting.
    // A page that just opened it asks once, and takes every change after that as `SESSION_QUEUED`. What waits on a parked or
    // ended record is read with the record (SAIDBACK1b), so it is listed here and in each `SESSION_QUEUED` the record tells.
    [DriverRoute("SESSION_QUEUE", PipeClass.Open)]
    private async Task<object?> SessionQueueAsync(SessionIdPress press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var reach = await _loop.Words.ReachAsync(id, cancellationToken).ConfigureAwait(false);
        return QueueAnswer(_loop, id, reach);
    }

    /// <summary>
    /// Where a session's turns stand and what a word said now would do, whole: the queue's answer, and <c>SESSION_QUEUED</c>'s, in one
    /// shape. <c>Queued</c> is read with the reach, never apart from it, so a record that tells its reach again lists its words
    /// again, never an empty list over them.
    /// </summary>
    internal static object QueueAnswer(DriverLoop loop, string id, WordsAnswer reach)
    {
        var inbox = loop.Processes.InboxOf(id);
        // CLITURN1: a conversation another Daoris process runs (a terminal's) is that process's to tell: what it told, or nothing
        // known, which the answer leaves without a `taking` so the page keeps the record's word instead of reading idle.
        var elsewhere = inbox is null && loop.HelpStarting.Queue(id) is null && RunsElsewhere(loop, id);
        var told = elsewhere ? new TurnMarks(loop.Home).Read(id) : null;
        // Ask Daoris's conversation still starting holds its words itself, opening (HELPSTART1).
        var queue = loop.HelpStarting.Queue(id) ?? inbox?.State
            ?? (elsewhere ? new ChatQueue(told?.Taking ?? false, [], told?.LastTurnEnded, told?.Opening ?? false) : loop.Chat?.Queue(id))
            ?? ChatQueue.Idle;
        var queued = inbox is null && queue.Queued.Count == 0 && reach.Waiting is { } waiting
            ? waiting.Select(Said).ToArray()
            : queue.Queued.Select(Said).ToArray();
        return new
        {
            Session = id, Queued = queued, Taking = elsewhere && told is null ? (bool?)null : queue.Taking, queue.Opening,
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
    /// Whether another Daoris process of this machine runs the session and this one holds nothing of it (CLITURN1): a terminal's chat,
    /// whose runner is that process's. This process then knows nothing of its turns, and is no door to them.
    /// </summary>
    internal static bool RunsElsewhere(DriverLoop loop, string id) =>
        !loop.Processes.RunsHere(id) && loop.Chat?.Holds(id) != true && loop.Processes.AliveOnThisMachine(id);

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
    /// The level a press asked for (COWORK1a, ONEWAY1l): the <paramref name="word"/> it names, else the person's default, urgent. A level
    /// this build does not know is refused in a sentence, before anything is kept.
    /// </summary>
    internal static MessageLevel LevelOf(string? word)
    {
        if (word is not null && MessageLevels.Read(word) is null)
        {
            throw new DriverException($"`{word}` is no level: when it reads your words is ordinary, urgent or now; nothing was kept.");
        }

        return MessageLevels.Asked(word);
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
    public static IReadOnlyList<ChatUpload> FilesOf(JsonElement payload) => UploadsOf(DriverArguments.Sent(payload, "files"));

    /// <summary>The uploads a <c>files</c> field holds, none where it is no list.</summary>
    public static IReadOnlyList<ChatUpload> UploadsOf(JsonElement? sent)
    {
        if (sent is not { ValueKind: JsonValueKind.Array } files) return [];

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
