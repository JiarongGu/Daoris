using Daoris.Contracts.Sessions;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The doors for the person's words on a session, a local host's alone: a session goes on only where its process and its conversation
/// are, and a shared host runs none. The words are kept on the record for it to go on with, the record staying as it is; what never goes
/// on is refused by its word, which the driver reads instead of the sentence.
/// </summary>
public sealed class SessionWordRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<AddToSession, AddedResponse>(AddedAsync);
        routes.Map<SaySession, SaidResponse>(SayAsync);
        routes.Map<RestoreSessionPark, SessionActionResponse>(RestoreAsync);
        routes.Map<TakeSaid, SessionActionResponse>(TakenAsync);
        routes.Map<AttemptSaid, AttemptResponse>(AttemptAsync);
        routes.Map<TakeBackSaid, TakeBackResponse>(TakeBackAsync);
        routes.Map<MoveSaid, MoveSaidResponse>(MoveAsync);
    }

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static List<string> IdsOf(IReadOnlyList<string?>? said) => (said ?? []).OfType<string>().Where(word => word.Length > 0).ToList();

    private Task<SessionResponse> Shown(Session session, HttpContext http) =>
        SessionAnswers.ToSessionAsync(Service, session, host.MachineLocal(http));

    private static SessionSayRefusalResponse Refused(SessionSayOutcome outcome, string? head = null) =>
        new(outcome.Message, SessionAnswers.SayRefusal(outcome.Refusal), outcome.Quest, outcome.Ask, outcome.Origin, outcome.Opinion, head);

    // The words kept on the ask its work is for, as the person said them: the words reach the session through the driver, which never
    // passes them here otherwise. A session on no ask is answered `kept: false` with the reason, which is no error.
    private async Task<Answer<AddedResponse>> AddedAsync(AddToSession request, HttpContext http, CancellationToken ct)
    {
        var outcome = await Service.Ledger.KeepOnAskAsync(request.Id, AskWordKind.Added, request.Text, DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            AskWordRefusal.None => new AddedResponse(
                true, outcome.Message,
                await Service.Asks.FindAsync(outcome.Ask!, ct) is { } ask ? AskAnswers.ToAsk(ask, Service.Files, host.MachineLocal(http)) : null),
            AskWordRefusal.NoAsk => new AddedResponse(false, outcome.Message, null),
            AskWordRefusal.NotFound => Refusal.NotFound(outcome.Message),
            _ => Refusal.BadRequest(outcome.Message),
        };
    }

    private async Task<Answer<SaidResponse>> SayAsync(SaySession request, HttpContext http, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var outcome = await Service.Ledger.SayAsync(
            request.Id, request.Text, request.Files, now, ct, Trimmed(request.Channel), Trimmed(request.From), Trimmed(request.Key),
            Trimmed(request.Level), request.Running == true, Trimmed(request.Decision), Trimmed(request.Question),
            string.IsNullOrEmpty(request.Choice) ? null : request.Choice,
            settled: request.Settled == true, ending: request.Ending == true);
        // A key the line settled is answered with the record its word left and no word, nothing kept, so nothing beside the say is.
        if (outcome.SettledOn is { } settledOn) return new SaidResponse(null, outcome.Message, null, settledOn);
        if (outcome.Refusal == SessionSayRefusal.None)
        {
            // Words to a park are its answer, kept on its ask at once; words to an ended session are kept there once a session took
            // them (the taken door), and words for a running session's run as its run takes them. Words that are a channel message
            // are on the ask already, and Daoris's own are never the person's, which every later session reads the ask's words as.
            // A say answered with the word its key already kept keeps nothing again.
            if (outcome.Answers && outcome.Word!.Channel is null && outcome.Word.Persons && !outcome.Again)
            {
                await Service.Ledger.KeepOnAskAsync(request.Id, AskWordKind.Answered, outcome.Word.Text, now, ct);
            }

            var local = host.MachineLocal(http);
            return new SaidResponse(await Shown(outcome.Session!, http), outcome.Message, local ? SessionAnswers.ToSaid(outcome.Word!) : null);
        }

        // The record's line went on past it: `continued` names the line's head beside its word, at the conflict the door gives a record as it stands.
        var refused = Refused(outcome, outcome.Head);
        return Answer<SaidResponse>.Else(
            outcome.Refusal switch
            {
                SessionSayRefusal.NotFound => StatusCodes.Status404NotFound,
                // An author, a level or a release this service does not know is the caller's mistake, as no words are.
                SessionSayRefusal.Empty or SessionSayRefusal.From or SessionSayRefusal.Level or SessionSayRefusal.SupervisorText
                    or SessionSayRefusal.NoHome => StatusCodes.Status400BadRequest,
                // A teammate's, an intake, a stand-down, a running session: a conflict with the record as it stands.
                _ => StatusCodes.Status409Conflict,
            },
            refused);
    }

    // A park the driver took up and could not hand put back, keeping every word but the supervisor's it admitted.
    private async Task<Answer<SessionActionResponse>> RestoreAsync(RestoreSessionPark request, HttpContext http, CancellationToken ct)
    {
        var outcome = await Service.Ledger.RestoreParkAsync(
            request.Id, (request.TakeBack ?? []).OfType<string>().ToList(), request.Note,
            request.Note is null ? null : NoteParts.Normalize(request.NoteParts), DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            SessionAdvanceRefusal.None => new SessionActionResponse(await Shown(outcome.Session!, http), outcome.Message),
            SessionAdvanceRefusal.NotFound => Refusal.NotFound(outcome.Message),
            _ => Refusal.Conflict(outcome.Message),
        };
    }

    // The words a session took off its record. Each of the person's said after the record ended is kept on the ask its work is for, as
    // `reopened`, said to the session that took it. Another agent's claims never are: on the ask they would reach every later session as
    // the person's requirements.
    private async Task<Answer<SessionActionResponse>> TakenAsync(TakeSaid request, HttpContext http, CancellationToken ct)
    {
        var outcome = await Service.Ledger.TakeSaidAsync(request.Id, IdsOf(request.Said), string.IsNullOrWhiteSpace(request.By) ? null : request.By, ct);
        if (outcome.Refusal == SessionSayRefusal.None)
        {
            foreach (var word in outcome.Taken.Where(word => word.Reopens && word.Persons && word.Channel is null))
            {
                await Service.Ledger.KeepOnAskAsync(
                    string.IsNullOrWhiteSpace(request.By) ? request.Id : request.By, AskWordKind.Reopened, word.Text, word.At, ct);
            }

            return new SessionActionResponse(await Shown(outcome.Session!, http), outcome.Message);
        }

        return Answer<SessionActionResponse>.Else(
            outcome.Refusal switch
            {
                SessionSayRefusal.NotFound => StatusCodes.Status404NotFound,
                SessionSayRefusal.Empty => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status409Conflict,
            },
            new SessionSayRefusalResponse(outcome.Message, SessionAnswers.SayRefusal(outcome.Refusal)));
    }

    // The dispatcher marks the words it is about to write to its harness, before the wire, so a shell that comes up again never sends one a second time.
    private async Task<Answer<AttemptResponse>> AttemptAsync(AttemptSaid request, HttpContext http, CancellationToken ct)
    {
        var outcome = await Service.Ledger.MarkAttemptAsync(request.Id, IdsOf(request.Said), DateTimeOffset.UtcNow, ct);
        return outcome.Refusal switch
        {
            SessionSayRefusal.None => new AttemptResponse(await Shown(outcome.Session!, http), outcome.Message, outcome.Unmarked),
            SessionSayRefusal.Empty => Answer<AttemptResponse>.Else(
                StatusCodes.Status400BadRequest, new SessionSayRefusalResponse(outcome.Message, SessionAnswers.SayRefusal(outcome.Refusal))),
            _ => Answer<AttemptResponse>.Else(
                StatusCodes.Status404NotFound, new SessionSayRefusalResponse(outcome.Message, SessionAnswers.SayRefusal(outcome.Refusal))),
        };
    }

    // A word that no longer waits, or one on a record that runs again, answers `done: false, why: taken`, never an error. Nothing is kept
    // on the ask, since nothing took the words.
    private async Task<Answer<TakeBackResponse>> TakeBackAsync(TakeBackSaid request, HttpContext http, CancellationToken ct)
    {
        var outcome = await Service.Ledger.TakeBackAsync(request.Id, request.Word, request.Text, DateTimeOffset.UtcNow, ct);
        if (outcome.Refusal == SessionSayRefusal.None)
        {
            return new TakeBackResponse(
                true, outcome.Message, Session: await Shown(outcome.Session!, http),
                Said: host.MachineLocal(http) && outcome.Word is { } word ? SessionAnswers.ToSaid(word) : null);
        }

        if (outcome.Refusal == SessionSayRefusal.Taken) return new TakeBackResponse(false, outcome.Message, SessionAnswers.SayRefusal(outcome.Refusal));

        return Answer<TakeBackResponse>.Else(
            outcome.Refusal switch
            {
                SessionSayRefusal.NotFound => StatusCodes.Status404NotFound,
                SessionSayRefusal.Empty => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status409Conflict,
            },
            Refused(outcome));
    }

    // One word held on a part moved to its line's head: taken off and kept there in one step under the key `move:<part>:<word>`, so a
    // retry after a lost answer is answered moved and nothing is said twice. `taken` is an answer, as the take-back's is.
    private async Task<Answer<MoveSaidResponse>> MoveAsync(MoveSaid request, HttpContext http, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var outcome = await Service.Ledger.MoveSaidAsync(request.Id, request.Word, request.To, now, ct);
        if (outcome.Refusal == SessionSayRefusal.None && outcome.Again) return new MoveSaidResponse(true, outcome.Message, To: outcome.MovedTo, Again: true);

        if (outcome.Refusal == SessionSayRefusal.None)
        {
            // A word said to an ended part answers the park it now waits on, kept on its ask at once as the say door keeps one.
            if (outcome.Answers && outcome.Word!.Channel is null && outcome.Word.Persons)
            {
                await Service.Ledger.KeepOnAskAsync(outcome.MovedTo!, AskWordKind.Answered, outcome.Word.Text, now, ct);
            }

            var local = host.MachineLocal(http);
            return new MoveSaidResponse(
                true, outcome.Message, To: outcome.MovedTo, Session: await Shown(outcome.Session!, http),
                Said: local ? SessionAnswers.ToSaid(outcome.Word!) : null);
        }

        if (outcome.Refusal == SessionSayRefusal.Taken) return new MoveSaidResponse(false, outcome.Message, SessionAnswers.SayRefusal(outcome.Refusal));

        return Answer<MoveSaidResponse>.Else(
            outcome.Refusal switch
            {
                SessionSayRefusal.NotFound => StatusCodes.Status404NotFound,
                SessionSayRefusal.Empty => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status409Conflict,
            },
            Refused(outcome, outcome.MovedTo));
    }
}
