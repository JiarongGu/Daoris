namespace Daoris.Knowledge;

/// <summary>
/// The phase quest operations are registered in. A kind is reached by its key and never run in an order, so one phase holds them all;
/// the kit asks every part for one.
/// </summary>
internal enum QuestOperationPhase
{
    /// <summary>A kind of row in the quest log.</summary>
    Log,
}
