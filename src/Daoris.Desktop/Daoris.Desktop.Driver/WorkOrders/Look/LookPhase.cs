namespace Daoris.Driver;

/// <summary>
/// Where in a look a pass runs: each phase is an anchor in the driver's look, which runs that phase's passes in order there. A
/// phase is added with its anchor, so a pass can never be declared in a phase that no look runs.
/// </summary>
internal enum LookPhase
{
    /// <summary>The look's head, before it reads anything local:the remote sync, then the lost-claim stop.</summary>
    Before,

    /// <summary>After the head: what the look reads and works locally before it reads the quests: the plugins, the rule proposals, the session watch, the moved lines, the operations bringing sessions together and the workspace plans.</summary>
    Local,

    /// <summary>After the starts are begun, their runs bound and what ended reported: the landing's settling of what a stopped run left.</summary>
    After,
}
