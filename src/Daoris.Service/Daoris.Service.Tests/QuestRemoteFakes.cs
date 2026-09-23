using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A remote that is a real store behind the real wire: every fetch and push is serialized through
/// <see cref="QuestWire"/> both ways, as the HTTP doors do, so a sync test proves the shape as well as
/// the logic. It judges a publish fit, and files it in the default circle.
/// </summary>
internal sealed class StoreRemote(QuestStore remote) : IQuestRemote
{
    public int Calls { get; private set; }

    public async Task<QuestFetch> FetchAsync(long since, CancellationToken ct = default)
    {
        Calls++;
        return QuestWire.ReadPage(QuestWire.Page(await remote.OperationsSinceAsync(since, ct: ct)))!;
    }

    public async Task<QuestPush> PushAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default)
    {
        Calls++;
        var (sent, pushed) = QuestWire.ReadPush(QuestWire.Push(@base, operations))!.Value;
        return QuestWire.ReadPushed(QuestWire.Pushed(
            await remote.ReceiveAsync(sent, pushed, _ => null, _ => Workspaces.Default, ct)))!;
    }
}

/// <summary>A remote nobody can reach — what a take meets offline.</summary>
internal sealed class UnreachableRemote : IQuestRemote
{
    public int Calls { get; private set; }

    public Task<QuestFetch> FetchAsync(long since, CancellationToken ct = default)
    {
        Calls++;
        throw new QuestRemoteException("the remote could not be reached (connection refused)");
    }

    public Task<QuestPush> PushAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default)
    {
        Calls++;
        throw new QuestRemoteException("the remote could not be reached (connection refused)");
    }
}

/// <summary>A machine whose every circle is wired to the one remote given — or to none.</summary>
internal sealed class OneRemote(IQuestRemote? remote) : IQuestRemotes
{
    public IQuestRemote? For(string workspace) => remote;
}
