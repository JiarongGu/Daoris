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

/// <summary>
/// A remote built before PAUSE1c: a real store behind the real wire, whose reader has no field for a decline's
/// <c>whileOpen</c>. The flag is taken out of every operation it reads and every one it serves, so it keeps, judges
/// and hands back a plain decline, as an older build does. Quests only: nothing else of an older build differs here.
/// </summary>
internal sealed class OlderRemote(QuestStore remote) : IRemote
{
    public async Task<QuestFetch> FetchQuestsAsync(long since, CancellationToken ct = default) =>
        QuestWire.ReadPage(WithoutWhileOpen(QuestWire.Page(await remote.OperationsSinceAsync(since, ct: ct))))!;

    public async Task<QuestPush> PushQuestsAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default)
    {
        var (sent, pushed) = QuestWire.ReadPush(WithoutWhileOpen(QuestWire.Push(@base, operations)))!.Value;
        return QuestWire.ReadPushed(QuestWire.Pushed(
            await remote.ReceiveAsync(sent, pushed, _ => null, _ => Workspaces.Default, ct)))!;
    }

    /// <summary>What an older reader makes of the JSON: every operation as it was, less the field it never knew.</summary>
    private static string WithoutWhileOpen(string json)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        foreach (var operation in root["operations"]!.AsArray()) operation!.AsObject().Remove("whileOpen");
        return root.ToJsonString();
    }

    public Task PushSessionsAsync(IReadOnlyList<FedSessionRecord> records, CancellationToken ct = default) => throw QuestsOnly();

    public Task<SessionFetch> FetchSessionsAsync(long since, CancellationToken ct = default) => throw QuestsOnly();

    public Task<string?> HeldCodeMapAsync(string repository, CancellationToken ct = default) => throw QuestsOnly();

    public Task<FedCodeMap?> FetchCodeMapAsync(string repository, CancellationToken ct = default) => throw QuestsOnly();

    private static NotSupportedException QuestsOnly() => new("an older remote here speaks quests only");
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
