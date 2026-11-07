namespace Daoris.Knowledge;

/// <summary>Why a session is not opened — or <see cref="None"/> when it is.</summary>
public enum SessionOpenRefusal
{
    None,

    Gone,

    Superseded,

    /// <summary>No quest under that id.</summary>
    QuestNotFound,

    /// <summary>The quest is not open. Taken is the mutex between the driver and outside work.</summary>
    QuestNotOpen,

    /// <summary>
    /// A carry-on over a take that is not this machine's: another machine's by the quest's log, or one made
    /// here after this machine's last session on the quest ended. A carry-on goes on with this machine's take, and there
    /// is none to go on with.
    /// </summary>
    TakenElsewhere,

    /// <summary>The repository already has an active session — the working tree is the unit of exclusion.</summary>
    RepositoryBusy,

    /// <summary>
    /// No such repository on this machine's registry. A chat runs IN a repository, so there has to be
    /// one — and a chat is the one way in that names a repository directly rather than inheriting it
    /// from a quest, which is why only this path can fail that way.
    /// </summary>
    RepositoryUnknown,

    /// <summary>No ask under that id — an intake answers an ask, so there has to be one.</summary>
    AskNotFound,

    /// <summary>
    /// The ask is already answered — published, closed, or served by an intake before. One intake per
    /// ask: a second would be the loop retrying a harness forever on a question it could not settle.
    /// </summary>
    AskAnswered,

    /// <summary>
    /// The workspace named is not one this machine holds: Ask Daoris's conversation
    /// is started in a workspace, and keeps it.
    /// </summary>
    WorkspaceUnknown,
}
