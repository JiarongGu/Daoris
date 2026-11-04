using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A session as the rail and its drawer act on it, the page's `bridge/sessions.ts`: a stop, the
/// person's answer to a parked session, and what sessions opened with and said, read off this
/// machine's own record.
/// </summary>
public sealed class SessionsRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;
    private readonly RelatedSessions _related = new(RelatedSessions.GitReads, () => DateTimeOffset.UtcNow);
    private readonly OpenFolder? _openFolder = context.OpenFolder;

    // What a session's own presses change: its record among the sessions, and the driver's state its loop,
    // nudged, reads again; a resolve moves its quest too.
    [DriverRoute("STOP_SESSION", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("STOP_SESSION", Session = "id")]
    [GroundSession("STOP_SESSION", "id")]
    [Says("STOP_SESSION", "id:session")]
    [Publishes("STOP_SESSION", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> StopSessionAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        // `sessions stop` sent over the pipe asks for the verb's own words; the page asks for none and its answer keeps its shape.
        // a stop the standing confirmation admitted says so on each record it ends.
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("stop") { Ids = [id], Standing = PipeDoor.Standing }, cancellationToken).ConfigureAwait(false);

        // False is an answer, not an error: the session already finished, and its record says how.
        // 🔴 Unless the record says it still runs and nothing on this machine runs it — a chat
        // left behind by an application that closed, or a crash. Then the person's stop is how
        // it ends: pressing it used to change nothing and say nothing (2026-09-25).
        // Which of the three it was is part of the answer: the page says it, and an orphan's
        // ending is not the person's — its record says nothing ran it.
        // 🔴 And when another Daoris process on this machine runs it — a terminal's chat — this
        // host can neither stop it nor call it ended: the record still says working.
        // The one implementation the terminal's request is honoured by too.
        var answer = await SessionMoves.StopAsync(_loop.Processes, _loop.Service, id, cancellationToken).ConfigureAwait(false);
        // the process that runs it takes the stop, as its own; none that does not is claimed to have stopped.
        if (answer.Elsewhere
            && await SessionMoves.StopThroughOwnerAsync(
                _loop.Home, id, SessionMoves.OwnerWait, TimeSpan.FromMilliseconds(100), () => DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false))
        {
            answer = answer with { Stopped = true, Elsewhere = false };
        }

        _loop.Nudge();
        return new { answer.Stopped, answer.Orphan, answer.Elsewhere };
    }

    /// <summary>
    /// The person's answer to a parked session: `completed`, `declined` or `stopped`, with what the record should say. It goes
    /// through the driver because the process goes first: a record saying `completed` while this machine holds the process is the
    /// lie the observed lifecycle prevents. Whether the move is legal stays the ledger's; declining needs a reason; a move with no
    /// note still writes one, since the store keeps the previous note and would read beside the parked analysis.
    /// </summary>
    [DriverRoute("RESOLVE_SESSION", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("RESOLVE_SESSION", Session = "id")]
    [GroundSession("RESOLVE_SESSION", "id")]
    [Says("RESOLVE_SESSION", "id:session", "note:words")]
    [Publishes("RESOLVE_SESSION", StateScopes.Sessions | StateScopes.Quests | StateScopes.Driver, Session = "id")]
    private async Task<object?> ResolveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var state = PayloadHelper.GetRequiredValue<string>(request.Payload, "state");
        var note = Optional(request, "note");

        if (state is not ("completed" or "declined" or "stopped"))
        {
            throw Refusals.Because(
                Refusals.SessionMoveNotYours, PersonMoves,
                ("state", state), ("moves", "completed, declined, stopped"));
        }

        if (state == "declined" && note is null)
        {
            throw Refusals.Because(Refusals.SessionDeclineNeedsReason, DeclineNeedsReason);
        }

        var service = _loop.Service ?? throw NotReady();

        // `sessions finish` and `decline` sent over the pipe ask for the verb's own words (`stopped` is the page's alone).
        if (Flag(request, "said") && state != "stopped")
        {
            // a finish the standing confirmation admitted never takes the request folder, and is moved only from awaiting-person.
            return await SaidAsync(
                    new SessionsAsk(state == "completed" ? "finish" : "decline") { Ids = [id], Note = note, Standing = PipeDoor.Standing }, cancellationToken)
                .ConfigureAwait(false);
        }

        // The process first, then the record, with the person's words or the sentence that says they moved it: the one
        // implementation the terminal's request is honoured by too. False from the stop is the common case.
        var message = await SessionMoves.ResolveAsync(
            session => _loop.Processes.Stop(session), service, id, state, note, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();

        return new { Session = id, State = state, Message = message };
    }

    // *Go on in a new session*: words a resume holds while the account their session ran on cools go on now
    // in a new session, handed them, without that conversation. The driver's door judges and keeps the person's choice
    //, the same door `daoris session go-on-new` calls; the loop is nudged so its next look carries
    // them on. What cannot is `sent: false` with its code, for the page to word, and the driver's sentence beside it. The
    // page's press is a later row.
    [DriverRoute("SESSION_GO_ON_NEW", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_GO_ON_NEW", Session = "id")]
    [GroundSession("SESSION_GO_ON_NEW", "id")]
    [Says("SESSION_GO_ON_NEW", "id:session")]
    [Publishes("SESSION_GO_ON_NEW", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SessionGoOnNewAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("go-on-new") { Ids = [id] }, cancellationToken).ConfigureAwait(false);

        var answer = await GoOnNew.AskAsync(
                service, _loop.Home, (adapter, profile) => _loop.Harnesses.CoolingOf(adapter ?? "", profile), id,
                DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        if (answer.Sent) _loop.Nudge();
        return new { answer.Sent, answer.Why, answer.Message };
    }

    // *Ask another agent…*: the person's own ask of a helper for a session,
    // the file `daoris session helper` writes (`HelperFiles.WriteAsk`, asked by the person, so the `helpers` rule's `ask`
    // and `off` do not hold it), judged as that verb judges: a question within the bound, an agent of one word, and a session of
    // this machine's that may ask. Everything else is the driver's, at its next look, which the nudge brings sooner. What cannot is
    // `asked: null` with its code for the page to word, and the verb's sentence beside it; the page never reads a path.
    [DriverRoute("SESSION_HELPER_ASK", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_HELPER_ASK", Session = "id")]
    [GroundSession("SESSION_HELPER_ASK", "id")]
    [Says("SESSION_HELPER_ASK", "id:session", "agent:agent", "question:words")]
    [Publishes("SESSION_HELPER_ASK", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SessionHelperAskAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var question = (Optional(request, "question") ?? "").Trim();
        var agent = Optional(request, "agent")?.Trim();
        var service = _loop.Service ?? throw NotReady();
        // `sessions helper` sent over the pipe asks for the verb's own words, judged by the same code.
        if (Flag(request, "said") && question.Length > 0)
        {
            return await SaidAsync(new SessionsAsk("helper") { Ids = [id], Text = question, Agent = agent }, cancellationToken).ConfigureAwait(false);
        }

        static object Refused(string why, string sentence) => new { Asked = (string?)null, Why = why, Message = $"{sentence} Nothing was asked." };
        if (question.Length == 0) return Refused(HelperAskWhy.Blank, "A helper is asked a question, and none was given.");
        if (question.Length > SessionsHelperCommand.MaxQuestion)
        {
            return Refused(HelperAskWhy.TooLong,
                $"A question is at most {SessionsHelperCommand.MaxQuestion} characters, and this is {question.Length}.");
        }

        if (agent is not null && agent.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return Refused(HelperAskWhy.Agent, $"An agent is named as one word, as it is listed in Settings, and `{agent}` is not.");
        }

        // The verb's sentences, so the person reads the same one from either door; who may ask is the start layer's.
        var asker = await service.RecordAsync(id, cancellationToken).ConfigureAwait(false);
        var refused = asker is null or { Teammate: true }
            ? (HelperAskWhy.NotHeld, $"No session `{id}` of this machine's is held to bring an answer back to.")
            : SessionsHelperCommand.Unfit(id, asker);
        if (refused is var (why, sentence)) return Refused(why, sentence);

        var asked = new HelperFiles(_loop.Home).WriteAsk(id, question, agent, DateTimeOffset.UtcNow);
        _loop.Nudge();
        return new
        {
            Asked = asked,
            Why = (string?)null,
            Message = $"Asked #{asked} for session {id}: the driver starts a helper on another agent at its next look, read-only, and "
                      + "its answer comes back to that session as its next turn, marked as the helper's.",
        };
    }

    // The asks a session made of helpers, oldest first, each as the driver's look left it: its state and code, the
    // helper's session once started and the agent it started on. Read off the home's `helpers` folder, the driver's own files;
    // what the state keeps for the driver alone (the last words, the word it was handed as) is not the page's.
    [DriverRoute("SESSION_HELPERS", PipeClass.Open)]
    private Task<object?> SessionHelpersAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var files = new HelperFiles(_loop.Home);
        var helpers = files.Asks()
            .Where(ask => string.Equals(ask.Asker, id, StringComparison.Ordinal))
            .Select(ask => (Ask: ask, State: files.ReadState(ask.Id)))
            .Select(each => new
            {
                each.Ask.Id,
                each.Ask.Question,
                each.Ask.Agent,
                each.Ask.AskedBy,
                At = Moments.Write(each.Ask.At),
                each.State?.State,
                each.State?.Code,
                each.State?.Helper,
                StartedOn = each.State?.Agent,
                // a cooling ask's account's reset, which its row says.
                Until = each.State?.Until is { } until ? Moments.Write(until) : null,
            })
            .ToList();
        return Task.FromResult<object?>(new { Helpers = helpers });
    }

    // *Ask another agent to work on a piece…*: the person's own ask of
    // a piece for a session, the file `daoris session partner` writes, by the one judge and writer both share
    // (`PartnerPersonAsk`): the piece within its bound, an agent named in one word, the paths inside, a session of this machine's that
    // may lead one, and its tree's commit read as the piece's exact base. Everything else is the driver's, at its next look, which the
    // nudge brings sooner. What cannot is `asked: null` with its code for the page to word, and the verb's sentence beside it.
    [DriverRoute("SESSION_PARTNER_ASK", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_PARTNER_ASK", Session = "id")]
    [GroundSession("SESSION_PARTNER_ASK", "id")]
    [Says("SESSION_PARTNER_ASK", "id:session", "agent:agent", "piece:words")]
    [Publishes("SESSION_PARTNER_ASK", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SessionPartnerAskAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var piece = (Optional(request, "piece") ?? "").Trim();
        var agent = Optional(request, "agent")?.Trim();
        IReadOnlyList<string> paths = request.Payload is { } payload && payload.TryGetProperty("paths", out var named) && named.ValueKind == JsonValueKind.Array
            ? [.. named.EnumerateArray().Where(path => path.ValueKind == JsonValueKind.String).Select(path => path.GetString()!)]
            : [];
        var service = _loop.Service ?? throw NotReady();
        // `sessions partner` sent over the pipe asks for the verb's own words, judged by the same code.
        if (Flag(request, "said"))
        {
            return await SaidAsync(new SessionsAsk("partner") { Ids = [id], Text = piece, Agent = agent, Paths = paths }, cancellationToken).ConfigureAwait(false);
        }

        var asked = await PartnerPersonAsk.AskAsync(
            _loop.Home, service, id, piece, paths, agent, _loop.PieceBases, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (asked.Asked is not null) _loop.Nudge();
        return new { asked.Asked, asked.Why, asked.Message };
    }

    // The pieces a session asked, oldest first, each as the driver's look left
    // it: the piece and its paths, the agent, where it stands and why, its partner's record once started, its frozen commit and ref
    // once imported, and the files Daoris read in it with those beyond its paths. Read off the home's `partners` folder, the driver's
    // own files; the partner's own words stay its answer's, which reaches the session as a word.
    [DriverRoute("SESSION_PARTNERS", PipeClass.Open)]
    private Task<object?> SessionPartnersAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        // A read of files, off the window's thread.
        return Task.Run<object?>(() =>
        {
            var files = new PartnerFiles(_loop.Home);
            var pieces = files.Asks()
                .Where(ask => string.Equals(ask.Asker, id, StringComparison.Ordinal))
                .Select(ask =>
                {
                    var state = files.ReadState(ask.Id);
                    var facts = state?.Commit is { Length: > 0 } ? files.ReadFacts(ask.Id) : null;
                    return new
                    {
                        ask.Id,
                        ask.Piece,
                        ask.Paths,
                        ask.Agent,
                        ask.AskedBy,
                        At = Moments.Write(ask.At),
                        State = state?.State ?? (files.Unreadable(ask.Id, "state") ? PartnerStates.Uncertain : PartnerStates.Asked),
                        state?.Code,
                        state?.End,
                        state?.Partner,
                        StartedOn = state?.Agent,
                        Commit = state?.Commit is { Length: > 0 } commit ? commit : null,
                        Ref = state?.Commit is { Length: > 0 } ? PieceRefs.Of(ask.Id) : null,
                        Files = facts?.Files,
                        Beyond = facts?.Beyond,
                    };
                })
                .ToList();
            return new { Pieces = pieces };
        }, cancellationToken);
    }

    // What of the agents' asks waits on the person's word across this machine's sessions, for
    // *What needs you*: each the driver's look holds `waiting` with no code under the `helpers` rule's `ask`, the one state the yes
    // and the no answer, oldest first, with the session that asked. A read off the home's `helpers` folder: it declares nothing,
    // and sits under the sessions' key on the page, which every tick asks again; the look says a new one in its report.
    [DriverRoute("HELPER_WAITS", PipeClass.Open)]
    private Task<object?> HelperWaitsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var files = new HelperFiles(_loop.Home);
        var waits = files.Asks()
            .Where(ask => ask.AskedBy == HelperAsk.ByAgent && files.ReadState(ask.Id) is { State: HelperStates.Waiting, Code: null })
            .OrderBy(ask => ask.At)
            .Select(ask => new { ask.Id, Session = ask.Asker, ask.Question, ask.Agent, At = Moments.Write(ask.At) })
            .ToList();
        return Task.FromResult<object?>(new { Waits = waits });
    }

    // The person's yes to an agent's ask under the `helpers` rule's `ask`: the `approved`
    // state the driver's look reads (`HelperJudge`), written where the ask waits on that yes: no state yet, or `waiting` with no
    // code. A yes again is the same yes. A person's own ask needs none, one past its start is past it, one waiting on a slot or an
    // agent waits on no yes, and one this session does not hold is gone: each `approved: false` with its code, its state untouched.
    [DriverRoute("SESSION_HELPER_APPROVE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_HELPER_APPROVE", Session = "session")]
    [GroundRead("SESSION_HELPER_APPROVE", nameof(PipeReads.HelperWait))]
    [Says("SESSION_HELPER_APPROVE", "session:session")]
    [Publishes("SESSION_HELPER_APPROVE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private async Task<object?> SessionHelperApproveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var askId = PayloadHelper.GetRequiredValue<string>(request.Payload, "ask");
        // `sessions helper --approve` names only the ask, and asks for the verb's own words.
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("helper-approve") { Ids = [askId] }, cancellationToken).ConfigureAwait(false);

        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        // The judge is the library's, which the terminal's `sessions helper --approve` shares.
        var answer = HelperPersonWord.Approve(new HelperFiles(_loop.Home), session, askId);
        if (answer.Wrote) _loop.Nudge();
        return new { Approved = answer.Stands, answer.Why, answer.Message };
    }

    // The person's no to an agent's ask under the `helpers` rule's `ask`, beside the yes: the ask
    // refused `declined` at once, with the coded note the look writes on the asker's record for every refusal (`helper.refused`), so
    // the look reads it as past judging and its timeline says it as it says the rule's `off`. Written only where the look holds the
    // ask waiting on the person's word, `waiting` with no code: one it has not read yet may be starting under `on`, so it waits on no
    // word yet. A no again is the same no, with no second note. A person's own ask needs none, one with the person's yes waits on the
    // look, one past its start is past it, one waiting on a slot or an agent waits on none, and one this session does not hold is
    // gone: each `declined: false` with its code, its state untouched.
    [DriverRoute("SESSION_HELPER_DECLINE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_HELPER_DECLINE", Session = "session")]
    [GroundRead("SESSION_HELPER_DECLINE", nameof(PipeReads.HelperWait))]
    [Says("SESSION_HELPER_DECLINE", "session:session")]
    [Publishes("SESSION_HELPER_DECLINE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private async Task<object?> SessionHelperDeclineAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var askId = PayloadHelper.GetRequiredValue<string>(request.Payload, "ask");
        // `sessions helper --decline` names only the ask, and asks for the verb's own words.
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("helper-decline") { Ids = [askId] }, cancellationToken).ConfigureAwait(false);

        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        // The judge is the library's, which the terminal's `sessions helper --decline` shares.
        var answer = HelperPersonWord.Decline(new HelperFiles(_loop.Home), _loop.Events, _loop.Output, session, askId);
        return new { Declined = answer.Stands, answer.Why, answer.Message };
    }

    // A session's agent's ask to hand it over, as the driver's look left it, for the session's page: its
    // note (the agent's claim), when, its state, code and the agent it goes to; null where its agent asked none. A read off the home's
    // sessions folder, the driver's own files: it declares nothing, and the look says a change in its report.
    [DriverRoute("SESSION_HAND_OVER_ASK", PipeClass.Open)]
    private Task<object?> SessionHandOverAskAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var files = new HandOverAskFiles(_loop.Home);
        var ask = files.Ask(id);
        var state = ask is null ? null : files.ReadState(id) is { } kept && kept.Id == ask.Id ? kept : null;
        return Task.FromResult<object?>(new
        {
            Ask = ask is null ? null : new { ask.Id, ask.Note, At = Moments.Write(ask.At), state?.State, state?.Code, state?.Agent },
        });
    }

    // A parked session's question to the person, as its agent asked it through `question_ask`
    // and the driver kept it for the park its last turn ended in: its words, the answers it offers with what it does on each, the one
    // it recommends with why and by when, and its mark that the question is the owner's alone, each the
    // agent's own; beside them the supervisor's defer on it (`left`: its bound and sourced reasons, SUPERVISOR1b), Daoris's record of
    // the holder's act; null where none is kept, and for a record that is not
    // parked (Fable's read, Q7), whatever a file says. A read off the home's sessions folder, the files leaving the window's thread
    // first: it declares nothing, and the page asks it only of a park of this machine's.
    [DriverRoute("SESSION_QUESTION", PipeClass.Open)]
    private async Task<object?> SessionQuestionAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var record = _loop.Service is { } service ? await service.RecordAsync(id, cancellationToken).ConfigureAwait(false) : null;
        if (record is not { Parked: true }) return new { Question = (object?)null };
        return await Task.Run<object?>(() =>
        {
            var asked = new QuestionFiles(_loop.Home).Read(id);
            // the supervisor's defer on this question, which the card says above its answers; never its holder.
            var left = asked is null
                ? null
                : _loop.Supervisor.History(id).LastOrDefault(each => each.State == SupervisorState.Left && each.Question.Id == asked.Id);
            return new
            {
                Question = asked is null ? null : new
                {
                    asked.Id,
                    Question = asked.Question,
                    Choices = asked.Choices.Select(choice => new { choice.Label, choice.Does }).ToArray(),
                    asked.Recommend,
                    asked.Why,
                    asked.By,
                    asked.Owner,
                    Left = left is null ? null : new
                    {
                        left.Bound,
                        Reasons = left.Reasons.Select(reason => new { reason.Says, reason.Source }).ToList(),
                    },
                    At = Moments.Write(asked.At),
                },
            };
        }, cancellationToken);
    }

    // The person's yes to their session's agent's ask to hand it over under the `handOver` rule's `ask`:
    // the judge the terminal's `sessions hand-over <id> --approve` shares (`HandOverAskWord`). A conversation this shell's chat runner
    // holds is handed over at once, through the runner's own hand-over (taken where it is not talking, else at its turn's end), the
    // state written `taken` first so no look keeps a second box; anything else is `approved` with the agent the look named, for the
    // look, nudged, to take as the rule's `on` would. `ask` names the one the page showed, else the session's own. A word that waits on
    // nothing is `approved: false` with its code, its state untouched.
    [DriverRoute("SESSION_HAND_OVER_APPROVE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_HAND_OVER_APPROVE", Session = "session")]
    [GroundRead("SESSION_HAND_OVER_APPROVE", nameof(PipeReads.HandOverAsk))]
    [Says("SESSION_HAND_OVER_APPROVE", "session:session")]
    [Publishes("SESSION_HAND_OVER_APPROVE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private async Task<object?> SessionHandOverApproveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        // `sessions hand-over <id> --approve` asks for the verb's own words.
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("hand-over-approve") { Ids = [session] }, cancellationToken).ConfigureAwait(false);

        var files = new HandOverAskFiles(_loop.Home);
        var askId = Optional(request, "ask") ?? files.Ask(session)?.Id ?? "";
        var record = _loop.Chat is not null && _loop.Service is { } service
            ? await service.RecordAsync(session, cancellationToken).ConfigureAwait(false)
            : null;
        var taking = record is { Kind: "chat" } && files.ReadState(session) is { Id: var named, Agent: { Length: > 0 } } && named == askId;
        var answer = HandOverAskWord.Approve(files, session, askId, taking);
        if (taking && answer.Wrote && answer.Agent is { } agent)
        {
            var handed = await _loop.Chat!.HandOverAsync(
                    session, agent, null, HandOverWhy.Agent, HandOverBy.Agent, DriverConfig.Load(_loop.ConfigPath),
                    onEnded: (ended, state) => DriverLoop.Ended(_context.Events, ended, state), cancellationToken)
                .ConfigureAwait(false);
            if (!handed.Kept && handed.SessionId is null)
            {
                // their yes stood, and the take could not go: not taken, by the take's own reason, never refused.
                var why = HandOverAskWhy.Of(handed.Why);
                files.WriteState(session, new HandOverAskState(askId, HandOverAskStates.Refused, why, null, DateTimeOffset.UtcNow));
                HandOverAskNotes.Say(_loop.Events, _loop.Output, session, HandOverAskNotes.NotTaken(agent, why));
                _loop.Nudge();
                return new { Approved = false, Why = handed.Why ?? HandOverAskWhy.Cannot, handed.Message, Agent = agent };
            }
        }

        if (answer.Wrote) _loop.Nudge();
        return new { Approved = answer.Stands, answer.Why, answer.Message, answer.Agent };
    }

    // The person's no to their session's agent's ask to hand it over, beside the yes: refused `declined` at
    // once, with the coded note the look writes for every refusal (`handover.refused`), where the look holds it waiting on their word.
    // One not read yet, one with their yes, one past it and one gone are `declined: false` with its code, its state untouched.
    [DriverRoute("SESSION_HAND_OVER_DECLINE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_HAND_OVER_DECLINE", Session = "session")]
    [GroundRead("SESSION_HAND_OVER_DECLINE", nameof(PipeReads.HandOverAsk))]
    [Says("SESSION_HAND_OVER_DECLINE", "session:session")]
    [Publishes("SESSION_HAND_OVER_DECLINE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private async Task<object?> SessionHandOverDeclineAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        // `sessions hand-over <id> --decline` asks for the verb's own words.
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("hand-over-decline") { Ids = [session] }, cancellationToken).ConfigureAwait(false);

        var files = new HandOverAskFiles(_loop.Home);
        var askId = Optional(request, "ask") ?? files.Ask(session)?.Id ?? "";
        var answer = HandOverAskWord.Decline(files, _loop.Events, _loop.Output, session, askId);
        return new { Declined = answer.Stands, answer.Why, answer.Message };
    }

    // The person's *Leave it running* on a quiet session's row: the watcher stands down
    // for it until it is heard again, its episode ends `left` with its coded note, and the row goes. `{ left, why, message }`; a session
    // that brings no row is `left: false`, `why: not-quiet`. A move of the watcher's memory alone, which wakes the attention slice.
    [DriverRoute("SESSION_QUIET_LEAVE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_QUIET_LEAVE", Session = "id")]
    [GroundSession("SESSION_QUIET_LEAVE", "id")]
    [Says("SESSION_QUIET_LEAVE", "id:session")]
    [Publishes("SESSION_QUIET_LEAVE", StateScopes.Sessions, Session = "id")]
    private Task<object?> SessionQuietLeaveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var left = SessionWatch.Of(_loop.Events).Leave(id);
        return Task.FromResult<object?>(left
            ? new { Left = true, Why = (string?)null, Message = "Daoris looks in again once it is heard from." }
            : new { Left = false, Why = (string?)QuietNot, Message = $"{id} is not quiet now: nothing was changed." });
    }

    // The person's *End and carry on* on a quiet quest session's row: their
    // stop released in the same press, *Stop* and *Try again* at once. The release is written first, then the run ends as the person's
    // stop ends one, concluding `stopped` with `ended.quiet-pressed` and no strike; the look its ending wakes carries the line on in a new
    // part handed the brief, and the person's words that waited for it go to that part, never to a resume of the run that hung.
    // `{ stopped, why, message }`: refused `not-quiet` where it brings no quiet row (a busy one is offered no end), `not-quest` for an
    // intake, `gone` where its run ended first, each with nothing changed.
    [DriverRoute("SESSION_QUIET_CARRY_ON", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_QUIET_CARRY_ON", Session = "id")]
    [GroundSession("SESSION_QUIET_CARRY_ON", "id")]
    [Says("SESSION_QUIET_CARRY_ON", "id:session")]
    [Publishes("SESSION_QUIET_CARRY_ON", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private Task<object?> SessionQuietCarryOnAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var watch = SessionWatch.Of(_loop.Events);
        object Refused(string why, string message) => new { Stopped = false, Why = (string?)why, Message = message };
        if (watch.RowOf(id) is not { Busy: false } row) return Task.FromResult<object?>(Refused(QuietNot, $"{id} is not quiet now, or something it runs keeps the machine busy: nothing was changed."));
        if (row is not { Kind: SessionWatch.Quest, Quest: { } quest }) return Task.FromResult<object?>(Refused(QuietNotQuest, $"{id} is an intake: it is stopped, never carried on."));

        StandingChoices.Write(_loop.ConfigPath, config => config.WithReleased(quest, id));
        if (!_loop.Processes.Stop(id, note: Observation.QuietPressed(row.Minutes)))
        {
            return Task.FromResult<object?>(Refused(QuietGone, $"{id}'s run had already ended; its quest is released for the next look."));
        }

        watch.Pressed(id);
        _loop.Nudge();
        return Task.FromResult<object?>(new { Stopped = true, Why = (string?)null, Message = "Its run ends, and the work goes on in the same tree at the next look." });
    }

    /// <summary>A quiet row's press refused: the session brings no quiet row, is an intake, or ended first.</summary>
    internal const string QuietNot = "not-quiet", QuietNotQuest = "not-quest", QuietGone = "gone";

    // What this machine's driven sessions hold for the person under their repository's asking permission prompts (AUTONOMY1b3a, D184
    // point 3), oldest first: each request with its session, its call as the agent named it, and what the person may choose (never a
    // standing allow), and when its bound releases it (`until`, PERMUNTIL1: the page's one source for the bound). A read off the
    // driver's memory (`HeldPermissions`), which a restart empties with the agents that asked; the attention slice's own projection,
    // so the pushed rows and the route say each hold alike.
    [DriverRoute("PERMISSION_WAITS", PipeClass.Open)]
    private Task<object?> PermissionWaitsAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new { Waits = AttentionStateSlice.PermissionWaits(HeldPermissions.Of(_loop.Events)) });

    // The person's answer to a request a session holds for them: an option the agent offered, by its id, or none to
    // refuse it, which takes the offered one-time rejection, else none. The allow is the person's press for this call alone, and
    // Daoris never answers one by itself; a standing allow is a rule's to give. What cannot is `answered: false` with
    // its code (`unheld`, `unoffered`, `standing`) and the registry's sentence, the hold left as it was. Its session hears the answer
    // at once, off its own reader, and the hold's end is told through the registry's `Changed`; the route publishes nothing.
    [DriverRoute("SESSION_PERMISSION_ANSWER", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_PERMISSION_ANSWER", Session = "session")]
    [GroundRead("SESSION_PERMISSION_ANSWER", nameof(PipeReads.PermissionWait))]
    [Says("SESSION_PERMISSION_ANSWER", "session:session")]
    private Task<object?> SessionPermissionAnswerAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        var held = PayloadHelper.GetRequiredValue<string>(request.Payload, "request");
        var answer = HeldPermissions.Of(_loop.Events).Answer(session, held, Optional(request, "option"));
        return Task.FromResult<object?>(new { answer.Answered, answer.Why, answer.Message });
    }

    // What the answers-alone bound holds for the person across this machine's sessions (COWORK1e, D188 point 4; the message-urgency
    // design §8.3), oldest first: each session whose answers would take its line on a third time in a row with nobody between, the agent
    // that answered, the answers' ids and since when. A read off the home's marks (`GoOnMarks`), which the look and the conversations
    // write; it declares nothing, and sits under the sessions' key on the page, which every tick asks again.
    [DriverRoute("ANSWER_WAITS", PipeClass.Open)]
    private Task<object?> AnswerWaitsAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new { Waits = AttentionStateSlice.AnswerWaits(_loop.Home) });

    // The person's answer to a third answer alone held for them: `goOn` true is *Go on with this answer*, their press
    // kept as their word and the mark taken away, so the driver's next look, nudged, or the conversation's own go-on takes the answer on;
    // false is *Not now*, the answer kept for them, going on only beside a word of theirs. Where nothing holds an answer of the session
    // for them, `answered: false` with its code (`unheld`), and nothing is written.
    [DriverRoute("SESSION_ANSWER_ALONE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_ANSWER_ALONE", Session = "session")]
    [GroundRead("SESSION_ANSWER_ALONE", nameof(PipeReads.AnswerAloneWait))]
    [Says("SESSION_ANSWER_ALONE", "session:session")]
    [Publishes("SESSION_ANSWER_ALONE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private Task<object?> SessionAnswerAloneAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        var goOn = PayloadHelper.GetRequiredValue<bool>(request.Payload, "goOn");
        var marks = new GoOnMarks(_loop.Home);
        var answer = goOn
            ? AnswersAloneWord.GoOn(marks, _loop.Events, session)
            : AnswersAloneWord.NotNow(marks, _loop.Events, session, DateTimeOffset.UtcNow);
        if (answer.Stands && goOn)
        {
            // A conversation this shell holds says it, or goes on with it where it ended; a driven record goes on at the look.
            _loop.Chat?.AnswerGoesOn(session);
            _loop.Nudge();
        }

        return Task.FromResult<object?>(new { Answered = answer.Stands, answer.Why, answer.Message });
    }

    // The supervisor's decisions on a session: those live in this shell's
    // memory as they stand, and every other read from the session's files as history (`earlier`), granting nothing. A read: `{ decisions }`,
    // oldest first, each its question's snapshot, choice, reasons, holder, state, when it goes on and when it last moved.
    [DriverRoute("SESSION_SUPERVISOR", PipeClass.Open)]
    private Task<object?> SessionSupervisorAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return Task.FromResult<object?>(new { Decisions = _loop.Supervisor.History(id).Select(SupervisorDecisionRow).ToList() });
    }

    /// <summary>One decision as the page reads it: never its receipt, its ticket nor its word's text.</summary>
    internal static object SupervisorDecisionRow(SupervisorDecisionView view) => new
    {
        view.Id,
        Question = new { view.Question.Id, Text = view.Question.Question, Choices = view.Question.Choices.Select(choice => choice.Label).ToList() },
        view.Choice,
        view.Bound,
        Reasons = view.Reasons.Select(reason => new { reason.Says, reason.Source }).ToList(),
        Holder = new { view.Holder.Kind, view.Holder.Parent },
        State = SupervisorStates.Spell(view.State),
        view.Why,
        KeptAt = Moments.Write(view.KeptAt),
        GoesOnAt = Moments.Write(view.GoesOnAt),
        StateAt = Moments.Write(view.StateAt),
        view.Earlier,
    };

    // The owner's *Overrule*: `{ id, decision }`, a press only the person may make,
    // taken under the session's decision lock. While held the decision is overruled with nothing to take back; while released its word
    // is taken back through the take-back door first. From admitted on nothing changes, and the answer says the state. `{ done, state }`,
    // `done` false with `state` null where no decision of the session is live by that id.
    [DriverRoute("SUPERVISOR_OVERRULE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SUPERVISOR_OVERRULE", Session = "id")]
    [GroundRead("SUPERVISOR_OVERRULE", nameof(PipeReads.SupervisorDecision))]
    [Says("SUPERVISOR_OVERRULE", "id:session")]
    [Publishes("SUPERVISOR_OVERRULE", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SupervisorOverruleAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var decision = PayloadHelper.GetRequiredValue<string>(request.Payload, "decision");
        var service = _loop.Service ?? throw NotReady();
        var state = await _loop.Supervisor.OverruleAsync(
            id, decision,
            async (session, word, token) => (await service.TakeBackAsync(session, word, ct: token).ConfigureAwait(false)).Done,
            DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (state == SupervisorState.Overruled) _loop.Nudge();
        return new { Done = state == SupervisorState.Overruled, State = state is { } moved ? SupervisorStates.Spell(moved) : null };
    }

    // The supervisor's answer: `{ id, question, generation, proof, choice, reasons }`, a role's act, `Role` in
    // the pipe's census. Refused on every door but a terminal's the judge's role method covered (`PipeDoor.Role`, never the presses'
    // standing flag); kept as a held decision on the park, its repository the appointment's. `{ kept, decision?, goesOnAt?, why? }`,
    // refused as data and keeping nothing.
    [DriverRoute("SUPERVISOR_ANSWER", PipeClass.Role, Drain = PipeDrain.Passes)]
    [Publishes("SUPERVISOR_ANSWER", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SupervisorAnswerAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await SupervisorDoor.AnswerAsync(_loop, request.Payload, cancellationToken).ConfigureAwait(false);

    // The supervisor's leaving a question to the owner: `{ id, question, generation, proof, bound, reasons }`,
    // the role's second act, `Role` in the pipe's census and judged as the answer is. Kept as a decision of state `left`, final at once:
    // nothing reaches the session, and the owner's card says *left to you* with the bound and the reasons. `{ kept, decision?, why? }`.
    [DriverRoute("SUPERVISOR_DEFER", PipeClass.Role, Drain = PipeDrain.Passes)]
    [Publishes("SUPERVISOR_DEFER", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SupervisorDeferAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await SupervisorDoor.DeferAsync(_loop, request.Payload, cancellationToken).ConfigureAwait(false);

    // A role's act: `{ role, act, generation, form, proof }`, `Role` in the pipe's census, judged by the
    // judge's role method as SUPERVISOR_ANSWER is, then admitted once with an id Daoris draws. Its handler checks the act once more
    // under the judge's lock (§3.4 step 8: still admitted, its holder's hash present, its appointment live) and runs its role's table's
    // handler; the steward's table is empty in this build, so every act is refused `not-role` at the judge. `{ done, why?, answer?,
    // roles, brief }`, refused as data.
    [DriverRoute("ROLE_ACT", PipeClass.Role, Drain = PipeDrain.Waits)]
    [Publishes("ROLE_ACT", StateScopes.Sessions | StateScopes.Driver)]
    private async Task<object?> RoleActAsync(IpcRequest request, CancellationToken cancellationToken) =>
        await SupervisorDoor.ActAsync(_loop.Pipe, request.Payload, cancellationToken, _context.OwnerScopesOf(), _context.StewardWorldOf()).ConfigureAwait(false);

    // What waits for the supervisor's holder: the parked sessions here with a recorded question and no
    // decision on it, in a repository a live appointment names, each with its generation. Told by event: it reads the
    // service and the question files, so an ask is answered from what is held and the verb asks until an answer computed after
    // its own first ask (`at`) comes. A read naming no key:
    // `{ waiting: [{ session, repository, question, asked, generation }], at }`.
    [DriverRoute("SUPERVISOR_WAITING", PipeClass.Open)]
    [ByEvent("SUPERVISOR_WAITING", Untold = "the question files, and the verb waits for an answer made after its own ask")]
    private async Task<object?> SupervisorWaitingAsync(IpcRequest request, CancellationToken cancellationToken) =>
        new { Waiting = await SupervisorDoor.WaitingAsync(_loop, cancellationToken).ConfigureAwait(false), At = Moments.Write(DateTimeOffset.UtcNow) };

    // The steward's acts this run, newest first, and its live hold's generation, what its holder's verb
    // reads before it proves an act: from the judge's memory alone, so it answers at once. A read naming no key:
    // `{ generation, acts: [{ id, act, repository, quest, session, at, standing, waiting }] }`, the generation null where none is live.
    [DriverRoute("STEWARD_ACTS", PipeClass.Open)]
    private object? StewardActs(IpcRequest request) => _loop.Pipe?.StandingConfirmation is { } judge
        ? new { Generation = judge.LiveGeneration(AppointedRoles.Steward), Acts = judge.StewardActsNow() }
        : new { Generation = (string?)null, Acts = (IReadOnlyList<StewardActNow>)[] };

    // A request at the steward's desk: `{ act, target, why }`, `Open` in the pipe's census, any caller's, kept
    // in the judge's memory and counted against the caller's agent job or nearest long-lived ancestor (ten open each, a hundred in all).
    // It grants nothing, admits no act and reaches no act's handler: the holder reads it as claims and answers it by an act, judged as
    // ROLE_ACT judges every act, or declines it. `{ filed, request?, why? }`, refused as data (`not-staffed` while no holder holds the
    // steward, `form`, `not-role`, `desk-full`).
    [DriverRoute("STEWARD_REQUEST", PipeClass.Open)]
    [Publishes("STEWARD_REQUEST", StateScopes.Driver)]
    private object? StewardRequest(IpcRequest request)
    {
        var filed = _loop.Pipe?.StandingConfirmation is { } judge
            ? judge.FileRequest(request.Payload, PipeDoor.Client)
            : new DeskFiled(false, null, DeskWhy.NotStaffed);
        return new { filed.Filed, Request = filed.Id, filed.Why };
    }

    // The steward's desk, what its holder's `steward desk` prints, fenced, as the callers' claims: from the
    // judge's memory alone, newest first, never a caller's key. A read: `{ requests: [{ id, act, target, why, filed, state, closed,
    // answeredBy, reason }] }`, none where no pipe is served.
    [DriverRoute("STEWARD_DESK", PipeClass.Open)]
    private object? StewardDeskRead(IpcRequest request) => new
    {
        Requests = _loop.Pipe?.StandingConfirmation is { } judge ? judge.Desk.Now().Select(DeskRow).ToList() : new List<object>(),
    };

    /// <summary>One desk request as the verb and the page read it: its moments written as every route writes them.</summary>
    internal static object DeskRow(DeskRequestNow each) => new
    {
        each.Id, each.Act, each.Target, each.Why, Filed = Moments.Write(each.Filed), each.State, Closed = Moments.Write(each.Closed),
        each.AnsweredBy, each.Reason,
    };

    private const string PersonMoves =
        "A person may finish, decline or stop a parked session — not move it anywhere else. "
        + "Answering it so it carries on is a message, not a move.";

    private const string DeclineNeedsReason =
        "Declining needs a reason: it is the part whoever reads this record can act on.";

    // What a person first said in each of these sessions: a conversation's identity, from
    // this machine's own record — never the session record, which travels. Told by event: each record's
    // read held the window up to 7.5 s, so an ask is answered from what is held and this runs on a task of its own.
    [DriverRoute("SESSION_OPENINGS", PipeClass.Open)]
    [ByEvent("SESSION_OPENINGS", Untold = "a session's first words, which SESSION_EVENTS tells and no scope or slice does")]
    private async Task<object?> SessionOpeningsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        if (request.Payload is { } payload && payload.TryGetProperty("ids", out var named)
            && named.ValueKind == JsonValueKind.Array)
        {
            ids.AddRange(named.EnumerateArray()
                .Where(id => id.ValueKind == JsonValueKind.String)
                .Select(id => id.GetString()!));
        }

        await Task.CompletedTask;
        return new { Openings = _loop.Events.Openings(ids) };
    }

    /// <summary>
    /// The sessions beside one: every other session of its repository with a tree here,
    /// chats included, with the declared and the changed paths that meet, or <c>not-read</c> where git cannot say. Read-only;
    /// git's reads make it, and a tree's read is kept only for an ended clean one, so it is read again at each ask.
    /// </summary>
    [DriverRoute("SESSION_RELATED", PipeClass.Open)]
    [ByEvent("SESSION_RELATED", Untold = "the sessions' working trees, which git reads at each ask (an uncommitted edit counts)")]
    private async Task<object?> SessionRelatedAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        var service = _loop.Service ?? throw NotReady();
        if (await service.RelatedCandidatesAsync(session, cancellationToken) is not var (mine, others)) return new { Session = session, Related = Array.Empty<object>() };
        var look = await _related.AskAsync(mine, others, cancellationToken);
        return new { Session = session, Related = look.Related, Own = look.Own is { } own ? new { Read = "not-read", own.ReadAt } : null };
    }

    /// <summary>
    /// What each of these sessions spent: the *Parts* strip's cost on each part's own
    /// row, read from this machine's own record as the openings are, so a moved session reads as one. Each id that names a record
    /// answers its turns, those whose door said what they consumed, and the four counts summed, a count none said null; an id
    /// with no turn is left out.
    /// </summary>
    [DriverRoute("SESSION_SPENT", PipeClass.Open)]
    private object? SpentOf(IpcRequest request)
    {
        var spent = new Dictionary<string, SessionSpent>(StringComparer.Ordinal);
        foreach (var id in Ids(request) ?? [])
        {
            if (_loop.Events.Spent(id) is { Turns: > 0 } read) spent[id] = read;
        }

        return new { Spent = spent };
    }

    /// <summary>
    /// The record of the finished tree a session ran in, for its page once its tree is let go: what the service kept of its
    /// landing, tip, recovery ref and commits, passed through as it answers. Null where none is kept.
    /// </summary>
    [DriverRoute("SESSION_RECORD", PipeClass.Open)]
    [ByEvent("SESSION_RECORD", Untold = "the service's record of the finished tree, kept when the driver posts it")]
    private async Task<object?> SessionRecordAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();
        var json = await service.SessionRecordAsync(id, cancellationToken).ConfigureAwait(false);
        return new { Record = json is null ? (JsonElement?)null : JsonDocument.Parse(json).RootElement.Clone() };
    }

    /// <summary>
    /// A session's memory rows for its page, passed through as the service answers
    /// them: its writes, ends and proposals with their states. None from a host with no door.
    /// </summary>
    [DriverRoute("SESSION_MEMORY", PipeClass.Open)]
    [ByEvent("SESSION_MEMORY", Untold = "the service's memory rows, which its applier and a quest's close move")]
    private async Task<object?> SessionMemoryAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        var service = _loop.Service ?? throw NotReady();
        var json = await service.SessionMemoryAsync(session, cancellationToken).ConfigureAwait(false);
        return json is null ? new { Ops = Array.Empty<object>() } : JsonDocument.Parse(json).RootElement.Clone();
    }

    // the person's decision on a session's memory, an awaiting end approved or declined by its row
    // (`op`, `approve`), or the ended session's staged changes kept or dropped (`keep`). The service judges; a refusal is answered as data,
    // `refused` with its words.
    [DriverRoute("SESSION_MEMORY_DECIDE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_MEMORY_DECIDE", Session = "session")]
    [GroundSession("SESSION_MEMORY_DECIDE", "session")]
    [Says("SESSION_MEMORY_DECIDE", "session:session")]
    [Publishes("SESSION_MEMORY_DECIDE", StateScopes.Sessions, Session = "session")]
    private async Task<object?> SessionMemoryDecideAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        var op = Optional(request, "op");
        var yes = op is { Length: > 0 } ? Flag(request, "approve") : Flag(request, "keep");
        var service = _loop.Service ?? throw NotReady();
        var (decided, message) = await service.DecideMemoryAsync(session, op, yes, cancellationToken).ConfigureAwait(false);
        return new { Decided = decided, Why = decided ? null : "refused", Message = message };
    }

    /// <summary>
    /// Where each of these sessions' work is now, for the rail in one ask: whether the tree it opened is still
    /// here, and its landing, standing or a trace, so a row says where the work landed rather than naming a tree a
    /// landing tidied away.
    /// </summary>
    /// <remarks>
    /// <para><b>From this machine's own files, and cheap</b>: the landing record and whether a folder is there. No service
    /// is asked and no git is run, so it answers on a cold start, and a landing's branch is read as the record holds it:
    /// the review is where git is asked where that branch stands.</para>
    ///
    /// <para><b>Only this home's trees are looked at.</b> The page sends the tree each record names; one outside the trees
    /// this home opened is a session in a repository's own checkout, which no landing tidies, and nothing is said of
    /// it. Nothing machine-local comes back.</para>
    ///
    /// <para><b>Told by event</b>: the landings' record parsed again per session and a folder listed per
    /// row held the window's thread up to 65.9 s, so an ask is answered from what is held and this runs on a task of its own; the
    /// record is read once per ask.</para>
    /// </remarks>
    [DriverRoute("SESSION_WHERE", PipeClass.Open)]
    [ByEvent("SESSION_WHERE", Reads = StateScopes.Sessions | StateScopes.Landing | StateScopes.Sweep, Slices = new[] { SweepStateSlice.Name })]
    private async Task<object?> SessionWhereAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var trees = new SessionTrees(_loop.Home);
        var landings = trees.Recorded.Entries();
        var rows = new List<object>();
        if (request.Payload is { } payload && payload.TryGetProperty("sessions", out var sessions)
            && sessions.ValueKind == JsonValueKind.Array)
        {
            foreach (var session in sessions.EnumerateArray())
            {
                if (session.ValueKind != JsonValueKind.Object
                    || !session.TryGetProperty("id", out var named) || named.ValueKind != JsonValueKind.String
                    || !session.TryGetProperty("tree", out var at) || at.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var id = named.GetString()!;
                var tree = at.GetString()!;
                if (!HeldTree(trees, tree)) continue;

                cancellationToken.ThrowIfCancellationRequested();
                // As LandedBranches.Landing reads it: the newest entry naming the session.
                var landing = landings.LastOrDefault(entry => entry.Names(id));
                rows.Add(new
                {
                    Session = id,
                    TreeGone = TreeFolders.TreeGone(tree),
                    Landed = landing is null
                        ? null
                        : new
                        {
                            landing.Repository,
                            landing.Branch,
                            State = landing.GoneAt is null ? LandedState.Standing : LandedState.Gone,
                        },
                });
            }
        }

        await Task.CompletedTask;
        return new { Sessions = rows };
    }

    /// <summary>Whether a path names a tree this home opened; a path no folder could have is none.</summary>
    private static bool HeldTree(SessionTrees trees, string path)
    {
        try
        {
            return path.Length > 0 && trees.Holds(path);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Where each session is listed by state: its group, its row's word and its second line, read by the driver library's one
    /// reader, which the terminal's <c>sessions</c> prints too. No git at a window's tick: a tree is read as the machine's facts
    /// judged it, so more windows never mean more git. Nothing machine-local comes back but a running call's title, the agent's
    /// own words, transcript-class and answered over this bridge alone.
    /// </summary>
    [DriverRoute("SESSION_GROUPS", PipeClass.Open)]
    private async Task<object?> SessionGroupsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var ids = Ids(request);
        var (look, grouped) = await GroupsAsync(ids, cancellationToken).ConfigureAwait(false);
        // the entries beside the rows, which the page reads until SESSONE1c draws entries.
        var sessions = grouped.Select(Grouped).ToArray();
        var entries = SessionGroups.ReadEntries(look, ids).Select(Entry).ToArray();
        if (!Flag(request, "said")) return new { Sessions = sessions, Entries = entries };

        // `daoris session list` asks for its own lines from this look (the group, the repository and `--json` it was given),
        // which the alone path prints from a look of its own through the same function (§7.3); the page asks for none.
        var ask = new SessionsAsk("list") { Group = Optional(request, "group"), Repository = Optional(request, "repository"), Json = Flag(request, "json") };
        return new
        {
            Sessions = sessions, Entries = entries,
            Said = new TreesSaid(SessionsCommand.ListLines(look, ask, _loop.Home, DateTimeOffset.UtcNow), [], 0).ToWire(),
        };
    }

    /// <summary>
    /// One entry of <c>SESSION_GROUPS</c>, field for field what <c>daoris session list --json</c> prints in <c>entries</c>
    ///: its key and the key's halves, the group its leading part put it in, and its parts, oldest first, each with
    /// how it began. Ids, words and counts only.
    /// </summary>
    public static object Entry(SessionEntry entry) => new
    {
        Entry = entry.Key,
        entry.Origin,
        entry.Repository,
        entry.Group,
        entry.Leading,
        entry.Head,
        entry.Opens,
        Parts = entry.Parts.Select(part => new { part.Session, part.Quest, part.Shown, part.Group, part.Began, part.After, part.AskedBy, part.Head }).ToArray(),
    };

    /// <summary>
    /// Archive these sessions on this machine, or bring them back: the marks as they now stand.
    /// </summary>
    /// <remarks>
    /// <para><b>Each is judged by where the reader places it now</b>: a live one, one waiting on you and one with work to
    /// review are refused, so archive never hides what needs the person, and an id no record has is refused too. Asked of
    /// one session, a refusal is the answer, in the catalogue's words; asked of several, as <i>Archive what ended</i>'s
    /// second press asks, what may go is archived and the rest are kept, each with its code, since a list is a fact about
    /// a moment.</para>
    ///
    /// <para><b>Unarchive needs no service</b>: it takes marks away, and one that was not archived is said, never refused
    ///.</para>
    /// </remarks>
    [DriverRoute("SESSION_ARCHIVE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_ARCHIVE", Sessions = "ids")]
    [GroundSession("SESSION_ARCHIVE", "ids", Many = true)]
    [Says("SESSION_ARCHIVE", "ids:sessions")]
    [Publishes("SESSION_ARCHIVE", StateScopes.Sessions)]
    private async Task<object?> SessionArchiveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var archive = PayloadHelper.GetRequiredValue<bool>(request.Payload, "archived");
        var ids = Ids(request) ?? [];
        var marks = new SessionArchive(_loop.Home);
        // `sessions archive` and `unarchive` sent over the pipe ask for their words (`said: true`), the alone path's own, and a
        // terminal's lines; the page asks for none, and its answers keep their shape.
        var said = Flag(request, "said");

        if (!archive)
        {
            var back = marks.Unarchive(ids);
            var marked = back.Marks.Select(Mark).ToArray();
            return said
                ? new { Archived = marked, back.NotArchived, Said = SessionArchive.UnarchiveSaid(ids, back.NotArchived).ToWire() }
                : new { Archived = marked, back.NotArchived };
        }

        var (look, grouped) = await GroupsAsync(ids, cancellationToken, walked: true).ConfigureAwait(false);
        var answer = marks.Archive(ids, grouped, [.. look.Records.Select(record => record.Id)], DateTimeOffset.UtcNow);
        var kept = answer.Outcomes.Where(outcome => outcome.Verdict != ArchiveVerdict.Archived).ToList();
        // counted in the machine log by the door it came in by: the screen's, or the terminal's over the pipe.
        SessionArchive.Said(_loop.Log, answer.Outcomes.Count - kept.Count, PipeDoor.Now == PipeDoor.Terminal ? PluginEvents.Terminal : PluginEvents.Screen);
        var archived = answer.Marks.Select(Mark).ToArray();
        var keptRows = kept.Select(outcome => new { outcome.Session, Code = CodeOf(outcome.Verdict), outcome.Group }).ToArray();
        // A terminal is told one kept in its own words and exit, as it says it alone, never thrown.
        if (said) return new { Archived = archived, Kept = keptRows, Said = SessionArchive.ArchiveSaid(answer.Outcomes).ToWire() };
        if (ids.Count == 1 && kept.Count == 1) throw Kept(kept[0]);

        return new { Archived = archived, Kept = keptRows };
    }

    /// <summary>One look at the sessions, gathered by the driver library's one gatherer, which the terminal's <c>sessions</c> reads too.</summary>
    /// <param name="walked">
    /// Whether the asked sessions' trees are walked with git now, as an archive's judgement walks them: a press is judged as the trees
    /// stand when it is pressed, never as the facts last judged them, so archive never hides work a tree gained since. The list's
    /// trees are the facts'.
    /// </param>
    private async Task<(SessionLook Look, IReadOnlyList<SessionGrouping> Grouped)> GroupsAsync(
        IReadOnlyCollection<string>? only, CancellationToken cancellationToken, bool walked = false)
    {
        var service = _loop.Service ?? throw NotReady();
        // the one look, in the groups slice's builder (PUSH1c1: no tree judged here unless walked; the waits, the calls and
        // the kept look are laid there), so the route and the pushed slice cannot drift.
        var look = await GroupsStateSlice.LookAsync(_loop, service, cancellationToken, only, walked).ConfigureAwait(false);

        return (look, SessionGroups.Read(look, only));
    }

    /// <summary>
    /// Delete a conversation that served no quest: its record through the ledger, then what this
    /// machine kept of it, by the driver library's one owner, which the terminal's <c>sessions delete</c> calls too.
    /// </summary>
    /// <remarks>
    /// <para><b>The ledger's half is said first</b>: whose the record is, whether it runs, whether it served a quest, what
    /// names it and whether a remote holds it; then this machine's, its tree and a landing. Each refusal is a code in
    /// <see cref="Refusals"/> said in the catalogue's words, read by the ledger's word, never its sentence. The page offers
    /// the act only where <c>SESSION_GROUPS</c> says <c>deletable</c>, so a refusal answers a race.</para>
    ///
    /// <para><b>Nothing machine-local comes back</b>: the id, the names of what went and the names of what the disk would not let
    /// go of (<c>stayed</c>, SESSDEL1, empty when everything went), never a path. <c>session.deleted</c> is written to the machine
    /// log with no word of it.</para>
    /// </remarks>
    [DriverRoute("SESSION_DELETE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_DELETE", Session = "id")]
    [GroundSession("SESSION_DELETE", "id")]
    [Says("SESSION_DELETE", "id:session", "ids:sessions")]
    [Publishes("SESSION_DELETE", StateScopes.Sessions | StateScopes.Help, Session = "id")]
    private async Task<object?> SessionDeleteAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();
        // `help delete <id> --yes` asks for its own words, its checks that the conversation is Ask Daoris's before the delete's.
        if (Flag(request, "said") && Flag(request, "help"))
        {
            return await SessionsVerbs.HelpSaidAsync(_context, new HelpVerbAsk("delete") { Id = id, Yes = true }, cancellationToken).ConfigureAwait(false);
        }

        // `sessions delete` sent over the pipe asks for the verb's own words, a refusal said with its exit 1 as the alone path says it.
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("delete") { Ids = [id] }, cancellationToken).ConfigureAwait(false);

        var outcome = await new SessionDeletion(_loop.Home)
            .DeleteAsync(service, id, PluginEvents.Screen, _loop.Log, _loop.Events, cancellationToken)
            .ConfigureAwait(false);
        if (outcome.Verdict != DeleteVerdict.Deleted) throw NotDeleted(outcome);

        _loop.Nudge();
        return new { Deleted = id, outcome.Removed, outcome.Stayed };
    }

    /// <summary>
    /// A delete refused, as the catalogue says it: the session, and the fact its sentence names. Which named it, and the
    /// archive's two codes asked of a delete, travel as the catalogue's <c>context</c>.
    /// </summary>
    private static Exception NotDeleted(DeleteOutcome outcome)
    {
        var session = ("session", outcome.Session);
        return outcome.Verdict switch
        {
            DeleteVerdict.Unknown => Refusals.Because(Refusals.SessionUnknown, outcome.Message, session, ("context", "delete")),
            DeleteVerdict.Live => Refusals.Because(Refusals.SessionLive, outcome.Message, session, ("context", "delete")),
            DeleteVerdict.NotOurs => Refusals.Because(Refusals.SessionNotOurs, outcome.Message, session, ("machine", outcome.Machine ?? "")),
            DeleteVerdict.ServedQuest => Refusals.Because(Refusals.SessionServedQuest, outcome.Message, session, ("quest", outcome.Quest ?? "")),
            DeleteVerdict.Named => Refusals.Because(
                Refusals.SessionNamed, outcome.Message, session, ("context", outcome.NamedBy ?? SessionDeletion.ByAsk),
                ("ask", outcome.Ask ?? ""), ("quest", outcome.Quest ?? "")),
            DeleteVerdict.TreeHere => Refusals.Because(Refusals.SessionTreeHere, outcome.Message, session),
            _ => Refusals.Because(Refusals.SessionOnRemote, outcome.Message, session, ("workspace", outcome.Workspace ?? "")),
        };
    }

    /// <summary>The verb's own lines and exit code for a press a terminal sent, as <see cref="SessionsVerbs"/> runs it.</summary>
    private Task<object> SaidAsync(SessionsAsk ask, CancellationToken cancellationToken) =>
        SessionsVerbs.SaidAsync(_context, ask, cancellationToken);

    /// <summary>The session ids the page sent, or null where it sent none.</summary>
    private static IReadOnlyCollection<string>? Ids(IpcRequest request) =>
        request.Payload is { } payload && payload.TryGetProperty("ids", out var named) && named.ValueKind == JsonValueKind.Array
            ? [.. named.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!).Distinct(StringComparer.Ordinal)]
            : null;

    /// <summary>One row of <c>SESSION_GROUPS</c>, field for field what <c>daoris session list --json</c> prints. Public, as <see cref="SweepRow"/> is, so its shape is tested without a service.</summary>
    public static object Grouped(SessionGrouping row) => new
    {
        row.Session,
        row.Group,
        row.Shown,
        row.Archived,
        row.Teammate,
        row.Strikes,
        row.Awaits,
        row.AwaitsOf,
        Work = row.Work is { } work ? new { work.Commits, work.Uncommitted, work.Branch } : null,
        // its stop holds its quest here, which its line says.
        row.HoldsQuest,
        // whose pause holds its quest, which its line says and whose *Resume* stands in *Try again*'s place.
        PausedBy = row.PausedBy is { } pause ? new { Scope = pause.Word, pause.Id } : null,
        // what holds the person's words on a record that resumes later, which its line says.
        Holds = row.Holds is { } holds ? new { holds.Why, holds.Reason, holds.Repository, holds.Until, holds.Account, holds.AccountName } : null,
        // *Delete…* is offered only where it would be taken.
        row.Deletable,
        // what its own tree offers to land, whatever group it rests in, which its page offers Accept beside. The tree's
        // name, never its path.
        Lands = row.Lands is { } lands ? new { lands.Branch, lands.Tree, lands.Commits, lands.Uncommitted } : null,
        // where the line holds its commits by content, its discard instead, with the driver's sentences the page
        // shows as they are: where its work is, and the ref an unforced discard keeps its commits at.
        Discards = row.Discards is { } discards
            ? new { discards.Branch, discards.Tree, discards.Says, discards.Work.Keeps, discards.KeptAt }
            : null,
        // the call a working session's agent is inside, as the loop heard it, which its row and head say with how long;
        // whether it waits on the person's answer to its permission request.
        Calling = row.Calling is { } call ? new { call.Title, call.Since, call.Waiting, call.Told } : null,
    };

    private static object Mark(ArchiveMark mark) => new { mark.Session, mark.At };

    private static string CodeOf(ArchiveVerdict verdict) => verdict switch
    {
        ArchiveVerdict.Live => Refusals.SessionLive,
        ArchiveVerdict.NeedsYou => Refusals.SessionNeedsYou,
        _ => Refusals.SessionUnknown,
    };

    /// <summary>
    /// The refusal for one session archive kept. A group travels as the catalogue's <c>context</c> too, so the page's
    /// sentence names which: waiting on you, or to review.
    /// </summary>
    private static Exception Kept(ArchiveOutcome outcome) => outcome.Verdict switch
    {
        ArchiveVerdict.Live => Refusals.Because(
            Refusals.SessionLive, $"{outcome.Session} is still running, so it was not archived. Stop it first.",
            ("session", outcome.Session)),
        ArchiveVerdict.NeedsYou => Refusals.Because(
            Refusals.SessionNeedsYou,
            $"{outcome.Session} {(outcome.Group == SessionGroup.Review ? "has work to review" : "is waiting on you")}, so it was not "
            + "archived: archive never hides what needs you.",
            ("session", outcome.Session), ("group", outcome.Group ?? SessionGroup.You), ("context", outcome.Group ?? SessionGroup.You)),
        _ => Refusals.Because(
            Refusals.SessionUnknown, $"No session here is {outcome.Session}.", ("session", outcome.Session)),
    };

    /// <summary>
    /// *Open folder*: the folder a session worked in, its own tree or its repository's checkout,
    /// opened in the system's file manager through the window kit's launcher, as the log's folder and a plugin's open.
    /// </summary>
    /// <remarks>
    /// <para><b>The module names the folder, never the page</b>: it is read from the session's record, and only a tree this
    /// home opened or the checkout the registry names for its repository is opened. A record naming any other folder, a
    /// teammate's record and a tree a tidy took are one refusal, <c>SESSION_FOLDER_GONE</c>, since each is a folder this
    /// machine does not hold for that session. The page offers the press only where it knows the folder is here, so the
    /// refusal answers a race.</para>
    ///
    /// <para><b>Nothing machine-local comes back</b>: whether it opened, and nothing else.</para>
    /// </remarks>
    [DriverRoute("SESSION_OPEN_FOLDER", PipeClass.Window)]
    [Owner("SESSION_OPEN_FOLDER", None = OwnerNone.ActsOnNothing)]
    private async Task<object?> SessionOpenFolderAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();
        var records = SessionRecords.Parse(await SessionRecords.ReadAsync(
            service.BaseUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable), ct: cancellationToken).ConfigureAwait(false));
        var record = records.FirstOrDefault(row => string.Equals(row.Id, id, StringComparison.Ordinal))
            ?? throw Refusals.Because(
                Refusals.SessionUnknown, $"No session here is {id}, so there is no folder to open.",
                ("session", id), ("context", "folder"));

        var folder = await FolderOfAsync(service, record, cancellationToken).ConfigureAwait(false)
            ?? throw Refusals.Because(
                Refusals.SessionFolderGone,
                $"The folder {id} worked in is not on this machine: the clean-up took its tree, or it ran somewhere else.",
                ("session", id));

        if (_openFolder is null) return new { Opened = false };
        try
        {
            // Through the start gate: the launcher starts the file manager's process.
            await _openFolder.OpenAsync(folder, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw Refusals.Because(
                Refusals.SessionFolderNotOpened,
                $"the folder would not open: {error.Message}",
                ("session", id), ("problem", error.Message));
        }

        return new { Opened = true };
    }

    /// <summary>
    /// The folder a session worked in, where this machine holds it: its own tree under this home's trees, else its
    /// repository's registered checkout when the record names that or no folder at all. Null for anything else.
    /// </summary>
    private async Task<string?> FolderOfAsync(ServiceClient service, SessionRecord record, CancellationToken cancellationToken)
    {
        // A teammate's record ran on their machine: a path it names is theirs, whatever this disk holds there.
        if (record.Teammate) return null;

        var trees = new SessionTrees(_loop.Home);
        if (record.Tree is { } tree && HeldTree(trees, tree)) return Directory.Exists(tree) ? tree : null;

        var root = (await service.RegistryAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(row => string.Equals(row.Repository, record.Repository, StringComparison.Ordinal))?.Root;
        if (root is null || (record.Tree is { } named && !SamePlace(named, root))) return null;
        return Directory.Exists(root) ? root : null;
    }

    /// <summary>Two paths as one place: separators and a trailing one ignored, and case where the system ignores it.</summary>
    private static bool SamePlace(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)).Replace('\\', '/'),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)).Replace('\\', '/'),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    // What sessions said, searched: the person's words and the agent's, from this machine's
    // own record, bounded and saying so.
    [DriverRoute("SESSION_SEARCH", PipeClass.Open)]
    private async Task<object?> SessionSearchAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var query = PayloadHelper.GetRequiredValue<string>(request.Payload, "q");
        await Task.CompletedTask;
        // Within one session, where the page names it: its words and its calls' titles.
        // A page of sessions, as the help list's is: `offset` counts sessions, `total` is every session
        // with a hit, and `next` is null on the last page. A caller naming no page reads the first, at the rail's old size.
        var found = Optional(request, "session") is { } one
            ? _loop.Events.Within(one, query)
            : _loop.Events.Search(
                query,
                (int)Math.Clamp(Number(request, "offset") ?? 0, 0, int.MaxValue),
                (int)Math.Clamp(Number(request, "limit") ?? SessionEvents.SearchSessions, 0, SessionEvents.SearchPageLimit));
        return new
        {
            Query = query,
            Hits = found.Hits.Select(hit => new { hit.Session, hit.Seq, hit.Kind, hit.Snippet }).ToArray(),
            found.Cut,
            found.Total,
            found.Next,
        };
    }
}
