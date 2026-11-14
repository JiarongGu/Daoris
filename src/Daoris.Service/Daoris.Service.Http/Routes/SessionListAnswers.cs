using Daoris.Contracts.Sessions;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The session list as the door answers it: each record with the ledger's entry key, whether the ledger would delete it, and, to a
/// caller on this machine, the part its line ends at. The window and the driver's look read the whole list again and again while
/// nothing in it moves, so an answer is kept with the store's mark and answered again until a write moves it.
/// </summary>
public sealed class SessionListAnswers(HostWiring host)
{
    private ComposedService Service => host.Service;

    private MarkedAnswers<(string? Repository, bool Closed, string? Workspace, bool Local), SessionResponse[]>? _kept;

    private MarkedAnswers<(string? Repository, bool Closed, string? Workspace, bool Local), SessionResponse[]> Kept =>
        LazyInitializer.EnsureInitialized(ref _kept, () => new(Service.Sessions.MarkAsync));

    public Task<SessionResponse[]> ListAsync(string? repository, bool includeClosed, string? workspace, bool local, CancellationToken ct) =>
        // Made apart from the asker's cancellation: readers at one mark share the making, and one that leaves stops none of the others.
        Kept.AnswerAsync((repository, includeClosed, workspace, local), () => MakeAsync(repository, includeClosed, workspace, local, CancellationToken.None), ct);

    /// <summary>One record as the list answers it, its facts read by the store's single reads; null for an unknown id.</summary>
    public async Task<SessionResponse?> OneAsync(string id, bool local, CancellationToken ct)
    {
        if (await Service.Sessions.FindAsync(id, ct) is not { } session) return null;
        var deletable = host.Mode == ServiceMode.Local && (await Service.Ledger.DeletableAsync([session], ct)).Contains(session.Id);
        var answered = await Service.Ledger.WithEntryAsync(session, ct);
        var head = local && session.Origin is null ? (await Lines.HeadOfLineAsync(Service.Sessions, Service.Quests, session, ct))?.Id : null;
        var answer = SessionAnswers.ToSession(answered, local) with { Deletable = deletable, Head = head };
        return local && (await SessionTurnLines.ReadAsync(Service.Sessions, [session], ct)).TryGetValue(session.Id, out var turns)
            ? answer with { Turns = SessionAnswers.ToTurns(turns) }
            : answer;
    }

    private async Task<SessionResponse[]> MakeAsync(string? repository, bool includeClosed, string? workspace, bool local, CancellationToken ct)
    {
        var sessions = await Service.Sessions.ListAsync(repository, includeClosed, workspace, ct);
        // Which records the ledger would delete, none at a shared deployment, which has no delete door.
        var deletable = host.Mode == ServiceMode.Local ? await Service.Ledger.DeletableAsync(sessions, ct) : new HashSet<string>();
        var facts = await ListedFacts.ReadAsync(Service.Sessions, Service.Quests, sessions, ct);
        var answered = await Service.Ledger.WithEntriesAsync(sessions, facts, ct);
        var views = new List<SessionResponse>(answered.Count);
        // Each session's turns across its line, read once for every line listed: answered on this machine only.
        var turns = local ? await SessionTurnLines.ReadAsync(Service.Sessions, sessions, ct) : null;
        foreach (var session in answered)
        {
            var head = local && session.Origin is null ? (await Lines.HeadOfLineAsync(facts, session, ct))?.Id : null;
            var view = SessionAnswers.ToSession(session, local) with { Deletable = deletable.Contains(session.Id), Head = head };
            views.Add(turns?.TryGetValue(session.Id, out var line) == true ? view with { Turns = SessionAnswers.ToTurns(line) } : view);
        }

        return [.. views];
    }
}
