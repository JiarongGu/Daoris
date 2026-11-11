using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A steward's act outside its grants, raised as an <c>allow</c> subject <c>{ ask }</c> at its target's place: the pipe's one pending entry
/// for that request, read and written under the pipe's gate alone. It rests on the request's reservation, so the terminal's
/// withdrawal, a close and the person's card all find the one press.
/// </summary>
public sealed class PendingAllow
{
    internal PendingAllow(
        string id, PipeReserved reserved, IpcRequest original, RoleJudgement asked, RolePlace place, DateTimeOffset raised, Func<Task> card,
        PipeClientProcess? client)
    {
        Id = id;
        Reserved = reserved;
        Original = original;
        Asked = asked;
        Place = place;
        Raised = raised;
        Card = card;
        Client = client;
    }

    public string Id { get; }

    /// <summary>The reservation the person's card would claim; this entry is live only while the pipe still holds it.</summary>
    internal PipeReserved Reserved { get; }

    /// <summary>The steward's <c>ROLE_ACT</c> as sent, which an allow admits and runs.</summary>
    internal IpcRequest Original { get; }

    /// <summary>What the judge answered: the role, the hold, the holder, the act.</summary>
    public RoleJudgement Asked { get; }

    public RolePlace Place { get; }

    public DateTimeOffset Raised { get; }

    /// <summary>The person's card for the press, which the request falls to.</summary>
    internal Func<Task> Card { get; }

    internal PipeClientProcess? Client { get; }

    /// <summary>Cancelled where the entry settles, so its ten minutes stop.</summary>
    internal CancellationTokenSource Clock { get; } = new();
}
