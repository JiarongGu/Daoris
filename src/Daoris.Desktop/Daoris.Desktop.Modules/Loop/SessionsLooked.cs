using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The last whole look at this machine's sessions this shell took, and when (HELPSTART1, D89's note): the Sessions list's
/// (<c>SESSION_GROUPS</c> asked of every session) or Ask Daoris's room's. The room reads it where it is recent rather than look
/// again: a look judges each tree that could be to review with git, and on a loaded machine one took 24.9 s against the room's
/// half-second bound (<c>help.room.look</c>).
/// </summary>
/// <remarks>
/// Replaced whole, as the loop's <see cref="LastLook"/> is. A look asked of some sessions only is never kept: it judged only
/// their trees, and the room says every group.
/// </remarks>
public sealed class SessionsLooked
{
    private volatile Looked? _looked;

    private sealed record Looked(SessionLook Look, DateTimeOffset At);

    /// <summary>Keep a whole look, taken at <paramref name="at"/>.</summary>
    public void Keep(SessionLook look, DateTimeOffset at) => _looked = new Looked(look, at);

    /// <summary>The look kept, where it was taken no longer than <paramref name="span"/> before <paramref name="now"/>; null otherwise.</summary>
    public SessionLook? Within(TimeSpan span, DateTimeOffset now) =>
        _looked is { } looked && looked.At <= now && now - looked.At <= span ? looked.Look : null;
}
