using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// What a session asks of others and what is asked of the person for it, behind the sessions doors: a helper or a partner asked for,
/// their asks listed and answered, the agent's ask to hand a session over, and a parked session's question. Each reads or writes the
/// driver's own files in the home, by the judges the terminal's verbs share.
/// </summary>
internal sealed class SessionsAskDesk(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    public async Task<object?> HelperAskAsync(SessionHelperAsk press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var question = (press.Question ?? "").Trim();
        var agent = press.Agent?.Trim();
        var service = _loop.Service ?? throw NotReady();
        // `sessions helper` sent over the pipe asks for the verb's own words, judged by the same code.
        if (press.Said && question.Length > 0)
        {
            return await SessionsVerbs.SaidAsync(context, new SessionsAsk("helper") { Ids = [id], Text = question, Agent = agent }, cancellationToken).ConfigureAwait(false);
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

    public Task<object?> HelpersAsync(SessionHelpers press, CancellationToken cancellationToken)
    {
        var id = press.Id;
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

    public async Task<object?> PartnerAskAsync(SessionPartnerAsk press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var piece = (press.Piece ?? "").Trim();
        var agent = press.Agent?.Trim();
        IReadOnlyList<string> paths = press.Paths ?? [];
        var service = _loop.Service ?? throw NotReady();
        // `sessions partner` sent over the pipe asks for the verb's own words, judged by the same code.
        if (press.Said)
        {
            return await SessionsVerbs.SaidAsync(context, new SessionsAsk("partner") { Ids = [id], Text = piece, Agent = agent, Paths = paths }, cancellationToken).ConfigureAwait(false);
        }

        var asked = await PartnerPersonAsk.AskAsync(
            _loop.Home, service, id, piece, paths, agent, _loop.PieceBases, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (asked.Asked is not null) _loop.Nudge();
        return new { asked.Asked, asked.Why, asked.Message };
    }

    public Task<object?> PartnersAsync(SessionPartners press, CancellationToken cancellationToken)
    {
        var id = press.Id;
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

    public Task<object?> HelperWaitsAsync()
    {
        var files = new HelperFiles(_loop.Home);
        var waits = files.Asks()
            .Where(ask => ask.AskedBy == HelperAsk.ByAgent && files.ReadState(ask.Id) is { State: HelperStates.Waiting, Code: null })
            .OrderBy(ask => ask.At)
            .Select(ask => new { ask.Id, Session = ask.Asker, ask.Question, ask.Agent, At = Moments.Write(ask.At) })
            .ToList();
        return Task.FromResult<object?>(new { Waits = waits });
    }

    public async Task<object?> HelperApproveAsync(SessionHelperApprove press, CancellationToken cancellationToken)
    {
        var askId = press.Ask;
        // `sessions helper --approve` names only the ask, and asks for the verb's own words.
        if (press.Said) return await SessionsVerbs.SaidAsync(context, new SessionsAsk("helper-approve") { Ids = [askId] }, cancellationToken).ConfigureAwait(false);

        var session = press.Session ?? throw new DriverException("a helper's ask is answered for a `session` — name it.");
        // The judge is the library's, which the terminal's `sessions helper --approve` shares.
        var answer = HelperPersonWord.Approve(new HelperFiles(_loop.Home), session, askId);
        if (answer.Wrote) _loop.Nudge();
        return new { Approved = answer.Stands, answer.Why, answer.Message };
    }

    public async Task<object?> HelperDeclineAsync(SessionHelperDecline press, CancellationToken cancellationToken)
    {
        var askId = press.Ask;
        // `sessions helper --decline` names only the ask, and asks for the verb's own words.
        if (press.Said) return await SessionsVerbs.SaidAsync(context, new SessionsAsk("helper-decline") { Ids = [askId] }, cancellationToken).ConfigureAwait(false);

        var session = press.Session ?? throw new DriverException("a helper's ask is answered for a `session` — name it.");
        // The judge is the library's, which the terminal's `sessions helper --decline` shares.
        var answer = HelperPersonWord.Decline(new HelperFiles(_loop.Home), _loop.Events, _loop.Output, session, askId);
        return new { Declined = answer.Stands, answer.Why, answer.Message };
    }

    public Task<object?> HandOverAskAsync(SessionHandOverAsk press)
    {
        var id = press.Id;
        var files = new HandOverAskFiles(_loop.Home);
        var ask = files.Ask(id);
        var state = ask is null ? null : files.ReadState(id) is { } kept && kept.Id == ask.Id ? kept : null;
        return Task.FromResult<object?>(new
        {
            Ask = ask is null ? null : new { ask.Id, ask.Note, At = Moments.Write(ask.At), state?.State, state?.Code, state?.Agent },
        });
    }

    public async Task<object?> QuestionAsync(SessionQuestion press, CancellationToken cancellationToken)
    {
        var id = press.Id;
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

    public async Task<object?> HandOverApproveAsync(SessionHandOverApprove press, CancellationToken cancellationToken)
    {
        var session = press.Session;
        // `sessions hand-over <id> --approve` asks for the verb's own words.
        if (press.Said) return await SessionsVerbs.SaidAsync(context, new SessionsAsk("hand-over-approve") { Ids = [session] }, cancellationToken).ConfigureAwait(false);

        var files = new HandOverAskFiles(_loop.Home);
        var askId = press.Ask ?? files.Ask(session)?.Id ?? "";
        var record = _loop.Chat is not null && _loop.Service is { } service
            ? await service.RecordAsync(session, cancellationToken).ConfigureAwait(false)
            : null;
        var taking = record is { Kind: "chat" } && files.ReadState(session) is { Id: var named, Agent: { Length: > 0 } } && named == askId;
        var answer = HandOverAskWord.Approve(files, session, askId, taking);
        if (taking && answer.Wrote && answer.Agent is { } agent)
        {
            var handed = await _loop.Chat!.HandOverAsync(
                    session, agent, null, HandOverWhy.Agent, HandOverBy.Agent, DriverConfig.Load(_loop.ConfigPath),
                    onEnded: (ended, state) => DriverLoop.Ended(context.Events, ended, state), cancellationToken)
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

    public async Task<object?> HandOverDeclineAsync(SessionHandOverDecline press, CancellationToken cancellationToken)
    {
        var session = press.Session;
        // `sessions hand-over <id> --decline` asks for the verb's own words.
        if (press.Said) return await SessionsVerbs.SaidAsync(context, new SessionsAsk("hand-over-decline") { Ids = [session] }, cancellationToken).ConfigureAwait(false);

        var files = new HandOverAskFiles(_loop.Home);
        var askId = press.Ask ?? files.Ask(session)?.Id ?? "";
        var answer = HandOverAskWord.Decline(files, _loop.Events, _loop.Output, session, askId);
        return new { Declined = answer.Stands, answer.Why, answer.Message };
    }
}
