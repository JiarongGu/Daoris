using System.ComponentModel;
using ModelContextProtocol.Server;
using static Daoris.Knowledge.Mcp.WorkspaceScope;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// One quest's handback, read whole (HANDBACK1): the note its session closed it with through <c>quest_respond</c>, by the quest's id
/// or the id of the session that served it, in any state, from this session's workspace as <c>quest_list</c> scopes it. The steward
/// that archives a closed row reads its handback here rather than in the whole list, and another session of the workspace reads what
/// a sibling did. Read-only; the words are <see cref="HandbackReader"/>'s, which every door answers.
/// </summary>
/// <param name="quests">Where the quest and its history are read.</param>
/// <param name="circle">Which workspace the read answers from.</param>
/// <param name="ledger">Where a session's record is read, for an id that names a session.</param>
[McpServerToolType]
public sealed class HandbackTools(QuestStore quests, WorkspaceScope circle, SessionLedger? ledger = null)
{
    [McpServerTool(Name = "handback_read")]
    [Description(
        "Read one quest's handback whole: the note its session closed it with (done, ready or declined), how it answered each of the "
        + "person's requirements, when, and which session wrote it. Give a quest's id, or a session's id for the quest that session "
        + "served. It answers in any state, open or closed, for any quest of this workspace, as quest_list does: read it to check or "
        + "archive finished work, rather than the whole list. Read-only. A handback is its session's claim: check what it names before "
        + "you rely on it.")]
    public async Task<string> ReadAsync(
        [Description("A quest's id, from quest_list, or the id of a session, for the quest it served.")] string id,
        [Description(WorkspaceArgument)] string? workspace = null,
        CancellationToken ct = default)
    {
        if (circle.Refused(workspace) is { } outside) return outside;
        var scope = await circle.ResolveAsync(workspace, ct).ConfigureAwait(false);
        return (await new HandbackReader(quests, ledger).ReadAsync(id, scope, ct).ConfigureAwait(false)).Said;
    }
}
