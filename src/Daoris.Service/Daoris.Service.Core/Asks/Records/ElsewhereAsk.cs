namespace Daoris.Knowledge;

/// <summary>An ask the mirrored messages name and this machine does not hold (SESSONE2h): all this machine knows of it.</summary>
/// <param name="Id">The ask's id.</param>
/// <param name="Workspace">Its circle, from its messages.</param>
/// <param name="Origins">The machines its messages came from, ordered; empty where only this machine's own are kept.</param>
/// <param name="Latest">The moment of its newest message.</param>
/// <param name="Messages">How many of its messages are kept.</param>
/// <param name="Unread">How many an agent or a teammate wrote after the person's local mark (NOTIFY2g), never the person's own.</param>
public sealed record ElsewhereAsk(string Id, string Workspace, IReadOnlyList<string> Origins, DateTimeOffset Latest, int Messages, int Unread);
