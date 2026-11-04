namespace Daoris.Driver;

/// <summary>
/// The phases the chat runner builds its executors in: every executor that opens a conversation before any that carries one on, since a
/// move opens the part it moves to through a start.
/// </summary>
internal enum ExecutorPhase
{
    /// <summary>Opens a conversation: Ask Daoris's, or one on words a person said.</summary>
    Start,

    /// <summary>Carries a conversation's words on to another part once its own run let them go.</summary>
    Move,
}
