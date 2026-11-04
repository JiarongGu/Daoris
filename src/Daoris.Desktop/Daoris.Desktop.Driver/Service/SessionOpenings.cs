using Daoris.Contracts;
using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>
/// The doors that open a session record (the service's session routes): a quest's driven session, a chat, an intake and Ask Daoris's
/// conversation, each also through the keyed door that moves words in its own step. A class over the client, reached as its
/// <see cref="ServiceClient.Openings"/> and by the client's own doors of the same names; what opened is told to the client's
/// <see cref="ServiceClient.Opened"/> watchers as the record names it. A refusal is an answer, not an exception.
/// </summary>
public sealed class SessionOpenings
{
    private readonly ServiceClient _service;

    internal SessionOpenings(ServiceClient service) => _service = service;

    /// <summary>Ask the ledger to queue a session. A refusal is an answer, not an exception.</summary>
    /// <param name="setup">
    /// That the quest is a set-up (<see cref="SetupQuests"/>), for the watchers alone (WSSETUP11): the ledger is not told, and the machine log
    /// marks the session's start.
    /// </param>
    /// <param name="from">The session this one carries on, why and who asked (MIXAGENT1a-iii), for its record; null for none.</param>
    /// <param name="access">
    /// The access setup this start resolved (AUTONOMY1a, D184 point 1), each pause with its value, for its record, which keeps it on this
    /// machine alone; null for none.
    /// </param>
    public async Task<(string? SessionId, string Message)> SessionAsync(
        string questId, string adapter, string? harnessVersion = null, string? profile = null,
        string? tree = null, string? baseCommit = null, CancellationToken ct = default, bool setup = false,
        ContinuedFrom? from = null, IReadOnlyDictionary<string, string>? access = null) =>
        await OpenRecordAsync(
                DrivenOpen(questId, adapter, harnessVersion, profile, tree, baseCommit, from, access), opened => (opened.Session, opened.Message),
                SessionOpened.Driven, adapter, repository: null, ct, setup)
            .ConfigureAwait(false);

    /// <summary>
    /// <see cref="SessionAsync"/> through the keyed door (FOUNDATION1d-ii part 5, <see cref="SessionOpenDoors.OpenAsync"/>), asked to move
    /// <paramref name="moves"/>' words onto the driven record it opens, in the open's own step: sent only to a host whose <c>opens</c> mark is 2,
    /// under a key of its own, so a host below it opens as before and moves nothing. A lost answer is held, naming the records that continue the
    /// one left, and opens nothing here.
    /// </summary>
    /// <param name="moves">
    /// The words to move; null for an open that moves none, which still goes under its key (FOUNDATION1f-iv part a) and then names the record it
    /// continues by <paramref name="from"/>.
    /// </param>
    /// <param name="opens">The key the open is sent under: the move record's, written before the open; null draws one of the open's own.</param>
    public async Task<DestinationOpen> SessionMovingAsync(
        string questId, string adapter, string? harnessVersion, string? profile, string? tree, string? baseCommit, ContinuedFrom? from,
        MoveWords? moves, CancellationToken ct = default, bool setup = false, IReadOnlyDictionary<string, string>? access = null,
        string? opens = null)
    {
        var left = moves?.From ?? from?.Session ?? throw new ArgumentException("A keyed open names the record it continues.", nameof(from));
        var opened = await _service.Opens
            .OpenAsync(
                DrivenOpen(questId, adapter, harnessVersion, profile, tree, baseCommit, from, access),
                opens ?? moves?.Opens ?? MoveWords.DrivenKey(), left, ct: ct, moves: moves)
            .ConfigureAwait(false);
        if (opened.SessionId is { } id)
        {
            _service.RecordOpened(new SessionOpened(id, SessionOpened.Driven, opened.Recorded.Adapter ?? adapter, opened.Recorded.Repository)
            {
                Workspace = opened.Recorded.Workspace,
                Setup = setup,
            });
        }

        return opened;
    }

    /// <summary>
    /// A driven record's open, as both of its doors send it. What the harness is, the account, the working tree and where it stood are observed
    /// on this side before the spawn: the service has no binaries or checkout to look at. The setup it starts under keeps the pauses' order,
    /// resolved here, which holds the driver's file; <paramref name="from"/> is the session it continues, why and who asked, known on this side alone.
    /// </summary>
    private static OpenSession DrivenOpen(
        string questId, string adapter, string? harnessVersion, string? profile, string? tree, string? baseCommit, ContinuedFrom? from,
        IReadOnlyDictionary<string, string>? access) =>
        new(questId, adapter, harnessVersion, profile, tree, baseCommit, from?.Session, from?.Why, from?.By,
            access is { Count: > 0 } ? new Dictionary<string, string>(access) : null, "build");

    /// <summary>
    /// Ask the ledger to open a CHAT in a repository (D49 §3) — a person's session, serving no quest yet. A refusal is an answer, not an
    /// exception: the usual one is that the repository is busy, and the sentence names what holds it.
    /// </summary>
    /// <param name="from">The conversation this one continues, handed over (MIXAGENT1a-iii), for its record; null for none.</param>
    public async Task<(string? SessionId, string Message)> ChatAsync(
        string repository, string adapter, string? harnessVersion = null, string? profile = null,
        string? tree = null, string? baseCommit = null, CancellationToken ct = default, ContinuedFrom? from = null) =>
        await OpenRecordAsync(
                ChatOpen(repository, adapter, harnessVersion, profile, tree, baseCommit, from), opened => (opened.Session, opened.Message),
                SessionOpened.Chat, adapter, repository, ct)
            .ConfigureAwait(false);

    /// <summary>
    /// <see cref="ChatAsync"/> through the keyed door (FOUNDATION1d-ii part 4, <see cref="SessionOpenDoors.OpenAsync"/>), asked to move
    /// <paramref name="moves"/>' words onto the record it opens in the open's own step: sent only to a host whose <c>opens</c> mark is 2, under a
    /// key of its own, so a host below it opens as before and moves nothing. A lost answer is held, naming the records that continue the one
    /// left, and opens nothing here.
    /// </summary>
    /// <param name="workspace">The circle the repository is wired into on this machine, for the watchers: the keyed answer names no record.</param>
    public async Task<DestinationOpen> ChatMovingAsync(
        string repository, string adapter, string? harnessVersion, string? profile, string? tree, string? baseCommit, ContinuedFrom? from,
        MoveWords moves, string? workspace, CancellationToken ct = default)
    {
        var opened = await _service.Opens
            .OpenAsync(
                ChatOpen(repository, adapter, harnessVersion, profile, tree, baseCommit, from),
                moves.Opens ?? MoveWords.ChatKey(), moves.From, ct: ct, moves: moves)
            .ConfigureAwait(false);
        if (opened.SessionId is { } id) _service.RecordOpened(new SessionOpened(id, SessionOpened.Chat, adapter, repository) { Workspace = workspace });
        return opened;
    }

    /// <summary>A chat's open, as both of its doors send it.</summary>
    private static OpenChat ChatOpen(
        string repository, string adapter, string? harnessVersion, string? profile, string? tree, string? baseCommit, ContinuedFrom? from) =>
        new(repository, adapter, harnessVersion, profile, tree, baseCommit, from?.Session, from?.Why, from?.By, "chat");

    /// <summary>
    /// Ask the ledger to open an INTAKE for an ask (D65 §1b), in the room this side is about to run it in. A refusal is an answer, not an
    /// exception — usually that the ask is already answered.
    /// </summary>
    public async Task<(string? SessionId, string Message)> IntakeAsync(
        string ask, string adapter, string room, string? harnessVersion = null, string? profile = null,
        CancellationToken ct = default) =>
        await OpenRecordAsync(
                new OpenIntake(ask, adapter, room, harnessVersion, profile, "intake"), opened => (opened.Session, opened.Message),
                SessionOpened.Intake, adapter, repository: null, ct)
            .ConfigureAwait(false);

    /// <summary>
    /// Ask the ledger to open Ask Daoris's conversation (HELP1a, D89), in the room this side is about to run it in. A refusal is an answer, not
    /// an exception — usually that one is already running in its workspace.
    /// </summary>
    /// <param name="workspace">
    /// The workspace the person's window was on at the press, which the record keeps (ASKSCOPE1a); null sends none, which the ledger records as
    /// <c>default</c>, as it did before.
    /// </param>
    /// <param name="mode">
    /// The access mode its start gets (HELPMODE1a2, D183 point 3), the effective one, which the record keeps; null sends none, which the ledger
    /// records as restricted.
    /// </param>
    public async Task<(string? SessionId, string Message)> HelpAsync(
        string adapter, string room, string? harnessVersion = null, string? profile = null, string? workspace = null,
        string? mode = null, CancellationToken ct = default) =>
        await OpenRecordAsync(
                new OpenHelp(adapter, room, harnessVersion, profile, workspace, mode, "ask-daoris"), opened => (opened.Session, opened.Message),
                SessionOpened.Help, adapter, repository: null, ct)
            .ConfigureAwait(false);

    /// <summary>
    /// Open a session record — a quest's, a chat's or an intake's, which the ledger answers alike. A refusal is an answer, not an exception,
    /// and so is a door that did not answer in JSON.
    /// </summary>
    /// <param name="route">The door's request.</param>
    /// <param name="read">The record and the sentence its answer carries.</param>
    /// <param name="kind">Which door this is, for the watchers (<see cref="SessionOpened"/>).</param>
    /// <param name="adapter">The adapter asked for — the record's own, when the answer carries one, wins.</param>
    /// <param name="repository">The repository asked for, where the door names one — the record's own wins.</param>
    /// <param name="setup">That a driven session's quest is a set-up, for the watchers (WSSETUP11).</param>
    private async Task<(string? SessionId, string Message)> OpenRecordAsync<TAnswer>(
        IRoute<TAnswer> route, Func<TAnswer, (SessionResponse? Session, string? Message)> read, string kind, string adapter,
        string? repository, CancellationToken ct, bool setup = false) where TAnswer : class
    {
        var sent = await _service.SendAsync(route, ct).ConfigureAwait(false);
        if (sent.Answer is not { } answer)
        {
            return (null, QuestViews.Error(sent) ?? $"the service at {_service.BaseUrl} has no `{RouteDeclaration.Of(route.GetType()).Path}` door ({sent.Reply.Status}) — is it older than this driver?");
        }

        var (session, message) = read(answer);
        if (session?.Id is { } id)
        {
            // What the ledger recorded rather than what was asked: a driven session names its quest, and the record answers with the quest's
            // receiver; a blank adapter takes the ledger's default. The workspace is the record's too, the circle its repository is wired into
            // on this machine (D48).
            _service.RecordOpened(new SessionOpened(id, kind, session.Adapter ?? adapter, session.Repository ?? repository)
            {
                Workspace = session.Workspace,
                Setup = setup,
            });
        }

        return (session?.Id, message ?? "");
    }
}
