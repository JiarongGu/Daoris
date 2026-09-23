using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A remote that is a real store behind the real wire: every fetch and push is serialized through
/// <see cref="QuestWire"/> and <see cref="SessionWire"/> both ways, as the HTTP doors do, so a sync test
/// proves the shape as well as the logic. Quests are judged fit and filed in the default circle;
/// session records go through the real <see cref="SessionFeed"/>, keyed by <paramref name="caller"/> —
/// the key this machine's feed would carry.
/// </summary>
internal sealed class StoreRemote(
    QuestStore remote, SessionStore? sessions = null, KnowledgeService? registry = null, string caller = "person@machine")
    : IRemote
{
    public int Calls { get; private set; }

    /// <summary>The last session feed as it crossed the wire — what a disclosure test reads.</summary>
    public string? LastFeed { get; private set; }

    public async Task<QuestFetch> FetchQuestsAsync(long since, CancellationToken ct = default)
    {
        Calls++;
        return QuestWire.ReadPage(QuestWire.Page(await remote.OperationsSinceAsync(since, ct: ct)))!;
    }

    public async Task<QuestPush> PushQuestsAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default)
    {
        Calls++;
        var (sent, pushed) = QuestWire.ReadPush(QuestWire.Push(@base, operations))!.Value;
        return QuestWire.ReadPushed(QuestWire.Pushed(
            await remote.ReceiveAsync(sent, pushed, _ => null, _ => Workspaces.Default, ct)))!;
    }

    public async Task PushSessionsAsync(IReadOnlyList<FedSessionRecord> records, CancellationToken ct = default)
    {
        Calls++;
        LastFeed = SessionWire.Feed(records);
        var outcome = await new SessionFeed(registry!, sessions!).FeedAsync(caller, SessionWire.ReadFeed(LastFeed)!, ct);
        if (outcome.Refusal != SessionFeedRefusal.None) throw new RemoteException(outcome.Message);
    }

    public async Task<SessionFetch> FetchSessionsAsync(long since, CancellationToken ct = default)
    {
        Calls++;
        return SessionWire.ReadPage(SessionWire.Page(await sessions!.TeamSinceAsync(since, caller, ct: ct)))!;
    }

    // The team's code maps (MAP3e) are asked of the remote's registry service, when one is given.
    public async Task<string?> HeldCodeMapAsync(string repository, CancellationToken ct = default)
    {
        Calls++;
        return registry is null ? null : (await registry.HeldAsync(repository, ct)).CodeMap;
    }

    public async Task<FedCodeMap?> FetchCodeMapAsync(string repository, CancellationToken ct = default)
    {
        Calls++;
        return registry is not null && await registry.CodeMapAsync(repository, ct) is { } read
            ? CodeMapWire.Read(CodeMapWire.Answer(repository, read))
            : null;
    }
}

/// <summary>A remote nobody can reach — what a take meets offline.</summary>
internal sealed class UnreachableRemote : IRemote
{
    public int Calls { get; private set; }

    private Exception Wall()
    {
        Calls++;
        return new RemoteException("the remote could not be reached (connection refused)");
    }

    public Task<QuestFetch> FetchQuestsAsync(long since, CancellationToken ct = default) => throw Wall();

    public Task<QuestPush> PushQuestsAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default) =>
        throw Wall();

    public Task PushSessionsAsync(IReadOnlyList<FedSessionRecord> records, CancellationToken ct = default) => throw Wall();

    public Task<SessionFetch> FetchSessionsAsync(long since, CancellationToken ct = default) => throw Wall();

    public Task<string?> HeldCodeMapAsync(string repository, CancellationToken ct = default) => throw Wall();

    public Task<FedCodeMap?> FetchCodeMapAsync(string repository, CancellationToken ct = default) => throw Wall();
}

/// <summary>A machine whose every circle is wired to the one remote given — or to none.</summary>
internal sealed class OneRemote(IRemote? remote) : IRemotes
{
    public IRemote? For(string workspace) => remote;
}
