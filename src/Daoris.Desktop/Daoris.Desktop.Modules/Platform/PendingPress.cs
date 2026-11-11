using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A working session's press asked of the person, raised as an <c>allow</c> subject <c>{ press }</c> at its session's place: the pipe's
/// one pending entry for that request, read and written under the pipe's gate alone. It rests on the request's reservation, so the
/// terminal's withdrawal, a close and the person's card all find the one press.
/// </summary>
public sealed class PendingPress
{
    internal PendingPress(
        string id, PipeReserved reserved, string session, string route, RolePlace place, DateTimeOffset raised, Func<Task> card, PipeClientProcess? client)
    {
        Id = id;
        Reserved = reserved;
        Session = session;
        Route = route;
        Place = place;
        Raised = raised;
        Card = card;
        Client = client;
    }

    public string Id { get; }

    internal PipeReserved Reserved { get; }

    public string Session { get; }

    /// <summary>The route pressed, <c>module.type</c>.</summary>
    public string Route { get; }

    public RolePlace Place { get; }

    public DateTimeOffset Raised { get; }

    /// <summary>The person's card for the press, which the request falls to.</summary>
    internal Func<Task> Card { get; }

    internal PipeClientProcess? Client { get; }

    /// <summary>Cancelled where the entry settles, so its ten minutes stop.</summary>
    internal CancellationTokenSource Clock { get; } = new();
}
