using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The bind at the take: a quest whose graph holds <c>quest.close</c> is bound to that graph when its
/// session opens, before the session can say <c>done</c>, and only where every machine of its circle and the host run a build that knows a
/// bound quest.
/// </summary>
/// <remarks>
/// The local host is asked (<c>/api/status</c>'s <c>closure</c>). A circle synced through a remote is bound
/// only by a host that judges the remote's handshake (<c>closeRemote</c>), whose bind refuses it, naming the machines,
/// until every machine of the workspace declared it knows a bound quest; a host before it is asked nothing there, and its quests close by
/// their done.
/// </remarks>
public static class ClosureBinding
{
    /// <summary>
    /// What a session's start owes its quest's closure graph: a quest bound already says `ready` (a resume, a carry-on),
    /// asking nothing. Null where it is bound to none: no choice holds a closure graph to bind at a take, so it closes by its done.
    /// </summary>
    public static Task<ClosureBound?> AtStartAsync(
        IClosureBindPort port, DriverConfig config, QuestView quest, string workspace, bool remote, string? home, CancellationToken ct,
        string? session = null, WorkflowRunBinding? run = null) =>
        Task.FromResult<ClosureBound?>(quest.Closure is { Length: > 0 } bound
            ? new(true, $"#{quest.Id} closes through its graph `{bound}`: its session says `ready`.")
            : null);
}
