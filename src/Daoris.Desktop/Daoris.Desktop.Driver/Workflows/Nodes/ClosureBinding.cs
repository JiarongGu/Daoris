using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The bind at the take (FABRIC-CLOSE1b; §E5's cutover): a quest whose graph holds <c>quest.close</c> is bound to that graph when its
/// session opens, before the session can say <c>done</c>, and only where every machine of its circle and the host run a build that knows a
/// bound quest (FABRIC-CLOSE1e's compatibility).
/// </summary>
/// <remarks>
/// D173's CLOSE1b note, choice 4: the local host is asked (<c>/api/status</c>'s <c>closure</c>). A circle synced through a remote is bound
/// only by a host that judges the remote's handshake (<c>closeRemote</c>, FABRIC-CLOSE1i), whose bind refuses it, naming the machines,
/// until every machine of the workspace declared it knows a bound quest; a host before it is asked nothing there, and its quests close by
/// their done as today.
/// </remarks>
public static class ClosureBinding
{
    /// <summary>
    /// What a session's start owes its quest's closure graph (FABRIC-CLOSE1l): a quest bound already says `ready` (a resume, a carry-on),
    /// asking nothing. Null where it is bound to none: no choice holds a closure graph to bind at a take, so it closes by its done.
    /// </summary>
    public static Task<ClosureBound?> AtStartAsync(
        IClosureBindPort port, DriverConfig config, QuestView quest, string workspace, bool remote, string? home, CancellationToken ct,
        string? session = null, WorkflowRunBinding? run = null) =>
        Task.FromResult<ClosureBound?>(quest.Closure is { Length: > 0 } bound
            ? new(true, $"#{quest.Id} closes through its graph `{bound}`: its session says `ready`.")
            : null);

    /// <summary>
    /// What the loop's line says at a session's end whose start named its quest's graph for the take, where that take left the quest
    /// unbound (FABRIC-CLOSE1o: a bind expired, a remote since, another session's take): said once, at that end, since a later start reads
    /// the quest taken and unbound and names nothing. Null everywhere else.
    /// </summary>
    /// <param name="why">
    /// What the take's record says it made of the graph (CLOSETAKE1, FABRIC-CLOSE1o-ii: <c>expired</c>, <c>replaced</c> or <c>remote</c>); the
    /// line names it. None where the record says nothing (another session's take, a host from before the field): the line leaves it to the answer.
    /// </param>
    public static string? UnboundAtEnd(ClosureBound? named, QuestView? after, string? why = null) =>
        named is { Bound: true } && after is { Status: "Taken" or "Done", Closure: null or "" }
            ? $" #{after.Id}'s closure graph was named for its take, and its take did not bind it ("
              + (why switch
              {
                  "expired" => "its naming expired before the take came",
                  "replaced" => "the quest was replaced since it was named",
                  "remote" => "its workspace syncs through a remote, and not every machine sharing it has declared it knows a bound quest",
                  _ => "its answer said why",
              }) + "), so it closes by its done."
            : null;
}
