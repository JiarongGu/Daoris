using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A session as the rail and its drawer act on it, the page's `bridge/sessions.ts` (MOD5): a stop, the
/// person's answer to a parked session (D52 §4), and what sessions opened with and said, read off this
/// machine's own record (RAIL1).
/// </summary>
public sealed class SessionsRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;
    private readonly OpenFolder? _openFolder = context.OpenFolder;

    // What a session's own presses change (STATEHUB1d, D169): its record among the sessions, and the driver's state its loop,
    // nudged, reads again; a resolve moves its quest too.
    [DriverRoute("STOP_SESSION")]
    [Publishes("STOP_SESSION", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> StopSessionAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        // RPC1d: `sessions stop` sent over the pipe asks for the verb's own words; the page asks for none and its answer keeps its shape.
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("stop") { Ids = [id] }, cancellationToken).ConfigureAwait(false);

        // False is an answer, not an error: the session already finished, and its record says how.
        // 🔴 Unless the record says it still runs and nothing on this machine runs it — a chat
        // left behind by an application that closed, or a crash. Then the person's stop is how
        // it ends: pressing it used to change nothing and say nothing (2026-09-25).
        // Which of the three it was is part of the answer: the page says it, and an orphan's
        // ending is not the person's — its record says nothing ran it.
        // 🔴 And when another Daoris process on this machine runs it — a terminal's chat — this
        // host can neither stop it nor call it ended: the record still says working (REV3).
        // The one implementation the terminal's request is honoured by too (SESSUX1g).
        var answer = await SessionMoves.StopAsync(_loop.Processes, _loop.Service, id, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();
        return new { answer.Stopped, answer.Orphan, answer.Elsewhere };
    }

    // The person's answer to a session parked at a checkpoint (D52 §4). It goes through the
    // DRIVER rather than straight to the service because the two halves must move together:
    // this machine lets the process go, and only then does the record say it ended.
    /// <summary>
    /// The three moves a person may make on a parked session — `completed`, `declined`, `stopped`,
    /// each carrying what they want the record to say (design §4).
    /// </summary>
    /// <remarks>
    /// <para><b>Narrowed here, not re-judged.</b> The ledger allows a fourth from `awaiting-person`
    /// — back to `working` — and that one is the DRIVER's observation, not a button: a person
    /// resumes a conversation by answering it, which is the composer's job. Narrowing the person's
    /// verbs is a surface rule and belongs on the surface; everything about whether the move is
    /// legal at all stays the ledger's (D36), and its refusal reaches the person verbatim.</para>
    ///
    /// <para><b>The process goes first.</b> A record that says `completed` while this machine still
    /// holds the process is exactly the lie the observed lifecycle exists to prevent — so the
    /// process is let go, and the record moves after. `Stop` answering false is not an error: the
    /// common case is a session parked with nothing of ours still running.</para>
    ///
    /// <para><b>Declining needs a reason</b>, the same rule the quest door already holds and for the
    /// same reason: the note is the part whoever reads the record can act on.</para>
    ///
    /// <para><b>A move with no note still writes one.</b> The store keeps the previous note when a
    /// move carries none (deliberately — a later move must not erase what an earlier one recorded),
    /// so a session finished at a checkpoint would otherwise read <i>reached completed</i> beside
    /// the analysis it was parked with, which says the opposite of what happened. The stamped
    /// sentence also carries the one fact the state cannot: `completed` normally means the session
    /// closed its own quest, and this one means a person decided it was done.</para>
    /// </remarks>
    [DriverRoute("RESOLVE_SESSION")]
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

        // RPC1d: `sessions finish` and `decline` sent over the pipe ask for the verb's own words (`stopped` is the page's alone).
        if (Flag(request, "said") && state != "stopped")
        {
            return await SaidAsync(new SessionsAsk(state == "completed" ? "finish" : "decline") { Ids = [id], Note = note }, cancellationToken)
                .ConfigureAwait(false);
        }

        // The process first, then the record, with the person's words or the sentence that says they moved it: the one
        // implementation the terminal's request is honoured by too (SESSUX1g). False from the stop is the common case.
        var message = await SessionMoves.ResolveAsync(
            session => _loop.Processes.Stop(session), service, id, state, note, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();

        return new { Session = id, State = state, Message = message };
    }

    // *Go on in a new session* (MSG1g, D137 §2.2): words a resume holds while the account their session ran on cools go on now
    // in a new session, handed them, without that conversation. The driver's door judges and keeps the person's choice
    // (D143 point 4), the same door `daoris-driver sessions go-on-new` calls (D50); the loop is nudged so its next look carries
    // them on. What cannot is `sent: false` with its code, for the page to word, and the driver's sentence beside it. The
    // page's press is a later row.
    [DriverRoute("SESSION_GO_ON_NEW")]
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

    // *Ask another agent…* (MIXAGENT2b, D163 point 2; the mixed-agents design §3): the person's own ask of a helper for a session,
    // the file `daoris-driver sessions helper` writes (`HelperFiles.WriteAsk`, asked by the person, so the `helpers` rule's `ask`
    // and `off` do not hold it), judged as that verb judges: a question within the bound, an agent of one word, and a session of
    // this machine's that may ask. Everything else is the driver's, at its next look, which the nudge brings sooner. What cannot is
    // `asked: null` with its code for the page to word, and the verb's sentence beside it; the page never reads a path.
    [DriverRoute("SESSION_HELPER_ASK")]
    [Publishes("SESSION_HELPER_ASK", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private async Task<object?> SessionHelperAskAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var question = (Optional(request, "question") ?? "").Trim();
        var agent = Optional(request, "agent")?.Trim();
        var service = _loop.Service ?? throw NotReady();
        // RPC1d: `sessions helper` sent over the pipe asks for the verb's own words, judged by the same code.
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

        // The verb's sentences, so the person reads the same one from either door.
        var asker = await service.RecordAsync(id, cancellationToken).ConfigureAwait(false);
        var refused = asker switch
        {
            null or { Teammate: true } => (HelperAskWhy.NotHeld, $"No session `{id}` of this machine's is held to bring an answer back to."),
            { Serves: { } serves } => (HelperAskWhy.Helper, $"Session `{id}` is itself a helper for session `{serves}`, and a helper cannot ask for a helper."),
            { Opinion: { } opinion } => (HelperAskWhy.Reviewer, $"Session `{id}` reads another session's work for second opinion `{opinion}`, and a reviewer cannot ask for a helper."),
            { Ask: { Length: > 0 } intake } => (HelperAskWhy.Intake, $"Session `{id}` is the intake for ask `#{intake}`, which does no work in a repository, and an intake cannot ask for a helper."),
            _ => ((string, string)?)null,
        };
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

    // The asks a session made of helpers (MIXAGENT2b), oldest first, each as the driver's look left it: its state and code, the
    // helper's session once started and the agent it started on. Read off the home's `helpers` folder, the driver's own files;
    // what the state keeps for the driver alone (the last words, the word it was handed as) is not the page's.
    [DriverRoute("SESSION_HELPERS")]
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
            })
            .ToList();
        return Task.FromResult<object?>(new { Helpers = helpers });
    }

    // What of the agents' asks waits on the person's word across this machine's sessions (MIXAGENT2c, D168; D163 point 3), for
    // *What needs you*: each the driver's look holds `waiting` with no code under the `helpers` rule's `ask`, the one state the yes
    // and the no answer, oldest first, with the session that asked. A read off the home's `helpers` folder: it declares nothing,
    // and sits under the sessions' key on the page, which every tick asks again; the look says a new one in its report.
    [DriverRoute("HELPER_WAITS")]
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

    // The person's yes to an agent's ask under the `helpers` rule's `ask` (MIXAGENT2b, D163 point 3; design §5): the `approved`
    // state the driver's look reads (`HelperJudge`), written where the ask waits on that yes: no state yet, or `waiting` with no
    // code. A yes again is the same yes. A person's own ask needs none, one past its start is past it, one waiting on a slot or an
    // agent waits on no yes, and one this session does not hold is gone: each `approved: false` with its code, its state untouched.
    [DriverRoute("SESSION_HELPER_APPROVE")]
    [Publishes("SESSION_HELPER_APPROVE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private async Task<object?> SessionHelperApproveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var askId = PayloadHelper.GetRequiredValue<string>(request.Payload, "ask");
        // RPC1d: `sessions helper --approve` names only the ask, and asks for the verb's own words.
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("helper-approve") { Ids = [askId] }, cancellationToken).ConfigureAwait(false);

        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        // The judge is the library's, which the terminal's `sessions helper --approve` shares (HELPERTERM1).
        var answer = HelperPersonWord.Approve(new HelperFiles(_loop.Home), session, askId);
        if (answer.Wrote) _loop.Nudge();
        return new { Approved = answer.Stands, answer.Why, answer.Message };
    }

    // The person's no to an agent's ask under the `helpers` rule's `ask` (HELPERNO1, D163 point 3; design §5), beside the yes: the ask
    // refused `declined` at once, with the coded note the look writes on the asker's record for every refusal (`helper.refused`), so
    // the look reads it as past judging and its timeline says it as it says the rule's `off`. Written only where the look holds the
    // ask waiting on the person's word, `waiting` with no code: one it has not read yet may be starting under `on`, so it waits on no
    // word yet. A no again is the same no, with no second note. A person's own ask needs none, one with the person's yes waits on the
    // look, one past its start is past it, one waiting on a slot or an agent waits on none, and one this session does not hold is
    // gone: each `declined: false` with its code, its state untouched.
    [DriverRoute("SESSION_HELPER_DECLINE")]
    [Publishes("SESSION_HELPER_DECLINE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private async Task<object?> SessionHelperDeclineAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var askId = PayloadHelper.GetRequiredValue<string>(request.Payload, "ask");
        // RPC1d: `sessions helper --decline` names only the ask, and asks for the verb's own words.
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("helper-decline") { Ids = [askId] }, cancellationToken).ConfigureAwait(false);

        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        // The judge is the library's, which the terminal's `sessions helper --decline` shares (HELPERTERM1).
        var answer = HelperPersonWord.Decline(new HelperFiles(_loop.Home), _loop.Events, _loop.Output, session, askId);
        return new { Declined = answer.Stands, answer.Why, answer.Message };
    }

    // A session's agent's ask to hand it over (MIXAGENT1c, D163 point 3), as the driver's look left it, for the session's page: its
    // note (the agent's claim), when, its state, code and the agent it goes to; null where its agent asked none. A read off the home's
    // sessions folder, the driver's own files: it declares nothing, and the look says a change in its report.
    [DriverRoute("SESSION_HAND_OVER_ASK")]
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

    // The person's yes to their session's agent's ask to hand it over under the `handOver` rule's `ask` (MIXAGENT1c, D163 point 3):
    // the judge the terminal's `sessions hand-over <id> --approve` shares (`HandOverAskWord`). A conversation this shell's chat runner
    // holds is handed over at once, through the runner's own hand-over (taken where it is not talking, else at its turn's end), the
    // state written `taken` first so no look keeps a second box; anything else is `approved` with the agent the look named, for the
    // look, nudged, to take as the rule's `on` would. `ask` names the one the page showed, else the session's own. A word that waits on
    // nothing is `approved: false` with its code, its state untouched.
    [DriverRoute("SESSION_HAND_OVER_APPROVE")]
    [Publishes("SESSION_HAND_OVER_APPROVE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private async Task<object?> SessionHandOverApproveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        // RPC1d: `sessions hand-over <id> --approve` asks for the verb's own words.
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
                // Refused as it was taken: the look's own refusal, said on the session's record.
                files.WriteState(session, new HandOverAskState(askId, HandOverAskStates.Refused, HandOverAskWhy.Cannot, null, DateTimeOffset.UtcNow));
                var note = HandOverAskNotes.Refused(HandOverAskWhy.Cannot);
                _loop.Output?.Append(session, $"— {note.Note}");
                _loop.Events.Keep(session, new SessionEvent { Kind = SessionEventKind.Note, Text = note.Note, Parts = note.Parts }, say: null);
                _loop.Nudge();
                return new { Approved = false, Why = handed.Why ?? HandOverAskWhy.Cannot, handed.Message, Agent = agent };
            }
        }

        if (answer.Wrote) _loop.Nudge();
        return new { Approved = answer.Stands, answer.Why, answer.Message, answer.Agent };
    }

    // The person's no to their session's agent's ask to hand it over (MIXAGENT1c, D163 point 3), beside the yes: refused `declined` at
    // once, with the coded note the look writes for every refusal (`handover.refused`), where the look holds it waiting on their word.
    // One not read yet, one with their yes, one past it and one gone are `declined: false` with its code, its state untouched.
    [DriverRoute("SESSION_HAND_OVER_DECLINE")]
    [Publishes("SESSION_HAND_OVER_DECLINE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private async Task<object?> SessionHandOverDeclineAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        // RPC1d: `sessions hand-over <id> --decline` asks for the verb's own words.
        if (Flag(request, "said")) return await SaidAsync(new SessionsAsk("hand-over-decline") { Ids = [session] }, cancellationToken).ConfigureAwait(false);

        var files = new HandOverAskFiles(_loop.Home);
        var askId = Optional(request, "ask") ?? files.Ask(session)?.Id ?? "";
        var answer = HandOverAskWord.Decline(files, _loop.Events, _loop.Output, session, askId);
        return new { Declined = answer.Stands, answer.Why, answer.Message };
    }

    // What this machine's driven sessions hold for the person under their repository's asking permission prompts (AUTONOMY1b3a, D184
    // point 3), oldest first: each request with its session, its call as the agent named it, and what the person may choose (never a
    // standing allow), and when its bound releases it (`until`, PERMUNTIL1: the page's one source for the bound). A read off the
    // driver's memory (`HeldPermissions`), which a restart empties with the agents that asked; the attention slice's own projection,
    // so the pushed rows and the route say each hold alike.
    [DriverRoute("PERMISSION_WAITS")]
    private Task<object?> PermissionWaitsAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new { Waits = AttentionStateSlice.PermissionWaits(HeldPermissions.Of(_loop.Events)) });

    // The person's answer to a request a session holds for them (AUTONOMY1b3a): an option the agent offered, by its id, or none to
    // refuse it, which takes the offered one-time rejection, else none. The allow is the person's press for this call alone, and
    // Daoris never answers one by itself (D37, D52); a standing allow is a rule's to give (D74). What cannot is `answered: false` with
    // its code (`unheld`, `unoffered`, `standing`) and the registry's sentence, the hold left as it was. Its session hears the answer
    // at once, off its own reader, and the hold's end is told through the registry's `Changed`; the route publishes nothing.
    [DriverRoute("SESSION_PERMISSION_ANSWER")]
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
    [DriverRoute("ANSWER_WAITS")]
    private Task<object?> AnswerWaitsAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new { Waits = AttentionStateSlice.AnswerWaits(_loop.Home) });

    // The person's answer to a third answer alone held for them (COWORK1e; §8.3): `goOn` true is *Go on with this answer*, their press
    // kept as their word and the mark taken away, so the driver's next look, nudged, or the conversation's own go-on takes the answer on;
    // false is *Not now*, the answer kept for them, going on only beside a word of theirs. Where nothing holds an answer of the session
    // for them, `answered: false` with its code (`unheld`), and nothing is written.
    [DriverRoute("SESSION_ANSWER_ALONE")]
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

    private const string PersonMoves =
        "A person may finish, decline or stop a parked session — not move it anywhere else. "
        + "Answering it so it carries on is a message, not a move.";

    private const string DeclineNeedsReason =
        "Declining needs a reason: it is the part whoever reads this record can act on.";

    // What a person first said in each of these sessions (RAIL1): a conversation's identity, from
    // this machine's own record — never the session record, which travels (D47 §4).
    [DriverRoute("SESSION_OPENINGS")]
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
    /// Where each of these sessions' work is now (LOOK2b), for the rail in one ask: whether the tree it opened is still
    /// here, and its landing, standing or a trace (D113), so a row says where the work landed rather than naming a tree a
    /// landing tidied away.
    /// </summary>
    /// <remarks>
    /// <para><b>From this machine's own files, and cheap</b>: the landing record and whether a folder is there. No service
    /// is asked and no git is run, so it answers on a cold start, and a landing's branch is read as the record holds it:
    /// the review is where git is asked where that branch stands (D113 §1).</para>
    ///
    /// <para><b>Only this home's trees are looked at.</b> The page sends the tree each record names; one outside the trees
    /// this home opened is a session in a repository's own checkout, which no landing tidies, and nothing is said of
    /// it. Nothing machine-local comes back.</para>
    /// </remarks>
    [DriverRoute("SESSION_WHERE")]
    private async Task<object?> SessionWhereAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var trees = new SessionTrees(_loop.Home);
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

                var landing = trees.Recorded.Landing(id);
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
    /// Where each session is listed by state (SESSUX1a, D126 §2.4): its group, the word its row shows, and what its second
    /// line says, in the order a list shows them, read by the driver library's one reader, which the terminal's
    /// <c>sessions</c> prints too. With <c>ids</c>, those sessions' alone.
    /// </summary>
    /// <remarks>
    /// <para><b>From the records, the quests, the planner's verdicts, the trees and the marks</b>: the loop's last look where
    /// it has looked, a plan over a fresh snapshot where it has not. Never from what a session printed.</para>
    ///
    /// <para><b>No git at a window's tick</b> (PUSH1c1, D182 point 4): an ended session's tree that could be to review is read as the
    /// machine's facts judged it, a part per repository (<see cref="HelpFacts.WithTreesAsync"/>), so more windows never mean more git.
    /// Only a tree no part judged yet is walked here, once the parts' first judgement was waited for.</para>
    ///
    /// <para><b>Nothing machine-local comes back</b>: ids, groups, words and counts. A tree's path stays here. The one
    /// exception is a working session's running call (WAITSAY1b): its title is the agent's own words, which may name a file,
    /// transcript-class as its conversation is (D47 §4) and answered over this bridge alone, as <c>SESSION_HISTORY</c> is.</para>
    /// </remarks>
    [DriverRoute("SESSION_GROUPS")]
    private async Task<object?> SessionGroupsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var ids = Ids(request);
        var (look, grouped) = await GroupsAsync(ids, cancellationToken).ConfigureAwait(false);
        // SESSONE1b (D167): the entries beside the rows, which the page reads until SESSONE1c draws entries.
        var sessions = grouped.Select(Grouped).ToArray();
        var entries = SessionGroups.ReadEntries(look, ids).Select(Entry).ToArray();
        if (!Flag(request, "said")) return new { Sessions = sessions, Entries = entries };

        // RPCOPEN1: `daoris-driver sessions` asks for its own lines from this look (the group, the repository and `--json` it was given),
        // which the alone path prints from a look of its own through the same function (§7.3); the page asks for none.
        var ask = new SessionsAsk("list") { Group = Optional(request, "group"), Repository = Optional(request, "repository"), Json = Flag(request, "json") };
        return new
        {
            Sessions = sessions, Entries = entries,
            Said = new TreesSaid(SessionsCommand.ListLines(look, ask, _loop.Home, DateTimeOffset.UtcNow), [], 0).ToWire(),
        };
    }

    /// <summary>
    /// One entry of <c>SESSION_GROUPS</c>, field for field what <c>daoris-driver sessions --json</c> prints in <c>entries</c>
    /// (SESSONE1b): its key and the key's halves, the group its leading part put it in, and its parts, oldest first, each with
    /// how it began. Ids, words and counts only.
    /// </summary>
    public static object Entry(SessionEntry entry) => new
    {
        Entry = entry.Key,
        entry.Origin,
        entry.Repository,
        entry.Group,
        entry.Leading,
        Parts = entry.Parts.Select(part => new { part.Session, part.Quest, part.Shown, part.Group, part.Began, part.After, part.AskedBy }).ToArray(),
    };

    /// <summary>
    /// Archive these sessions on this machine, or bring them back (SESSUX1a, D126 §5.2): the marks as they now stand.
    /// </summary>
    /// <remarks>
    /// <para><b>Each is judged by where the reader places it now</b>: a live one, one waiting on you and one with work to
    /// review are refused, so archive never hides what needs the person, and an id no record has is refused too. Asked of
    /// one session, a refusal is the answer, in the catalogue's words; asked of several, as <i>Archive what ended</i>'s
    /// second press asks, what may go is archived and the rest are kept, each with its code, since a list is a fact about
    /// a moment.</para>
    ///
    /// <para><b>Unarchive needs no service</b>: it takes marks away, and one that was not archived is said, never refused
    /// (D48 §6).</para>
    /// </remarks>
    [DriverRoute("SESSION_ARCHIVE")]
    [Publishes("SESSION_ARCHIVE", StateScopes.Sessions)]
    private async Task<object?> SessionArchiveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var archive = PayloadHelper.GetRequiredValue<bool>(request.Payload, "archived");
        var ids = Ids(request) ?? [];
        var marks = new SessionArchive(_loop.Home);
        // RPC1c: `sessions archive` and `unarchive` sent over the pipe ask for their words (`said: true`), the alone path's own, and a
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
        // SESSUX1g (D126 §7.4): counted in the machine log by the door it came in by: the screen's, or the terminal's over the pipe.
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
    /// trees are the facts' (PUSH1c1).
    /// </param>
    private async Task<(SessionLook Look, IReadOnlyList<SessionGrouping> Grouped)> GroupsAsync(
        IReadOnlyCollection<string>? only, CancellationToken cancellationToken, bool walked = false)
    {
        var service = _loop.Service ?? throw NotReady();
        // PUSH1c3c: the one look, in the groups slice's builder (PUSH1c1: no tree judged here unless walked; the waits, the calls and
        // the kept look are laid there), so the route and the pushed slice cannot drift.
        var look = await GroupsStateSlice.LookAsync(_loop, service, cancellationToken, only, walked).ConfigureAwait(false);

        return (look, SessionGroups.Read(look, only));
    }

    /// <summary>
    /// Delete a conversation that served no quest (SESSUX1f, D126 §5.4): its record through the ledger, then what this
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
    [DriverRoute("SESSION_DELETE")]
    [Publishes("SESSION_DELETE", StateScopes.Sessions | StateScopes.Help, Session = "id")]
    private async Task<object?> SessionDeleteAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();
        // RPC1d: `sessions delete` sent over the pipe asks for the verb's own words, a refusal said with its exit 1 as the alone path says it.
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

    /// <summary>The verb's own lines and exit code for a press a terminal sent (RPC1d), as <see cref="SessionsVerbs"/> runs it.</summary>
    private Task<object> SaidAsync(SessionsAsk ask, CancellationToken cancellationToken) =>
        SessionsVerbs.SaidAsync(_context, ask, cancellationToken);

    /// <summary>The session ids the page sent, or null where it sent none.</summary>
    private static IReadOnlyCollection<string>? Ids(IpcRequest request) =>
        request.Payload is { } payload && payload.TryGetProperty("ids", out var named) && named.ValueKind == JsonValueKind.Array
            ? [.. named.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!).Distinct(StringComparer.Ordinal)]
            : null;

    /// <summary>One row of <c>SESSION_GROUPS</c>, field for field what <c>daoris-driver sessions --json</c> prints (SESSUX1g). Public, as <see cref="SweepRow"/> is, so its shape is tested without a service.</summary>
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
        // SESSUX1b (D126 §2.2): its stop holds its quest here, which its line says.
        row.HoldsQuest,
        // PAUSE1b (D132 §6.1): whose pause holds its quest, which its line says and whose *Resume* stands in *Try again*'s place.
        PausedBy = row.PausedBy is { } pause ? new { Scope = pause.Word, pause.Id } : null,
        // MSG1f2 (D137 §3.2): what holds the person's words on a record that resumes later, which its line says.
        Holds = row.Holds is { } holds ? new { holds.Why, holds.Reason, holds.Repository, holds.Until } : null,
        // SESSUX1f (D126 §5.4): *Delete…* is offered only where it would be taken.
        row.Deletable,
        // LAND4: what its own tree offers to land, whatever group it rests in, which its page offers Accept beside. The tree's
        // name, never its path.
        Lands = row.Lands is { } lands ? new { lands.Branch, lands.Tree, lands.Commits, lands.Uncommitted } : null,
        // SQUASHTIDY1b: where the line holds its commits by content, its discard instead, with the driver's sentences the page
        // shows as they are: where its work is, and the ref an unforced discard keeps its commits at.
        Discards = row.Discards is { } discards
            ? new { discards.Branch, discards.Tree, discards.Says, discards.Work.Keeps, discards.KeptAt }
            : null,
        // WAITSAY1b: the call a working session's agent is inside, as the loop heard it, which its row and head say with how long;
        // AUTONOMY1b3b: whether it waits on the person's answer to its permission request.
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
    /// *Open folder* (SESSUX1d, D126 §3.5): the folder a session worked in, its own tree or its repository's checkout,
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
    [DriverRoute("SESSION_OPEN_FOLDER")]
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
            // Through the start gate (PROCSTART2): the launcher starts the file manager's process.
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
        // A teammate's record ran on their machine (SYNC4): a path it names is theirs, whatever this disk holds there.
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

    // What sessions said, searched (RAIL1): the person's words and the agent's, from this machine's
    // own record, bounded and saying so.
    [DriverRoute("SESSION_SEARCH")]
    private async Task<object?> SessionSearchAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var query = PayloadHelper.GetRequiredValue<string>(request.Payload, "q");
        await Task.CompletedTask;
        // Within one session, where the page names it (SESS1 S9): its words and its calls' titles.
        // A page of sessions, as the help list's is (RAILPAGE1a, ASKHIST1d1): `offset` counts sessions, `total` is every session
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
