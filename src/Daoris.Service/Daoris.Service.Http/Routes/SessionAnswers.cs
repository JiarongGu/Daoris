using Daoris.Contracts.Sessions;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// A session's record and what hangs on it as every door answers it, and the wire's records read into the domain's. What names this
/// machine's tree, account or words answers a caller on this machine only, as the transcript does: which account a session
/// ran as is this machine's wiring, and the tree is a filesystem path outright.
/// </summary>
public static class SessionAnswers
{
    /// <summary>A record answered with the entry's key the one rule gives it: the stored one, else derived at read and never written back.</summary>
    public static async Task<SessionResponse> ToSessionAsync(ComposedService service, Session session, bool loopback) =>
        ToSession(await service.Ledger.WithEntryAsync(session), loopback);

    public static SessionResponse ToSession(Session s, bool loopback) => new(
        s.Id, s.Quest, s.Repository, s.Door, s.StateName,
        // The note is free text the driver wrote, and it named the tree and the account.
        loopback ? s.Note : SessionNote.ForAnotherMachine(s.Note, s), s.Evidence,
        Transcript: loopback ? s.Transcript : null,
        s.Created, s.Updated, s.Workspace, s.Kind.ToString().ToLowerInvariant(),
        s.DoorVersion,
        Profile: loopback ? s.Profile : null,
        Tree: loopback ? s.Tree : null,
        BaseCommit: loopback ? s.BaseCommit : null,
        Ask: s.Ask,
        Took: s.Took,
        Interrupted: s.Interrupted,
        // A limit names no account, so it is answered to every caller, as the state beside it is.
        Limit: s.Limit,
        Said: loopback ? s.Said.Select(ToSaid).ToList() : null,
        NoteParts: NoteParts.Element(loopback ? s.NoteParts : NoteParts.ForAnotherMachine(s.NoteParts, s)),
        Opinion: loopback ? s.Opinion : null,
        Serves: loopback ? s.Serves : null,
        From: loopback ? s.From : null,
        FromWhy: loopback ? s.FromWhy : null,
        FromBy: loopback ? s.FromBy : null,
        // A key, no path and no account, so every caller is told. Filled by the ledger's reader.
        Entry: s.Entry,
        HelpMode: loopback ? s.HelpMode : null,
        Access: loopback ? s.Access : null,
        Resolved: loopback ? s.Resolved : null,
        Piece: loopback ? s.Piece : null,
        TookQuest: loopback ? s.TookQuest : null,
        Actor: loopback ? s.Actor : null, Choice: loopback ? s.Choice : null,
        Asked: loopback ? s.Asked : null, Override: loopback ? s.Override : null, Ran: loopback ? s.Ran : null, Chosen: loopback ? s.Chosen : null,
        Work: loopback ? ParticipantWork.Of(s) : null, Decides: loopback ? s.Decides : null,
        HoldsPlace: loopback ? s.HoldsPlace : null);

    public static SaidWordResponse ToSaid(SaidWord word) =>
        new(word.Id, word.Text, word.At, word.Files, word.Reopens, word.By, word.Helper, word.From, word.Key, word.Level, word.Attempt,
            word.Decision, word.Question, word.Choice);

    /// <summary>The request's <c>moves</c> as the ledger judges them: a member missing is passed on missing, so the ledger refuses it with its sentence.</summary>
    public static OpenMoves? MovesOf(OpenMovesRequest? moves) => moves is null ? null : new(moves.From!, moves.Words!, moves.Read!);

    /// <summary>The lines by session, each line with an end; null for none.</summary>
    public static IReadOnlyDictionary<string, MemoryTurns>? TurnsOf(Dictionary<string, MemoryTurnsRequest>? turns) =>
        turns?.ToDictionary(
            pair => pair.Key,
            pair => new MemoryTurns(
                [.. (pair.Value.Turns ?? []).Where(line => line.Ended is not null)
                    .Select(line => new MemoryTurn(line.Seq, line.Started, line.Ended!.Value, line.Model ?? ""))],
                pair.Value.Asked ?? []),
            StringComparer.Ordinal);

    /// <summary>A delete's judgement: the sentence as <c>error</c>, beside its word and the facts the word names, kebab-case like a session's state.</summary>
    public static SessionDeletionResponse ToDeletion(SessionDeleteOutcome outcome) => outcome.Refusal == SessionDeleteRefusal.None
        ? new SessionDeletionResponse(Deletable: true)
        : new SessionDeletionResponse(
            Deletable: false, Error: outcome.Message,
            Refusal: outcome.Refusal switch
            {
                SessionDeleteRefusal.NotFound => "not-found",
                SessionDeleteRefusal.NotOurs => "not-ours",
                SessionDeleteRefusal.Live => "live",
                SessionDeleteRefusal.ServedQuest => "served-quest",
                SessionDeleteRefusal.Named => "named",
                _ => "on-remote",
            },
            outcome.Quest, outcome.Ask, outcome.Origin, outcome.Workspace);

    /// <summary>The finished tree's post, where every part the record is keyed or read by is here; null otherwise.</summary>
    public static WorkRecordPost? PostOf(PostWorkRecord r) =>
        Blank(r.Workspace) || Blank(r.Repository) || Blank(r.Folder) || Blank(r.Tree) || r.Moved is not { } moved || Blank(r.Branch)
        || r.Hold is null || Blank(r.Hold.Kind) || Blank(r.Tip) || r.Left is null || r.Left.Names is null
            ? null
            : new(
                r.Workspace!, r.Repository!, r.Folder!, r.Tree!, moved, r.Unit, r.Branch!, r.Line,
                new WorkRecordHold(r.Hold.Kind, r.Hold.Where, r.Hold.Form), r.Tip!, r.GrewFrom, r.Ref,
                r.Commits?.Select(commit => new WorkRecordCommit(
                    commit.Id, commit.Subject, [.. (commit.Files ?? []).Select(file => new WorkRecordFile(file.Path, file.Added, file.Removed))])).ToList(),
                r.CommitsUnread, new WorkRecordLeft(r.Left.Names, r.Left.Count, r.Left.Cut), r.Whole)
            {
                Landing = r.Landing is { Branch.Length: > 0 } landing
                    ? new WorkRecordLanding(landing.Branch, landing.Line, landing.At, landing.PullRequest)
                    : null,
                LandingWhy = r.LandingWhy,
            };

    public static WorkRecordSessionResponse ToRecordSession(WorkRecordSession session) => new(session.Session, session.Commits);

    public static WorkRecordShownResponse ToShown(WorkRecord record)
    {
        var post = record.Post;
        return new(
            new WorkRecordPostResponse(
                post.Workspace, post.Repository, post.Folder, post.Tree, post.Moved, post.Unit, post.Branch, post.Line,
                new WorkRecordHoldWire(post.Hold.Kind, post.Hold.Where, post.Hold.Form), post.Tip, post.GrewFrom, post.Ref,
                post.Commits?.Select(commit => new WorkRecordCommitWire(
                    commit.Id, commit.Subject, [.. commit.Files.Select(file => new WorkRecordFileWire(file.Path, file.Added, file.Removed))])).ToList(),
                post.CommitsUnread, new WorkRecordLeftWire(post.Left.Names, post.Left.Count, post.Left.Cut), post.Whole,
                post.Landing is { } landing ? new WorkRecordLandingWire(landing.Branch, landing.Line, landing.At, landing.PullRequest) : null,
                post.LandingWhy),
            record.At, record.Schema, [.. record.Sessions.Select(ToRecordSession)]);
    }

    public static MemoryOpResponse ToMemoryOp(MemoryOp op)
    {
        var change = op.Change;
        return new(
            op.Id, op.Repository, op.State,
            new MemoryChangeResponse(
                change.Kind, change.Text, change.Target, change.Revision, change.Source, change.Relation, change.Recheck,
                change.Predecessors?.Select(each => new MemoryPredecessorResponse(each.Entry, each.Revision)).ToList()),
            op.By is { } by ? new MemoryAuthorResponse(by.Session, by.Workspace, by.Quest, by.Door, by.DoorVersion, by.Role, by.RoleRevision, by.At) : null,
            op.At, op.Models, op.Approver, op.Holds, op.Entry, op.Applied, op.Settled, op.Carried, op.Answer, op.Told, op.Began);
    }

    /// <summary>
    /// A say or a take refused, its word as the wire spells it, kebab-case like a session's state. Every refusal the ledger knows has its
    /// own word, held cell for cell by <c>SaidSettledHostTests</c>, so the default is reached by no known value and a new refusal is never
    /// sent as a running session's.
    /// </summary>
    public static string SayRefusal(SessionSayRefusal refusal) => refusal switch
    {
        // Never sent: a say that kept its words is answered with them, never with a refusal.
        SessionSayRefusal.None => "none",
        SessionSayRefusal.Empty => "no-words",
        SessionSayRefusal.NotFound => "not-found",
        SessionSayRefusal.NotOurs => "not-ours",
        SessionSayRefusal.Intake => "intake",
        SessionSayRefusal.StoodDown => "stood-down",
        SessionSayRefusal.Running => "running",
        SessionSayRefusal.Opinion => "opinion",
        SessionSayRefusal.Helper => "helper",
        SessionSayRefusal.Taken => "taken",
        SessionSayRefusal.From => "from",
        SessionSayRefusal.Level => "level",
        SessionSayRefusal.SupervisorText => "supervisor-text",
        SessionSayRefusal.KeyMismatch => "key-mismatch",
        SessionSayRefusal.DecisionTaken => "decision-taken",
        SessionSayRefusal.NotParked => "not-parked",
        SessionSayRefusal.NoQuest => "no-quest",
        SessionSayRefusal.QuestionChanged => "question-changed",
        SessionSayRefusal.NotOfferedChoice => "not-offered-choice",
        SessionSayRefusal.OwnerQuestion => "owner-question",
        SessionSayRefusal.GoAheadOpen => "go-ahead-open",
        SessionSayRefusal.GoAheadUnread => "go-ahead-unread",
        SessionSayRefusal.OwnerAnswered => "owner-answered",
        SessionSayRefusal.NoHome => "no-home",
        // The line went on past the record, its head beside the word as `head`; and a line that loops or runs past the walk's bound.
        SessionSayRefusal.Continued => "continued",
        SessionSayRefusal.Unplaced => "unplaced",
        SessionSayRefusal.NotHeld => "not-held",
        SessionSayRefusal.Files => "files",
        SessionSayRefusal.AtHead => "at-head",
        SessionSayRefusal.NotYours => "not-yours",
        // Reached by no value the ledger declares, which the table's test holds; the word it always sent is kept for a value cast from outside.
        _ => "running",
    };

    private static bool Blank(string? text) => string.IsNullOrWhiteSpace(text);
}
