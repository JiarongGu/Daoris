using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The last whole look at this machine's sessions this shell took, and when (HELPSTART1, D89's note): the Sessions list's
/// (<c>SESSION_GROUPS</c> asked of every session) or Ask Daoris's room's. The room's sessions' judge reads it where it is recent
/// rather than read the service again (<c>help.room.look</c>).
/// </summary>
/// <remarks>
/// <para>Replaced whole, as the loop's <see cref="LastLook"/> is. A look asked of some sessions only is never kept: it judged only
/// their trees, and the room says every group.</para>
///
/// <para><b>Kept without its trees</b> (FACTSPLIT1b, D181's note). The room's trees are judged per repository, each part on its own
/// repository's <c>refs</c>, after which a tree judged before is the one fact that moved; a kept tree would be read exactly when it
/// is stale. What is kept is the part no ref moves: the records, the quests, the verdicts and the marks.</para>
/// </remarks>
public sealed class SessionsLooked
{
    private static readonly IReadOnlyDictionary<string, TreeWork> NoTrees = new Dictionary<string, TreeWork>(StringComparer.OrdinalIgnoreCase);

    private volatile Looked? _looked;

    private sealed record Looked(SessionLook Look, DateTimeOffset At);

    /// <summary>Keep a whole look, taken at <paramref name="at"/>, without its trees' judgement.</summary>
    public void Keep(SessionLook look, DateTimeOffset at) => _looked = new Looked(look with { Trees = NoTrees }, at);

    /// <summary>The look kept, where it was taken no longer than <paramref name="span"/> before <paramref name="now"/>; null otherwise.</summary>
    public SessionLook? Within(TimeSpan span, DateTimeOffset now) =>
        _looked is { } looked && looked.At <= now && now - looked.At <= span ? looked.Look : null;
}
